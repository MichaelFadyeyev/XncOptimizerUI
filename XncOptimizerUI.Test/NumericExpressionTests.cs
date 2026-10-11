using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class NumericExpressionTests
    {
        /// <summary>Program symbols: dx/dy/dz plus one declared &lt;var&gt;.</summary>
        private static XncSymbolTable Symbols()
        {
            var symbols = new XncSymbolTable();
            symbols.Set("dx", 800);
            symbols.Set("dy", 400);
            symbols.Set("dz", 18);
            symbols.Set("half", 9);

            return symbols;
        }

        private static (bool Valid, double Value, string? Error) Evaluate(string? text)
        {
            var valid = NumericExpression.TryEvaluate(text, Symbols(), out var value, out var error);

            return (valid, value, error);
        }

        [TestCase("0", 0)]
        [TestCase("42", 42)]
        [TestCase("3.25", 3.25)]
        [TestCase("-dz", -18)]
        [TestCase("(-dz)", -18)]
        [TestCase("dx/2 - dy/4", 300)]
        [TestCase("Dx*2", 1600)]
        [TestCase("half*2", 18)]
        [TestCase("((dz))", 18)]
        public void Valid_Evaluates(string text, double expected)
        {
            var (valid, value, error) = Evaluate(text);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.True, error);
                Assert.That(value, Is.EqualTo(expected).Within(1e-9));
                Assert.That(error, Is.Null);
            });
        }

        [TestCase(null, "Value is required")]
        [TestCase("", "Value is required")]
        [TestCase("   ", "Value is required")]
        [TestCase("1,5", "Invalid character ','")]
        [TestCase("dx^2", "Invalid character '^'")]
        [TestCase("1.2.3", "'1.2.3' has more than one decimal separator")]
        [TestCase("dw", "Unknown variable 'dw' (use dx, dy, dz or a program variable)")]
        [TestCase("tool.dia", "Unknown variable 'tool.dia' (use dx, dy, dz or a program variable)")]
        [TestCase("2*-3", "'-' sign is allowed only at the beginning")]
        [TestCase("dx--5", "'-' sign is allowed only at the beginning")]
        [TestCase("dx+", "Incomplete or malformed expression")]
        [TestCase("(dx", "Incomplete or malformed expression")]
        [TestCase("dx dy", "Incomplete or malformed expression")]
        [TestCase("1/0", "Value is not a finite number")]
        public void Invalid_ReportsFirstProblem(string? text, string expectedError)
        {
            var (valid, _, error) = Evaluate(text);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(error, Is.EqualTo(expectedError));
            });
        }
    }
}
