using System.Xml.Linq;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class XncNumberFormatTests
    {
        [TestCase(1.23456, "1.2346")]
        [TestCase(2.5, "2.5")]
        [TestCase(10, "10")]
        [TestCase(-0.00001, "0")]
        [TestCase(0.00004, "0")]
        [TestCase(-3.00006, "-3.0001")]
        public void FormatNumber_RoundsToFourDecimalsInvariant(double value, string expected)
        {
            Assert.That(XncNumberFormat.FormatNumber(value), Is.EqualTo(expected));
        }

        [Test]
        public void SetNumber_KeepsAuthoredTextWhenValueIsUnchanged()
        {
            var element = new XElement("bf", new XAttribute("x", "dx-10"));

            XncNumberFormat.SetNumber(element, "x", 790.00001, 790);

            Assert.That((string?)element.Attribute("x"), Is.EqualTo("dx-10"));
        }

        [Test]
        public void SetNumber_WritesChangedValueAsNumber()
        {
            var element = new XElement("bf", new XAttribute("x", "dx-10"));

            XncNumberFormat.SetNumber(element, "x", 785.123456, 790);

            Assert.That((string?)element.Attribute("x"), Is.EqualTo("785.1235"));
        }
    }
}
