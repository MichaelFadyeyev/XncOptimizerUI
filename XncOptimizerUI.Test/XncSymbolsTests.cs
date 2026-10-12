using System.Xml.Linq;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class XncSymbolsTests
    {
        private static XElement Program() => XElement.Parse("""
            <program dx="800" dy="400" dz="18">
              <var name="half" type="double" expr="dz/2" />
              <var name="label" type="string" expr="abc" />
              <var name="twice" type="int" expr="half*2" />
              <tool name="Bore8" d="8" />
              <bf x="half" y="10" dp="twice" name="Bore8" />
            </program>
            """);

        [Test]
        public void SeedProgramSymbols_RegistersDimensionsAndNumericVarsInOrder()
        {
            var symbols = XncSymbols.SeedProgramSymbols(Program());

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

            var symbols = XncSymbols.SeedProgramSymbols(program, before: twice);

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

            Assert.That(() => XncSymbols.SeedProgramSymbols(program),
                Throws.Exception.With.Message.EqualTo("<program> @dy is missing or not a number (was '?')."));
        }

        [Test]
        public void Eval_MissingValue_NamesWhereWhenGiven()
        {
            var symbols = XncSymbols.DimensionSymbols(Program());

            Assert.Multiple(() =>
            {
                Assert.That(XncSymbols.Eval("dx/2+1", symbols), Is.EqualTo(401));
                Assert.That(() => XncSymbols.Eval(null, symbols, "<ms> @x"),
                    Throws.TypeOf<XncProgramFormatException>().With.Message.EqualTo("Missing value for <ms> @x."));
                Assert.That(() => XncSymbols.Eval(null, symbols),
                    Throws.TypeOf<XncProgramFormatException>().With.Message.EqualTo("Missing XNC coordinate/value."));
            });
        }

        [TestCase("dx-half", "half", "depth", "dx-depth")]
        [TestCase("HALF*2+half", "half", "h", "h*2+h")]
        [TestCase("halfway+half.x+tool.half", "half", "h", "halfway+half.x+tool.half")]
        [TestCase("a+b", "a", "cost$1", "cost$1+b")]
        public void RenameIdentifier_ReplacesWholeIdentifiersOnly(string expression, string oldName, string newName, string expected)
        {
            Assert.That(XncSymbols.RenameIdentifier(expression, oldName, newName), Is.EqualTo(expected));
        }

        [TestCase("dx-half", "HALF", true)]
        [TestCase("halfway", "half", false)]
        [TestCase("tool.dia/2", "dia", false)]
        [TestCase("tool.dia/2", "tool.dia", true)]
        public void ReferencesIdentifier_MatchesWholeIdentifiers(string expression, string name, bool expected)
        {
            Assert.That(XncSymbols.ReferencesIdentifier(expression, name), Is.EqualTo(expected));
        }
    }
}
