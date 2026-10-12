using System.Windows;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws an edge-plane groove (<see cref="XncGrooving.SideCode"/> <c>1</c>/<c>2</c>/<c>3</c>/<c>4</c>
    /// = Left/Right/Top/Bottom), the mirror image of a front-plane one: its true rectangle on the one
    /// band it belongs to (offset from the band's inner edge by its own <c>Z</c>), a dashed mirror of
    /// it on the opposite band, a dashed <c>dp</c> rectangle on the Face rectangle flush to that same
    /// edge and growing inward, and a small <c>t</c>-wide/<c>dp</c>-long marker on each of the two
    /// bands perpendicular to its own plane. Every band position measures from a band's inner edge,
    /// regardless of the program's <c>Side</c>, and the groove always takes
    /// <see cref="BoreBrushes.SideTrue"/> (mirrors an edge bore).
    /// </summary>
    internal static class EdgePlaneGrooveRenderer
    {
        /// <summary>One groove's drawing context; along-band coordinates are order-normalized (<see cref="AxisLow"/> &lt;= <see cref="AxisHigh"/>).</summary>
        private sealed record Groove(
            GrooveShapes Shapes,
            FaceLayout Layout,
            BoreSurface Surface,
            double AxisLow,
            double AxisHigh,
            double ZPx,
            double WidthPx,
            double DepthPx,
            ToolPosition TravelPosition)
        {
            /// <summary>Top/Bottom plane: the groove runs along X.</summary>
            public bool Horizontal => Surface is BoreSurface.Top or BoreSurface.Bottom;

            /// <summary>Left/Top plane.</summary>
            public bool Near => Surface is BoreSurface.Top or BoreSurface.Left;

            /// <summary>Across-band position of the groove on band (<paramref name="horizontal"/>, <paramref name="near"/>): <c>Z</c> in from its inner edge.</summary>
            public double AcrossPx(bool horizontal, bool near) => Layout.Band(horizontal, near).At(ZPx, fromInner: true);
        }

        public static void Draw(ICollection<UIElement> shapes, XncProgram program, XncGrooving groove, FaceLayout layout, BoreBrushes brushes)
        {
            var overshootPx = layout.MmToPx(brushes.CenterLineOvershootMm);
            var surface = PlaneOf(groove);
            var horizontal = surface is BoreSurface.Top or BoreSurface.Bottom;

            // The axis not carrying the groove's along-edge extent stays constant (Start ~ End) at
            // the panel edge (e.g. x1=x2=dx for a right-edge groove) - it's not the through-thickness
            // position; that comes from the groove's own Z.
            var (axisLow, axisHigh) = horizontal
                ? (Math.Min(groove.Start.X, groove.End.X), Math.Max(groove.Start.X, groove.End.X))
                : (Math.Min(groove.Start.Y, groove.End.Y), Math.Max(groove.Start.Y, groove.End.Y));

            var context = new Groove(
                new GrooveShapes(shapes, new OverlayShapeSink(new GrooveTag(program, groove), brushes.SideTrue), brushes, overshootPx),
                layout,
                surface,
                axisLow,
                axisHigh,
                layout.MmToPx(groove.Z),
                layout.MmToPx(groove.Width),
                layout.MmToPx(groove.Depth),
                TravelPosition(groove));

            DrawBandRectangle(context, onOwnBand: true);
            DrawBandRectangle(context, onOwnBand: false);
            DrawFaceProjection(context);
            DrawCrossBandMarker(context, markerNear: true);
            DrawCrossBandMarker(context, markerNear: false);

            context.Shapes.AttachClickToggle();
        }

        private static BoreSurface PlaneOf(XncGrooving groove) => groove.SideCode switch
        {
            1 => BoreSurface.Left,
            2 => BoreSurface.Right,
            3 => BoreSurface.Top,
            _ => BoreSurface.Bottom,
        };

        /// <summary>
        /// Resolves the groove's <c>c</c> against the drawing's low-to-high along-band direction.
        /// <c>c</c> is defined relative to the tool's real travel, which is max(y1,y2)-&gt;min(y1,y2)
        /// for a Left/Right-plane groove (<c>p</c> 1|2) - opposite to the drawing direction, so
        /// mirrored - and min(x1,x2)-&gt;max(x1,x2) for a Top/Bottom-plane groove (<c>p</c> 3|4) -
        /// the drawing direction itself. Both are order-normalized, so (unlike a front-plane groove's
        /// literal Start-&gt;End) this doesn't depend on which endpoint the file labels Start.
        /// </summary>
        private static ToolPosition TravelPosition(XncGrooving groove) =>
            groove.SideCode is 1 or 2 ? ToolPositionOffset.Mirror(groove.Position) : groove.Position;

        /// <summary>
        /// The groove's true rectangle on its own band - solid, with its center line - or, when not
        /// <paramref name="onOwnBand"/>, its dashed mirror without a center line on the band
        /// directly across (Right for a Left-plane groove, Left for Right, Bottom for Top, Top for
        /// Bottom): same along-band extent and <c>Z</c> offset from that band's own inner edge. The
        /// inner edges face opposite ways, so <c>c</c>'s Right/Left is mirrored to land on the
        /// physically correct side.
        /// </summary>
        private static void DrawBandRectangle(Groove groove, bool onOwnBand)
        {
            var (horizontal, layout) = (groove.Horizontal, groove.Layout);
            var acrossPx = groove.AcrossPx(horizontal, onOwnBand ? groove.Near : !groove.Near);
            var start = AlongBand(horizontal, layout.AlongPx(horizontal, groove.AxisLow), acrossPx);
            var end = AlongBand(horizontal, layout.AlongPx(horizontal, groove.AxisHigh), acrossPx);
            var position = onOwnBand ? groove.TravelPosition : ToolPositionOffset.Mirror(groove.TravelPosition);

            groove.Shapes.OffsetRectangle(start, end, groove.WidthPx, position, solid: onOwnBand, withCentreLine: onOwnBand);
        }

        /// <summary>
        /// The depth projection on the Face rectangle: flush to the groove's own Face edge, growing
        /// inward - dashed, without a center line (the "front plane" symbol of an edge groove).
        /// </summary>
        private static void DrawFaceProjection(Groove groove)
        {
            var (horizontal, layout) = (groove.Horizontal, groove.Layout);
            var (rect, _) = PartPreviewOverlayGeometry.FaceRectangleRange(
                layout.AlongPx(horizontal, groove.AxisLow),
                layout.AlongPx(horizontal, groove.AxisHigh),
                groove.DepthPx,
                0,
                layout,
                groove.Surface,
                horizontal);

            groove.Shapes.Rectangle(rect, solid: false);
        }

        /// <summary>
        /// A depth marker on one of the two bands perpendicular to the groove's own plane (Top/Bottom
        /// for a Left/Right-plane groove, Left/Right for a Top/Bottom-plane groove;
        /// <paramref name="markerNear"/> picks Top/Left): <c>t</c> wide and <c>dp</c> long, flush at
        /// the groove's own Face edge and growing inward along its depth axis, displaced by the
        /// groove's <c>Z</c> from that band's inner edge, with a center line. Solid only where the
        /// groove's own along-edge extent reaches that band's edge, else a dashed reference.
        /// <c>c</c> is mirrored when the marker's band lies on the same (near or far) side as the
        /// groove's own plane, whose inner edges face opposite ways.
        /// </summary>
        private static void DrawCrossBandMarker(Groove groove, bool markerNear)
        {
            var layout = groove.Layout;
            var markerHorizontal = !groove.Horizontal;
            var (depthLow, depthHigh) = markerHorizontal
                ? (groove.Surface == BoreSurface.Left
                    ? (layout.OriginX, layout.OriginX + groove.DepthPx)
                    : (layout.OriginX + layout.FaceWidth - groove.DepthPx, layout.OriginX + layout.FaceWidth))
                : (groove.Surface == BoreSurface.Top
                    ? (layout.OriginY, layout.OriginY + groove.DepthPx)
                    : (layout.OriginY + layout.FaceHeight - groove.DepthPx, layout.OriginY + layout.FaceHeight));

            var acrossPx = groove.AcrossPx(markerHorizontal, markerNear);
            var position = markerNear == groove.Near ? ToolPositionOffset.Mirror(groove.TravelPosition) : groove.TravelPosition;
            var faceExtentMm = groove.Horizontal ? layout.FaceWidthMm : layout.FaceHeightMm;
            var solid = markerNear
                ? GroovePreviewGeometry.ReachesNearEdge(groove.AxisLow)
                : GroovePreviewGeometry.ReachesFarEdge(groove.AxisHigh, faceExtentMm);

            groove.Shapes.OffsetRectangle(
                AlongBand(markerHorizontal, depthLow, acrossPx),
                AlongBand(markerHorizontal, depthHigh, acrossPx),
                groove.WidthPx,
                position,
                solid,
                withCentreLine: true);
        }

        /// <summary>A canvas point on a horizontal (Top/Bottom) band from its along-band x and across-band y, or the other way round.</summary>
        private static Vec2 AlongBand(bool horizontal, double alongPx, double acrossPx) =>
            horizontal ? new Vec2(alongPx, acrossPx) : new Vec2(acrossPx, alongPx);
    }
}
