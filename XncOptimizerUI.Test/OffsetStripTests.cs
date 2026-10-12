using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.Views.PartPreview;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class OffsetStripTests
    {
        [TestCase(ToolPosition.Right, 0, 4)]
        [TestCase(ToolPosition.Left, -4, 0)]
        [TestCase(ToolPosition.Center, -2, 2)]
        [TestCase(ToolPosition.Pocket, -2, 2)]
        public void Sides_putTheWidthRightOrLeftOfTravelOrStraddleIt(ToolPosition position, double a, double b)
        {
            Assert.That(ToolPositionOffset.Sides(position, 4), Is.EqualTo((a, b)));
        }

        [TestCase(ToolPosition.Right, ToolPosition.Left)]
        [TestCase(ToolPosition.Left, ToolPosition.Right)]
        [TestCase(ToolPosition.Center, ToolPosition.Center)]
        [TestCase(ToolPosition.Pocket, ToolPosition.Pocket)]
        public void Mirror_swapsRightAndLeftOnly(ToolPosition position, ToolPosition mirrored)
        {
            Assert.That(ToolPositionOffset.Mirror(position), Is.EqualTo(mirrored));
        }

        [Test]
        public void Along_isNullForAZeroLengthCut()
        {
            Assert.That(OffsetStrip.Along(new Vec2(3, 3), new Vec2(3, 3), 4, ToolPosition.Center), Is.Null);
        }

        [Test]
        public void Corners_lieRightOfTravelInTheYDownFrameForToolRight()
        {
            // Travelling +X on a Y-down screen, right of travel is +Y.
            var strip = OffsetStrip.Along(new Vec2(0, 0), new Vec2(10, 0), 2, ToolPosition.Right)!.Value;

            Assert.That(strip.Corners, Is.EqualTo(new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 2), new Vec2(0, 2) }));
        }

        [Test]
        public void CentreLine_extendsTheTravelLineAtBothEnds()
        {
            var strip = OffsetStrip.Along(new Vec2(0, 5), new Vec2(0, 15), 2, ToolPosition.Center)!.Value;

            Assert.That(strip.CentreLine(1), Is.EqualTo(new PxLine(0, 4, 0, 16)));
        }

        [Test]
        public void CrossingAt_findsWhereAnOffsetSideCrossesAnEdge()
        {
            // 45° travel from the origin; the side 1 unit right of it is y = x + sqrt(2).
            var strip = OffsetStrip.Along(new Vec2(0, 0), new Vec2(10, 10), 2, ToolPosition.Right)!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(strip.CrossingAt(horizontal: true, edge: 5, offset: 0), Is.EqualTo(5).Within(1e-9));
                Assert.That(strip.CrossingAt(horizontal: true, edge: 5, offset: 1), Is.EqualTo(5 - Math.Sqrt(2)).Within(1e-9));
                Assert.That(strip.CrossingAt(horizontal: false, edge: 5, offset: 1), Is.EqualTo(5 + Math.Sqrt(2)).Within(1e-9));
            });
        }
    }
}
