using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Projection geometry shared by the bore and groove overlays, in canvas px: both draw a
    /// "true" shape on one plane plus a depth/extent rectangle projected onto the four side bands
    /// or onto the Face rectangle.
    /// </summary>
    internal static class PartPreviewOverlayGeometry
    {
        /// <summary>
        /// One side-band rectangle spanning <paramref name="axisLowPx"/>..<paramref name="axisHighPx"/>
        /// along the band's own length, plus its center line. <paramref name="horizontal"/> selects
        /// the Top/Bottom band pair (spanning the part's X axis) vs the Left/Right pair (spanning Y);
        /// <paramref name="near"/> selects which of that pair (Top/Left = true). Across the band the
        /// rectangle is <paramref name="perpendicularPx"/> deep, starting flush at the band's inner
        /// edge when <paramref name="fromInner"/> (a front-side program), else at its outer edge - a
        /// bore's drilling depth, or a front-plane groove's <c>dp</c> into the band.
        /// </summary>
        public static (PxRect Rect, PxLine CentreLine) SideRectangleRange(
            double axisLowPx,
            double axisHighPx,
            double perpendicularPx,
            double overshootPx,
            FaceLayout layout,
            bool fromInner,
            bool horizontal,
            bool near)
        {
            var (start, direction) = layout.Band(horizontal, near).Anchor(fromInner);
            var end = start + (direction * perpendicularPx);
            var depthLow = Math.Min(start, end);
            var depth = Math.Abs(end - start);
            var rect = horizontal
                ? new PxRect(axisLowPx, depthLow, axisHighPx - axisLowPx, depth)
                : new PxRect(depthLow, axisLowPx, depth, axisHighPx - axisLowPx);

            return (rect, DepthCentreLine(rect, horizontal, overshootPx));
        }

        /// <summary>
        /// One Face-rectangle projection spanning <paramref name="axisLowPx"/>..<paramref name="axisHighPx"/>
        /// along the edge's own length, plus its center line. Always starts flush at the physical
        /// Face edge given by <paramref name="surface"/> and grows inward toward the panel center by
        /// <paramref name="perpendicularPx"/> - a bore's drilling depth, or an edge-plane groove's
        /// <c>dp</c> - since that edge is fixed regardless of the program's side.
        /// </summary>
        public static (PxRect Rect, PxLine CentreLine) FaceRectangleRange(
            double axisLowPx,
            double axisHighPx,
            double perpendicularPx,
            double overshootPx,
            FaceLayout layout,
            BoreSurface surface,
            bool horizontal)
        {
            var rect = horizontal
                ? new PxRect(
                    axisLowPx,
                    surface == BoreSurface.Top ? layout.OriginY : layout.OriginY + layout.FaceHeight - perpendicularPx,
                    axisHighPx - axisLowPx,
                    perpendicularPx)
                : new PxRect(
                    surface == BoreSurface.Left ? layout.OriginX : layout.OriginX + layout.FaceWidth - perpendicularPx,
                    axisLowPx,
                    perpendicularPx,
                    axisHighPx - axisLowPx);

            return (rect, DepthCentreLine(rect, horizontal, overshootPx));
        }

        /// <summary>
        /// The center line of a depth rectangle: through its middle along the depth axis (across
        /// a horizontal band's length), overshooting the rectangle by <paramref name="overshootPx"/>
        /// at each end.
        /// </summary>
        public static PxLine DepthCentreLine(PxRect rect, bool horizontal, double overshootPx)
        {
            if (horizontal)
            {
                var midX = rect.Left + (rect.Width / 2);

                return new PxLine(midX, rect.Top - overshootPx, midX, rect.Top + rect.Height + overshootPx);
            }

            var midY = rect.Top + (rect.Height / 2);

            return new PxLine(rect.Left - overshootPx, midY, rect.Left + rect.Width + overshootPx, midY);
        }

        /// <summary>
        /// The crossed center lines of a circular symbol (bore, mill start marker) as four arms of
        /// <paramref name="armPx"/> running outward from <paramref name="centre"/>. Starting every
        /// arm at the centre keeps a dashed pattern symmetric about it (a dash always crosses the
        /// centre) instead of drifting with the full line's length.
        /// </summary>
        public static IReadOnlyList<PxLine> CrossArms(Vec2 centre, double armPx)
        {
            var (cx, cy) = centre;

            return
            [
                new PxLine(cx, cy, cx + armPx, cy),
                new PxLine(cx, cy, cx - armPx, cy),
                new PxLine(cx, cy, cx, cy + armPx),
                new PxLine(cx, cy, cx, cy - armPx),
            ];
        }
    }
}
