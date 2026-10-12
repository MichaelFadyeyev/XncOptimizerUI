using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>An axis-aligned box in program mm.</summary>
    internal readonly record struct MmBounds(double XLo, double XHi, double YLo, double YHi);

    /// <summary>WPF-free groove geometry shared by the front-plane and edge-plane groove renderers.</summary>
    internal static class GroovePreviewGeometry
    {
        /// <summary>Tolerance (mm) for deciding whether a groove's own extent actually reaches a part edge.</summary>
        private const double EdgeTouchEpsilonMm = 1e-3;

        /// <summary>
        /// True when a groove's low bound (<paramref name="loMm"/>, on some axis) reaches or
        /// crosses the part's near edge (0) on that axis, meaning the groove physically reaches -
        /// or slightly overruns - that edge rather than stopping short of it.
        /// </summary>
        public static bool ReachesNearEdge(double loMm) => loMm <= EdgeTouchEpsilonMm;

        /// <summary>
        /// True when a groove's high bound (<paramref name="hiMm"/>, on some axis) reaches or
        /// crosses the part's far edge (<paramref name="edgeMm"/> - the face width or height on
        /// that axis).
        /// </summary>
        public static bool ReachesFarEdge(double hiMm, double edgeMm) => hiMm >= edgeMm - EdgeTouchEpsilonMm;

        /// <summary>
        /// True when the <paramref name="bounds"/> of a groove reach the edge of the face that the
        /// side band (<paramref name="horizontal"/> Top/Bottom, <paramref name="near"/> Top/Left)
        /// runs along.
        /// </summary>
        public static bool ReachesBand(MmBounds bounds, FaceLayout layout, bool horizontal, bool near) =>
            (horizontal, near) switch
            {
                (true, true) => ReachesNearEdge(bounds.YLo),
                (true, false) => ReachesFarEdge(bounds.YHi, layout.FaceHeightMm),
                (false, true) => ReachesNearEdge(bounds.XLo),
                _ => ReachesFarEdge(bounds.XHi, layout.FaceWidthMm),
            };

        /// <summary>
        /// The mm bounds of a front-plane groove's true (home-plane) footprint - its
        /// <see cref="OffsetStrip"/>, accounting for width and <c>c</c>, not just the start-end run;
        /// a zero-length groove's is its start point.
        /// </summary>
        public static MmBounds TrueBounds(XncGrooving groove)
        {
            var start = groove.Start.ToVec2();
            var corners = OffsetStrip.Along(start, groove.End.ToVec2(), groove.Width, groove.Position)?.Corners
                ?? [start, start, start, start];
            var (p1, p2, p3, p4) = (corners[0], corners[1], corners[2], corners[3]);

            return new MmBounds(
                Math.Min(Math.Min(p1.X, p2.X), Math.Min(p3.X, p4.X)),
                Math.Max(Math.Max(p1.X, p2.X), Math.Max(p3.X, p4.X)),
                Math.Min(Math.Min(p1.Y, p2.Y), Math.Min(p3.Y, p4.Y)),
                Math.Max(Math.Max(p1.Y, p2.Y), Math.Max(p3.Y, p4.Y)));
        }
    }
}
