using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.Services.Xnc.Geometry;
using static XncOptimizerUI.Services.Xnc.XncAttributeParsing;
using static XncOptimizerUI.Services.Xnc.XncNumberFormat;
using static XncOptimizerUI.Services.Xnc.XncSymbols;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Rotates an XNC <c>&lt;program&gt;</c> by whole quarter turns, clockwise in the operator
    /// (preview) frame: origin at the part's top-left corner, X right, Y down. One quarter step
    /// maps a point <c>(x, y)</c> to <c>(dy - y, x)</c> and swaps <c>dx</c>/<c>dy</c>, so the
    /// origin stays at the top-left corner of the turned part.
    /// <para>
    /// Plane names are visual, independent of the Y-down coordinates: <c>&lt;bt&gt;</c> / groove
    /// <c>p="3"</c> is the screen-top edge (<c>y = 0</c>), <c>&lt;bb&gt;</c> / <c>p="4"</c> the
    /// screen-bottom edge (<c>y = dy</c>). A clockwise step moves top → right → bottom → left.
    /// </para>
    /// <para>
    /// Values are resolved against the program's original symbols. A coordinate whose value
    /// changes is written as a number (<see cref="XncNumberFormat.FormatNumber"/>); every other
    /// value keeps its authored text. On an odd number of steps every expression gets its
    /// <c>dx</c>/<c>dy</c> identifiers swapped, so it still evaluates to the same length in the
    /// turned frame (e.g. <c>dp="dx"</c> becomes <c>dp="dy"</c>).
    /// </para>
    /// </summary>
    internal static class XncProgramRotator
    {
        /// <summary>Edge-bore tags in clockwise order (top → right → bottom → left).</summary>
        private static readonly string[] EdgeBoreCycle = [XncTags.TopBore, XncTags.RightBore, XncTags.BottomBore, XncTags.LeftBore];

        /// <summary>Groove plane codes <c>p</c> in clockwise order (top → right → bottom → left).</summary>
        private static readonly int[] GroovePlaneCycle = [3, 2, 4, 1];

        private static readonly Regex DxDyIdentifier = new(
            @"(?<![\w.])([dD])([xXyY])(?![\w.])",
            RegexOptions.CultureInvariant);

        /// <summary>Normalizes any integer number of quarter turns to <c>0..3</c>.</summary>
        public static int NormalizeQuarterTurns(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;

        /// <summary>
        /// Rotates <paramref name="program"/> in place by <paramref name="quarterTurns"/> clockwise
        /// quarter turns. Returns <c>false</c> (and leaves it untouched) for a whole-turn multiple.
        /// <paramref name="flipEdgeGrooveTcl"/> <c>false</c> keeps every edge groove's <c>c</c>
        /// as authored (GibLab's behavior) instead of flipping it (see <see cref="FlipsTravelConvention"/>).
        /// </summary>
        public static bool RotateProgram(XElement program, int quarterTurns, bool flipEdgeGrooveTcl = true)
        {
            var steps = NormalizeQuarterTurns(quarterTurns);

            if (steps == 0)
            {
                return false;
            }

            var frame = new Frame(
                RequireDimension(program, "dx"),
                RequireDimension(program, "dy"),
                steps);
            var context = new RotationContext(frame, SeedProgramSymbols(program), flipEdgeGrooveTcl);

            foreach (var element in program.Elements().ToList())
            {
                RotateElement(element, context);
            }

            if (frame.IsOdd)
            {
                SwapProgramSize(program);
                program.Elements().ToList().ForEach(SwapDxDyInExpressions);
            }

            return true;
        }

        /// <summary>
        /// Swaps whole <c>dx</c>/<c>dy</c> identifiers in <paramref name="expression"/>, keeping
        /// their letter case; dotted or longer identifiers (<c>tool.dia</c>, <c>dxOffset</c>) are
        /// left alone.
        /// </summary>
        public static string SwapDxDyIdentifiers(string expression) =>
            DxDyIdentifier.Replace(expression, m => m.Groups[1].Value + SwapAxisLetter(m.Groups[2].Value));

        #region Element dispatch

        private static void RotateElement(XElement element, RotationContext context)
        {
            var tag = element.Name.LocalName;

            if (tag == XncTags.Tool)
            {
                context.RegisterTool(element);
                return;
            }

            context.SelectTool(element);

            switch (tag)
            {
                case XncTags.FaceBore:
                    RotateFaceBore(element, context);
                    break;
                case XncTags.TopBore or XncTags.RightBore or XncTags.BottomBore or XncTags.LeftBore:
                    RotateEdgeBore(element, context);
                    break;
                case XncTags.Groove:
                    RotateGroove(element, context);
                    break;
                case XncTags.ContourStart or XncTags.Line or XncTags.RadiusArc:
                    RotatePointAttributes(element, "x", "y", context);
                    break;
                case XncTags.CentreArc:
                    RotatePointAttributes(element, "x", "y", context);
                    RotatePointAttributes(element, "cx", "cy", context);
                    break;
                case XncTags.Rectangle or XncTags.Ellipse:
                    RotatePointAttributes(element, "x", "y", context);
                    SwapLengthAndWidth(element, context.Frame);
                    break;
            }
        }

        #endregion

        #region Bores

        private static void RotateFaceBore(XElement bore, RotationContext context)
        {
            var start = context.ReadPoint(bore, "x", "y");
            var step = ReadFaceArrayStep(bore, context);
            var (newStart, newStep) = RotateArray(start, step, HoleCount(bore), context.Frame);

            WritePoint(bore, "x", "y", newStart, start);

            if (newStep is { } s)
            {
                WriteFaceArrayStep(bore, s, step!.Value);
            }
        }

        /// <summary>
        /// An edge bore sits on its edge at one along-edge coordinate (<c>x</c> on top/bottom,
        /// <c>y</c> on left/right). The edge point is rotated like any other point; the bore moves
        /// to the next edge clockwise and takes the rotated point's along-edge coordinate.
        /// </summary>
        private static void RotateEdgeBore(XElement bore, RotationContext context)
        {
            var tag = bore.Name.LocalName;
            var alongAttribute = AlongAttributeOf(tag);
            var along = context.Eval(bore.Attribute(alongAttribute)?.Value, $"<{tag}> @{alongAttribute}");
            var point = EdgePoint(tag, along, context.Frame.Dx, context.Frame.Dy);
            var step = ReadEdgeArrayStep(bore, tag, context);
            var (newPoint, newStep) = RotateArray(point, step, HoleCount(bore), context.Frame);

            var newTag = NextInCycle(EdgeBoreCycle, tag, context.Frame.Steps);
            var newAlongAttribute = AlongAttributeOf(newTag);
            var newAlong = AlongValueOf(newTag, newPoint);

            bore.Name = newTag;
            ReplaceAttribute(bore, alongAttribute, newAlongAttribute,
                FormatNumber(newAlong) == FormatNumber(along) ? bore.Attribute(alongAttribute)!.Value : FormatNumber(newAlong));

            if (newStep is { } s)
            {
                SetNumber(bore, "as", AlongValueOf(newTag, s), step!.Value.X + step.Value.Y);
            }
        }

        private static string AlongAttributeOf(string edgeTag) => edgeTag is XncTags.TopBore or XncTags.BottomBore ? "x" : "y";

        private static double AlongValueOf(string edgeTag, Vec2 point) => edgeTag is XncTags.TopBore or XncTags.BottomBore ? point.X : point.Y;

        private static Vec2 EdgePoint(string edgeTag, double along, double dx, double dy) => edgeTag switch
        {
            XncTags.TopBore => new Vec2(along, 0d),
            XncTags.BottomBore => new Vec2(along, dy),
            XncTags.LeftBore => new Vec2(0d, along),
            _ => new Vec2(dx, along),
        };

        #endregion

        #region Bore arrays

        // A bore with ac > 1 expands into ac holes, `as` mm apart. A face bore steps along X, or
        // along Y when av="true"; an edge bore (av="false") steps along its own edge. The step
        // vector turns with the part; when it would point backwards the array restarts from its
        // last hole so `as` stays positive.

        private static Vec2? ReadFaceArrayStep(XElement bore, RotationContext context)
        {
            if (ReadArrayStepLength(bore, context) is not { } length)
            {
                return null;
            }

            return IsVerticalArray(bore) ? new Vec2(0d, length) : new Vec2(length, 0d);
        }

        private static Vec2? ReadEdgeArrayStep(XElement bore, string tag, RotationContext context)
        {
            if (IsVerticalArray(bore) || ReadArrayStepLength(bore, context) is not { } length)
            {
                return null;
            }

            return AlongAttributeOf(tag) == "x" ? new Vec2(length, 0d) : new Vec2(0d, length);
        }

        private static double? ReadArrayStepLength(XElement bore, RotationContext context)
        {
            var step = bore.Attribute("as")?.Value;

            if (HoleCount(bore) <= 1 || string.IsNullOrWhiteSpace(step) || step == "null")
            {
                return null;
            }

            return context.Eval(step, $"<{bore.Name.LocalName}> @as");
        }

        private static bool IsVerticalArray(XElement bore) => bool.TryParse(bore.GetAvValue(), out var av) && av;

        /// <summary>The array count <c>ac</c>; anything but a positive integer reads as a single hole.</summary>
        private static int HoleCount(XElement bore) => ArrayCount(bore) ?? 1;

        /// <summary>
        /// Rotates an array's first hole and step; a step that would point backwards (negative
        /// component) is flipped and the array restarts from its last hole.
        /// </summary>
        private static (Vec2 Start, Vec2? Step) RotateArray(Vec2 start, Vec2? step, int count, Frame frame)
        {
            var rotatedStart = frame.RotatePoint(start);

            if (step is not { } s)
            {
                return (rotatedStart, null);
            }

            var rotatedStep = frame.RotateVector(s);

            if (rotatedStep.X >= 0d && rotatedStep.Y >= 0d)
            {
                return (rotatedStart, rotatedStep);
            }

            var span = count - 1;
            var lastHole = rotatedStart + (rotatedStep * span);

            return (lastHole, rotatedStep * -1d);
        }

        private static void WriteFaceArrayStep(XElement bore, Vec2 newStep, Vec2 oldStep)
        {
            bore.SetAttributeValue("av", newStep.Y != 0d ? "true" : "false");
            SetNumber(bore, "as", newStep.X + newStep.Y, oldStep.X + oldStep.Y);
        }

        #endregion

        #region Grooves and mills

        /// <summary>
        /// Rotates both groove points. An edge-plane groove (<c>p</c> 1..4) also moves to the next
        /// plane clockwise, keeps GibLab's start ≤ end order (points swapped when the turn reversed
        /// them), and gets its <c>c</c> Right/Left flipped where its plane's travel convention
        /// reverses (see <see cref="FlipsTravelConvention"/>).
        /// </summary>
        private static void RotateGroove(XElement groove, RotationContext context)
        {
            var start = context.ReadPoint(groove, "x1", "y1");
            var end = context.ReadPoint(groove, "x2", "y2");
            var newStart = context.Frame.RotatePoint(start);
            var newEnd = context.Frame.RotatePoint(end);

            WritePoint(groove, "x1", "y1", newStart, start);
            WritePoint(groove, "x2", "y2", newEnd, end);

            if (TryParseInt(groove.GetPValue()) is not { } plane || Array.IndexOf(GroovePlaneCycle, plane) < 0)
            {
                return;
            }

            if (newStart.X > newEnd.X || newStart.Y > newEnd.Y)
            {
                SwapGroovePoints(groove);
            }

            if (context.FlipEdgeGrooveTcl && FlipsTravelConvention(plane, context.Frame.Steps))
            {
                FlipToolPosition(groove);
            }

            groove.SetAttributeValue("p", NextInCycle(GroovePlaneCycle, plane, context.Frame.Steps).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// An edge-plane groove's <c>c</c> is relative to a fixed travel direction per plane, not
        /// to its start/end order: +X for top/bottom (<c>p</c> 3/4), -Y for left/right (<c>p</c>
        /// 1/2). A clockwise step turns +X into +Y, so leaving top/bottom for right/left reverses
        /// the travel against the new plane's convention; right/left → bottom/top keeps it (-Y
        /// turns into +X). An odd number of such reversals flips Right ↔ Left.
        /// </summary>
        private static bool FlipsTravelConvention(int plane, int steps) =>
            Enumerable.Range(0, steps)
                .Select(step => NextInCycle(GroovePlaneCycle, plane, step))
                .Count(p => p is 3 or 4) % 2 == 1;

        private static void FlipToolPosition(XElement groove)
        {
            var flipped = groove.GetCValue() switch
            {
                "1" => "2",
                "2" => "1",
                _ => null,
            };

            if (flipped != null)
            {
                groove.SetAttributeValue("c", flipped);
            }
        }

        /// <summary>Swaps the start and end point texts, keeping the attribute order.</summary>
        private static void SwapGroovePoints(XElement groove)
        {
            var (x1, y1) = (groove.Attribute("x1")!.Value, groove.Attribute("y1")!.Value);
            groove.Attribute("x1")!.Value = groove.Attribute("x2")!.Value;
            groove.Attribute("y1")!.Value = groove.Attribute("y2")!.Value;
            groove.Attribute("x2")!.Value = x1;
            groove.Attribute("y2")!.Value = y1;
        }

        private static void RotatePointAttributes(XElement element, string xAttribute, string yAttribute, RotationContext context)
        {
            var point = context.ReadPoint(element, xAttribute, yAttribute);

            WritePoint(element, xAttribute, yAttribute, context.Frame.RotatePoint(point), point);
        }

        /// <summary>
        /// A rectangle / ellipse is centrally symmetric: turning it a quarter is the same as
        /// swapping its length and width at the same angle, so <c>a</c> never changes.
        /// </summary>
        private static void SwapLengthAndWidth(XElement element, Frame frame)
        {
            if (!frame.IsOdd)
            {
                return;
            }

            var length = element.Attribute("l")?.Value;
            var width = element.Attribute("w")?.Value;
            element.SetAttributeValue("l", width);
            element.SetAttributeValue("w", length);
        }

        #endregion

        #region Attribute writing

        private static void WritePoint(XElement element, string xAttribute, string yAttribute, Vec2 value, Vec2 original)
        {
            SetNumber(element, xAttribute, value.X, original.X);
            SetNumber(element, yAttribute, value.Y, original.Y);
        }

        /// <summary>Replaces <paramref name="oldName"/> by <paramref name="newName"/> at the same position in the attribute list.</summary>
        private static void ReplaceAttribute(XElement element, string oldName, string newName, string value) =>
            element.ReplaceAttributes(element.Attributes()
                .Select(a => a.Name == oldName ? new XAttribute(newName, value) : a)
                .ToList());

        private static void SwapProgramSize(XElement program)
        {
            var dx = program.GetDxValue();
            program.SetAttributeValue("dx", program.GetDyValue());
            program.SetAttributeValue("dy", dx);
        }

        private static void SwapDxDyInExpressions(XElement element)
        {
            foreach (var attribute in element.Attributes().Where(a => ExpressionAttributes.Contains(a.Name.LocalName)))
            {
                attribute.Value = SwapDxDyIdentifiers(attribute.Value);
            }
        }

        private static string SwapAxisLetter(string letter) => letter switch
        {
            "x" => "y",
            "y" => "x",
            "X" => "Y",
            _ => "X",
        };

        private static T NextInCycle<T>(T[] cycle, T value, int steps) =>
            cycle[(Array.IndexOf(cycle, value) + steps) % cycle.Length];

        #endregion

        #region Frame and context

        /// <summary>Original part size and the number of clockwise quarter steps to apply.</summary>
        private readonly record struct Frame(double Dx, double Dy, int Steps)
        {
            public bool IsOdd => Steps % 2 == 1;

            /// <summary>Applies the steps to a position; each step: <c>(x, y) → (dy - y, x)</c>, then the size swaps.</summary>
            public Vec2 RotatePoint(Vec2 point)
            {
                var (x, y, dx, dy) = (point.X, point.Y, Dx, Dy);

                for (var i = 0; i < Steps; i++)
                {
                    (x, y) = (dy - y, x);
                    (dx, dy) = (dy, dx);
                }

                return new Vec2(x, y);
            }

            /// <summary>Applies the steps to a direction; each step: <c>(vx, vy) → (-vy, vx)</c>.</summary>
            public Vec2 RotateVector(Vec2 vector)
            {
                var (x, y) = (vector.X, vector.Y);

                for (var i = 0; i < Steps; i++)
                {
                    (x, y) = (-y, x);
                }

                return new Vec2(x, y);
            }
        }

        /// <summary>Walk state: the frame, the original symbols, and the tools declared so far.</summary>
        private sealed class RotationContext(Frame frame, XncSymbolTable symbols, bool flipEdgeGrooveTcl)
        {
            private readonly Dictionary<string, double> _toolDiameters = new(StringComparer.OrdinalIgnoreCase);

            public Frame Frame { get; } = frame;

            public bool FlipEdgeGrooveTcl { get; } = flipEdgeGrooveTcl;

            public void RegisterTool(XElement tool)
            {
                if (tool.GetNameValue() is { } name && ToolDiameter(tool) is { } diameter)
                {
                    _toolDiameters[name] = diameter;
                }
            }

            /// <summary>Makes <c>tool.dia</c> resolve to the diameter of the tool the element names.</summary>
            public void SelectTool(XElement element)
            {
                if (element.GetNameValue() is { } name && _toolDiameters.TryGetValue(name, out var diameter))
                {
                    symbols.SetToolDiameter(diameter);
                }
            }

            public double Eval(string? expression, string where) => XncSymbols.Eval(expression, symbols, where);

            public Vec2 ReadPoint(XElement element, string xAttribute, string yAttribute)
            {
                var tag = element.Name.LocalName;

                return new Vec2(
                    Eval(element.Attribute(xAttribute)?.Value, $"<{tag}> @{xAttribute}"),
                    Eval(element.Attribute(yAttribute)?.Value, $"<{tag}> @{yAttribute}"));
            }
        }

        #endregion
    }
}
