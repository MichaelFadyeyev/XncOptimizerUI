using System.Xml.Linq;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class XncAttributeParsingTests
    {
        [Test]
        public void ReadToolDiameters_IsCaseInsensitiveLastWinsAndSkipsNonNumeric()
        {
            var program = XElement.Parse("""
                <program dx="800" dy="400" dz="18">
                  <tool name="Bore8" d="8" />
                  <tool name="Mill6" d="6.0" />
                  <tool name="bore8" d="8.5" />
                  <tool name="Broken" d="x" />
                </program>
                """);

            var diameters = XncAttributeParsing.ReadToolDiameters(program);

            Assert.Multiple(() =>
            {
                Assert.That(diameters, Has.Count.EqualTo(2));
                Assert.That(diameters["BORE8"], Is.EqualTo(8.5));
                Assert.That(diameters["Mill6"], Is.EqualTo(6));
            });
        }

        [TestCase("0", ToolPosition.Center)]
        [TestCase("1", ToolPosition.Right)]
        [TestCase("2", ToolPosition.Left)]
        [TestCase("3", ToolPosition.Pocket)]
        [TestCase("7", ToolPosition.Center)]
        [TestCase("x", ToolPosition.Center)]
        [TestCase(null, ToolPosition.Center)]
        public void ParsePositionCode_UnknownFallsBackToCenter(string? raw, ToolPosition expected)
        {
            Assert.That(XncAttributeParsing.ParsePositionCode(raw), Is.EqualTo(expected));
        }

        [TestCase("true", true)]
        [TestCase("True", true)]
        [TestCase("false", false)]
        [TestCase(null, false)]
        [TestCase("1", false)]
        public void IsClockwise_OnlyTrueIsClockwise(string? dir, bool expected)
        {
            var arc = new XElement("mac");
            arc.SetAttributeValue("dir", dir);

            Assert.That(XncAttributeParsing.IsClockwise(arc), Is.EqualTo(expected));
        }

        [TestCase(null, 1)]
        [TestCase("3", 3)]
        [TestCase("0", null)]
        [TestCase("-2", null)]
        [TestCase("two", null)]
        public void ArrayCount_AbsentIsOne_InvalidIsNull(string? ac, int? expected)
        {
            var bore = new XElement("bf");
            bore.SetAttributeValue("ac", ac);

            Assert.That(XncAttributeParsing.ArrayCount(bore), Is.EqualTo(expected));
        }

        [Test]
        public void RequireDouble_ParsesInvariantOrNamesTheValue()
        {
            Assert.Multiple(() =>
            {
                Assert.That(XncAttributeParsing.RequireDouble("1.5e1", "<tool> @d"), Is.EqualTo(15));
                Assert.That(() => XncAttributeParsing.RequireDouble("1,5", "<tool> @d"),
                    Throws.TypeOf<XncProgramFormatException>().With.Message.EqualTo("<tool> @d is missing or not a number (was '1,5')."));
            });
        }
    }
}
