namespace XncOptimizerUI.Services.Xnc.Geometry
{
    /// <summary>A point or vector in raw XNC program coordinates (mm).</summary>
    internal readonly record struct Vec2(double X, double Y)
    {
        public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, double k) => new(a.X * k, a.Y * k);
        public static Vec2 operator /(Vec2 a, double k) => new(a.X / k, a.Y / k);

        public double Length => Math.Sqrt(LengthSquared);

        public double LengthSquared => (X * X) + (Y * Y);

        public Vec2 Normalized() => this / Length;

        public double Dot(Vec2 other) => (X * other.X) + (Y * other.Y);

        /// <summary>Z component of the 3D cross product; positive when <paramref name="other"/> lies counter-clockwise (raw math sense) of this vector.</summary>
        public double Cross(Vec2 other) => (X * other.Y) - (Y * other.X);

        public double DistanceTo(Vec2 other) => (this - other).Length;

        public double SquaredDistanceTo(Vec2 other) => (this - other).LengthSquared;

        public static Vec2 Midpoint(Vec2 a, Vec2 b) => (a + b) / 2d;
    }
}
