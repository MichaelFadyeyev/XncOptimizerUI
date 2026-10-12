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
        private static readonly FaceLayout Layout = new(0, 0, 100, 100, 1, 10, 5);

        private static readonly BoreBrushes Style = new(
            Brushes.Black,
            Brushes.Gray,
            Brushes.Red,
            Brushes.LightBlue,
            Thickness2Px: 2,
            Thickness1Px: 1,
            CenterLineOvershootMm: 3,
            DashLengthPx: 3,
            CenterLineDashMm: 3);

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

        [Test]
        public void DrawBores_findsTheToolIgnoringCaseLikeMills()
        {
            var program = new XncProgram
            {
                Tools = [new XncTool { Name = "Drill", Diameter = 8 }],
                Bores = [new XncBore { Surface = BoreSurface.Face, ToolName = "DRILL", Depth = 5, X = 20, Y = 20 }],
            };

            var shapes = BorePreviewRenderer.DrawBores([program], Layout, Style);

            Assert.That(shapes.OfType<Ellipse>().Single().Width, Is.EqualTo(8));
        }

        private static IReadOnlyList<Line> DrawCrossLines(BoreSurface surface, double diameter)
        {
            var program = new XncProgram
            {
                Tools = [new XncTool { Name = "Drill", Diameter = diameter }],
                Bores = [new XncBore { Surface = surface, ToolName = "Drill", Depth = 5, X = 20, Y = 20, Z = 2 }],
            };

            var shapes = BorePreviewRenderer.DrawBores([program], Layout, Style);

            return shapes.OfType<Line>().Take(4).ToArray();
        }
    }
}
