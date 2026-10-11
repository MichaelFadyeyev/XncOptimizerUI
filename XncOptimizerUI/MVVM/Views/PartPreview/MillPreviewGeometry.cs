using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;
using Primitive = XncOptimizerUI.Services.Xnc.ContourOffsetGeometry.Primitive;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// WPF-free geometry behind <see cref="MillPreviewRenderer"/>, in raw program millimetres. Raw
    /// <c>x</c>/<c>y</c> are drawn straight onto the Y-down canvas, which is exactly the operator's
    /// view <see cref="ContourOffsetGeometry"/> defines clockwise/right in - so "right of travel"
    /// here is right on screen.
    /// <para>Every mill becomes one <see cref="MillPath"/> whose <see cref="MillPath.Travel"/> chain
    /// already runs in the real traversal direction (<c>fwd</c> applied), so tool side, start point
    /// and cut-off side all read straight off it.</para>
    /// </summary>
    internal static class MillPreviewGeometry
    {
        /// <summary>Line pieces per full turn used to approximate a non-circular <c>&lt;me&gt;</c> ellipse.</summary>
        private const int EllipseSegments = 96;

        private const double Tolerance = ContourOffsetGeometry.Tolerance;

        /// <summary>
        /// One mill reduced to its programmed centre line in travel order.
        /// </summary>
        /// <param name="Travel">Centre-line pieces in traversal order (starting at the first point the tool cuts).</param>
        /// <param name="Closed">Whether the centre line returns to its start (always true for <c>&lt;mr&gt;</c>/<c>&lt;me&gt;</c>).</param>
        /// <param name="Position">The mill's <c>c</c>.</param>
        /// <param name="Diameter">Tool diameter, mm.</param>
        /// <param name="Depth">Deepest cut along the mill, mm.</param>
        /// <param name="ToolSide">Side of <paramref name="Travel"/> the tool runs on, or <c>null</c> when it is centred on the centre line.</param>
        internal sealed record MillPath(
            IReadOnlyList<Primitive> Travel,
            bool Closed,
            ToolPosition Position,
            double Diameter,
            double Depth,
            MillOffsetSide? ToolSide);

        /// <summary>
        /// The removed (cut-off) area of a mill, as a closed primitive chain: either the area the
        /// chain itself encloses (<see cref="Outside"/> false), or everything of the part outside it.
        /// </summary>
        internal sealed record CutOffRegion(IReadOnlyList<Primitive> Boundary, bool Outside);

        #region Building mill paths

        /// <summary>
        /// Builds the path of a segment contour (<c>&lt;ms&gt;</c> + its moves), or <c>null</c> when it
        /// has no drawable move.
        /// </summary>
        public static MillPath? FromContour(XncMillingContour contour, double diameter)
        {
            var authored = AuthoredPrimitives(contour);

            if (authored.Count == 0)
            {
                return null;
            }

            var closed = ContourOffsetGeometry.IsClosed(authored);
            var depth = contour.Segments.Select(s => s.Depth).Append(contour.EntryDepth).Max();

            var travel = Orient(authored, closed, contour.Forward, isRectangleOrEllipse: false);
            var toolSide = closed
                ? ClosedContourToolSide(contour.Position, IsCounterClockwise(authored), IsCounterClockwise(travel))
                : OpenToolSide(contour.Position);

            return new MillPath(travel, closed, contour.Position, diameter, depth, toolSide);
        }

        /// <summary>
        /// Tool side of an open path: <c>c="1"</c> right and <c>c="2"</c> left of its travel
        /// direction; a pocket has no meaning on an open path and is treated as centred.
        /// </summary>
        private static MillOffsetSide? OpenToolSide(ToolPosition position) => position switch
        {
            ToolPosition.Right => MillOffsetSide.Right,
            ToolPosition.Left => MillOffsetSide.Left,
            _ => null,
        };

        /// <summary>
        /// Tool side of a closed <c>&lt;ms&gt;</c> contour, relative to its travel (confirmed with
        /// the user). Inside/outside follows the segments' declaration order alone, whatever
        /// <c>fwd</c> says: declared counter-clockwise on screen, <c>c="1"</c> puts the tool outside
        /// the shape and <c>c="2"</c> inside; declared clockwise, the other way round. A pocket
        /// (<c>c="3"</c>) is always inside.
        /// </summary>
        private static MillOffsetSide? ClosedContourToolSide(ToolPosition position, bool declaredCounterClockwise, bool travelCounterClockwise)
        {
            bool? outside = position switch
            {
                ToolPosition.Right => declaredCounterClockwise,
                ToolPosition.Left => !declaredCounterClockwise,
                ToolPosition.Pocket => false,
                _ => null,
            };

            return outside is { } isOutside ? SideOf(isOutside, travelCounterClockwise) : null;
        }

        /// <summary>
        /// Tool side of an <c>&lt;mr&gt;</c>/<c>&lt;me&gt;</c>: <c>c="1"</c> right and <c>c="2"</c>
        /// left of its travel direction; a pocket is inside.
        /// </summary>
        private static MillOffsetSide? ShapeToolSide(ToolPosition position, IReadOnlyList<Primitive> travel) =>
            position == ToolPosition.Pocket
                ? SideOf(outside: false, IsCounterClockwise(travel))
                : OpenToolSide(position);

        /// <summary>Which side of a closed travel the outside or inside lies on: counter-clockwise travel has the outside on its right.</summary>
        private static MillOffsetSide SideOf(bool outside, bool travelCounterClockwise) =>
            outside == travelCounterClockwise ? MillOffsetSide.Right : MillOffsetSide.Left;

        /// <summary>
        /// Builds the path of a <c>&lt;mr&gt;</c>: an <c>l</c> x <c>w</c> rectangle centred on
        /// <c>x</c>/<c>y</c> with corner radius <c>r</c>, rotated by <c>a</c> degrees
        /// clockwise (operator's view, i.e. on screen). Travel starts at the middle of its local <c>y-</c>
        /// side (confirmed with the user).
        /// </summary>
        public static MillPath? FromRectangle(XncMillingRectangle rectangle, double diameter)
        {
            var halfLength = rectangle.Length / 2d;
            var halfWidth = rectangle.Width / 2d;

            if (halfLength <= Tolerance || halfWidth <= Tolerance)
            {
                return null;
            }

            var radius = Math.Clamp(rectangle.CornerRadius, 0d, Math.Min(halfLength, halfWidth));
            var counterClockwise = MillShapeGeometry.RoundedRectangle(halfLength, halfWidth, radius)
                .Select(p => MillShapeGeometry.Place(p, ToVec(rectangle.Origin), rectangle.Angle))
                .ToList();

            var travel = Orient(counterClockwise, true, rectangle.Forward, isRectangleOrEllipse: true);

            return new MillPath(travel, true, rectangle.Position, diameter, rectangle.Depth, ShapeToolSide(rectangle.Position, travel));
        }

        /// <summary>
        /// Builds the path of a <c>&lt;me&gt;</c>: semi-axes <c>l</c> (local X) and <c>w</c> (local Y)
        /// around <c>x</c>/<c>y</c>, rotated like <see cref="FromRectangle"/>. Travel starts at the
        /// local <c>y-</c> end of the <c>w</c> semi-axis (confirmed with the user). A circle (<c>l == w</c>) stays two exact arcs; any other
        /// ellipse is approximated by <see cref="EllipseSegments"/> chords.
        /// </summary>
        public static MillPath? FromEllipse(XncMillingEllipse ellipse, double diameter)
        {
            if (ellipse.Length <= Tolerance || ellipse.Width <= Tolerance)
            {
                return null;
            }

            var counterClockwise = (Math.Abs(ellipse.Length - ellipse.Width) <= Tolerance
                    ? MillShapeGeometry.Circle(ellipse.Length)
                    : MillShapeGeometry.EllipseChords(ellipse.Length, ellipse.Width, EllipseSegments))
                .Select(p => MillShapeGeometry.Place(p, ToVec(ellipse.Center), ellipse.Angle))
                .ToList();

            var travel = Orient(counterClockwise, true, ellipse.Forward, isRectangleOrEllipse: true);

            return new MillPath(travel, true, ellipse.Position, diameter, ellipse.Depth, ShapeToolSide(ellipse.Position, travel));
        }

        private static List<Primitive> AuthoredPrimitives(XncMillingContour contour)
        {
            var primitives = new List<Primitive>(contour.Segments.Count);
            var current = ToVec(contour.Entry);

            foreach (var segment in contour.Segments)
            {
                var end = ToVec(segment.End);

                if (segment is XncArcSegment arc && arc.Radius > Tolerance)
                {
                    primitives.Add(Primitive.Arc(current, end, ToVec(arc.Center), arc.Radius, arc.Clockwise));
                }
                else if (current.DistanceTo(end) > Tolerance)
                {
                    primitives.Add(Primitive.Line(current, end));
                }

                current = end;
            }

            return primitives;
        }

        /// <summary>
        /// Puts <paramref name="authored"/> into traversal order: an open path runs as authored for
        /// <c>fwd="true"</c> and reversed otherwise; a closed one runs counter-clockwise or
        /// clockwise per <see cref="ContourOffsetGeometry.TravelsCounterClockwise"/> (opposite
        /// senses for an <c>&lt;ms&gt;</c> contour and an <c>&lt;mr&gt;</c>/<c>&lt;me&gt;</c>),
        /// whatever order it was written in.
        /// </summary>
        private static IReadOnlyList<Primitive> Orient(IReadOnlyList<Primitive> authored, bool closed, bool forward, bool isRectangleOrEllipse)
        {
            var reverse = closed
                ? (ContourOffsetGeometry.OperatorSignedArea(authored) > 0d) != ContourOffsetGeometry.TravelsCounterClockwise(forward, isRectangleOrEllipse)
                : !forward;

            return reverse ? Reverse(authored) : authored;
        }

        private static List<Primitive> Reverse(IReadOnlyList<Primitive> path) =>
            path.Reverse().Select(p => p.Reversed()).ToList();

        #endregion

        #region Derived geometry

        /// <summary>Whether the mill cuts through the whole panel thickness <paramref name="thickness"/>.</summary>
        public static bool IsThrough(MillPath mill, double thickness) => mill.Depth >= thickness - Tolerance;

        /// <summary>
        /// The tool-centre path: the centre line shifted by half the tool diameter to
        /// <see cref="MillPath.ToolSide"/>, open ends kept clear of the outline like "Offset mill path" does.
        /// Falls back to the centre line itself when the tool is centred or the shift collapses.
        /// </summary>
        public static IReadOnlyList<Primitive> ToolPath(MillPath mill, Vec2 outline)
        {
            if (mill.ToolSide is not { } side || mill.Diameter <= Tolerance)
            {
                return mill.Travel;
            }

            // Travel is already in traversal order, so it is passed as an authored path with
            // fwd="true" (open) or with its own orientation (closed).
            var forward = !mill.Closed || IsCounterClockwise(mill.Travel);

            return ContourOffsetGeometry.TryOffset(mill.Travel, forward, outline, mill.Diameter / 2d, side)?.Chain()
                ?? mill.Travel;
        }

        /// <summary>
        /// The part area the mill removes, or <c>null</c> when it keeps both sides (<c>c="0"</c>) or
        /// cannot split the part (an open path with an end inside the outline). A pocket removes
        /// its inside; a right/left closed mill removes the side its tool runs on; a right/left open
        /// mill whose both ends are on or beyond the outline removes the part on its tool side,
        /// bounded by the centre line extended along its end tangents to beyond the part.
        /// </summary>
        public static CutOffRegion? RemovedRegion(MillPath mill, Vec2 outline)
        {
            if (mill.ToolSide is not { } side)
            {
                return null;
            }

            if (mill.Closed)
            {
                // Counter-clockwise travel has the outside on its right.
                var removesOutside = mill.Position != ToolPosition.Pocket
                    && (side == MillOffsetSide.Right) == IsCounterClockwise(mill.Travel);

                return new CutOffRegion(mill.Travel, removesOutside);
            }

            if (!IsOnOrBeyondOutline(mill.Travel[0].Start, outline) || !IsOnOrBeyondOutline(mill.Travel[^1].End, outline))
            {
                return null;
            }

            // Keeping the region on the right of travel: walk the far box clockwise from the exit
            // back to the entry. For the left side, the same walk on the reversed path.
            var travel = side == MillOffsetSide.Right ? mill.Travel : Reverse(mill.Travel);

            return new CutOffRegion(CloseAroundRight(travel, outline), false);
        }

        /// <summary>Where the tool enters: the first point of <see cref="ToolPath"/>.</summary>
        public static Vec2 StartPoint(IReadOnlyList<Primitive> toolPath) => toolPath[0].Start;

        /// <summary>Whether a closed path runs counter-clockwise on screen.</summary>
        private static bool IsCounterClockwise(IReadOnlyList<Primitive> path) =>
            ContourOffsetGeometry.OperatorSignedArea(path) > 0d;

        private static bool IsOnOrBeyondOutline(Vec2 point, Vec2 outline) =>
            point.X <= Tolerance || point.Y <= Tolerance
            || point.X >= outline.X - Tolerance || point.Y >= outline.Y - Tolerance;

        /// <summary>
        /// Closes an open, outline-crossing path into a boundary enclosing everything on its right:
        /// both ends are extended along their tangents to a box far around the part, and the box is
        /// walked clockwise (on screen: right, down, left, up) from the exit back to the entry.
        /// </summary>
        private static List<Primitive> CloseAroundRight(IReadOnlyList<Primitive> travel, Vec2 outline)
        {
            var points = travel.SelectMany(p => new[] { p.Start, p.End }).Append(new Vec2(0d, 0d)).Append(outline).ToList();
            var margin = outline.X + outline.Y;
            var minX = points.Min(p => p.X) - margin;
            var minY = points.Min(p => p.Y) - margin;
            var maxX = points.Max(p => p.X) + margin;
            var maxY = points.Max(p => p.Y) + margin;

            var entry = travel[0].Start;
            var exit = travel[^1].End;
            var entryOnBox = ExitThroughBox(entry, ContourOffsetGeometry.TangentAtStart(travel[0]) * -1d, minX, minY, maxX, maxY);
            var exitOnBox = ExitThroughBox(exit, ContourOffsetGeometry.TangentAtEnd(travel[^1]), minX, minY, maxX, maxY);

            // Clockwise on screen: perimeter position grows right along the top, down the right
            // side, left along the bottom, up the left side.
            var width = maxX - minX;
            var height = maxY - minY;

            double Perimeter(Vec2 p) =>
                Math.Abs(p.Y - minY) <= Tolerance ? p.X - minX
                : Math.Abs(p.X - maxX) <= Tolerance ? width + (p.Y - minY)
                : Math.Abs(p.Y - maxY) <= Tolerance ? width + height + (maxX - p.X)
                : (2d * width) + height + (maxY - p.Y);

            var corners = new[]
            {
                (Position: width, Point: new Vec2(maxX, minY)),
                (Position: width + height, Point: new Vec2(maxX, maxY)),
                (Position: (2d * width) + height, Point: new Vec2(minX, maxY)),
                (Position: 2d * (width + height), Point: new Vec2(minX, minY)),
            };

            var full = 2d * (width + height);
            var from = Perimeter(exitOnBox);

            double Ahead(double position) => (((position - from) % full) + full) % full;

            var span = Ahead(Perimeter(entryOnBox));
            var walk = corners
                .Select(c => (Ahead: Ahead(c.Position), c.Point))
                .Where(c => c.Ahead > Tolerance && c.Ahead < span)
                .OrderBy(c => c.Ahead)
                .Select(c => c.Point);

            var boundary = new List<Primitive>();
            var cursor = entryOnBox;

            void LineTo(Vec2 next)
            {
                if (cursor.DistanceTo(next) > Tolerance)
                {
                    boundary.Add(Primitive.Line(cursor, next));
                }

                cursor = next;
            }

            LineTo(entry);
            boundary.AddRange(travel);
            cursor = exit;
            LineTo(exitOnBox);

            foreach (var corner in walk)
            {
                LineTo(corner);
            }

            LineTo(entryOnBox);

            return boundary;
        }

        /// <summary>Where the ray from <paramref name="origin"/> along <paramref name="direction"/> leaves the box.</summary>
        private static Vec2 ExitThroughBox(Vec2 origin, Vec2 direction, double minX, double minY, double maxX, double maxY)
        {
            var tx = direction.X > Tolerance ? (maxX - origin.X) / direction.X
                : direction.X < -Tolerance ? (minX - origin.X) / direction.X
                : double.PositiveInfinity;
            var ty = direction.Y > Tolerance ? (maxY - origin.Y) / direction.Y
                : direction.Y < -Tolerance ? (minY - origin.Y) / direction.Y
                : double.PositiveInfinity;
            var hit = origin + (direction * Math.Min(tx, ty));
            var clamped = new Vec2(Math.Clamp(hit.X, minX, maxX), Math.Clamp(hit.Y, minY, maxY));

            // Snap exactly onto the crossed side so the perimeter walk recognises it.
            return tx <= ty
                ? clamped with { X = direction.X > 0d ? maxX : minX }
                : clamped with { Y = direction.Y > 0d ? maxY : minY };
        }

        private static Vec2 ToVec(XncPoint point) => new(point.X, point.Y);

        #endregion
    }
}
