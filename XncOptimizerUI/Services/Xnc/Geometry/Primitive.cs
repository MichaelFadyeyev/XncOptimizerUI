namespace XncOptimizerUI.Services.Xnc.Geometry
{
    /// <summary>A straight or circular piece of a milling path; arcs keep their centre when offset. <see cref="Clockwise"/> is in the operator's sense.</summary>
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

    /// <summary>An offset contour: new entry, one trimmed primitive per segment, and any round join to insert after each.</summary>
    internal sealed record OffsetLayout(Vec2 Entry, IReadOnlyList<Primitive> Primitives, IReadOnlyList<Primitive?> RoundJoins)
    {
        /// <summary>The offset path as one primitive chain, round joins inserted in place.</summary>
        public IReadOnlyList<Primitive> Chain() => Primitives
            .Zip(RoundJoins)
            .SelectMany(pair => pair.Second is { } join ? new[] { pair.First, join } : new[] { pair.First })
            .ToList();
    }
}
