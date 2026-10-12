namespace XncOptimizerUI.Services.Xnc.Geometry
{
    /// <summary>
    /// Intersections of infinite lines and full circles, the building blocks of path offsetting.
    /// Every method returns all real solutions; callers pick the relevant one.
    /// </summary>
    internal static class PlanarGeometry
    {
        /// <summary>Tolerance for comparing reconstructed geometry (lengths, radii, chords), mm.</summary>
        public const double Tolerance = 1e-3;

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

        /// <summary>
        /// Centre of a radius-defined arc (<c>&lt;ma&gt;</c>) from <paramref name="start"/> to
        /// <paramref name="end"/>: on the right normal of the chord for a <paramref name="clockwise"/>
        /// (<c>dir="true"</c>) arc, on the left one otherwise. For the closed-circle-in-two-half-arcs
        /// case the chord equals the diameter and the centre is the chord midpoint either way.
        /// <c>null</c> when the chord is degenerate or longer than the diameter.
        /// </summary>
        public static Vec2? ArcCentreFromChord(Vec2 start, Vec2 end, double radius, bool clockwise)
        {
            var chord = start.DistanceTo(end);

            if (chord <= Tolerance || chord > (2d * radius) + Tolerance)
            {
                return null;
            }

            var halfChord = chord / 2d;
            var offset = Math.Sqrt(Math.Max(0d, (radius * radius) - (halfChord * halfChord)));
            var perpendicular = new Vec2(-(end.Y - start.Y) / chord, (end.X - start.X) / chord);

            return Vec2.Midpoint(start, end) + (perpendicular * ((clockwise ? 1d : -1d) * offset));
        }

        /// <summary>Distance from <paramref name="p"/> to the segment <paramref name="a"/>-<paramref name="b"/>.</summary>
        public static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            var d = b - a;
            var lengthSquared = d.Dot(d);
            var t = lengthSquared <= 0d ? 0d : Math.Clamp((p - a).Dot(d) / lengthSquared, 0d, 1d);

            return p.DistanceTo(a + (d * t));
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
