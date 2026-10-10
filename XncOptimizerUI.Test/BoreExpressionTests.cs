using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class BoreExpressionTests
    {
        private const double Dx = 1000;
        private const double Dy = 600;
        private const double Dz = 19;

        private const double ThroughBoreDepth = 21;

        /// <summary>Program symbols: dx/dy/dz plus one declared &lt;var&gt;.</summary>
        private static XncSymbolTable Symbols()
        {
            var symbols = new XncSymbolTable();
            symbols.Set("dx", Dx);
            symbols.Set("dy", Dy);
            symbols.Set("dz", Dz);
            symbols.Set("throughBoreDepth", ThroughBoreDepth);

            return symbols;
        }

        private static (bool Valid, double Value, string? Error) Evaluate(string? text, BoreAttribute attribute = BoreAttribute.X)
        {
            var valid = BoreExpression.TryEvaluate(text, attribute, Symbols(), out var value, out var error);

            return (valid, value, error);
        }

        [TestCase("12", 12)]
        [TestCase("-5", -5)]
        [TestCase("+5", 5)]
        [TestCase("12.5", 12.5)]
        [TestCase(".5", 0.5)]
        [TestCase("dx-32", 968)]
        [TestCase("DZ/2", 9.5)]
        [TestCase("(dx-10)/2", 495)]
        [TestCase("-(dy-3)", -597)]
        [TestCase("2*(-3)", -6)]
        [TestCase("  dy - 35 - 40 ", 525)]
        [TestCase("dx*0.5+dy", 1100)]
        [TestCase("throughBoreDepth", 21)]
        [TestCase("THROUGHBOREDEPTH-1", 20)]
        [TestCase("dx-throughBoreDepth", 979)]
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

        [TestCase("12,5", "Invalid character ','")]
        [TestCase("1.2.3", "more than one decimal separator")]
        [TestCase("dx--5", "sign is allowed only at the beginning")]
        [TestCase("2*-3", "sign is allowed only at the beginning")]
        [TestCase("abc", "Unknown variable 'abc'")]
        [TestCase("tool.dia", "Unknown variable 'tool.dia'")]
        [TestCase("dw+1", "Unknown variable 'dw'")]
        [TestCase("5-", "Incomplete or malformed expression")]
        [TestCase("(dx", "Incomplete or malformed expression")]
        [TestCase("1/0", "not a finite number")]
        [TestCase("", "Value is required")]
        [TestCase("   ", "Value is required")]
        [TestCase(null, "Value is required")]
        public void Invalid_ReportsReason(string? text, string reason)
        {
            var (valid, _, error) = Evaluate(text);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(error, Does.Contain(reason));
            });
        }

        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("dz-dz")]
        public void Depth_MustBePositive(string text)
        {
            var (valid, _, error) = Evaluate(text, BoreAttribute.Depth);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(error, Is.EqualTo("Depth must be greater than 0"));
            });
        }

        [Test]
        public void Coordinate_MayBeZeroOrNegative()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Evaluate("0").Valid, Is.True);
                Assert.That(Evaluate("-1", BoreAttribute.Z).Valid, Is.True);
            });
        }
    }
}
