using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XncOptimizerUI.MVVM.ViewModels.Preview;
using XncOptimizerUI.MVVM.Views.PartPreview;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="EdgeBandSymbols"/> turning, and the band symbols
    /// <see cref="PreviewDecorations.BandSymbols"/> places beside the side views.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class EdgeBandSymbolsTests
    {
        private const double Gap = 5;

        private static readonly EdgeBandSymbols PartSymbols = new(Top: "T", Right: "R", Bottom: "B", Left: "L");

        private static readonly BandSymbolStyle Style = new(Brushes.Black, FontSize: 24, Gap);

        // Face 200 x 100 at (100, 100), 10 px gap, 20 px bands.
        private static readonly PartPreviewLayout Layout = new(400, 300, new FaceLayout(100, 100, 200, 100, 1, 10, 20));

        [TestCase(0, "T", "R", "B", "L")]
        [TestCase(1, "L", "T", "R", "B")]
        [TestCase(2, "B", "L", "T", "R")]
        [TestCase(3, "R", "B", "L", "T")]
        [TestCase(4, "T", "R", "B", "L")]
        [TestCase(-1, "R", "B", "L", "T")]
        public void Turned_movesEachEdgeClockwisePerQuarterTurn(int turn, string top, string right, string bottom, string left)
        {
            Assert.That(PartSymbols.Turned(turn), Is.EqualTo(new EdgeBandSymbols(top, right, bottom, left)));
        }

        [Test]
        public void BandSymbols_skipsUnbandedSides()
        {
            var labels = Draw(new EdgeBandSymbols("T", string.Empty, string.Empty, "L"));

            Assert.That(labels.Select(l => l.Text), Is.EqualTo(new[] { "T", "L" }));
        }

        [Test]
        public void BandSymbols_faceEachSideViewCentreFromOutside()
        {
            var labels = Draw(PartSymbols).ToDictionary(l => l.Text);
            var bands = Layout.Bands.ToDictionary(b => b.Side, b => b.Rect);

            Assert.Multiple(() =>
            {
                AssertCentredAlong(labels["T"], bands["Top"], horizontal: true);
                Assert.That(Bottom(labels["T"]), Is.EqualTo(bands["Top"].Top - Gap).Within(1e-9));

                AssertCentredAlong(labels["B"], bands["Bottom"], horizontal: true);
                Assert.That(Canvas.GetTop(labels["B"]), Is.EqualTo(bands["Bottom"].Top + bands["Bottom"].Height + Gap).Within(1e-9));

                AssertCentredAlong(labels["L"], bands["Left"], horizontal: false);
                Assert.That(Right(labels["L"]), Is.EqualTo(bands["Left"].Left - Gap).Within(1e-9));

                AssertCentredAlong(labels["R"], bands["Right"], horizontal: false);
                Assert.That(Canvas.GetLeft(labels["R"]), Is.EqualTo(bands["Right"].Left + bands["Right"].Width + Gap).Within(1e-9));
            });
        }

        private static List<TextBlock> Draw(EdgeBandSymbols symbols) =>
            PreviewDecorations.BandSymbols(Layout, symbols, Style).Cast<TextBlock>().ToList();

        private static void AssertCentredAlong(TextBlock label, PxRect band, bool horizontal)
        {
            var size = label.DesiredSize;
            var (labelCentre, bandCentre) = horizontal
                ? (Canvas.GetLeft(label) + (size.Width / 2), band.Left + (band.Width / 2))
                : (Canvas.GetTop(label) + (size.Height / 2), band.Top + (band.Height / 2));

            Assert.That(labelCentre, Is.EqualTo(bandCentre).Within(1e-9), $"\"{label.Text}\" centred along its side view");
        }

        private static double Bottom(FrameworkElement element) => Canvas.GetTop(element) + element.DesiredSize.Height;

        private static double Right(FrameworkElement element) => Canvas.GetLeft(element) + element.DesiredSize.Width;
    }
}
