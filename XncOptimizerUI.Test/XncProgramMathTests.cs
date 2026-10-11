using System.Xml.Linq;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class XncProgramMathTests
    {
        private static XElement Program() => XElement.Parse("""
            <program dx="800" dy="400" dz="18">
              <var name="half" type="double" expr="dz/2" />
              <var name="label" type="string" expr="abc" />
              <var name="twice" type="int" expr="half*2" />
              <tool name="Bore8" d="8" />
              <tool name="Mill6" d="6.0" />
              <tool name="bore8" d="8.5" />
              <tool name="Broken" d="x" />
              <bf x="half" y="10" dp="twice" name="Bore8" />
            </program>
            """);

        [TestCase(1.23456, "1.2346")]
        [TestCase(2.5, "2.5")]
        [TestCase(10, "10")]
        [TestCase(-0.00001, "0")]
        [TestCase(0.00004, "0")]
        [TestCase(-3.00006, "-3.0001")]
        public void FormatNumber_RoundsToFourDecimalsInvariant(double value, string expected)
        {
            Assert.That(XncProgramMath.FormatNumber(value), Is.EqualTo(expected));
        }

        [Test]
        public void SetNumber_KeepsAuthoredTextWhenValueIsUnchanged()
        {
            var element = new XElement("bf", new XAttribute("x", "dx-10"));

            XncProgramMath.SetNumber(element, "x", 790.00001, 790);

            Assert.That((string?)element.Attribute("x"), Is.EqualTo("dx-10"));
        }

        [Test]
        public void SetNumber_WritesChangedValueAsNumber()
        {
            var element = new XElement("bf", new XAttribute("x", "dx-10"));

            XncProgramMath.SetNumber(element, "x", 785.123456, 790);

            Assert.That((string?)element.Attribute("x"), Is.EqualTo("785.1235"));
        }

        [Test]
        public void SeedProgramSymbols_RegistersDimensionsAndNumericVarsInOrder()
        {
            var symbols = XncProgramMath.SeedProgramSymbols(Program());

            Assert.Multiple(() =>
            {
                Assert.That(symbols.TryGet("dx", out var dx) ? dx : double.NaN, Is.EqualTo(800));
                Assert.That(symbols.TryGet("DZ", out var dz) ? dz : double.NaN, Is.EqualTo(18));
                Assert.That(symbols.TryGet("half", out var half) ? half : double.NaN, Is.EqualTo(9));
                Assert.That(symbols.TryGet("twice", out var twice) ? twice : double.NaN, Is.EqualTo(18));
                Assert.That(symbols.TryGet("label", out _), Is.False, "string vars are not numeric symbols");
            });
        }

        [Test]
        public void SeedProgramSymbols_WithStopElement_RegistersOnlyEarlierVars()
        {
            var program = Program();
            var twice = program.Elements("var").Last();

            var symbols = XncProgramMath.SeedProgramSymbols(program, before: twice);

            Assert.Multiple(() =>
            {
                Assert.That(symbols.TryGet("half", out _), Is.True);
                Assert.That(symbols.TryGet("twice", out _), Is.False);
            });
        }

        [Test]
        public void SeedProgramSymbols_WithoutNumericDimension_Throws()
        {
            var program = XElement.Parse("""<program dx="800" dy="?" dz="18" />""");

            Assert.That(() => XncProgramMath.SeedProgramSymbols(program),
                Throws.Exception.With.Message.EqualTo("<program> @dy is missing or not a number (was '?')."));
        }

        [Test]
        public void ReadToolDiameters_IsCaseInsensitiveLastWinsAndSkipsNonNumeric()
        {
            var diameters = XncProgramMath.ReadToolDiameters(Program());

            Assert.Multiple(() =>
            {
                Assert.That(diameters, Has.Count.EqualTo(2));
                Assert.That(diameters["BORE8"], Is.EqualTo(8.5));
                Assert.That(diameters["Mill6"], Is.EqualTo(6));
            });
        }

        [TestCase("dx-half", "half", "depth", "dx-depth")]
        [TestCase("HALF*2+half", "half", "h", "h*2+h")]
        [TestCase("halfway+half.x+tool.half", "half", "h", "halfway+half.x+tool.half")]
        [TestCase("a+b", "a", "cost$1", "cost$1+b")]
        public void RenameIdentifier_ReplacesWholeIdentifiersOnly(string expression, string oldName, string newName, string expected)
        {
            Assert.That(XncProgramMath.RenameIdentifier(expression, oldName, newName), Is.EqualTo(expected));
        }

        [TestCase("dx-half", "HALF", true)]
        [TestCase("halfway", "half", false)]
        [TestCase("tool.dia/2", "dia", false)]
        [TestCase("tool.dia/2", "tool.dia", true)]
        public void ReferencesIdentifier_MatchesWholeIdentifiers(string expression, string name, bool expected)
        {
            Assert.That(XncProgramMath.ReferencesIdentifier(expression, name), Is.EqualTo(expected));
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
            Assert.That(XncProgramMath.ParsePositionCode(raw), Is.EqualTo(expected));
        }

        [TestCase("true", 1)]
        [TestCase("True", 1)]
        [TestCase("false", -1)]
        [TestCase(null, -1)]
        [TestCase("1", -1)]
        public void ParseDirSign_OnlyTrueIsClockwise(string? raw, int expected)
        {
            Assert.That(XncProgramMath.ParseDirSign(raw), Is.EqualTo(expected));
        }

        [TestCase(1, 5, 8.660254)]
        [TestCase(-1, 5, -8.660254)]
        public void TryReconstructArcCentre_PlacesCentreOnDirSide(int dirSign, double expectedX, double expectedY)
        {
            var found = XncProgramMath.TryReconstructArcCentre(0, 0, 10, 0, 10, dirSign, out var cx, out var cy);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(cx, Is.EqualTo(expectedX).Within(1e-6));
                Assert.That(cy, Is.EqualTo(expectedY).Within(1e-6));
            });
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void TryReconstructArcCentre_HalfCircleCentreIsChordMidpoint(int dirSign)
        {
            var found = XncProgramMath.TryReconstructArcCentre(0, 0, 10, 0, 5, dirSign, out var cx, out var cy);

            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(cx, Is.EqualTo(5).Within(1e-9));
                Assert.That(cy, Is.EqualTo(0).Within(1e-9));
            });
        }

        [TestCase(0, 0, 10, 0, 4.9, Description = "chord longer than the diameter")]
        [TestCase(3, 3, 3, 3, 5, Description = "zero chord")]
        public void TryReconstructArcCentre_Impossible_ReturnsFalse(double sx, double sy, double ex, double ey, double radius)
        {
            Assert.That(XncProgramMath.TryReconstructArcCentre(sx, sy, ex, ey, radius, 1, out _, out _), Is.False);
        }
    }
}
