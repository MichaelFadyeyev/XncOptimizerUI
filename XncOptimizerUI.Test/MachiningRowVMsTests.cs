using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Machining;

namespace XncOptimizerUI.Test
{
    /// <summary>Every program table shows its values like the bores and grooves tables: at most 3 decimals.</summary>
    [TestFixture]
    public class MachiningRowVMsTests
    {
        private const double LongValue = 12.34567;
        private const double SumWithRoundingNoise = 0.1 + 0.2;

        [Test]
        public void ContourRow_FormatsValuesToThreeDecimals()
        {
            var contour = new XncMillingContour
            {
                Entry = new XncPoint(LongValue, SumWithRoundingNoise),
                EntryDepth = LongValue
            };

            var row = new MillingContourRowVM(1, "Front", contour, (_, _) => { });

            Assert.Multiple(() =>
            {
                Assert.That(row.EntryX, Is.EqualTo("12.346"));
                Assert.That(row.EntryY, Is.EqualTo("0.3"));
                Assert.That(row.Depth, Is.EqualTo("12.346"));
            });
        }

        [Test]
        public void EllipseRow_FormatsValuesToThreeDecimals()
        {
            var ellipse = new XncMillingEllipse { Center = new XncPoint(LongValue, 1), Angle = SumWithRoundingNoise };

            var row = new MillingEllipseRowVM(1, "Front", ellipse, (_, _) => { });

            Assert.Multiple(() =>
            {
                Assert.That(row.CenterX, Is.EqualTo("12.346"));
                Assert.That(row.Angle, Is.EqualTo("0.3"));
            });
        }

        [Test]
        public void RectangleRow_FormatsValuesToThreeDecimals()
        {
            var rectangle = new XncMillingRectangle { Length = LongValue, CornerRadius = SumWithRoundingNoise };

            var row = new MillingRectangleRowVM(1, "Front", rectangle, (_, _) => { });

            Assert.Multiple(() =>
            {
                Assert.That(row.Length, Is.EqualTo("12.346"));
                Assert.That(row.CornerRadius, Is.EqualTo("0.3"));
            });
        }

        [Test]
        public void ToolRow_FormatsDiameterToThreeDecimals()
        {
            var row = new ToolRowVM(1, "Front", new XncTool { Name = "T1", Diameter = SumWithRoundingNoise });

            Assert.That(row.Diameter, Is.EqualTo("0.3"));
        }
    }
}
