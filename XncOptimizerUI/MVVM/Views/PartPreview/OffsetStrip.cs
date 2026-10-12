using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// The footprint of a straight cut: a strip along the travel line from <see cref="Start"/> to
    /// <see cref="End"/>, its long sides <see cref="OffsetA"/> and <see cref="OffsetB"/> off the
    /// line along <see cref="Normal"/> (right of travel in the Y-down frame). Works in program mm
    /// or canvas px alike.
    /// </summary>
    internal readonly record struct OffsetStrip(Vec2 Start, Vec2 End, Vec2 Direction, Vec2 Normal, double OffsetA, double OffsetB)
    {
        /// <summary>
        /// The <paramref name="width"/>-wide strip of a cut from <paramref name="start"/> to
        /// <paramref name="end"/> placed per <paramref name="position"/> (see
        /// <see cref="ToolPositionOffset.Sides"/>), or <c>null</c> for a zero-length cut.
        /// </summary>
        public static OffsetStrip? Along(Vec2 start, Vec2 end, double width, ToolPosition position)
        {
            var length = (end - start).Length;

            if (length <= 0)
            {
                return null;
            }

            var direction = (end - start) / length;
            var (offsetA, offsetB) = ToolPositionOffset.Sides(position, width);

            return new OffsetStrip(start, end, direction, new Vec2(-direction.Y, direction.X), offsetA, offsetB);
        }

        /// <summary>
        /// The corners in drawing order: the <see cref="OffsetA"/> side at start and end, then the
        /// <see cref="OffsetB"/> side at end and start.
        /// </summary>
        public IReadOnlyList<Vec2> Corners =>
        [
            Start + (Normal * OffsetA),
            End + (Normal * OffsetA),
            End + (Normal * OffsetB),
            Start + (Normal * OffsetB),
        ];

        /// <summary>The travel line extended by <paramref name="overshoot"/> at both ends.</summary>
        public PxLine CentreLine(double overshoot)
        {
            var from = Start - (Direction * overshoot);
            var to = End + (Direction * overshoot);

            return new PxLine(from.X, from.Y, to.X, to.Y);
        }

        /// <summary>
        /// Where the line <paramref name="offset"/> off the travel line crosses
        /// <c>y = <paramref name="edge"/></c> (returning its x) when <paramref name="horizontal"/>,
        /// else <c>x = <paramref name="edge"/></c> (returning its y).
        /// </summary>
        public double CrossingAt(bool horizontal, double edge, double offset)
        {
            if (horizontal)
            {
                var s = (edge - Start.Y - (Normal.Y * offset)) / Direction.Y;

                return Start.X + (s * Direction.X) + (Normal.X * offset);
            }

            var t = (edge - Start.X - (Normal.X * offset)) / Direction.X;

            return Start.Y + (t * Direction.Y) + (Normal.Y * offset);
        }
    }
}
