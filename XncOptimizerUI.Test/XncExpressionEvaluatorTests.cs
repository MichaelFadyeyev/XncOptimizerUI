using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class XncExpressionEvaluatorTests
    {
        private static XncSymbolTable Symbols(params (string Name, double Value)[] entries)
        {
            var table = new XncSymbolTable();

            foreach (var (name, value) in entries)
            {
                table.Set(name, value);
            }

            return table;
        }

        [TestCase("-10", -10)]
        [TestCase("382.5", 382.5)]
        [TestCase("dy-35-40", 525)]      // 600 - 35 - 40
        [TestCase("dx+10", 1390)]        // 1380 + 10
        [TestCase("dz+2.00", 21)]        // 19 + 2
        [TestCase("2+3*4", 14)]          // precedence
        [TestCase("(2+3)*4", 20)]        // parentheses
        [TestCase("  dx / 2 ", 690)]     // whitespace
        public void Evaluates_literalsAndArithmetic(string expression, double expected)
        {
            var symbols = Symbols(("dx", 1380), ("dy", 600), ("dz", 19));

            Assert.That(XncExpressionEvaluator.Evaluate(expression, symbols), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void Resolves_dottedIdentifier_caseInsensitively()
        {
            var symbols = Symbols(("tool.dia", 10));

            Assert.That(XncExpressionEvaluator.Evaluate("TOOL.DIA/2", symbols), Is.EqualTo(5));
        }

        [Test]
        public void Throws_onUnknownIdentifier()
        {
            Assert.Throws<XncProgramFormatException>(
                () => XncExpressionEvaluator.Evaluate("missing + 1", new XncSymbolTable()));
        }

        [Test]
        public void Throws_onTrailingGarbage()
        {
            Assert.Throws<XncProgramFormatException>(
                () => XncExpressionEvaluator.Evaluate("1 2", new XncSymbolTable()));
        }

        [Test]
        public void Throws_onEmptyExpression()
        {
            Assert.Throws<XncProgramFormatException>(
                () => XncExpressionEvaluator.Evaluate("   ", new XncSymbolTable()));
        }
    }
}
