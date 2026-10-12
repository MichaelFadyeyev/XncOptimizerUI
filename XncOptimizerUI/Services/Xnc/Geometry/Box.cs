namespace XncOptimizerUI.Services.Xnc.Geometry
{
    /// <summary>Axis-aligned box <c>[Min, Max]</c>: a part face (<c>dx</c> x <c>dy</c>), an edge (length x <c>dz</c>), or an inset of one.</summary>
    internal readonly record struct Box(Vec2 Min, Vec2 Max)
    {
        public static Box Of(double width, double height) => new(new Vec2(0d, 0d), new Vec2(width, height));

        public Vec2 Centre => Vec2.Midpoint(Min, Max);

        public IEnumerable<Vec2> Corners =>
            [Min, new(Max.X, Min.Y), Max, new(Min.X, Max.Y)];

        /// <summary>The box shrunk by <paramref name="margin"/> on every side.</summary>
        public Box Inset(double margin) => new(Min + new Vec2(margin, margin), Max - new Vec2(margin, margin));

        public bool Contains(Vec2 p) => p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y;

        /// <summary>Distance from <paramref name="p"/> to the box; 0 inside.</summary>
        public double DistanceTo(Vec2 p)
        {
            var gapX = Math.Max(Math.Max(Min.X - p.X, p.X - Max.X), 0d);
            var gapY = Math.Max(Math.Max(Min.Y - p.Y, p.Y - Max.Y), 0d);

            return Math.Sqrt((gapX * gapX) + (gapY * gapY));
        }

        /// <summary>Distance from segment <paramref name="a"/>-<paramref name="b"/> to the box; 0 when they meet.</summary>
        public double DistanceTo(Vec2 a, Vec2 b)
        {
            if (Contains(a) || Contains(b) || IsCrossedBy(a, b))
            {
                return 0d;
            }

            return Corners
                .Select(corner => PlanarGeometry.DistanceToSegment(corner, a, b))
                .Append(DistanceTo(a))
                .Append(DistanceTo(b))
                .Min();
        }

        /// <summary>
        /// Largest gap between the convex <paramref name="polygon"/> and the box along a separating
        /// axis candidate (box axes, polygon edge normals); negative when they overlap.
        /// </summary>
        public double SeparationFrom(IReadOnlyList<Vec2> polygon)
        {
            var corners = Corners.ToList();
            var axes = polygon
                .Select((p, i) => polygon[(i + 1) % polygon.Count] - p)
                .Where(edge => edge.Length > PlanarGeometry.Tolerance)
                .Select(Normal)
                .Concat([new Vec2(1d, 0d), new Vec2(0d, 1d)]);

            return axes.Max(axis =>
            {
                var (polygonMin, polygonMax) = Project(polygon, axis);
                var (boxMin, boxMax) = Project(corners, axis);

                return Math.Max(polygonMin - boxMax, boxMin - polygonMax);
            });
        }

        /// <summary>Whether the segment crosses any side of the box (Liang-Barsky clip leaves a piece).</summary>
        private bool IsCrossedBy(Vec2 a, Vec2 b)
        {
            var d = b - a;
            var low = 0d;
            var high = 1d;

            bool Clip(double p, double q)
            {
                if (p == 0d)
                {
                    return q >= 0d;
                }

                var t = q / p;

                if (p < 0d)
                {
                    low = Math.Max(low, t);
                }
                else
                {
                    high = Math.Min(high, t);
                }

                return low <= high;
            }

            return Clip(-d.X, a.X - Min.X) && Clip(d.X, Max.X - a.X) && Clip(-d.Y, a.Y - Min.Y) && Clip(d.Y, Max.Y - a.Y);
        }

        private static Vec2 Normal(Vec2 edge)
        {
            var u = edge.Normalized();

            return new Vec2(-u.Y, u.X);
        }

        private static (double Min, double Max) Project(IReadOnlyList<Vec2> points, Vec2 axis)
        {
            var projections = points.Select(p => p.Dot(axis)).ToList();

            return (projections.Min(), projections.Max());
        }
    }
}
