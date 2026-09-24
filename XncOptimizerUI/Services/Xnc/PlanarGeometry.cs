namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>A point or vector in raw XNC program coordinates (mm).</summary>
    internal readonly record struct Vec2(double X, double Y)
    {
        public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, double k) => new(a.X * k, a.Y * k);
        public static Vec2 operator /(Vec2 a, double k) => new(a.X / k, a.Y / k);

        public double Length => Math.Sqrt((X * X) + (Y * Y));

        public Vec2 Normalized() => this / Length;

        public double Dot(Vec2 other) => (X * other.X) + (Y * other.Y);

        /// <summary>Z component of the 3D cross product; positive when <paramref name="other"/> lies counter-clockwise (raw math sense) of this vector.</summary>
        public double Cross(Vec2 other) => (X * other.Y) - (Y * other.X);

        public double DistanceTo(Vec2 other) => (this - other).Length;

        public static Vec2 Midpoint(Vec2 a, Vec2 b) => (a + b) / 2d;
    }

    /// <summary>
    /// Intersections of infinite lines and full circles, the building blocks of path offsetting.
    /// Every method returns all real solutions; callers pick the relevant one.
    /// </summary>
    internal static class PlanarGeometry
    {
        private const double ParallelEpsilon = 1e-12;

        /// <summary>Line through <paramref name="p1"/> along <paramref name="d1"/> meets line through <paramref name="p2"/> along <paramref name="d2"/>.</summary>
        public static IReadOnlyList<Vec2> IntersectLines(Vec2 p1, Vec2 d1, Vec2 p2, Vec2 d2)
        {
            var denominator = d1.Cross(d2);

            if (Math.Abs(denominator) <= ParallelEpsilon * d1.Length * d2.Length)
            {
                return [];
            }

            var t = (p2 - p1).Cross(d2) / denominator;

            return [p1 + (d1 * t)];
        }

        public static IReadOnlyList<Vec2> IntersectLineCircle(Vec2 p, Vec2 d, Vec2 centre, double radius)
        {
            var u = d.Normalized();
            var foot = p + (u * (centre - p).Dot(u));
            var distance = centre.DistanceTo(foot);

            if (distance > radius)
            {
                return [];
            }

            var half = Math.Sqrt(Math.Max(0d, (radius * radius) - (distance * distance)));

            return [foot - (u * half), foot + (u * half)];
        }

        public static IReadOnlyList<Vec2> IntersectCircles(Vec2 c1, double r1, Vec2 c2, double r2)
        {
            var between = c2 - c1;
            var d = between.Length;

            if (d <= ParallelEpsilon || d > r1 + r2 || d < Math.Abs(r1 - r2))
            {
                return [];
            }

            var along = ((r1 * r1) - (r2 * r2) + (d * d)) / (2d * d);
            var half = Math.Sqrt(Math.Max(0d, (r1 * r1) - (along * along)));
            var axis = between / d;
            var basePoint = c1 + (axis * along);
            var normal = new Vec2(-axis.Y, axis.X);

            return [basePoint + (normal * half), basePoint - (normal * half)];
        }

        public static Vec2? Nearest(IEnumerable<Vec2> candidates, Vec2 target)
        {
            Vec2? best = null;
            var bestDistance = double.MaxValue;

            foreach (var candidate in candidates)
            {
                var distance = candidate.DistanceTo(target);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Normalizes an angle to <c>[0, 2π)</c>.</summary>
        public static double NormalizeAngle(double angle)
        {
            var full = 2d * Math.PI;
            var result = angle % full;

            return result < 0d ? result + full : result;
        }
    }
}
