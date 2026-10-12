namespace XncOptimizerUI.Services.Xnc.Geometry
{
    /// <summary>
    /// Centre lines of the self-contained mills - <c>&lt;mr&gt;</c> rounded rectangles and
    /// <c>&lt;me&gt;</c> ellipses - as <see cref="Primitive"/> chains in raw program millimetres (the
    /// Y-down operator's view). Built in a local frame, counter-clockwise on screen from the local
    /// <c>y-</c> side (confirmed with the user), then turned and moved by <see cref="Place"/>.
    /// Shared by the part preview's mill overlay and <see cref="Outside.OutsideOperationDetector"/>.
    /// </summary>
    internal static class MillShapeGeometry
    {
        /// <summary>Local rounded rectangle, counter-clockwise on screen, from the middle of its local <c>y-</c> side.</summary>
        internal static IEnumerable<Primitive> RoundedRectangle(double halfLength, double halfWidth, double radius)
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

                if (current.DistanceTo(sideEnd) > PlanarGeometry.Tolerance)
                {
                    yield return Primitive.Line(current, sideEnd);
                }

                var arcEnd = corner + (sides[(i + 1) % 4] * radius);

                if (radius > PlanarGeometry.Tolerance)
                {
                    yield return Primitive.Arc(sideEnd, arcEnd, corner, radius, clockwise: false);
                }

                current = arcEnd;
            }

            if (current.DistanceTo(start) > PlanarGeometry.Tolerance)
            {
                yield return Primitive.Line(current, start);
            }
        }

        /// <summary>Local full circle as two counter-clockwise (on screen) half arcs from <c>(0, -r)</c>, the <c>y-</c> end of its <c>w</c> semi-axis.</summary>
        internal static IEnumerable<Primitive> Circle(double radius)
        {
            var start = new Vec2(0d, -radius);
            var opposite = new Vec2(0d, radius);
            var origin = new Vec2(0d, 0d);

            yield return Primitive.Arc(start, opposite, origin, radius, clockwise: false);
            yield return Primitive.Arc(opposite, start, origin, radius, clockwise: false);
        }

        /// <summary>
        /// Local ellipse as <paramref name="segments"/> chords, counter-clockwise on screen, from
        /// <c>(0, -w)</c>, the <c>y-</c> end of its <c>w</c> semi-axis. The chords lie inside the
        /// ellipse, by at most <see cref="EllipseChordSagitta"/>.
        /// </summary>
        internal static IEnumerable<Primitive> EllipseChords(double semiX, double semiY, int segments)
        {
            Vec2 PointAt(int i)
            {
                // t = pi/2 is (0, -w); increasing t runs counter-clockwise on screen.
                var t = (Math.PI / 2d) + (2d * Math.PI * i / segments);

                return i % segments == 0
                    ? new Vec2(0d, -semiY)
                    : new Vec2(semiX * Math.Cos(t), -semiY * Math.Sin(t));
            }

            return Enumerable.Range(0, segments)
                .Select(i => Primitive.Line(PointAt(i), PointAt(i + 1)));
        }

        /// <summary>
        /// Upper bound of the gap between an ellipse and its <see cref="EllipseChords"/>: a chord
        /// is at most <c>a * dt</c> long (<c>a</c> the larger semi-axis) and the curvature at most
        /// <c>a / b^2</c>, so the sagitta is at most <c>(a / b^2) * (a * dt)^2 / 8</c>.
        /// </summary>
        internal static double EllipseChordSagitta(double semiX, double semiY, int segments)
        {
            var major = Math.Max(semiX, semiY);
            var minor = Math.Min(semiX, semiY);
            var chord = major * 2d * Math.PI / segments;

            return major / (minor * minor) * chord * chord / 8d;
        }

        /// <summary>Rotates a local primitive by <paramref name="angleDegrees"/> (positive = clockwise on screen, confirmed with the user) and moves it to <paramref name="centre"/>.</summary>
        internal static Primitive Place(Primitive local, Vec2 centre, double angleDegrees)
        {
            var radians = angleDegrees * Math.PI / 180d;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            // Y-down frame: the standard rotation of the raw coordinates turns clockwise on screen
            // (+x towards +y, i.e. right towards down).
            Vec2 Map(Vec2 p) => centre + new Vec2((p.X * cos) - (p.Y * sin), (p.X * sin) + (p.Y * cos));

            return local with
            {
                Start = Map(local.Start),
                End = Map(local.End),
                Centre = local.Centre is { } c ? Map(c) : null,
            };
        }
    }
}
