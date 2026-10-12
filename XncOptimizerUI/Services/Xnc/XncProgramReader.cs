using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;
using static XncOptimizerUI.Services.Xnc.XncAttributeParsing;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Reads the escaped <c>program</c> sub-document on an
    /// <c>&lt;operation typeId="XNC"&gt;</c> into an <see cref="XncProgram"/>.
    /// The format is documented in <c>.claude/skills/project-file-processing-skill/references/xnc-program-format.md</c>.
    /// </summary>
    public static class XncProgramReader
    {
        /// <summary>
        /// One parser per known element. Unknown elements are ignored (see
        /// <c>xnc-program-format.md</c> §9); every known element but a contour segment ends the open contour.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, Action<XElement, ReadState>> Parsers =
            new Dictionary<string, Action<XElement, ReadState>>
            {
                [XncTags.Tool] = ParseTool,
                [XncTags.Variable] = ParseVariable,
                [XncTags.ContourStart] = ParseContourStart,
                [XncTags.Line] = ParseLine,
                [XncTags.CentreArc] = ParseCentreArc,
                [XncTags.RadiusArc] = ParseRadiusArc,
                [XncTags.Groove] = ParseGroove,
                [XncTags.Ellipse] = ParseEllipse,
                [XncTags.Rectangle] = ParseRectangle,
                [XncTags.FaceBore] = ParseBore,
                [XncTags.TopBore] = ParseBore,
                [XncTags.BottomBore] = ParseBore,
                [XncTags.LeftBore] = ParseBore,
                [XncTags.RightBore] = ParseBore,
            };

        /// <summary>Parses the machining program carried by <paramref name="xncOperation"/>.</summary>
        /// <exception cref="XncProgramFormatException">The <c>program</c> attribute is missing, not valid XML, or contains values that cannot be resolved.</exception>
        public static XncProgram Read(XElement xncOperation)
        {
            var program = XncProgramText.Root(XncProgramText.Load(xncOperation));

            return Parse(
                program,
                ParseInt(xncOperation.Attribute("id")?.Value),
                ParseBool(xncOperation.GetSideValue(), false),
                ParseInt(xncOperation.GetTurnValue()));
        }

        private static XncProgram Parse(XElement program, int operationId, bool side, int turn)
        {
            var state = new ReadState(program);

            foreach (var element in program.Elements())
            {
                var tag = element.Name.LocalName;

                if (Parsers.TryGetValue(tag, out var parse))
                {
                    if (!XncTags.IsContourSegment(tag))
                    {
                        state.CloseContour();
                    }

                    parse(element, state);
                }
            }

            state.CloseContour();

            return state.ToProgram(operationId, side, turn);
        }

        #region Tools and variables

        private static void ParseTool(XElement element, ReadState state) =>
            state.AddTool(new XncTool
            {
                Name = element.GetNameValue() ?? string.Empty,
                Diameter = RequireDouble(element.GetDValue(), "<tool> @d")
            });

        private static void ParseVariable(XElement element, ReadState state)
        {
            var name = element.GetNameValue()
                ?? throw new XncProgramFormatException("<var> has no name.");
            var type = XncVariableTypes.Parse(element.GetTypeValue());

            // Only int/double vars are arithmetic symbols; a string/bool var's expr is
            // not a number and nothing numeric may reference it.
            double? value = type.IsNumeric() ? state.Eval(element.GetExprValue(), $"<var> '{name}'") : null;

            state.AddVariable(new XncVariable
            {
                Index = state.DeclaredVariables.Count,
                Name = name,
                Type = type,
                Expr = element.GetExprValue() ?? string.Empty,
                Comment = element.GetCommentValue(),
                Value = value
            });
        }

        #endregion

        #region Contours

        private static void ParseContourStart(XElement element, ReadState state)
        {
            state.SelectTool(element);
            var entryDepth = state.Eval(element, "dp");

            state.OpenContour(new ContourBuilder
            {
                ToolName = element.GetNameValue() ?? string.Empty,
                Entry = state.Point(element, "x", "y"),
                EntryDepth = entryDepth,
                Position = ParsePositionCode(element.GetCValue()),
                Forward = ParseBool(element.GetFwdValue(), true),
                LeadIn = ParseInt(element.GetInValue()),
                LeadOut = ParseInt(element.GetOutValue()),
                StartOffsetXY = state.EvalOptional(element, "sxy"),
                CurrentDepth = entryDepth
            });
        }

        private static void ParseLine(XElement element, ReadState state)
        {
            var contour = state.ContinueContour(element);

            contour.Segments.Add(new XncLineSegment
            {
                End = state.Point(element, "x", "y"),
                Depth = contour.CurrentDepth
            });
        }

        private static void ParseCentreArc(XElement element, ReadState state)
        {
            var contour = state.ContinueContour(element);
            var end = state.Point(element, "x", "y");
            var center = state.Point(element, "cx", "cy");

            contour.Segments.Add(new XncArcSegment
            {
                End = end,
                Center = center,
                Clockwise = IsClockwise(element),
                Radius = center.ToVec2().DistanceTo(end.ToVec2()),
                Depth = contour.CurrentDepth
            });
        }

        private static void ParseRadiusArc(XElement element, ReadState state)
        {
            var contour = state.ContinueContour(element);
            var start = contour.Segments.Count > 0 ? contour.Segments[^1].End : contour.Entry;
            var end = state.Point(element, "x", "y");
            var radius = state.Eval(element, "r");
            var center = PlanarGeometry.ArcCentreFromChord(start.ToVec2(), end.ToVec2(), radius, IsClockwise(element))
                ?? throw new XncProgramFormatException($"<ma> radius {radius} cannot span {start} -> {end}.");

            contour.Segments.Add(new XncArcSegment
            {
                End = end,
                Center = new XncPoint(center.X, center.Y),
                Clockwise = IsClockwise(element),
                Radius = radius,
                Depth = contour.CurrentDepth
            });
        }

        #endregion

        #region Grooves and self-contained mills

        private static void ParseGroove(XElement element, ReadState state)
        {
            state.SelectTool(element);

            state.Groovings.Add(new XncGrooving
            {
                ToolName = element.GetNameValue() ?? string.Empty,
                Start = state.Point(element, "x1", "y1"),
                End = state.Point(element, "x2", "y2"),
                Depth = state.Eval(element, "dp"),
                Width = state.Eval(element, "t"),
                SideCode = ParseInt(element.GetPValue()),
                Z = state.EvalOptional(element, "z") ?? 0d,
                Position = ParsePositionCode(element.GetCValue()),
                Comment = element.GetCommentValue() ?? string.Empty
            });
        }

        private static void ParseEllipse(XElement element, ReadState state)
        {
            state.SelectTool(element);

            state.Ellipses.Add(new XncMillingEllipse
            {
                ToolName = element.GetNameValue() ?? string.Empty,
                Center = state.Point(element, "x", "y"),
                Length = state.Eval(element, "l"),
                Width = state.Eval(element, "w"),
                Angle = state.Eval(element, "a"),
                Depth = state.Eval(element, "dp"),
                Position = ParsePositionCode(element.GetCValue()),
                Forward = ParseBool(element.GetFwdValue(), true),
                LeadIn = ParseInt(element.GetInValue()),
                LeadOut = ParseInt(element.GetOutValue()),
                StartOffsetXY = state.EvalOptional(element, "sxy")
            });
        }

        private static void ParseRectangle(XElement element, ReadState state)
        {
            state.SelectTool(element);

            state.Rectangles.Add(new XncMillingRectangle
            {
                ToolName = element.GetNameValue() ?? string.Empty,
                Origin = state.Point(element, "x", "y"),
                Length = state.Eval(element, "l"),
                Width = state.Eval(element, "w"),
                Angle = state.Eval(element, "a"),
                CornerRadius = state.Eval(element, "r"),
                Depth = state.Eval(element, "dp"),
                Position = ParsePositionCode(element.GetCValue()),
                Forward = ParseBool(element.GetFwdValue(), true),
                LeadIn = ParseInt(element.GetInValue()),
                LeadOut = ParseInt(element.GetOutValue()),
                StartOffsetXY = state.EvalOptional(element, "sxy")
            });
        }

        #endregion

        #region Bores

        private static void ParseBore(XElement element, ReadState state)
        {
            state.SelectTool(element);

            var tag = element.Name.LocalName;

            // Edge bore through-thickness position: m="true" ("middle") pins it to dz/2 and no z
            // attribute is needed; otherwise z is required.
            var isMiddle = ParseBool(element.GetMValue(), false);

            double EdgeZ() => isMiddle ? state.Dz / 2d : state.Eval(element, "z");

            var (surface, x, y, z) = tag switch
            {
                XncTags.FaceBore => (BoreSurface.Face, state.Eval(element, "x"), state.Eval(element, "y"), 0d),
                XncTags.LeftBore => (BoreSurface.Left, 0d, state.Eval(element, "y"), EdgeZ()),
                XncTags.RightBore => (BoreSurface.Right, state.Dx, state.Eval(element, "y"), EdgeZ()),
                // Plane names are visual: <bt> is the screen-top edge (y = 0 in the Y-down frame),
                // <bb> the screen-bottom edge (y = dy).
                XncTags.BottomBore => (BoreSurface.Bottom, state.Eval(element, "x"), state.Dy, EdgeZ()),
                _ => (BoreSurface.Top, state.Eval(element, "x"), 0d, EdgeZ()),
            };

            var depth = state.Eval(element, "dp");

            state.Bores.Add(new XncBore
            {
                Surface = surface,
                ToolName = element.GetNameValue() ?? string.Empty,
                X = x,
                Y = y,
                Z = z,
                Depth = depth,
                Through = depth >= ThroughDepth(tag, state),
                Index = state.Bores.Count,
                XText = element.GetXValue(),
                YText = element.GetYValue(),
                ZText = isMiddle ? null : element.GetZValue(),
                DepthText = element.GetDpValue()
            });
        }

        /// <summary>
        /// "Through" isn't carried by any attribute - <c>av</c>/<c>ac</c>/<c>as</c> describe a
        /// repeated-bore array, unrelated to hole depth. A bore is through when its depth reaches the
        /// panel dimension it drills into: <c>dz</c> for a face bore, <c>dx</c> for an edge bore
        /// drilled from the left/right, <c>dy</c> for one drilled from the top/bottom.
        /// </summary>
        private static double ThroughDepth(string tag, ReadState state) => tag switch
        {
            XncTags.LeftBore or XncTags.RightBore => state.Dx,
            XncTags.TopBore or XncTags.BottomBore => state.Dy,
            _ => state.Dz
        };

        #endregion

        #region Read state

        /// <summary>Everything read so far: symbols, tools, the open contour and the parsed elements.</summary>
        private sealed class ReadState
        {
            private readonly XncSymbolTable _symbols;
            private readonly Dictionary<string, XncTool> _toolsByName = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, double> _variables = new(StringComparer.OrdinalIgnoreCase);
            private readonly List<XncTool> _tools = [];
            private readonly List<XncMillingContour> _contours = [];

            private ContourBuilder? _contour;

            public ReadState(XElement program)
            {
                _symbols = XncSymbols.DimensionSymbols(program);
                Dx = Symbol("dx");
                Dy = Symbol("dy");
                Dz = Symbol("dz");
            }

            public double Dx { get; }

            public double Dy { get; }

            public double Dz { get; }

            public List<XncVariable> DeclaredVariables { get; } = [];

            public List<XncBore> Bores { get; } = [];

            public List<XncGrooving> Groovings { get; } = [];

            public List<XncMillingEllipse> Ellipses { get; } = [];

            public List<XncMillingRectangle> Rectangles { get; } = [];

            public void AddTool(XncTool tool)
            {
                _tools.Add(tool);
                _toolsByName[tool.Name] = tool;
            }

            public void AddVariable(XncVariable variable)
            {
                if (variable.Value is { } number)
                {
                    _symbols.Set(variable.Name, number);
                    _variables[variable.Name] = number;
                }

                DeclaredVariables.Add(variable);
            }

            /// <summary>Makes <c>tool.dia</c> resolve to the element's tool, which must be declared before it.</summary>
            public void SelectTool(XElement element)
            {
                var toolName = element.GetNameValue();

                if (toolName is null || !_toolsByName.TryGetValue(toolName, out var tool))
                {
                    throw new XncProgramFormatException($"<{element.Name.LocalName}> references unknown tool '{toolName}'.");
                }

                _symbols.SetToolDiameter(tool.Diameter);
            }

            public double Eval(string? expression, string where) => XncSymbols.Eval(expression, _symbols, where);

            /// <summary>The element's required <paramref name="attribute"/>, evaluated.</summary>
            public double Eval(XElement element, string attribute) =>
                Eval(element.Attribute(attribute)?.Value, $"<{element.Name.LocalName}> @{attribute}");

            /// <summary>The element's optional <paramref name="attribute"/>, evaluated; <c>null</c> when absent.</summary>
            public double? EvalOptional(XElement element, string attribute) =>
                element.Attribute(attribute) is null ? null : Eval(element, attribute);

            public XncPoint Point(XElement element, string xAttribute, string yAttribute) =>
                new(Eval(element, xAttribute), Eval(element, yAttribute));

            public void OpenContour(ContourBuilder contour) => _contour = contour;

            /// <summary>
            /// The open contour a segment continues, with the segment's <c>dp</c> applied - optional
            /// on a segment: absent means keep cutting at the contour's current depth.
            /// </summary>
            public ContourBuilder ContinueContour(XElement segment)
            {
                var contour = _contour
                    ?? throw new XncProgramFormatException($"<{segment.Name.LocalName}> outside a milling contour.");

                contour.CurrentDepth = segment.GetDpValue() is { } depth
                    ? XncExpressionEvaluator.Evaluate(depth, _symbols)
                    : contour.CurrentDepth;

                return contour;
            }

            public void CloseContour()
            {
                if (_contour is not null)
                {
                    _contours.Add(_contour.Build());
                    _contour = null;
                }
            }

            public XncProgram ToProgram(int operationId, bool side, int turn) => new()
            {
                OperationId = operationId,
                Dx = Dx,
                Dy = Dy,
                Dz = Dz,
                Side = side,
                Turn = turn,
                Tools = _tools,
                Bores = Bores,
                Groovings = Groovings,
                MillingContours = _contours,
                MillingEllipses = Ellipses,
                MillingRectangles = Rectangles,
                Variables = _variables,
                DeclaredVariables = DeclaredVariables
            };

            private double Symbol(string name) => _symbols.TryGet(name, out var value) ? value : 0d;
        }

        private sealed class ContourBuilder
        {
            public string ToolName { get; init; } = string.Empty;
            public XncPoint Entry { get; init; }
            public double EntryDepth { get; init; }
            public ToolPosition Position { get; init; }
            public bool Forward { get; init; } = true;
            public int LeadIn { get; init; }
            public int LeadOut { get; init; }
            public double? StartOffsetXY { get; init; }

            /// <summary>Running cut depth, seeded from the entry and updated by each segment's <c>dp</c>.</summary>
            public double CurrentDepth { get; set; }

            public List<XncMillingSegment> Segments { get; } = [];

            public XncMillingContour Build() => new()
            {
                ToolName = ToolName,
                Entry = Entry,
                EntryDepth = EntryDepth,
                Position = Position,
                Forward = Forward,
                LeadIn = LeadIn,
                LeadOut = LeadOut,
                StartOffsetXY = StartOffsetXY,
                Segments = Segments
            };
        }

        #endregion
    }
}
