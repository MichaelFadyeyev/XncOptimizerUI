using System.Windows;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Adds the shapes of one groove in the groove styles: a rectangle is solid
    /// (<see cref="BoreBrushes.Bold"/>) where it is the groove's true (home-plane) shape or a real
    /// continuation onto an adjacent plane, else a dashed reference projection
    /// (<see cref="BoreBrushes.Dashed"/>); a groove center line is always dashed.
    /// </summary>
    internal sealed class GrooveShapes(ICollection<UIElement> shapes, OverlayShapeSink sink, BoreBrushes brushes, double overshootPx)
    {
        public void Rectangle(PxRect rect, bool solid) => sink.AddRectangle(shapes, rect, Style(solid));

        public void CentreLine(PxLine line) => sink.AddCentreLine(shapes, line, brushes.Dashed);

        /// <summary>A dashed edge of a projected 3D footprint; not a click target.</summary>
        public void Rib(PxLine line) =>
            sink.AddOutline(shapes, new Line { X1 = line.X1, Y1 = line.Y1, X2 = line.X2, Y2 = line.Y2 }, brushes.Dashed, isClickTarget: false);

        /// <summary>
        /// A <paramref name="widthPx"/>-wide rectangle along the line from <paramref name="start"/>
        /// to <paramref name="end"/>, placed beside it per <paramref name="position"/> (see
        /// <see cref="OffsetStrip"/>), plus - when <paramref name="withCentreLine"/> - the line
        /// itself extended at both ends. Works for any orientation. Draws nothing for a
        /// zero-length line.
        /// </summary>
        public void OffsetRectangle(Vec2 start, Vec2 end, double widthPx, ToolPosition position, bool solid, bool withCentreLine)
        {
            if (OffsetStrip.Along(start, end, widthPx, position) is not { } strip)
            {
                return;
            }

            sink.AddPolygon(shapes, strip.Corners, Style(solid));

            if (withCentreLine)
            {
                CentreLine(strip.CentreLine(overshootPx));
            }
        }

        public void AttachClickToggle() => sink.AttachClickToggle(brushes.Selected);

        private LineStyle Style(bool solid) => solid ? brushes.Bold : brushes.Dashed;
    }
}
