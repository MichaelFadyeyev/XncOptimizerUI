using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.Services.Xnc.Geometry;
using static XncOptimizerUI.Services.Xnc.Geometry.ContourOffsetGeometry;
using static XncOptimizerUI.Services.Xnc.XncAttributeParsing;
using static XncOptimizerUI.Services.Xnc.XncSymbols;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Finds the mills (<c>&lt;ms&gt;</c> contours with their segments, <c>&lt;mr&gt;</c>,
    /// <c>&lt;me&gt;</c>) of one decoded XNC <c>&lt;program&gt;</c> and reads a contour's centre line
    /// as <see cref="Primitive"/>s. Shared by the program rewriters that work on mills
    /// (<see cref="MillPathOffsetter"/>, <see cref="OutsideOperationDetector"/>).
    /// </summary>
    internal static class XncMillElements
    {
        /// <summary>A mill's head element and, for an <c>&lt;ms&gt;</c> contour, the segments that follow it.</summary>
        internal sealed record Mill(XElement Head, IReadOnlyList<XElement> Segments);

        /// <summary>Snapshots every mill of <paramref name="program"/>, so later rewrites never change the input set.</summary>
        internal static List<Mill> CollectMills(XElement program)
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

        internal static bool IsContourSegment(XElement element) => element.Name.LocalName is "ml" or "mac" or "ma";

        /// <summary>The mill's <c>fwd</c>; absent or unparseable reads as <c>true</c>.</summary>
        internal static bool ReadForward(XElement head) => !bool.TryParse(head.GetFwdValue(), out var forward) || forward;

        /// <summary>
        /// The centre line of a contour from <paramref name="entry"/> through its segments, in authored
        /// order, or <c>null</c> when a segment is degenerate (zero-length line, arc that does not fit).
        /// </summary>
        internal static List<Primitive>? ReadPath(Vec2 entry, IReadOnlyList<XElement> segments, XncSymbolTable symbols)
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

        internal static Vec2 ReadPoint(XElement element, XncSymbolTable symbols) =>
            new(Eval(element.GetXValue(), symbols), Eval(element.GetYValue(), symbols));

        private static Primitive? ReadPrimitive(Vec2 start, XElement segment, XncSymbolTable symbols)
        {
            var end = ReadPoint(segment, symbols);

            return segment.Name.LocalName switch
            {
                "ml" => start.DistanceTo(end) > PlanarGeometry.Tolerance ? Primitive.Line(start, end) : null,
                "mac" => ReadCentreArc(start, end, segment, symbols),
                _ => ReadRadiusArc(start, end, segment, symbols),
            };
        }

        private static Primitive? ReadCentreArc(Vec2 start, Vec2 end, XElement mac, XncSymbolTable symbols)
        {
            var centre = new Vec2(Eval(mac.GetCxValue(), symbols), Eval(mac.GetCyValue(), symbols));
            var radius = start.DistanceTo(centre);

            return radius > PlanarGeometry.Tolerance && Math.Abs(end.DistanceTo(centre) - radius) <= PlanarGeometry.Tolerance
                ? Primitive.Arc(start, end, centre, radius, IsClockwise(mac))
                : null;
        }

        private static Primitive? ReadRadiusArc(Vec2 start, Vec2 end, XElement ma, XncSymbolTable symbols)
        {
            var radius = Eval(ma.GetRValue(), symbols);

            return PlanarGeometry.ArcCentreFromChord(start, end, radius, IsClockwise(ma)) is { } centre
                ? Primitive.Arc(start, end, centre, radius, IsClockwise(ma))
                : null;
        }
    }
}
