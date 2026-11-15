using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Machining;

namespace XncOptimizerUI.Test
{
    [TestFixture]
    public class BoreRowVMTests
    {
        private readonly List<(MachiningAttribute Attribute, string Text)> _edits = [];

        [SetUp]
        public void SetUp() => _edits.Clear();

        private static XncProgram Program(params XncBore[] bores) => new()
        {
            Dx = 600,
            Dy = 300,
            Dz = 18,
            Bores = bores,
            Tools = [new XncTool { Name = "Bore8", Diameter = 8 }],
            Variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["throughBoreDepth"] = 20 }
        };

        private static XncBore FaceBore(string depthText, double depth) => new()
        {
            Surface = BoreSurface.Face,
            ToolName = "Bore8",
            X = 300,
            XText = "DX/2",
            Y = 150,
            YText = "DY/2",
            Depth = depth,
            DepthText = depthText
        };

        private BoreRowVM Row(XncBore bore) =>
            new(1, "Front", "68", bore, Program(bore), (_, _) => { }, (_, attribute, text) =>
            {
                _edits.Add((attribute, text));
                return true;
            });

        [TestCase("throughBoreDepth", 20)]
        // Authored with a construct the editor itself rejects (mid-expression sign).
        [TestCase("dz*-1+40", 22)]
        public void UnchangedAuthoredDepth_DoesNotBlockEditingX(string depthText, double depth)
        {
            var row = Row(FaceBore(depthText, depth));

            row.BeginCellEdit();
            row.X.Input = "dx/2-10";
            var committed = row.TryCommitCellEdit();

            Assert.Multiple(() =>
            {
                Assert.That(row.HasErrors, Is.False);
                Assert.That(committed, Is.True);
                Assert.That(_edits, Is.EqualTo(new[] { (MachiningAttribute.X, "dx/2-10") }));
            });
        }

        [Test]
        public void ToolTips_ShowExpressionAndExactValue_OnlyForExpressionsAndVariables()
        {
            var bore = new XncBore
            {
                Surface = BoreSurface.Face,
                X = 100d / 3,
                XText = "dx/18",
                Y = 150,
                YText = "150",
                Depth = 20,
                DepthText = "throughBoreDepth"
            };
            var row = Row(bore);

            Assert.Multiple(() =>
            {
                Assert.That(row.X.Display, Is.EqualTo("33.333"));
                Assert.That(row.X.ToolTip, Is.EqualTo("dx/18 = 33.333333333333336"));
                Assert.That(row.Y.ToolTip, Is.Null);
                Assert.That(row.Depth.ToolTip, Is.EqualTo("throughBoreDepth = 20"));
            });
        }

        [Test]
        public void ToolTips_FollowAppliedBore()
        {
            var row = Row(FaceBore("10", 10));
            var applied = FaceBore("dz-2", 16);
            var changed = new List<string?>();
            row.Depth.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            row.Apply(applied, Program(applied));

            Assert.Multiple(() =>
            {
                Assert.That(row.Model, Is.SameAs(applied));
                Assert.That(row.Depth.ToolTip, Is.EqualTo("dz-2 = 16"));
                Assert.That(row.Depth.Input, Is.EqualTo("dz-2"));
                Assert.That(changed, Does.Contain(nameof(ExpressionCellVM.ToolTip)));
            });
        }

        [TestCase(BoreSurface.Face, MachiningAttribute.X, MachiningAttribute.Y)]
        [TestCase(BoreSurface.Top, MachiningAttribute.X, MachiningAttribute.Z)]
        [TestCase(BoreSurface.Bottom, MachiningAttribute.X, MachiningAttribute.Z)]
        [TestCase(BoreSurface.Left, MachiningAttribute.Z, MachiningAttribute.Y)]
        [TestCase(BoreSurface.Right, MachiningAttribute.Z, MachiningAttribute.Y)]
        public void Columns_EditTheBoresPositionInThePartPlane(BoreSurface surface, MachiningAttribute x, MachiningAttribute y)
        {
            var row = Row(new XncBore { Surface = surface, Depth = 10 });

            Assert.Multiple(() =>
            {
                Assert.That(row.X.Attribute, Is.EqualTo(x));
                Assert.That(row.Y.Attribute, Is.EqualTo(y));
                Assert.That(row.Depth.Attribute, Is.EqualTo(MachiningAttribute.Depth));
            });
        }

        [Test]
        public void DepthInput_AcceptsProgramVariableAndToolDiameter()
        {
            var row = Row(FaceBore("10", 10));

            row.BeginCellEdit();
            row.Depth.Input = "throughBoreDepth-tool.dia";

            Assert.Multiple(() =>
            {
                Assert.That(row.HasErrors, Is.False);
                Assert.That(row.TryCommitCellEdit(), Is.True);
                Assert.That(_edits, Is.EqualTo(new[] { (MachiningAttribute.Depth, "throughBoreDepth-tool.dia") }));
            });
        }

        [Test]
        public void DepthInput_RejectsUndeclaredVariable()
        {
            var row = Row(FaceBore("10", 10));

            row.BeginCellEdit();
            row.Depth.Input = "blindBoreDepth";

            Assert.Multiple(() =>
            {
                Assert.That(row.HasErrors, Is.True);
                Assert.That(row.Depth.GetErrors(nameof(ExpressionCellVM.Input)).Cast<string>().Single(),
                    Does.Contain("Unknown variable 'blindBoreDepth'"));
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(_edits, Is.Empty);
            });
        }
    }
}
