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
        internal sealed record MillPath(
            IReadOnlyList<Primitive> Travel,
            bool Closed,
            ToolPosition Position,
            double Diameter,
            double Depth);

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

            return new MillPath(Orient(authored, closed, contour.Forward, isRectangleOrEllipse: false), closed, contour.Position, diameter, depth);
        }

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
            var counterClockwise = RoundedRectangle(halfLength, halfWidth, radius)
                .Select(p => Place(p, rectangle.Origin, rectangle.Angle))
                .ToList();

            return new MillPath(Orient(counterClockwise, true, rectangle.Forward, isRectangleOrEllipse: true), true, rectangle.Position, diameter, rectangle.Depth);
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
                    ? Circle(ellipse.Length)
                    : EllipseChords(ellipse.Length, ellipse.Width))
                .Select(p => Place(p, ellipse.Center, ellipse.Angle))
                .ToList();

            return new MillPath(Orient(counterClockwise, true, ellipse.Forward, isRectangleOrEllipse: true), true, ellipse.Position, diameter, ellipse.Depth);
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

        /// <summary>Local rounded rectangle, counter-clockwise on screen, from the middle of its local <c>y-</c> side.</summary>
        private static IEnumerable<Primitive> RoundedRectangle(double halfLength, double halfWidth, double radius)
        {
            // Corner centres in Y-down coordinates, counter-clockwise on screen starting from the
            // y- side: (x-, y-), (x-, y+), (x+, y+), (x+, y-).
            var corners = new[]
            {
                new Vec2(-halfLength + radius, -halfWidth + radius),
                new Vec2(-halfLength + radius, halfWidth - radius),
                new Vec2(halfLength - radius, halfWidth - radius),
                new Vec2(halfLength - radius, -halfWidth + radius),
            };

            // Outward normals of the sides: side i runs into corner i (y- side first).
            var start = new Vec2(0d, -halfWidth);
            var current = start;
            var sides = new[] { new Vec2(0d, -1d), new Vec2(-1d, 0d), new Vec2(0d, 1d), new Vec2(1d, 0d) };

            for (var i = 0; i < 4; i++)
            {
                var corner = corners[i];
                var sideEnd = corner + (sides[i] * radius);

                if (current.DistanceTo(sideEnd) > Tolerance)
                {
                    yield return Primitive.Line(current, sideEnd);
                }

                var arcEnd = corner + (sides[(i + 1) % 4] * radius);

                if (radius > Tolerance)
                {
                    yield return Primitive.Arc(sideEnd, arcEnd, corner, radius, clockwise: false);
                }

                current = arcEnd;
            }

            if (current.DistanceTo(start) > Tolerance)
            {
                yield return Primitive.Line(current, start);
            }
        }

        /// <summary>Local full circle as two counter-clockwise (on screen) half arcs from <c>(0, -r)</c>, the <c>y-</c> end of its <c>w</c> semi-axis.</summary>
        private static IEnumerable<Primitive> Circle(double radius)
        {
            var start = new Vec2(0d, -radius);
            var opposite = new Vec2(0d, radius);
            var origin = new Vec2(0d, 0d);

            yield return Primitive.Arc(start, opposite, origin, radius, clockwise: false);
            yield return Primitive.Arc(opposite, start, origin, radius, clockwise: false);
        }

        /// <summary>Local ellipse chords, counter-clockwise on screen, from <c>(0, -w)</c>, the <c>y-</c> end of its <c>w</c> semi-axis.</summary>
        private static IEnumerable<Primitive> EllipseChords(double semiX, double semiY)
        {
            Vec2 PointAt(int i)
            {
                // t = pi/2 is (0, -w); increasing t runs counter-clockwise on screen.
                var t = (Math.PI / 2d) + (2d * Math.PI * i / EllipseSegments);

                return i % EllipseSegments == 0
                    ? new Vec2(0d, -semiY)
                    : new Vec2(semiX * Math.Cos(t), -semiY * Math.Sin(t));
            }

            return Enumerable.Range(0, EllipseSegments)
                .Select(i => Primitive.Line(PointAt(i), PointAt(i + 1)));
        }

        /// <summary>Rotates a local primitive by <paramref name="angleDegrees"/> (positive = clockwise on screen, confirmed with the user) and moves it to <paramref name="centre"/>.</summary>
        private static Primitive Place(Primitive local, XncPoint centre, double angleDegrees)
        {
            var radians = angleDegrees * Math.PI / 180d;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            var origin = ToVec(centre);

            // Y-down frame: the standard rotation of the raw coordinates turns clockwise on screen
            // (+x towards +y, i.e. right towards down).
            Vec2 Map(Vec2 p) => origin + new Vec2((p.X * cos) - (p.Y * sin), (p.X * sin) + (p.Y * cos));

            return local with
            {
                Start = Map(local.Start),
                End = Map(local.End),
                Centre = local.Centre is { } c ? Map(c) : null,
            };
        }

        #endregion

        #region Derived geometry

        /// <summary>Whether the mill cuts through the whole panel thickness <paramref name="thickness"/>.</summary>
        public static bool IsThrough(MillPath mill, double thickness) => mill.Depth >= thickness - Tolerance;

        /// <summary>
        /// Side of the travel direction the tool runs on, or <c>null</c> when the tool is centred on
        /// the centre line. A pocket runs on the inside: left of counter-clockwise travel.
        /// </summary>
        public static MillOffsetSide? ToolSide(MillPath mill) => mill.Position switch
        {
            ToolPosition.Right => MillOffsetSide.Right,
            ToolPosition.Left => MillOffsetSide.Left,
            ToolPosition.Pocket when mill.Closed => IsCounterClockwise(mill) ? MillOffsetSide.Left : MillOffsetSide.Right,
            _ => null,
        };

        /// <summary>
        /// The tool-centre path: the centre line shifted by half the tool diameter to
        /// <see cref="ToolSide"/>, open ends kept clear of the outline like "Offset mill path" does.
        /// Falls back to the centre line itself when the tool is centred or the shift collapses.
        /// </summary>
        public static IReadOnlyList<Primitive> ToolPath(MillPath mill, Vec2 outline)
        {
            if (ToolSide(mill) is not { } side || mill.Diameter <= Tolerance)
            {
                return mill.Travel;
            }

            // Travel is already in traversal order, so it is passed as an authored path with
            // fwd="true" (open) or with its own orientation (closed).
            var forward = !mill.Closed || IsCounterClockwise(mill);

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
            if (ToolSide(mill) is not { } side)
            {
                return null;
            }

            if (mill.Closed)
            {
                // Counter-clockwise travel has the outside on its right.
                var removesOutside = mill.Position != ToolPosition.Pocket
                    && (side == MillOffsetSide.Right) == IsCounterClockwise(mill);

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

        private static bool IsCounterClockwise(MillPath mill) =>
            ContourOffsetGeometry.OperatorSignedArea(mill.Travel) > 0d;

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
