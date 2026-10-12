using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Machining;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class MachiningDisplayTests
    {
        private static readonly XncProgram FrontProgram = new() { Side = true };
        private static readonly XncProgram BackProgram = new() { Side = false };

        [TestCase(true, "Front")]
        [TestCase(false, "Back")]
        public void SideName_NamesTheProgramFace(bool front, string expected) =>
            Assert.That(MachiningDisplay.SideName(front), Is.EqualTo(expected));

        [TestCase(BoreSurface.Top, "Top")]
        [TestCase(BoreSurface.Bottom, "Bottom")]
        [TestCase(BoreSurface.Left, "Left")]
        [TestCase(BoreSurface.Right, "Right")]
        public void BoreSide_OfAnEdgeBore_IsItsSurface(BoreSurface surface, string expected) =>
            Assert.That(MachiningDisplay.BoreSide(new XncBore { Surface = surface }, FrontProgram), Is.EqualTo(expected));

        [Test]
        public void BoreSide_OfAFaceBore_IsItsProgramSide()
        {
            var bore = new XncBore { Surface = BoreSurface.Face };

            Assert.Multiple(() =>
            {
                Assert.That(MachiningDisplay.BoreSide(bore, FrontProgram), Is.EqualTo("Front"));
                Assert.That(MachiningDisplay.BoreSide(bore, BackProgram), Is.EqualTo("Back"));
            });
        }

        [TestCase(0, "Back")]
        [TestCase(1, "Left")]
        [TestCase(2, "Right")]
        [TestCase(3, "Top")]
        [TestCase(4, "Bottom")]
        [TestCase(7, "7")]
        public void GrooveSide_MapsThePCode(int sideCode, string expected) =>
            Assert.That(MachiningDisplay.GrooveSide(new XncGrooving { SideCode = sideCode }, BackProgram), Is.EqualTo(expected));

        [TestCase(ToolPosition.Center, "C")]
        [TestCase(ToolPosition.Left, "L")]
        [TestCase(ToolPosition.Right, "R")]
        [TestCase(ToolPosition.Pocket, "P")]
        public void ToolPositionCode_IsOneLetter(ToolPosition position, string expected) =>
            Assert.That(MachiningDisplay.ToolPositionCode(position), Is.EqualTo(expected));
    }
}
