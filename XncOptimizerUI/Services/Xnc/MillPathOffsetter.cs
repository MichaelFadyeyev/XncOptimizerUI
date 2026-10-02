using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using static XncOptimizerUI.Services.Xnc.ContourOffsetGeometry;
using static XncOptimizerUI.Services.Xnc.XncProgramMath;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Shifts the tool traversal path of every mill in one decoded XNC <c>&lt;program&gt;</c> by a
    /// fixed distance to the right or left of its traversal direction, rewriting the element tree
    /// in place. Mills whose geometry cannot be resolved or would collapse are left untouched and
    /// counted as ignored.
    /// <para>Direction conventions (confirmed with the user):</para>
    /// <list type="bullet">
    /// <item>The traversal direction belongs to the mill's head element (<c>&lt;ms&gt;</c>,
    /// <c>&lt;mr&gt;</c>, <c>&lt;me&gt;</c>) through <c>fwd</c> (absent = <c>true</c>) and holds for
    /// all of its segments. An arc's <c>dir</c> only selects which arc is drawn
    /// (<c>true</c> = clockwise sweep); it never sets the traversal direction.</item>
    /// <item>Closed <c>&lt;ms&gt;</c> contours (pockets included): <c>fwd="true"</c> travels
    /// counter-clockwise, <c>fwd="false"</c> clockwise.</item>
    /// <item><c>&lt;mr&gt;</c> and <c>&lt;me&gt;</c> (pockets included): <c>fwd="true"</c> travels
    /// clockwise, <c>fwd="false"</c> counter-clockwise.</item>
    /// <item>Open paths: <c>fwd="true"</c> travels in authored order (entry, then each segment end),
    /// <c>fwd="false"</c> in reverse.</item>
    /// <item>Clockwise and right are meant in the operator's view, which is the raw XNC frame
    /// mirrored in Y (GibLab's own <c>dir="false"</c> circles sweep with the raw math angle
    /// decreasing).</item>
    /// </list>
    /// <para>The path geometry itself (shift, joins, open-end clipping, the operator-frame mirror)
    /// lives in <see cref="ContourOffsetGeometry"/>; this class only reads and rewrites elements.</para>
    /// </summary>
    internal static class MillPathOffsetter
    {
        /// <summary>
        /// Offsets every mill of <paramref name="program"/> whose kind is in <paramref name="kinds"/>;
        /// returns how many were offset, ignored (unresolvable or collapsing) and skipped (kind not
        /// selected). A contour that can't be read is counted as ignored whatever its kind.
        /// </summary>
        public static (int offset, int ignored, int skipped) OffsetInProgram(
            XElement program, double distance, MillOffsetSide side, MillPathKinds kinds)
        {
            var symbols = SeedProgramSymbols(program);
            var outline = new Vec2(
                RequireProgramDouble(program.GetDxValue(), "dx"),
                RequireProgramDouble(program.GetDyValue(), "dy"));

            var outcomes = CollectMills(program)
                .Select(mill => TryOffsetMill(mill, symbols, outline, distance, side, kinds))
                .ToList();

            return (
                outcomes.Count(o => o == Outcome.Offset),
                outcomes.Count(o => o == Outcome.Ignored),
                outcomes.Count(o => o == Outcome.Skipped));
        }

        private enum Outcome
        {
            Offset,
            Ignored,
            Skipped
        }

        private static Outcome ToOutcome(bool offset) => offset ? Outcome.Offset : Outcome.Ignored;

        private static MillPathKinds KindOf(bool closed) => closed ? MillPathKinds.Closed : MillPathKinds.Open;

        #region Mill discovery

        private sealed record Mill(XElement Head, IReadOnlyList<XElement> Segments);

        /// <summary>Snapshots every mill before any rewrite, so inserted round-join arcs are never picked up as input.</summary>
        private static List<Mill> CollectMills(XElement program)
        {
            var elements = program.Elements().ToList();

            return elements
                .Select((element, index) => (element, index))
                .Where(e => e.element.Name.LocalName is "ms" or "mr" or "me")
                .Select(e => new Mill(
                    e.element,
                    e.element.Name.LocalName == "ms"
                        ? elements.Skip(e.index + 1).TakeWhile(IsContourSegment).ToList()
                        : []))
                .ToList();
        }

        private static bool IsContourSegment(XElement element) => element.Name.LocalName is "ml" or "mac" or "ma";

        private static Outcome TryOffsetMill(
            Mill mill, XncSymbolTable symbols, Vec2 outline, double distance, MillOffsetSide side, MillPathKinds kinds)
        {
            var closedSelected = kinds.HasFlag(MillPathKinds.Closed);

            try
            {
                return mill.Head.Name.LocalName switch
                {
                    "mr" => closedSelected ? ToOutcome(TryOffsetRectangle(mill.Head, symbols, distance, side)) : Outcome.Skipped,
                    "me" => closedSelected ? ToOutcome(TryOffsetEllipse(mill.Head, symbols, distance, side)) : Outcome.Skipped,
                    _ => TryOffsetContour(mill, symbols, outline, distance, side, kinds),
                };
            }
            catch (Exception)
            {
                // A value that doesn't resolve to a number can't be offset geometrically. Every
                // attribute write happens after all evaluation, so the mill is still untouched.
                return Outcome.Ignored;
            }
        }

        #endregion

        #region Traversal side

        private static bool ReadForward(XElement head) => !bool.TryParse(head.GetFwdValue(), out var forward) || forward;

        #endregion

        #region Rectangles and ellipses

        private static bool TryOffsetRectangle(XElement mr, XncSymbolTable symbols, double distance, MillOffsetSide side)
        {
            var growth = OutwardSign(TravelsCounterClockwise(ReadForward(mr), isRectangleOrEllipse: true), side) * distance;

            var length = EvalXnc(mr.GetLengthValue(), symbols);
            var width = EvalXnc(mr.GetWidthValue(), symbols);
            var radius = mr.GetRValue() is { } rawRadius ? EvalXnc(rawRadius, symbols) : 0d;

            var newLength = length + (2d * growth);
            var newWidth = width + (2d * growth);

            if (newLength <= Tolerance || newWidth <= Tolerance)
            {
                return false;
            }

            SetNumber(mr, "l", newLength, length);
            SetNumber(mr, "w", newWidth, width);
            SetNumber(mr, "r", Math.Max(0d, radius + growth), radius);

            return true;
        }

        private static bool TryOffsetEllipse(XElement me, XncSymbolTable symbols, double distance, MillOffsetSide side)
        {
            var growth = OutwardSign(TravelsCounterClockwise(ReadForward(me), isRectangleOrEllipse: true), side) * distance;

            // <me> l/w are semi-axes, so they move by the offset itself.
            var length = EvalXnc(me.GetLengthValue(), symbols);
            var width = EvalXnc(me.GetWidthValue(), symbols);

            var newLength = length + growth;
            var newWidth = width + growth;

            if (newLength <= Tolerance || newWidth <= Tolerance)
            {
                return false;
            }

            SetNumber(me, "l", newLength, length);
            SetNumber(me, "w", newWidth, width);

            return true;
        }

        #endregion

        #region Contours

        private static Outcome TryOffsetContour(
            Mill mill, XncSymbolTable symbols, Vec2 outline, double distance, MillOffsetSide side, MillPathKinds kinds)
        {
            if (mill.Segments.Count == 0)
            {
                return Outcome.Ignored;
            }

            var entry = ReadPoint(mill.Head, symbols);

            if (ReadPath(entry, mill.Segments, symbols) is not { } path)
            {
                return Outcome.Ignored;
            }

            if (!kinds.HasFlag(KindOf(IsClosed(path))))
            {
                return Outcome.Skipped;
            }

            var layout = TryOffset(path, ReadForward(mill.Head), outline, distance, side);

            if (layout == null)
            {
                return Outcome.Ignored;
            }

            WriteContour(mill, entry, path, layout);

            return Outcome.Offset;
        }

        private static List<Primitive>? ReadPath(Vec2 entry, IReadOnlyList<XElement> segments, XncSymbolTable symbols)
        {
            var path = new List<Primitive>(segments.Count);
            var current = entry;

            foreach (var segment in segments)
            {
                if (ReadPrimitive(current, segment, symbols) is not { } primitive)
                {
                    return null;
                }

                path.Add(primitive);
                current = primitive.End;
            }

            return path;
        }

        private static Primitive? ReadPrimitive(Vec2 start, XElement segment, XncSymbolTable symbols)
        {
            var end = ReadPoint(segment, symbols);

            return segment.Name.LocalName switch
            {
                "ml" => start.DistanceTo(end) > Tolerance ? Primitive.Line(start, end) : null,
                "mac" => ReadCentreArc(start, end, segment, symbols),
                _ => ReadRadiusArc(start, end, segment, symbols),
            };
        }

        private static Primitive? ReadCentreArc(Vec2 start, Vec2 end, XElement mac, XncSymbolTable symbols)
        {
            var centre = new Vec2(EvalXnc(mac.GetCxValue(), symbols), EvalXnc(mac.GetCyValue(), symbols));
            var radius = start.DistanceTo(centre);

            return radius > Tolerance && Math.Abs(end.DistanceTo(centre) - radius) <= Tolerance
                ? Primitive.Arc(start, end, centre, radius, IsClockwiseDir(mac))
                : null;
        }

        private static Primitive? ReadRadiusArc(Vec2 start, Vec2 end, XElement ma, XncSymbolTable symbols)
        {
            var radius = EvalXnc(ma.GetRValue(), symbols);

            return TryReconstructArcCentre(
                    start.X, start.Y, end.X, end.Y, radius, ParseDirSign(ma.GetDirValue()),
                    out var centreX, out var centreY)
                ? Primitive.Arc(start, end, new Vec2(centreX, centreY), radius, IsClockwiseDir(ma))
                : null;
        }

        private static bool IsClockwiseDir(XElement arc) => ParseDirSign(arc.GetDirValue()) > 0;

        private static Vec2 ReadPoint(XElement element, XncSymbolTable symbols) =>
            new(EvalXnc(element.GetXValue(), symbols), EvalXnc(element.GetYValue(), symbols));

        #endregion

        #region Output

        private static void WriteContour(Mill mill, Vec2 entry, IReadOnlyList<Primitive> path, OffsetLayout layout)
        {
            SetPoint(mill.Head, layout.Entry, entry);

            for (var i = 0; i < mill.Segments.Count; i++)
            {
                var segment = mill.Segments[i];

                SetPoint(segment, layout.Primitives[i].End, path[i].End);

                if (segment.Name.LocalName == "ma")
                {
                    SetNumber(segment, "r", layout.Primitives[i].Radius, path[i].Radius);
                }

                if (layout.RoundJoins[i] is { } roundJoin)
                {
                    segment.AddAfterSelf(MakeCentreArc(roundJoin));
                }
            }
        }

        private static XElement MakeCentreArc(Primitive arc) => new("mac",
            new XAttribute("x", FormatNumber(arc.End.X)),
            new XAttribute("y", FormatNumber(arc.End.Y)),
            new XAttribute("cx", FormatNumber(arc.Centre!.Value.X)),
            new XAttribute("cy", FormatNumber(arc.Centre!.Value.Y)),
            new XAttribute("dir", arc.Clockwise ? "true" : "false"));

        private static void SetPoint(XElement element, Vec2 value, Vec2 original)
        {
            SetNumber(element, "x", value.X, original.X);
            SetNumber(element, "y", value.Y, original.Y);
        }

        #endregion
    }
}
