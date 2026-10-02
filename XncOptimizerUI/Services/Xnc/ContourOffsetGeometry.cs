using XncOptimizerUI.MVVM.Models.Xnc;
using static XncOptimizerUI.Services.Xnc.XncProgramMath;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Pure geometry of shifting a milling path (a chain of line/arc <see cref="Primitive"/>s) by a
    /// fixed distance to the right or left of its traversal direction. Shared by
    /// <see cref="MillPathOffsetter"/> (rewrites the XNC program) and the part preview's mill
    /// overlay (draws the real tool path next to the programmed centre line).
    /// <para>Direction conventions (confirmed with the user):</para>
    /// <list type="bullet">
    /// <item>Closed <c>&lt;ms&gt;</c> contours (pockets included): <c>fwd="true"</c> travels
    /// counter-clockwise, <c>fwd="false"</c> clockwise.</item>
    /// <item><c>&lt;mr&gt;</c> and <c>&lt;me&gt;</c> (pockets included): the opposite -
    /// <c>fwd="true"</c> travels clockwise, <c>fwd="false"</c> counter-clockwise.</item>
    /// <item>Open paths: <c>fwd="true"</c> travels in authored order, <c>fwd="false"</c> in reverse.</item>
    /// <item>Clockwise and right are meant in the operator's view, which is the raw XNC frame
    /// mirrored in Y (GibLab's own <c>dir="false"</c> circles sweep with the raw math angle
    /// decreasing) - the same Y-down frame the part preview draws raw coordinates in. Only the
    /// "Operator frame" region below encodes that mirror.</item>
    /// </list>
    /// </summary>
    internal static class ContourOffsetGeometry
    {
        internal const double Tolerance = GeomTolerance;

        /// <summary>A convex corner whose sharp (mitred) offset reaches farther than this many offset distances from the original vertex is rounded instead.</summary>
        private const double MiterLimit = 4d;

        /// <summary>A straight or circular piece of a contour; arcs keep their centre when offset. <see cref="Clockwise"/> is in the operator's sense.</summary>
        internal sealed record Primitive(Vec2 Start, Vec2 End, Vec2? Centre, double Radius, bool Clockwise)
        {
            public static Primitive Line(Vec2 start, Vec2 end) => new(start, end, null, 0d, false);

            public static Primitive Arc(Vec2 start, Vec2 end, Vec2 centre, double radius, bool clockwise) =>
                new(start, end, centre, radius, clockwise);

            public bool IsArc => Centre.HasValue;

            public Vec2 Direction => End - Start;

            public Primitive WithEnds(Vec2 start, Vec2 end) => this with { Start = start, End = end };

            /// <summary>The same piece travelled the other way.</summary>
            public Primitive Reversed() => this with { Start = End, End = Start, Clockwise = !Clockwise };
        }

        /// <summary>Where two consecutive offset primitives meet; a convex gap is bridged by <see cref="RoundJoin"/>.</summary>
        private sealed record Joint(Vec2 In, Vec2 Out, Primitive? RoundJoin);

        /// <summary>The offset contour: new entry, one trimmed primitive per segment, and any round join to insert after each.</summary>
        internal sealed record OffsetLayout(Vec2 Entry, IReadOnlyList<Primitive> Primitives, IReadOnlyList<Primitive?> RoundJoins)
        {
            /// <summary>The offset path as one primitive chain, round joins inserted in place.</summary>
            public IReadOnlyList<Primitive> Chain() => Primitives
                .Zip(RoundJoins)
                .SelectMany(pair => pair.Second is { } join ? new[] { pair.First, join } : new[] { pair.First })
                .ToList();
        }

        /// <summary>Whether <paramref name="path"/> ends where it starts.</summary>
        internal static bool IsClosed(IReadOnlyList<Primitive> path) =>
            path.Count > 0 && path[^1].End.DistanceTo(path[0].Start) <= Tolerance;

        /// <summary>
        /// Shifts <paramref name="path"/> (authored order, starting at its entry) by
        /// <paramref name="distance"/> to <paramref name="side"/> of its traversal direction, given by
        /// <paramref name="forward"/> (the <c>&lt;ms&gt;</c> contour's <c>fwd</c>). Open-path ends on or beyond
        /// <paramref name="outline"/> (the part's <c>dx</c>/<c>dy</c>) keep their distance from the
        /// crossed edge. Returns <c>null</c> when the offset collapses or reverses part of the path.
        /// </summary>
        internal static OffsetLayout? TryOffset(
            IReadOnlyList<Primitive> path, bool forward, Vec2 outline, double distance, MillOffsetSide side)
        {
            if (path.Count == 0)
            {
                return null;
            }

            var closed = IsClosed(path);
            var shift = distance * (closed ? ClosedShiftSign(path, forward, side) : OpenShiftSign(forward, side));
            var shifted = path.Select(p => Shift(p, shift)).ToList();

            if (shifted.Any(p => p.IsArc && p.Radius <= Tolerance))
            {
                return null; // an inward offset swallows an arc entirely
            }

            var layout = closed ? JoinClosed(path, shifted) : JoinOpen(path, shifted, outline);

            return layout != null && KeepsShape(path, layout) ? layout : null;
        }

        #region Traversal side

        private static int SideSign(MillOffsetSide side) => side == MillOffsetSide.Right ? 1 : -1;

        /// <summary>
        /// Whether a closed mill with the given <c>fwd</c> travels counter-clockwise (operator's
        /// view): a closed <c>&lt;ms&gt;</c> contour does for <c>fwd="true"</c>, while
        /// <c>&lt;mr&gt;</c> and <c>&lt;me&gt;</c> run clockwise for <c>fwd="true"</c> - pockets
        /// (<c>c="3"</c>) included.
        /// </summary>
        internal static bool TravelsCounterClockwise(bool forward, bool isRectangleOrEllipse) =>
            isRectangleOrEllipse ? !forward : forward;

        /// <summary>
        /// <c>+1</c> when the offset grows a closed mill, <c>-1</c> when it shrinks it:
        /// counter-clockwise travel has the outside on its right.
        /// </summary>
        internal static int OutwardSign(bool counterClockwise, MillOffsetSide side) => (counterClockwise ? 1 : -1) * SideSign(side);

        /// <summary>Signed shift along the authored direction's right normal for a closed contour.</summary>
        private static int ClosedShiftSign(IReadOnlyList<Primitive> path, bool forward, MillOffsetSide side)
        {
            // Authored clockwise has the inside on its right, counter-clockwise the outside.
            var authoredClockwise = OperatorSignedArea(path) < 0d;
            var outward = OutwardSign(TravelsCounterClockwise(forward, isRectangleOrEllipse: false), side);

            return authoredClockwise ? -outward : outward;
        }

        /// <summary>Signed shift along the authored direction's right normal for an open contour.</summary>
        private static int OpenShiftSign(bool forward, MillOffsetSide side) => (forward ? 1 : -1) * SideSign(side);

        #endregion

        #region Operator frame

        /// <summary>Right of the travel direction <paramref name="direction"/>, as the operator sees it (raw left).</summary>
        internal static Vec2 RightNormal(Vec2 direction)
        {
            var u = direction.Normalized();

            return new Vec2(-u.Y, u.X);
        }

        /// <summary>Signed sweep in the operator's sense (counter-clockwise positive); a clockwise (<c>dir="true"</c>) arc sweeps raw counter-clockwise.</summary>
        internal static double OperatorSweep(Primitive arc)
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
        internal static double OperatorSignedArea(IReadOnlyList<Primitive> path)
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

        #region Shift and join

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

        /// <summary>Unit travel direction at the end of <paramref name="primitive"/>.</summary>
        internal static Vec2 TangentAtEnd(Primitive primitive)
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

        /// <summary>Unit travel direction at the start of <paramref name="primitive"/>.</summary>
        internal static Vec2 TangentAtStart(Primitive primitive) => TangentAtEnd(primitive.Reversed()) * -1d;

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

        #region Validation

        /// <summary>Rejects an offset that reversed a line or turned an arc inside out (offset larger than the feature).</summary>
        private static bool KeepsShape(IReadOnlyList<Primitive> path, OffsetLayout layout) =>
            path.Zip(layout.Primitives).All(pair => pair.First.IsArc
                ? Math.Abs(OperatorSweep(pair.Second) - OperatorSweep(pair.First)) < Math.PI
                : pair.Second.Direction.Length > Tolerance && pair.Second.Direction.Dot(pair.First.Direction) > 0d);

        #endregion
    }
}
