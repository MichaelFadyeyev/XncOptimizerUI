using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
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
    /// <item>Closed paths, <c>&lt;mr&gt;</c> and <c>&lt;me&gt;</c>: <c>fwd="true"</c> travels
    /// counter-clockwise, <c>fwd="false"</c> clockwise.</item>
    /// <item>Open paths: <c>fwd="true"</c> travels in authored order (entry, then each segment end),
    /// <c>fwd="false"</c> in reverse.</item>
    /// <item>Clockwise and right are meant in the operator's view, which is the raw XNC frame
    /// mirrored in Y (GibLab's own <c>dir="false"</c> circles sweep with the raw math angle
    /// decreasing). Only the "Operator frame" region below encodes that mirror.</item>
    /// </list>
    /// </summary>
    internal static class MillPathOffsetter
    {
        private const double Tolerance = GeomTolerance;

        /// <summary>A convex corner whose sharp (mitred) offset reaches farther than this many offset distances from the original vertex is rounded instead.</summary>
        private const double MiterLimit = 4d;

        private const int OutputDecimals = 4;

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

        private static int SideSign(MillOffsetSide side) => side == MillOffsetSide.Right ? 1 : -1;

        /// <summary>
        /// <c>+1</c> when the offset grows a closed mill, <c>-1</c> when it shrinks it:
        /// <c>fwd="true"</c> travels counter-clockwise, which has the outside on its right.
        /// </summary>
        private static int OutwardSign(bool forward, MillOffsetSide side) => (forward ? 1 : -1) * SideSign(side);

        /// <summary>Signed shift along the authored direction's right normal for a closed contour.</summary>
        private static int ClosedShiftSign(IReadOnlyList<Primitive> path, bool forward, MillOffsetSide side)
        {
            // Authored clockwise has the inside on its right, counter-clockwise the outside.
            var authoredClockwise = OperatorSignedArea(path) < 0d;

            return authoredClockwise ? -OutwardSign(forward, side) : OutwardSign(forward, side);
        }

        /// <summary>Signed shift along the authored direction's right normal for an open contour.</summary>
        private static int OpenShiftSign(bool forward, MillOffsetSide side) => (forward ? 1 : -1) * SideSign(side);

        #endregion

        #region Operator frame

        /// <summary>Right of the travel direction <paramref name="direction"/>, as the operator sees it (raw left).</summary>
        private static Vec2 RightNormal(Vec2 direction)
        {
            var u = direction.Normalized();

            return new Vec2(-u.Y, u.X);
        }

        /// <summary>Signed sweep in the operator's sense (counter-clockwise positive); a clockwise (<c>dir="true"</c>) arc sweeps raw counter-clockwise.</summary>
        private static double OperatorSweep(Primitive arc)
        {
            var centre = arc.Centre!.Value;
            var startAngle = Math.Atan2(arc.Start.Y - centre.Y, arc.Start.X - centre.X);
            var endAngle = Math.Atan2(arc.End.Y - centre.Y, arc.End.X - centre.X);

            var magnitude = arc.Start.DistanceTo(arc.End) <= Tolerance
                ? 2d * Math.PI
                : PlanarGeometry.NormalizeAngle(arc.Clockwise ? endAngle - startAngle : startAngle - endAngle);

            return arc.Clockwise ? -magnitude : magnitude;
        }

        /// <summary>Whether turning from <paramref name="from"/> to <paramref name="to"/> the short way is clockwise for the operator.</summary>
        private static bool IsClockwiseTurn(Vec2 from, Vec2 to) => from.Cross(to) > 0d;

        /// <summary>Area enclosed by a closed path in the operator's sense: positive when it runs counter-clockwise.</summary>
        private static double OperatorSignedArea(IReadOnlyList<Primitive> path)
        {
            // Shoelace over the chords (raw sense, mirrored), plus each arc's circular segment.
            var chords = -path.Sum(p => p.Start.Cross(p.End)) / 2d;
            var bulges = path
                .Where(p => p.IsArc)
                .Sum(p =>
                {
                    var sweep = OperatorSweep(p);

                    return p.Radius * p.Radius / 2d * (sweep - Math.Sin(sweep));
                });

            return chords + bulges;
        }

        #endregion

        #region Rectangles and ellipses

        private static bool TryOffsetRectangle(XElement mr, XncSymbolTable symbols, double distance, MillOffsetSide side)
        {
            var growth = OutwardSign(ReadForward(mr), side) * distance;

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
            var growth = OutwardSign(ReadForward(me), side) * distance;

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

        /// <summary>A straight or circular piece of a contour; arcs keep their centre when offset.</summary>
        private sealed record Primitive(Vec2 Start, Vec2 End, Vec2? Centre, double Radius, bool Clockwise)
        {
            public static Primitive Line(Vec2 start, Vec2 end) => new(start, end, null, 0d, false);

            public static Primitive Arc(Vec2 start, Vec2 end, Vec2 centre, double radius, bool clockwise) =>
                new(start, end, centre, radius, clockwise);

            public bool IsArc => Centre.HasValue;

            public Vec2 Direction => End - Start;

            public Primitive WithEnds(Vec2 start, Vec2 end) => this with { Start = start, End = end };
        }

        /// <summary>Where two consecutive offset primitives meet; a convex gap is bridged by <see cref="RoundJoin"/>.</summary>
        private sealed record Joint(Vec2 In, Vec2 Out, Primitive? RoundJoin);

        /// <summary>The offset contour: new entry, one trimmed primitive per segment, and any round join to insert after each.</summary>
        private sealed record OffsetLayout(Vec2 Entry, IReadOnlyList<Primitive> Primitives, IReadOnlyList<Primitive?> RoundJoins);

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

            var closed = path[^1].End.DistanceTo(entry) <= Tolerance;

            return kinds.HasFlag(KindOf(closed))
                ? ToOutcome(TryOffsetPath(mill, entry, path, closed, outline, distance, side))
                : Outcome.Skipped;
        }

        private static bool TryOffsetPath(
            Mill mill, Vec2 entry, IReadOnlyList<Primitive> path, bool closed, Vec2 outline, double distance, MillOffsetSide side)
        {
            var forward = ReadForward(mill.Head);

            var shift = distance * (closed ? ClosedShiftSign(path, forward, side) : OpenShiftSign(forward, side));
            var shifted = path.Select(p => Shift(p, shift)).ToList();

            if (shifted.Any(p => p.IsArc && p.Radius <= Tolerance))
            {
                return false; // an inward offset swallows an arc entirely
            }

            var layout = closed ? JoinClosed(path, shifted) : JoinOpen(path, shifted, outline);

            if (layout == null || !KeepsShape(path, layout))
            {
                return false;
            }

            WriteContour(mill, entry, path, layout);

            return true;
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

        /// <summary>Moves a primitive by <paramref name="shift"/> along the right normal of its travel direction.</summary>
        private static Primitive Shift(Primitive primitive, double shift)
        {
            if (primitive.Centre is not { } centre)
            {
                var offset = RightNormal(primitive.Direction) * shift;

                return primitive.WithEnds(primitive.Start + offset, primitive.End + offset);
            }

            // A clockwise arc has its centre on the right, so shifting right shrinks it.
            var radius = primitive.Radius + (primitive.Clockwise ? -shift : shift);

            return primitive with
            {
                Start = centre + ((primitive.Start - centre) * (radius / primitive.Radius)),
                End = centre + ((primitive.End - centre) * (radius / primitive.Radius)),
                Radius = radius,
            };
        }

        private static OffsetLayout? JoinClosed(IReadOnlyList<Primitive> path, IReadOnlyList<Primitive> shifted)
        {
            var count = shifted.Count;
            var joints = Enumerable.Range(0, count)
                .Select(i => Join(shifted[i], shifted[(i + 1) % count], path[i].End))
                .ToList();

            if (joints.Any(j => j == null))
            {
                return null;
            }

            // The joint between the last and the first primitive becomes the new entry point.
            var primitives = Enumerable.Range(0, count)
                .Select(i => shifted[i].WithEnds(joints[(i + count - 1) % count]!.Out, joints[i]!.In))
                .ToList();

            return new OffsetLayout(joints[^1]!.Out, primitives, joints.Select(j => j!.RoundJoin).ToList());
        }

        private static OffsetLayout? JoinOpen(IReadOnlyList<Primitive> path, IReadOnlyList<Primitive> shifted, Vec2 outline)
        {
            var count = shifted.Count;
            var joints = Enumerable.Range(0, count - 1)
                .Select(i => Join(shifted[i], shifted[i + 1], path[i].End))
                .ToList();

            if (joints.Any(j => j == null))
            {
                return null;
            }

            var start = ClipOpenEnd(path[0], shifted[0], atStart: true, outline);
            var end = ClipOpenEnd(path[^1], shifted[^1], atStart: false, outline);

            var primitives = Enumerable.Range(0, count)
                .Select(i => shifted[i].WithEnds(
                    i == 0 ? start : joints[i - 1]!.Out,
                    i == count - 1 ? end : joints[i]!.In))
                .ToList();

            var roundJoins = joints.Select(j => j!.RoundJoin).Append(null).ToList();

            return new OffsetLayout(start, primitives, roundJoins);
        }

        /// <summary>
        /// Joins the offset copies of two consecutive primitives: tangent pieces meet already;
        /// otherwise the pieces are extended or trimmed to their intersection, and a convex gap
        /// with no usable intersection is bridged by an arc around the original vertex.
        /// Returns <c>null</c> for a concave corner the offset cannot resolve.
        /// </summary>
        private static Joint? Join(Primitive previous, Primitive next, Vec2 vertex)
        {
            var arrival = previous.End;
            var departure = next.Start;

            if (arrival.DistanceTo(departure) <= Tolerance)
            {
                var meeting = Vec2.Midpoint(arrival, departure);

                return new Joint(meeting, meeting, null);
            }

            var convex = (departure - arrival).Dot(TangentAtEnd(previous)) > 0d;
            var hit = PlanarGeometry.Nearest(Intersect(previous, next), Vec2.Midpoint(arrival, departure));
            var miterReach = MiterLimit * arrival.DistanceTo(vertex);

            if (hit is { } point && (!convex || point.DistanceTo(vertex) <= miterReach))
            {
                return new Joint(point, point, null);
            }

            return convex ? new Joint(arrival, departure, RoundJoin(arrival, departure, vertex)) : null;
        }

        private static Primitive RoundJoin(Vec2 from, Vec2 to, Vec2 vertex) =>
            Primitive.Arc(from, to, vertex, from.DistanceTo(vertex), IsClockwiseTurn(from - vertex, to - vertex));

        private static Vec2 TangentAtEnd(Primitive primitive)
        {
            if (primitive.Centre is not { } centre)
            {
                return primitive.Direction.Normalized();
            }

            // A clockwise arc keeps its centre on its right, so it travels along the right normal
            // of the outward radius; a counter-clockwise arc travels the opposite way.
            var clockwiseTangent = RightNormal(primitive.End - centre);

            return primitive.Clockwise ? clockwiseTangent : clockwiseTangent * -1d;
        }

        /// <summary>Intersections of two primitives taken as an infinite line or a full circle.</summary>
        private static IReadOnlyList<Vec2> Intersect(Primitive a, Primitive b) => (a.Centre, b.Centre) switch
        {
            (null, null) => PlanarGeometry.IntersectLines(a.Start, a.Direction, b.Start, b.Direction),
            (null, { } cb) => PlanarGeometry.IntersectLineCircle(a.Start, a.Direction, cb, b.Radius),
            ({ } ca, null) => PlanarGeometry.IntersectLineCircle(b.Start, b.Direction, ca, a.Radius),
            ({ } ca, { } cb) => PlanarGeometry.IntersectCircles(ca, a.Radius, cb, b.Radius),
        };

        private static IReadOnlyList<Vec2> IntersectWithLine(Primitive primitive, Vec2 point, Vec2 direction) =>
            primitive.Centre is { } centre
                ? PlanarGeometry.IntersectLineCircle(point, direction, centre, primitive.Radius)
                : PlanarGeometry.IntersectLines(primitive.Start, primitive.Direction, point, direction);

        #endregion

        #region Open path ends

        /// <summary>
        /// New position of an open path's entry or final end. An end on or beyond the part outline
        /// keeps its perpendicular distance from the outline edge the path crosses (it slides along
        /// a guide line parallel to that edge); an end inside the part is offset perpendicularly.
        /// </summary>
        private static Vec2 ClipOpenEnd(Primitive original, Primitive shifted, bool atStart, Vec2 outline)
        {
            var point = atStart ? original.Start : original.End;
            var perpendicular = atStart ? shifted.Start : shifted.End;

            if (CrossedEdgeGuide(point, atStart ? original.End : original.Start, outline) is not { } guideDirection)
            {
                return perpendicular;
            }

            return PlanarGeometry.Nearest(IntersectWithLine(shifted, point, guideDirection), perpendicular)
                ?? perpendicular;
        }

        /// <summary>
        /// Direction of the outline edge (through <paramref name="point"/>) that an end on or beyond
        /// the outline belongs to, or <c>null</c> for an end inside the part. Near a corner the edge
        /// the chord towards <paramref name="toward"/> actually crosses wins, then the farther one.
        /// </summary>
        private static Vec2? CrossedEdgeGuide(Vec2 point, Vec2 toward, Vec2 outline)
        {
            var vertical = new Vec2(0d, 1d);
            var horizontal = new Vec2(1d, 0d);

            var edges = new[]
            {
                (Outside: -point.X, Guide: vertical, Crosses: CrossesX(point, toward, 0d, outline.Y)),
                (Outside: point.X - outline.X, Guide: vertical, Crosses: CrossesX(point, toward, outline.X, outline.Y)),
                (Outside: -point.Y, Guide: horizontal, Crosses: CrossesY(point, toward, 0d, outline.X)),
                (Outside: point.Y - outline.Y, Guide: horizontal, Crosses: CrossesY(point, toward, outline.Y, outline.X)),
            };

            return edges
                .Where(e => e.Outside >= -Tolerance)
                .OrderByDescending(e => e.Crosses)
                .ThenByDescending(e => e.Outside)
                .Select(e => (Vec2?)e.Guide)
                .FirstOrDefault();
        }

        /// <summary>Whether segment <paramref name="a"/>-<paramref name="b"/> crosses the line <c>x = <paramref name="x"/></c> within <c>[0, <paramref name="height"/>]</c>.</summary>
        private static bool CrossesX(Vec2 a, Vec2 b, double x, double height)
        {
            if (Math.Abs(b.X - a.X) <= Tolerance)
            {
                return false;
            }

            var t = (x - a.X) / (b.X - a.X);
            var y = a.Y + ((b.Y - a.Y) * t);

            return t >= 0d && t <= 1d && y >= -Tolerance && y <= height + Tolerance;
        }

        private static bool CrossesY(Vec2 a, Vec2 b, double y, double width) =>
            CrossesX(new Vec2(a.Y, a.X), new Vec2(b.Y, b.X), y, width);

        #endregion

        #region Validation and output

        /// <summary>Rejects an offset that reversed a line or turned an arc inside out (offset larger than the feature).</summary>
        private static bool KeepsShape(IReadOnlyList<Primitive> path, OffsetLayout layout) =>
            path.Zip(layout.Primitives).All(pair => pair.First.IsArc
                ? Math.Abs(OperatorSweep(pair.Second) - OperatorSweep(pair.First)) < Math.PI
                : pair.Second.Direction.Length > Tolerance && pair.Second.Direction.Dot(pair.First.Direction) > 0d);

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
            new XAttribute("x", Format(arc.End.X)),
            new XAttribute("y", Format(arc.End.Y)),
            new XAttribute("cx", Format(arc.Centre!.Value.X)),
            new XAttribute("cy", Format(arc.Centre!.Value.Y)),
            new XAttribute("dir", arc.Clockwise ? "true" : "false"));

        private static void SetPoint(XElement element, Vec2 value, Vec2 original)
        {
            SetNumber(element, "x", value.X, original.X);
            SetNumber(element, "y", value.Y, original.Y);
        }

        /// <summary>Writes a changed value; an unchanged one keeps its authored text (e.g. an expression such as <c>dx+10</c>).</summary>
        private static void SetNumber(XElement element, string attribute, double value, double original)
        {
            if (Format(value) != Format(original))
            {
                element.SetAttributeValue(attribute, Format(value));
            }
        }

        private static string Format(double value)
        {
            var rounded = Math.Round(value, OutputDecimals);

            return XmlConvert.ToString(rounded == 0d ? 0d : rounded);
        }

        #endregion
    }
}
