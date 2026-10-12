using XncOptimizerUI.MVVM.Views.PartPreview;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class PartPreviewLayoutTests
    {
        private static readonly PartPreviewMetrics Metrics = new(GapMm: 10, Margin: 20, MinBandThickness: 1);

        [TestCase(0, 100, 400, 300)]
        [TestCase(100, 0, 400, 300)]
        [TestCase(100, 100, 0, 300)]
        [TestCase(100, 100, 400, 0)]
        [TestCase(100, 100, 40, 300)]
        [TestCase(100, 100, 400, 40)]
        public void Fit_returnsNullWithoutPartSizeOrRoomInsideTheMargins(double lengthMm, double widthMm, double viewportWidth, double viewportHeight)
        {
            var layout = PartPreviewLayout.Fit(viewportWidth, viewportHeight, lengthMm, widthMm, sheetThicknessMm: 18, zoom: 1, Metrics);

            Assert.That(layout, Is.Null);
        }

        [Test]
        public void Fit_scalesFaceGapAndBandsToTheTighterViewportAxisAndCentresThem()
        {
            // Drawing 600 x 300 mm (face + 2 x (10 gap + 20 band)); room 360 x 260 px -> 0.6 px/mm by width.
            var layout = PartPreviewLayout.Fit(400, 300, lengthMm: 540, widthMm: 240, sheetThicknessMm: 20, zoom: 1, Metrics)!;

            Assert.Multiple(() =>
            {
                Assert.That(layout.CanvasWidth, Is.EqualTo(400));
                Assert.That(layout.CanvasHeight, Is.EqualTo(300));
                Assert.That(layout.Face.Scale, Is.EqualTo(0.6).Within(1e-12));
                Assert.That(layout.Face.FaceWidth, Is.EqualTo(324).Within(1e-9));
                Assert.That(layout.Face.FaceHeight, Is.EqualTo(144).Within(1e-9));
                Assert.That(layout.Face.Gap, Is.EqualTo(6).Within(1e-9));
                Assert.That(layout.Face.BandThickness, Is.EqualTo(12).Within(1e-9));
                Assert.That(layout.Face.OriginX, Is.EqualTo(38).Within(1e-9));
                Assert.That(layout.Face.OriginY, Is.EqualTo(78).Within(1e-9));
            });
        }

        [Test]
        public void Fit_magnifiesTheFitScaleAndGrowsTheCanvasToTheZoomedDrawing()
        {
            // 1.2 px/mm: drawing 720 x 360 px + 2 x 20 margin.
            var layout = PartPreviewLayout.Fit(400, 300, 540, 240, 20, zoom: 2, Metrics)!;

            Assert.Multiple(() =>
            {
                Assert.That(layout.Face.Scale, Is.EqualTo(1.2).Within(1e-12));
                Assert.That(layout.CanvasWidth, Is.EqualTo(760).Within(1e-9));
                Assert.That(layout.CanvasHeight, Is.EqualTo(400).Within(1e-9));
                Assert.That(layout.Face.OriginX, Is.EqualTo(56).Within(1e-9));
                Assert.That(layout.Face.OriginY, Is.EqualTo(56).Within(1e-9));
            });
        }

        [Test]
        public void Fit_keepsBandsVisibleWithoutSheetThickness()
        {
            var layout = PartPreviewLayout.Fit(400, 300, 540, 240, sheetThicknessMm: 0, zoom: 1, Metrics)!;

            Assert.That(layout.Face.BandThickness, Is.EqualTo(Metrics.MinBandThickness));
        }

        [Test]
        public void Bands_surroundTheFaceOneGapAway()
        {
            var layout = new PartPreviewLayout(400, 300, new FaceLayout(OriginX: 50, OriginY: 40, FaceWidth: 200, FaceHeight: 100, Scale: 1, Gap: 10, BandThickness: 5));

            Assert.That(layout.Bands, Is.EqualTo(new[]
            {
                ("Top", new PxRect(50, 25, 200, 5)),
                ("Bottom", new PxRect(50, 150, 200, 5)),
                ("Left", new PxRect(35, 40, 5, 100)),
                ("Right", new PxRect(260, 40, 5, 100)),
            }));
            Assert.That(layout.FaceRect, Is.EqualTo(new PxRect(50, 40, 200, 100)));
        }
    }
}
