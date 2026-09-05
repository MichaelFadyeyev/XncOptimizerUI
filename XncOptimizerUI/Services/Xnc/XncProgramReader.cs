using System.Globalization;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Reads the escaped <c>program</c> sub-document on an
    /// <c>&lt;operation typeId="XNC"&gt;</c> into an <see cref="XncProgram"/>.
    /// The format is documented in <c>.claude/xnc-program-read.md</c>.
    /// </summary>
    public static class XncProgramReader
    {
        /// <summary>Parses the machining program carried by <paramref name="xncOperation"/>.</summary>
        /// <exception cref="XncProgramFormatException">The <c>program</c> attribute is missing, not valid XML, or contains values that cannot be resolved.</exception>
        public static XncProgram Read(XElement xncOperation)
        {
            var raw = xncOperation.GetProgramValue()
                ?? throw new XncProgramFormatException("XNC operation has no 'program' attribute.");

            XDocument document;
            try
            {
                // The attribute value is entity-escaped once; LINQ-to-XML already decoded it
                // when we read .Value. HtmlDecode is kept for parity with GibLabProjectService.
                document = XDocument.Parse(WebUtility.HtmlDecode(raw));
            }
            catch (XmlException ex)
            {
                throw new XncProgramFormatException("The 'program' attribute is not valid XML.", ex);
            }

            var program = document.Element("program")
                ?? throw new XncProgramFormatException("The 'program' document has no <program> root element.");

            return Parse(program, ParseBool(xncOperation.GetSideValue(), false));
        }

        private static XncProgram Parse(XElement program, bool side)
        {
            var symbols = new XncSymbolTable();
            var dx = RequireDouble(program.GetDxValue(), "<program> @dx");
            var dy = RequireDouble(program.GetDyValue(), "<program> @dy");
            var dz = RequireDouble(program.GetDzValue(), "<program> @dz");
            symbols.Set("dx", dx);
            symbols.Set("dy", dy);
            symbols.Set("dz", dz);

            var tools = new List<XncTool>();
            var toolsByName = new Dictionary<string, XncTool>(StringComparer.OrdinalIgnoreCase);
            var variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var bores = new List<XncBore>();
            var groovings = new List<XncGrooving>();
            var contours = new List<XncMillingContour>();
            var rectangles = new List<XncMillingRectangle>();

            ContourBuilder? contour = null;

            void CloseContour()
            {
                if (contour is not null)
                {
                    contours.Add(contour.Build());
                    contour = null;
                }
            }

            double Eval(string? expression, string where) =>
                XncExpressionEvaluator.Evaluate(
                    expression ?? throw new XncProgramFormatException($"Missing value for {where}."),
                    symbols);

            double EvalOr(string? expression, double fallback) =>
                expression is { } e ? XncExpressionEvaluator.Evaluate(e, symbols) : fallback;

            double Sym(string name) => symbols.TryGet(name, out var value) ? value : 0d;

            void SetToolDia(string? toolName, string where)
            {
                if (toolName is null || !toolsByName.TryGetValue(toolName, out var tool))
                {
                    throw new XncProgramFormatException($"{where} references unknown tool '{toolName}'.");
                }

                symbols.Set("tool.dia", tool.Diameter);
            }

            foreach (var element in program.Elements())
            {
                var tag = element.Name.LocalName;

                switch (tag)
                {
                    case "tool":
                    {
                        CloseContour();
                        var tool = new XncTool
                        {
                            Name = element.GetNameValue() ?? string.Empty,
                            Diameter = RequireDouble(element.GetDValue(), "<tool> @d")
                        };
                        tools.Add(tool);
                        toolsByName[tool.Name] = tool;
                        break;
                    }

                    case "var":
                    {
                        CloseContour();
                        var name = element.GetNameValue()
                            ?? throw new XncProgramFormatException("<var> has no name.");
                        var value = Eval(element.GetExprValue(), $"<var> '{name}'");
                        symbols.Set(name, value);
                        variables[name] = value;
                        break;
                    }

                    case "ms":
                    {
                        CloseContour();
                        SetToolDia(element.GetNameValue(), "<ms>");
                        var entryDepth = Eval(element.GetDpValue(), "<ms> @dp");
                        contour = new ContourBuilder
                        {
                            ToolName = element.GetNameValue() ?? string.Empty,
                            Entry = new XncPoint(Eval(element.GetXValue(), "<ms> @x"), Eval(element.GetYValue(), "<ms> @y")),
                            EntryDepth = entryDepth,
                            Position = ParsePosition(element.GetCValue()),
                            LeadIn = ParseInt(element.GetInValue()),
                            LeadOut = ParseInt(element.GetOutValue()),
                            StartOffsetXY = element.GetSxyValue() is { } sxy ? Eval(sxy, "<ms> @sxy") : null,
                            CurrentDepth = entryDepth
                        };
                        break;
                    }

                    case "ml":
                    {
                        var open = contour ?? throw new XncProgramFormatException("<ml> outside a milling contour.");
                        // dp is optional on <ml>: absent means keep cutting at the contour's current depth.
                        open.CurrentDepth = EvalOr(element.GetDpValue(), open.CurrentDepth);
                        open.Segments.Add(new XncLineSegment
                        {
                            End = new XncPoint(Eval(element.GetXValue(), "<ml> @x"), Eval(element.GetYValue(), "<ml> @y")),
                            Depth = open.CurrentDepth
                        });
                        break;
                    }

                    case "mac":
                    {
                        var open = contour ?? throw new XncProgramFormatException("<mac> outside a milling contour.");
                        open.CurrentDepth = EvalOr(element.GetDpValue(), open.CurrentDepth);
                        var end = new XncPoint(Eval(element.GetXValue(), "<mac> @x"), Eval(element.GetYValue(), "<mac> @y"));
                        var center = new XncPoint(Eval(element.GetCxValue(), "<mac> @cx"), Eval(element.GetCyValue(), "<mac> @cy"));
                        open.Segments.Add(new XncArcSegment
                        {
                            End = end,
                            Center = center,
                            Clockwise = !ParseBool(element.GetDirValue(), false),
                            Radius = Distance(center, end),
                            Depth = open.CurrentDepth
                        });
                        break;
                    }

                    case "gr":
                    {
                        CloseContour();
                        SetToolDia(element.GetNameValue(), "<gr>");
                        groovings.Add(new XncGrooving
                        {
                            ToolName = element.GetNameValue() ?? string.Empty,
                            Start = new XncPoint(Eval(element.GetX1Value(), "<gr> @x1"), Eval(element.GetY1Value(), "<gr> @y1")),
                            End = new XncPoint(Eval(element.GetX2Value(), "<gr> @x2"), Eval(element.GetY2Value(), "<gr> @y2")),
                            Depth = Eval(element.GetDpValue(), "<gr> @dp"),
                            Width = Eval(element.GetTValue(), "<gr> @t"),
                            Position = ParsePosition(element.GetCValue()),
                            Comment = element.GetCommentValue() ?? string.Empty
                        });
                        break;
                    }

                    case "mr":
                    {
                        CloseContour();
                        SetToolDia(element.GetNameValue(), "<mr>");
                        rectangles.Add(new XncMillingRectangle
                        {
                            ToolName = element.GetNameValue() ?? string.Empty,
                            Origin = new XncPoint(Eval(element.GetXValue(), "<mr> @x"), Eval(element.GetYValue(), "<mr> @y")),
                            Length = Eval(element.GetLengthValue(), "<mr> @l"),
                            Width = Eval(element.GetWidthValue(), "<mr> @w"),
                            Angle = Eval(element.GetAValue(), "<mr> @a"),
                            CornerRadius = Eval(element.GetRValue(), "<mr> @r"),
                            Depth = Eval(element.GetDpValue(), "<mr> @dp"),
                            Position = ParsePosition(element.GetCValue()),
                            LeadIn = ParseInt(element.GetInValue()),
                            LeadOut = ParseInt(element.GetOutValue()),
                            StartOffsetXY = element.GetSxyValue() is { } sxy ? Eval(sxy, "<mr> @sxy") : null
                        });
                        break;
                    }

                    case "bf":
                    case "bt":
                    case "bb":
                    case "bl":
                    case "br":
                    {
                        CloseContour();
                        SetToolDia(element.GetNameValue(), $"<{tag}>");

                        var (surface, bx, by, bz) = tag switch
                        {
                            "bf" => (BoreSurface.Face,   Eval(element.GetXValue(), "<bf> @x"), Eval(element.GetYValue(), "<bf> @y"), 0d),
                            "bl" => (BoreSurface.Left,   0d,                                   Eval(element.GetYValue(), "<bl> @y"), Eval(element.GetZValue(), "<bl> @z")),
                            "br" => (BoreSurface.Right,  Sym("dx"),                            Eval(element.GetYValue(), "<br> @y"), Eval(element.GetZValue(), "<br> @z")),
                            "bb" => (BoreSurface.Bottom, Eval(element.GetXValue(), "<bb> @x"), 0d,                                   Eval(element.GetZValue(), "<bb> @z")),
                            "bt" => (BoreSurface.Top,    Eval(element.GetXValue(), "<bt> @x"), Sym("dy"),                            Eval(element.GetZValue(), "<bt> @z")),
                            _ => throw new XncProgramFormatException($"<{tag}> is not a bore element.")
                        };

                        bores.Add(new XncBore
                        {
                            Surface = surface,
                            ToolName = element.GetNameValue() ?? string.Empty,
                            X = bx,
                            Y = by,
                            Z = bz,
                            Depth = Eval(element.GetDpValue(), $"<{tag}> @dp"),
                            Through = ParseBool(element.GetAvValue(), false)
                        });
                        break;
                    }

                    default:
                        // Unknown element type: ignore (see .claude/xnc-program-read.md §9).
                        break;
                }
            }

            CloseContour();

            return new XncProgram
            {
                Dx = dx,
                Dy = dy,
                Dz = dz,
                Side = side,
                Tools = tools,
                Bores = bores,
                Groovings = groovings,
                MillingContours = contours,
                MillingRectangles = rectangles,
                Variables = variables
            };
        }

        private static double RequireDouble(string? raw, string where) =>
            double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new XncProgramFormatException($"{where} is missing or not a number (was '{raw}').");

        private static int ParseInt(string? raw) =>
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

        private static bool ParseBool(string? raw, bool fallback) =>
            bool.TryParse(raw, out var value) ? value : fallback;

        private static ToolPosition ParsePosition(string? raw) =>
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
                && Enum.IsDefined(typeof(ToolPosition), code)
                    ? (ToolPosition)code
                    : ToolPosition.Center;

        private static double Distance(XncPoint a, XncPoint b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;

            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private sealed class ContourBuilder
        {
            public string ToolName { get; init; } = string.Empty;
            public XncPoint Entry { get; init; }
            public double EntryDepth { get; init; }
            public ToolPosition Position { get; init; }
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
                LeadIn = LeadIn,
                LeadOut = LeadOut,
                StartOffsetXY = StartOffsetXY,
                Segments = Segments
            };
        }
    }
}
