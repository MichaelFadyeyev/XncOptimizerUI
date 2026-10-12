using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class PlanarGeometryTests
    {
        [TestCase(true, 5, 8.660254)]
        [TestCase(false, 5, -8.660254)]
        public void ArcCentreFromChord_PlacesCentreOnDirSide(bool clockwise, double expectedX, double expectedY)
        {
            var centre = PlanarGeometry.ArcCentreFromChord(new Vec2(0, 0), new Vec2(10, 0), 10, clockwise);

            Assert.That(centre, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(centre!.Value.X, Is.EqualTo(expectedX).Within(1e-6));
                Assert.That(centre.Value.Y, Is.EqualTo(expectedY).Within(1e-6));
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ArcCentreFromChord_HalfCircleCentreIsChordMidpoint(bool clockwise)
        {
            var centre = PlanarGeometry.ArcCentreFromChord(new Vec2(0, 0), new Vec2(10, 0), 5, clockwise);

            Assert.That(centre, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(centre!.Value.X, Is.EqualTo(5).Within(1e-9));
                Assert.That(centre.Value.Y, Is.EqualTo(0).Within(1e-9));
            });
        }

        [TestCase(0, 0, 10, 0, 4.9, Description = "chord longer than the diameter")]
        [TestCase(3, 3, 3, 3, 5, Description = "zero chord")]
        public void ArcCentreFromChord_Impossible_ReturnsNull(double sx, double sy, double ex, double ey, double radius)
        {
            Assert.That(PlanarGeometry.ArcCentreFromChord(new Vec2(sx, sy), new Vec2(ex, ey), radius, clockwise: true), Is.Null);
        }

        [TestCase(5, 5, 0, Description = "on the segment")]
        [TestCase(0, 10, 7.0710678, Description = "projects inside the segment")]
        [TestCase(-3, -4, 5, Description = "beyond the start")]
        [TestCase(13, 14, 5, Description = "beyond the end")]
        public void DistanceToSegment_MeasuresToTheNearestSegmentPoint(double px, double py, double expected)
        {
            var distance = PlanarGeometry.DistanceToSegment(new Vec2(px, py), new Vec2(0, 0), new Vec2(10, 10));

            Assert.That(distance, Is.EqualTo(expected).Within(1e-6));
        }

        [TestCase(-5, 5, 15, 5, 0, Description = "crosses the box")]
        [TestCase(2, 2, 3, 3, 0, Description = "inside the box")]
        [TestCase(-5, -1, 15, -1, 1, Description = "runs below the box")]
        [TestCase(13, 14, 20, 20, 5, Description = "beyond a corner")]
        public void Box_DistanceToSegment(double ax, double ay, double bx, double by, double expected)
        {
            var box = Box.Of(10, 10);

            Assert.That(box.DistanceTo(new Vec2(ax, ay), new Vec2(bx, by)), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void Box_SeparationFrom_NegativeWhenOverlapping_PositiveGapOtherwise()
        {
            var box = Box.Of(10, 10);
            Vec2[] overlapping = [new(8, 8), new(12, 8), new(12, 12), new(8, 12)];
            Vec2[] diagonalAway = [new(14, 10), new(20, 4), new(24, 8), new(18, 14)];

            Assert.Multiple(() =>
            {
                Assert.That(box.SeparationFrom(overlapping), Is.EqualTo(-2).Within(1e-9));
                Assert.That(box.SeparationFrom(diagonalAway), Is.GreaterThan(0));
            });
        }
    }
}
