using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.Views.PartPreview;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class MillPreviewRendererTests
    {
        [TestCase(8, false)]
        [TestCase(8.1, true)]
        public void DrawMills_stylesStartMarkerCrossLinesBySharedEightMillimetreThreshold(double diameter, bool dashed)
        {
            var program = new XncProgram
            {
                Dx = 100,
                Dy = 80,
                Dz = 10,
                Tools = [new XncTool { Name = "Mill", Diameter = diameter }],
                MillingContours =
                [
                    new XncMillingContour
                    {
                        ToolName = "Mill",
                        Entry = new XncPoint(20, 20),
                        EntryDepth = 5,
                        Segments = [new XncLineSegment { End = new XncPoint(60, 20), Depth = 5 }],
                    },
                ],
            };
            var boreBrushes = new BoreBrushes(
                Brushes.Black,
                Brushes.Gray,
                Brushes.Red,
                Brushes.LightBlue,
                Thickness2Px: 2,
                Thickness1Px: 1,
                CenterLineOvershootMm: 3,
                DashLengthPx: 3,
                CenterLineDashMm: 3);
            var millBrushes = new MillBrushes(
                Brushes.White,
                Brushes.LightBlue,
                Brushes.LightGray,
                Brushes.Black,
                CentreLineThickness: 1,
                StripOpacity: 0.5,
                Lines: boreBrushes);
            var layout = new FaceLayout(0, 0, 100, 80, 1, 10, 5);

            var shapes = MillPreviewRenderer.DrawMills([program], layout, millBrushes);

            var lines = shapes.OfType<Line>().ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(lines.Length, Is.EqualTo(4));
                Assert.That(lines, Has.All.Matches<Line>(line => (line.StrokeDashArray is not null) == dashed));
            });
        }
    }
}
