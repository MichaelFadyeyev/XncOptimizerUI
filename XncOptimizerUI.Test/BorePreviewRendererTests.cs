using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.Views.PartPreview;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class BorePreviewRendererTests
    {
        [TestCase(BoreSurface.Face)]
        [TestCase(BoreSurface.Right)]
        public void DrawBores_keepsCrossLinesSolidThroughEightMillimetres(BoreSurface surface)
        {
            var lines = DrawCrossLines(surface, diameter: 8);

            Assert.Multiple(() =>
            {
                Assert.That(lines, Has.Count.EqualTo(4));
                Assert.That(lines, Has.All.Matches<Line>(line => line.StrokeDashArray is null));
            });
        }

        [TestCase(BoreSurface.Face)]
        [TestCase(BoreSurface.Right)]
        public void DrawBores_dashesCrossLinesAboveEightMillimetres(BoreSurface surface)
        {
            var lines = DrawCrossLines(surface, diameter: 8.1);

            Assert.Multiple(() =>
            {
                Assert.That(lines, Has.Count.EqualTo(4));
                Assert.That(lines, Has.All.Matches<Line>(line => line.StrokeDashArray is not null));
            });
        }

        private static IReadOnlyList<Line> DrawCrossLines(BoreSurface surface, double diameter)
        {
            var canvas = new Canvas();
            var program = new XncProgram
            {
                Tools = [new XncTool { Name = "Drill", Diameter = diameter }],
                Bores = [new XncBore { Surface = surface, ToolName = "Drill", Depth = 5, X = 20, Y = 20, Z = 2 }],
            };
            var brushes = new BorePreviewRenderer.BoreBrushes(
                Brushes.Black,
                Brushes.Gray,
                Brushes.Red,
                Brushes.LightBlue,
                Thickness2Px: 2,
                Thickness1Px: 1,
                CenterLineOvershootMm: 3,
                DashLengthPx: 3,
                CenterLineDashMm: 3);
            var layout = new BorePreviewRenderer.FaceLayout(0, 0, 100, 100, 1, 10, 5);

            BorePreviewRenderer.DrawBores(canvas, [program], layout, brushes);

            return canvas.Children.OfType<Line>().Take(4).ToArray();
        }

    }
}
