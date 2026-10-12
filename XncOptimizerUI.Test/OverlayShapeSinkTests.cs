using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Views.PartPreview;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class OverlayShapeSinkTests
    {
        private const string Tag = "element";

        [Test]
        public void AddRectangle_placesATaggedOutlineWithAClickableTransparentInside()
        {
            var layer = new List<UIElement>();
            var sink = new OverlayShapeSink(Tag, Brushes.Navy);

            var rect = sink.AddRectangle(layer, new PxRect(10, 20, 30, 40), new LineStyle(2));

            Assert.Multiple(() =>
            {
                Assert.That(layer, Is.EqualTo(new[] { rect }));
                Assert.That((System.Windows.Controls.Canvas.GetLeft(rect), System.Windows.Controls.Canvas.GetTop(rect), rect.Width, rect.Height), Is.EqualTo((10d, 20d, 30d, 40d)));
                Assert.That(rect.Tag, Is.EqualTo(Tag));
                Assert.That(rect.Stroke, Is.SameAs(Brushes.Navy));
                Assert.That(rect.Fill, Is.SameAs(Brushes.Transparent));
                Assert.That(rect.StrokeThickness, Is.EqualTo(2));
                Assert.That(rect.StrokeDashArray, Is.Null);
            });
        }

        [Test]
        public void SetStroke_expressesTheDashInPxAsMultiplesOfTheThickness()
        {
            var line = new Line();

            OverlayShapeSink.SetStroke(line, new LineStyle(Thickness: 2, DashPx: 6));

            Assert.That(line.StrokeDashArray, Is.EqualTo(new DoubleCollection { 3, 3 }));
        }

        [Test]
        public void AddCentreLine_isStrokedButUntagged()
        {
            var layer = new List<UIElement>();
            var sink = new OverlayShapeSink(Tag, Brushes.Navy);

            var line = sink.AddCentreLine(layer, new PxLine(1, 2, 3, 4), new LineStyle(1, 3));

            Assert.Multiple(() =>
            {
                Assert.That((line.X1, line.Y1, line.X2, line.Y2), Is.EqualTo((1d, 2d, 3d, 4d)));
                Assert.That(line.Tag, Is.Null);
                Assert.That(line.Stroke, Is.SameAs(Brushes.Navy));
                Assert.That(line.Fill, Is.Null);
            });
        }

        [Test]
        public void AddCrossArms_drawsFourArmsOutwardFromTheCentre()
        {
            var layer = new List<UIElement>();
            var sink = new OverlayShapeSink(Tag, Brushes.Navy);

            sink.AddCrossArms(layer, new Vec2(10, 10), 5, new LineStyle(1));

            var ends = layer.Cast<Line>().Select(l => (l.X1, l.Y1, l.X2, l.Y2));
            Assert.That(ends, Is.EqualTo(new[] { (10d, 10d, 15d, 10d), (10d, 10d, 5d, 10d), (10d, 10d, 10d, 15d), (10d, 10d, 10d, 5d) }));
        }

        [Test]
        public void AttachClickToggle_switchesEveryTrackedStrokeOnEachClickOfAClickTarget()
        {
            var layer = new List<UIElement>();
            var sink = new OverlayShapeSink(Tag, Brushes.Navy);
            var circle = sink.AddCircle(layer, new Vec2(0, 0), 4, new LineStyle(2));
            var centreLine = sink.AddCentreLine(layer, new PxLine(0, 0, 1, 1), new LineStyle(1));
            var ownBrush = sink.Add(layer, new Polyline(), Brushes.Black, isClickTarget: false);
            var untracked = sink.Add(layer, new Polyline { Stroke = Brushes.Wheat }, stroke: null, isClickTarget: true);
            sink.AttachClickToggle(Brushes.Red);

            Click(untracked);
            var selected = (circle.Stroke, centreLine.Stroke, ownBrush.Stroke, untracked.Stroke);
            Click(circle);
            var deselected = (circle.Stroke, centreLine.Stroke, ownBrush.Stroke, untracked.Stroke);

            Assert.Multiple(() =>
            {
                Assert.That(selected, Is.EqualTo((Brushes.Red, Brushes.Red, Brushes.Red, Brushes.Wheat)));
                Assert.That(deselected, Is.EqualTo((Brushes.Navy, Brushes.Navy, Brushes.Black, Brushes.Wheat)));
            });
        }

        [Test]
        public void AttachClickToggle_ignoresClicksOnShapesThatAreNotClickTargets()
        {
            var layer = new List<UIElement>();
            var sink = new OverlayShapeSink(Tag, Brushes.Navy);
            var line = sink.AddLine(layer, new PxLine(0, 0, 1, 1), new LineStyle(1));
            sink.AttachClickToggle(Brushes.Red);

            Click(line);

            Assert.That(line.Stroke, Is.SameAs(Brushes.Navy));
        }

        private static void Click(UIElement element) =>
            element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
    }
}
