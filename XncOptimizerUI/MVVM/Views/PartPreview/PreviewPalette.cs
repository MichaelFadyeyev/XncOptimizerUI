using System.Windows;
using System.Windows.Media;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Every brush and metric the part preview draws with, resolved once from the application
    /// resources (<c>Resources/PartPreviewResources.xaml</c>).
    /// </summary>
    internal sealed record PreviewPalette(
        PartPreviewMetrics Metrics,
        PartBrushes Part,
        DecorationBrushes Decorations,
        BoreBrushes Bores,
        MillBrushes Mills)
    {
        /// <summary>Looks every preview resource key up from <paramref name="source"/>.</summary>
        public static PreviewPalette Resolve(FrameworkElement source)
        {
            Brush Brush(string key) => (Brush)source.FindResource(key);
            double Number(string key) => (double)source.FindResource(key);

            var part = new PartBrushes(
                Brush("PartFaceBrush"),
                Brush("PartEdgeProjectionBrush"),
                Brush("PartOutlineBrush"),
                Number("PartOutlineThickness"),
                Brush("PartTextureBrush"),
                Number("PartTextureLineThickness"));
            var bores = new BoreBrushes(
                Brush("BoreSideTrueBrush"),
                Brush("BoreSideFalseBrush"),
                Brush("BoreSelectedBrush"),
                Brush("PartPreviewBackgroundBrush"),
                Number("LineThickness2Px"),
                Number("LineThickness1Px"),
                Number("BoreCenterLineOvershootMm"),
                Number("DashLengthPx"),
                Number("CenterLineDashMm"));

            return new PreviewPalette(
                new PartPreviewMetrics(
                    Number("ProjectionGapMm"),
                    Number("PartPreviewMargin"),
                    Number("MinEdgeProjectionThickness")),
                part,
                new DecorationBrushes(
                    Brush("AxisXBrush"),
                    Brush("AxisYBrush"),
                    Number("AxisThickness"),
                    Brush("TurnLabelBrush"),
                    Number("TurnLabelFontSize")),
                bores,
                new MillBrushes(
                    part.Face,
                    Brush("ThroughCutOffsBrush"),
                    Brush("BlindCutOffsBrush"),
                    part.Outline,
                    part.OutlineThickness,
                    Number("MillStripOpacity"),
                    bores));
        }
    }
}
