using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>How a cut of some width sits beside its travel line, per the tool position <c>c</c>.</summary>
    internal static class ToolPositionOffset
    {
        /// <summary>
        /// Offsets of the cut's two long sides from the travel line, positive to the right of
        /// travel: <see cref="ToolPosition.Right"/>/<see cref="ToolPosition.Left"/> put the whole
        /// <paramref name="width"/> to that side, anything else (centre, or an unexpected pocket)
        /// straddles the line.
        /// </summary>
        public static (double A, double B) Sides(ToolPosition position, double width) => position switch
        {
            ToolPosition.Right => (0d, width),
            ToolPosition.Left => (-width, 0d),
            _ => (-width / 2, width / 2),
        };

        /// <summary>
        /// Swaps <see cref="ToolPosition.Right"/>/<see cref="ToolPosition.Left"/>, leaving
        /// <see cref="ToolPosition.Center"/>/<see cref="ToolPosition.Pocket"/> unchanged: the same
        /// physical side seen against the opposite travel direction.
        /// </summary>
        public static ToolPosition Mirror(ToolPosition position) => position switch
        {
            ToolPosition.Right => ToolPosition.Left,
            ToolPosition.Left => ToolPosition.Right,
            _ => position,
        };
    }
}
