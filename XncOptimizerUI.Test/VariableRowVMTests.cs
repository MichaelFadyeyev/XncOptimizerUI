using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class VariableRowVMTests
    {
        private readonly List<(XncVariableAttribute Attribute, string Value)> _edits = [];

        [SetUp]
        public void SetUp() => _edits.Clear();

        /// <summary>dz=18; vars: throughBoreDepth double dz+2 (20), half double throughBoreDepth/2 (10), label string.</summary>
        private static XncProgram Program() => new()
        {
            Dx = 600,
            Dy = 300,
            Dz = 18,
            DeclaredVariables =
            [
                new XncVariable { Index = 0, Name = "throughBoreDepth", Type = XncVariableType.Double, Expr = "dz+2", Comment = "Глибина", Value = 20 },
                new XncVariable { Index = 1, Name = "half", Type = XncVariableType.Double, Expr = "throughBoreDepth/2", Value = 10 },
                new XncVariable { Index = 2, Name = "label", Type = XncVariableType.String, Expr = "some text" }
            ]
        };

        private VariableRowVM Row(int index, bool saveResult = true)
        {
            var program = Program();

            return new VariableRowVM(index + 1, "Front", program.DeclaredVariables[index], program, (_, attribute, value) =>
            {
                _edits.Add((attribute, value));
                return saveResult;
            });
        }

        [Test]
        public void Displays_DeclarationAndValue()
        {
            var numeric = Row(1);
            var text = Row(2);

            Assert.Multiple(() =>
            {
                Assert.That((numeric.Name, numeric.Type, numeric.Expr, numeric.Value), Is.EqualTo(("half", "double", "throughBoreDepth/2", "10")));
                Assert.That((text.Type, text.Value), Is.EqualTo(("string", "some text")));
                Assert.That(VariableRowVM.TypeOptions, Is.EqualTo(new[] { "int", "double", "string", "bool" }));
            });
        }

        [TestCase("throughBoreDepth*3", true)]
        [TestCase("label+1", false)]     // string vars are no symbols
        [TestCase("half+1", false)]      // a var cannot reference itself
        [TestCase("2*-3", false)]
        public void ExprDraft_CheckedAgainstEarlierNumericVars(string expr, bool valid)
        {
            var row = Row(1);

            row.BeginCellEdit();
            row.ExprInput = expr;

            Assert.That(row.HasErrors, Is.EqualTo(!valid));
        }

        [Test]
        public void ExprDraft_Committed_SavedTrimmed()
        {
            var row = Row(1);

            row.BeginCellEdit();
            row.ExprInput = " throughBoreDepth/4 ";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.True);
                Assert.That(_edits, Is.EqualTo(new[] { (XncVariableAttribute.Expr, "throughBoreDepth/4") }));
            });
        }

        [Test]
        public void StringExpr_SavedAsTyped()
        {
            var row = Row(2);

            row.BeginCellEdit();
            row.ExprInput = " padded ";
            row.TryCommitCellEdit();

            Assert.That(_edits, Is.EqualTo(new[] { (XncVariableAttribute.Expr, " padded ") }));
        }

        [TestCase("half", "already exists")]
        [TestCase("dy", "built-in name")]
        [TestCase("_x", "must start with a letter")]
        public void NameDraft_InvalidIsRedAndRefusesCommit(string name, string reason)
        {
            var row = Row(0);

            row.BeginCellEdit();
            row.NameInput = name;

            Assert.Multiple(() =>
            {
                Assert.That(row.GetErrors(nameof(VariableRowVM.NameInput)).Cast<string>().Single(), Does.Contain(reason));
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(_edits, Is.Empty);
            });
        }

        [Test]
        public void NameDraft_ValidWithDotAndDigits_Saved()
        {
            var row = Row(0);

            row.BeginCellEdit();
            row.NameInput = "hole.depth_2";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.True);
                Assert.That(_edits, Is.EqualTo(new[] { (XncVariableAttribute.Name, "hole.depth_2") }));
            });
        }

        [TestCase("int", false)]     // 10 is whole -> fits
        [TestCase("bool", true)]     // "throughBoreDepth/2" is no bool
        [TestCase("string", false)]
        public void TypeDraft_CheckedAgainstExpr(string type, bool hasError)
        {
            var row = Row(1);

            row.BeginCellEdit();
            row.TypeInput = type;

            Assert.That(row.HasErrors, Is.EqualTo(hasError));
        }

        [Test]
        public void Comment_Cleared_SavedEmpty()
        {
            var row = Row(0);

            row.BeginCellEdit();
            row.CommentInput = string.Empty;
            row.TryCommitCellEdit();

            Assert.That(_edits, Is.EqualTo(new[] { (XncVariableAttribute.Comment, string.Empty) }));
        }

        [Test]
        public void Cancel_RestoresDraftsAndClearsErrors()
        {
            var row = Row(0);

            row.BeginCellEdit();
            row.NameInput = "half";
            row.CancelCellEdit();

            Assert.Multiple(() =>
            {
                Assert.That(row.NameInput, Is.EqualTo("throughBoreDepth"));
                Assert.That(row.HasErrors, Is.False);
            });
        }

        [Test]
        public void SaveFailure_KeepsEditWithError()
        {
            var row = Row(0, saveResult: false);

            row.BeginCellEdit();
            row.ExprInput = "dz+4";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(row.GetErrors(nameof(VariableRowVM.ExprInput)).Cast<string>().Single(), Does.Contain("Saving failed"));
            });
        }
    }
}
