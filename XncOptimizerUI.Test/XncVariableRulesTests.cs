using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class XncVariableRulesTests
    {
        private static readonly string[] OtherNames = ["depth", "Width.Half"];

        private static XncSymbolTable Symbols()
        {
            var symbols = new XncSymbolTable();
            symbols.Set("dx", 800);
            symbols.Set("dy", 400);
            symbols.Set("dz", 18);
            symbols.Set("depth", 12);

            return symbols;
        }

        [TestCase("offset")]
        [TestCase("  offset  ")]
        [TestCase("a1_b.c")]
        [TestCase("Глибина")]
        public void CheckName_Valid_ReturnsNull(string name)
        {
            Assert.That(XncVariableRules.CheckName(name, OtherNames), Is.Null);
        }

        [TestCase(null, "Name is required")]
        [TestCase("  ", "Name is required")]
        [TestCase("1abc", "Name must start with a letter, followed by letters, digits, '_' or '.'")]
        [TestCase("_abc", "Name must start with a letter, followed by letters, digits, '_' or '.'")]
        [TestCase("a b", "Name must start with a letter, followed by letters, digits, '_' or '.'")]
        [TestCase("DX", "'DX' is a built-in name")]
        [TestCase("tool.dia", "'tool.dia' is a built-in name")]
        [TestCase("DEPTH", "Variable 'DEPTH' already exists")]
        [TestCase("width.half", "Variable 'width.half' already exists")]
        public void CheckName_Invalid_ReportsProblem(string? name, string expectedError)
        {
            Assert.That(XncVariableRules.CheckName(name, OtherNames), Is.EqualTo(expectedError));
        }

        [TestCase(XncVariableType.Double, "dz/4", 4.5)]
        [TestCase(XncVariableType.Double, "depth+1", 13)]
        [TestCase(XncVariableType.Int, "dz/2", 9)]
        [TestCase(XncVariableType.Int, " 7 ", 7)]
        public void TryCheckExpr_Numeric_ReturnsValue(XncVariableType type, string expr, double expected)
        {
            var valid = XncVariableRules.TryCheckExpr(expr, type, Symbols(), out var value, out var error);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.True, error);
                Assert.That(value, Is.EqualTo(expected).Within(1e-9));
            });
        }

        [TestCase(XncVariableType.Int, "dz/4", "An int value must be whole (got 4.5)")]
        [TestCase(XncVariableType.Double, "unknown", "Unknown variable 'unknown' (use dx, dy, dz or a program variable)")]
        [TestCase(XncVariableType.Double, "", "Value is required")]
        [TestCase(XncVariableType.Bool, "yes", "A bool value must be true or false")]
        [TestCase(XncVariableType.Bool, null, "A bool value must be true or false")]
        public void TryCheckExpr_Invalid_ReportsProblem(XncVariableType type, string? expr, string expectedError)
        {
            var valid = XncVariableRules.TryCheckExpr(expr, type, Symbols(), out var value, out var error);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.False);
                Assert.That(error, Is.EqualTo(expectedError));
            });
        }

        [TestCase(XncVariableType.Bool, " TRUE ")]
        [TestCase(XncVariableType.Bool, "false")]
        [TestCase(XncVariableType.String, "")]
        [TestCase(XncVariableType.String, "anything at all")]
        public void TryCheckExpr_NonNumeric_ValidHasNoValue(XncVariableType type, string expr)
        {
            var valid = XncVariableRules.TryCheckExpr(expr, type, Symbols(), out var value, out var error);

            Assert.Multiple(() =>
            {
                Assert.That(valid, Is.True, error);
                Assert.That(value, Is.Null);
            });
        }

        [TestCase(XncVariableType.Double, " dz/2 ", "dz/2")]
        [TestCase(XncVariableType.Bool, " true ", "true")]
        [TestCase(XncVariableType.String, " keep spaces ", " keep spaces ")]
        [TestCase(XncVariableType.Int, null, "")]
        [TestCase(XncVariableType.String, null, "")]
        public void NormalizeExpr_TrimsAllButStrings(XncVariableType type, string? expr, string expected)
        {
            Assert.That(XncVariableRules.NormalizeExpr(expr, type), Is.EqualTo(expected));
        }
    }
}
