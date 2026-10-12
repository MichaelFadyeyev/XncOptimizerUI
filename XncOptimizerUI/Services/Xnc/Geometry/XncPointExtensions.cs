using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Services.Xnc.Geometry
{
    internal static class XncPointExtensions
    {
        /// <summary>The read-only model point as a geometry point.</summary>
        public static Vec2 ToVec2(this XncPoint point) => new(point.X, point.Y);
    }
}
