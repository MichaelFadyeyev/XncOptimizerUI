namespace XncOptimizerUI.Services.Xnc.Geometry
{
    /// <summary>A milling path flattened to straight chords, for distance and containment tests.</summary>
    internal static class ChordPath
    {
        /// <summary>Largest chord error allowed when an arc is flattened, mm.</summary>
        private const double ArcFlatteningError = 1e-3;

        /// <summary>Upper bound of chords per arc, so a huge radius never explodes the flattening.</summary>
        private const int MaxArcSegments = 4096;

        /// <summary>A path as chords, with the largest chord error.</summary>
        public static (IReadOnlyList<Primitive> Chords, double Error) Flatten(IReadOnlyList<Primitive> path)
        {
            var flattened = path.SelectMany(Flatten).ToList();

            return (flattened.Select(f => f.Chord).ToList(), flattened.Max(f => f.Error));
        }

        /// <summary>Even-odd ray cast of <paramref name="point"/> against the closed chord chain.</summary>
        public static bool Encloses(IReadOnlyList<Primitive> chords, Vec2 point) =>
            chords.Count(c => (c.Start.Y > point.Y) != (c.End.Y > point.Y)
                && point.X < c.Start.X + ((point.Y - c.Start.Y) / (c.End.Y - c.Start.Y) * (c.End.X - c.Start.X))) % 2 == 1;

        /// <summary>An arc as chords whose error stays within <see cref="ArcFlatteningError"/>; a line as itself.</summary>
        private static IEnumerable<(Primitive Chord, double Error)> Flatten(Primitive primitive)
        {
            if (primitive.Centre is not { } centre)
            {
                return [(primitive, 0d)];
            }

            // Raw-frame sweep: an operator-clockwise arc turns the raw angle up (Y is mirrored).
            var sweep = -ContourOffsetGeometry.OperatorSweep(primitive);
            var maxStep = 2d * Math.Acos(Math.Max(-1d, 1d - (ArcFlatteningError / primitive.Radius)));
            var count = Math.Clamp((int)Math.Ceiling(Math.Abs(sweep) / maxStep), 1, MaxArcSegments);
            var step = sweep / count;
            var error = primitive.Radius * (1d - Math.Cos(step / 2d));
            var startAngle = Math.Atan2(primitive.Start.Y - centre.Y, primitive.Start.X - centre.X);

            Vec2 PointAt(int i) => i == count
                ? primitive.End
                : centre + new Vec2(Math.Cos(startAngle + (step * i)), Math.Sin(startAngle + (step * i))) * primitive.Radius;

            return Enumerable.Range(0, count).Select(i => (Primitive.Line(PointAt(i), PointAt(i + 1)), error));
        }
    }
}
