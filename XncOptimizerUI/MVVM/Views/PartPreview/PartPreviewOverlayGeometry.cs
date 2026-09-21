using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Band-edge and rectangle-placement geometry shared by <see cref="BorePreviewRenderer"/>
    /// and <see cref="GroovePreviewRenderer"/>: both draw a "true" shape on one plane plus a
    /// depth/extent rectangle projected onto the four edge bands or onto the Face rectangle,
    /// anchored to whichever edge is nearest the Face when <c>side</c> is true, else the
    /// far/outer edge.
    /// </summary>
    internal static class PartPreviewOverlayGeometry
    {
        /// <summary>
        /// Sets <paramref name="shape"/>'s stroke thickness and, when <paramref name="dashLengthPx"/>
        /// is given, a dash/gap pattern of that length. WPF expresses <c>StrokeDashArray</c> as
        /// multiples of <c>StrokeThickness</c>, so the dash length is divided by
        /// <paramref name="thickness"/> to keep call sites in plain px/mm.
        /// </summary>
        internal static void SetStroke(Shape shape, double thickness, double? dashLengthPx = null)
        {
            shape.StrokeThickness = thickness;
            shape.StrokeDashArray = dashLengthPx is double d
                ? new DoubleCollection { d / thickness, d / thickness }
                : null;
        }

        /// <summary>
        /// Returns the near/far pixel edges of one Top/Bottom/Left/Right band and the
        /// near-to-far direction sign (-1 for Top/Left, +1 for Bottom/Right).
        /// </summary>
        internal static (double NearEdge, double FarEdge, double Direction) GetBandEdges(
            BorePreviewRenderer.FaceLayout layout, bool horizontal, bool near)
        {
            var direction = near ? -1d : 1d;
            var nearEdge = horizontal
                ? (near ? layout.OriginY - layout.Gap : layout.OriginY + layout.FaceHeight + layout.Gap)
                : (near ? layout.OriginX - layout.Gap : layout.OriginX + layout.FaceWidth + layout.Gap);
            var farEdge = nearEdge + (direction * layout.BandThickness);

            return (nearEdge, farEdge, direction);
        }

        /// <summary>
        /// Draws one edge-band rectangle spanning <paramref name="axisLowPx"/>..<paramref name="axisHighPx"/>
        /// along the band's own length axis, plus its center line. <paramref name="horizontal"/>
        /// selects the Top/Bottom band pair (band spans the part's X axis) vs the Left/Right pair
        /// (spans Y); <paramref name="near"/> selects which of that pair (Top/Left = true,
        /// Bottom/Right = false). The rectangle's <paramref name="perpendicularPx"/> extent starts
        /// flush at the band edge nearest the Face when <paramref name="side"/> is true (front), or
        /// the band's far/outer edge when false (back), and grows toward the other edge - a bore's
        /// drilling depth, or a front-plane groove's <c>dp</c> into the band.
        /// </summary>
        internal static void AddSideRectangleRange(
            Action<Shape, bool> addShape,
            Action<double, double, double, double> addCenterLine,
            double axisLowPx,
            double axisHighPx,
            double perpendicularPx,
            double overshootPx,
            BorePreviewRenderer.FaceLayout layout,
            bool side,
            bool horizontal,
            bool near)
        {
            var (nearEdge, farEdge, direction) = GetBandEdges(layout, horizontal, near);

            var start = side ? nearEdge : farEdge;
            var dir = side ? direction : -direction;
            var end = start + (dir * perpendicularPx);
            var lo = Math.Min(start, end);

            double left, top, width, height;

            if (horizontal)
            {
                width = axisHighPx - axisLowPx;
                height = Math.Abs(end - start);
                left = axisLowPx;
                top = lo;
            }
            else
            {
                height = axisHighPx - axisLowPx;
                width = Math.Abs(end - start);
                top = axisLowPx;
                left = lo;
            }

            var rect = new Rectangle { Width = width, Height = height };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            addShape(rect, true);

            // Center line runs along the depth/perpendicular axis, overshooting the rectangle
            // by overshootPx at each end - not along the band's length axis.
            if (horizontal)
            {
                var midX = left + (width / 2);
                addCenterLine(midX, top - overshootPx, midX, top + height + overshootPx);
            }
            else
            {
                var midY = top + (height / 2);
                addCenterLine(left - overshootPx, midY, left + width + overshootPx, midY);
            }
        }

        /// <summary>
        /// Draws one Face-rectangle projection spanning <paramref name="axisLowPx"/>..<paramref name="axisHighPx"/>
        /// along the edge's own length axis, plus its center line. Always starts flush at the
        /// physical Face edge given by <paramref name="surface"/> and grows inward toward the
        /// panel center by <paramref name="perpendicularPx"/> - a bore's drilling depth, or an
        /// edge-plane groove's <c>dp</c> - since that edge is fixed regardless of <c>side</c>.
        /// </summary>
        internal static void AddFaceRectangleRange(
            Action<Shape, bool> addShape,
            Action<double, double, double, double> addCenterLine,
            double axisLowPx,
            double axisHighPx,
            double perpendicularPx,
            double overshootPx,
            BorePreviewRenderer.FaceLayout layout,
            BoreSurface surface,
            bool horizontal)
        {
            double left, top, width, height;

            if (horizontal)
            {
                width = axisHighPx - axisLowPx;
                height = perpendicularPx;
                left = axisLowPx;
                top = surface == BoreSurface.Top
                    ? layout.OriginY
                    : layout.OriginY + layout.FaceHeight - height;
            }
            else
            {
                height = axisHighPx - axisLowPx;
                width = perpendicularPx;
                top = axisLowPx;
                left = surface == BoreSurface.Left
                    ? layout.OriginX
                    : layout.OriginX + layout.FaceWidth - width;
            }

            var rect = new Rectangle { Width = width, Height = height };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            addShape(rect, true);

            if (horizontal)
            {
                var midX = left + (width / 2);
                addCenterLine(midX, top - overshootPx, midX, top + height + overshootPx);
            }
            else
            {
                var midY = top + (height / 2);
                addCenterLine(left - overshootPx, midY, left + width + overshootPx, midY);
            }
        }
    }
}
