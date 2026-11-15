using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Machining;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// The groove and mill rows: values shown with at most 3 decimals like the bores table, their
    /// editable cells mapped to the element's attributes and validated like the bore cells.
    /// </summary>
    [TestFixture]
    public class MachiningRowVMsTests
    {
        private const double LongValue = 12.34567;
        private const double SumWithRoundingNoise = 0.1 + 0.2;

        private readonly List<(MachiningAttribute Attribute, string Text)> _edits = [];

        [SetUp]
        public void SetUp() => _edits.Clear();

        private static XncProgram Program() => new()
        {
            OperationId = 7,
            Dx = 600,
            Dy = 300,
            Dz = 18,
            Tools = [new XncTool { Name = "Mill10", Diameter = 10 }],
            Variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["pocketDepth"] = 8 }
        };

        private static XncElementSource Source(params (string Name, string Text)[] texts) =>
            new(0, texts.ToDictionary(t => t.Name, t => t.Text));

        private bool Record<TModel>(MachiningElementRowVM<TModel> row, MachiningAttribute attribute, string text)
            where TModel : IXncMachiningElement
        {
            _edits.Add((attribute, text));
            return true;
        }

        [Test]
        public void ContourRow_FormatsValuesToThreeDecimals()
        {
            var contour = new XncMillingContour
            {
                Entry = new XncPoint(LongValue, SumWithRoundingNoise),
                EntryDepth = LongValue
            };

            var row = new MillingContourRowVM(1, "Front", contour, Program(), (_, _) => { }, Record);

            Assert.Multiple(() =>
            {
                Assert.That(row.EntryX, Is.EqualTo("12.346"));
                Assert.That(row.EntryY, Is.EqualTo("0.3"));
                Assert.That(row.Depth.Display, Is.EqualTo("12.346"));
                Assert.That(row.Cells.Select(cell => cell.Attribute), Is.EqualTo(new[] { MachiningAttribute.Depth }));
                Assert.That(row.OperationId, Is.EqualTo(7));
            });
        }

        [Test]
        public void EllipseRow_FormatsValuesToThreeDecimals()
        {
            var ellipse = new XncMillingEllipse { Center = new XncPoint(LongValue, 1), Angle = SumWithRoundingNoise, Length = 5, Width = 5, Depth = 5 };

            var row = new MillingEllipseRowVM(1, "Front", ellipse, Program(), (_, _) => { }, Record);

            Assert.Multiple(() =>
            {
                Assert.That(row.CenterX.Display, Is.EqualTo("12.346"));
                Assert.That(row.Angle.Display, Is.EqualTo("0.3"));
            });
        }

        [Test]
        public void RectangleRow_FormatsValuesToThreeDecimals()
        {
            var rectangle = new XncMillingRectangle { Length = LongValue, CornerRadius = SumWithRoundingNoise };

            var row = new MillingRectangleRowVM(1, "Front", rectangle, Program(), (_, _) => { }, Record);

            Assert.Multiple(() =>
            {
                Assert.That(row.Length.Display, Is.EqualTo("12.346"));
                Assert.That(row.CornerRadius.Display, Is.EqualTo("0.3"));
            });
        }

        [Test]
        public void ToolRow_FormatsDiameterToThreeDecimals()
        {
            var row = new ToolRowVM(1, "Front", new XncTool { Name = "T1", Diameter = SumWithRoundingNoise });

            Assert.That(row.Diameter, Is.EqualTo("0.3"));
        }

        [Test]
        public void GrooveRow_EditorsStartFromAuthoredText_AndCommitTheirAttribute()
        {
            var groove = new XncGrooving
            {
                ToolName = "Mill10",
                Start = new XncPoint(-10, 565),
                End = new XncPoint(610, 565),
                Depth = 4,
                Width = 10,
                Source = Source(("x1", "-10"), ("y1", "565"), ("x2", "dx+10"), ("y2", "565"), ("dp", "4"), ("t", "10"))
            };
            var row = new GrooveRowVM(1, "Front", groove, Program(), (_, _) => { }, Record);

            row.BeginCellEdit();
            row.EndY.Input = "dy-35";
            var committed = row.TryCommitCellEdit();

            Assert.Multiple(() =>
            {
                Assert.That(row.EndX.Input, Is.EqualTo("dx+10"));
                Assert.That(row.EndX.ToolTip, Is.EqualTo("dx+10 = 610"));
                Assert.That(committed, Is.True);
                Assert.That(_edits, Is.EqualTo(new[] { (MachiningAttribute.Y2, "dy-35") }));
            });
        }

        [TestCase("0", "Width must be greater than 0")]
        [TestCase("-tool.dia", "Width must be greater than 0")]
        [TestCase("blindWidth", "Unknown variable 'blindWidth'")]
        public void GrooveRow_InvalidWidth_IsRefused(string text, string error)
        {
            var groove = new XncGrooving { ToolName = "Mill10", Depth = 4, Width = 10 };
            var row = new GrooveRowVM(1, "Front", groove, Program(), (_, _) => { }, Record);

            row.BeginCellEdit();
            row.Width.Input = text;

            Assert.Multiple(() =>
            {
                Assert.That(row.Width.GetErrors(nameof(ExpressionCellVM.Input)).Cast<string>().Single(), Does.Contain(error));
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(_edits, Is.Empty);
            });
        }

        [Test]
        public void RectangleRow_AcceptsZeroCornerRadiusAndToolDiameter_RejectsNegativeRadius()
        {
            var rectangle = new XncMillingRectangle { ToolName = "Mill10", Length = 100, Width = 20, CornerRadius = 5, Depth = 8 };
            var row = new MillingRectangleRowVM(1, "Front", rectangle, Program(), (_, _) => { }, Record);

            row.BeginCellEdit();
            row.CornerRadius.Input = "-1";
            var negativeError = row.CornerRadius.GetErrors(nameof(ExpressionCellVM.Input)).Cast<string>().SingleOrDefault();
            row.CornerRadius.Input = "tool.dia/2-5";
            var committed = row.TryCommitCellEdit();

            Assert.Multiple(() =>
            {
                Assert.That(negativeError, Is.EqualTo("Corner radius must not be negative"));
                Assert.That(committed, Is.True);
                Assert.That(_edits, Is.EqualTo(new[] { (MachiningAttribute.CornerRadius, "tool.dia/2-5") }));
            });
        }

        [Test]
        public void ContourRow_ApplyRefreshesReadOnlyColumnsAndCells()
        {
            var row = new MillingContourRowVM(1, "Front", new XncMillingContour { EntryDepth = 5 }, Program(), (_, _) => { }, Record);
            var changed = new List<string?>();
            row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            var reread = new XncMillingContour
            {
                Entry = new XncPoint(3, 4),
                EntryDepth = 8,
                Source = Source(("dp", "pocketDepth"))
            };

            row.Apply(reread, Program());

            Assert.Multiple(() =>
            {
                Assert.That(row.EntryX, Is.EqualTo("3"));
                Assert.That(row.Depth.Input, Is.EqualTo("pocketDepth"));
                Assert.That(row.Depth.ToolTip, Is.EqualTo("pocketDepth = 8"));
                Assert.That(changed, Does.Contain(string.Empty));
            });
        }

        [Test]
        public void FailedSave_MarksTheCellAndKeepsTheDraft()
        {
            var ellipse = new XncMillingEllipse { Length = 20, Width = 20, Depth = 5 };
            var row = new MillingEllipseRowVM(1, "Front", ellipse, Program(), (_, _) => { }, (_, _, _) => false);

            row.BeginCellEdit();
            row.Angle.Input = "45";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(row.Angle.Input, Is.EqualTo("45"));
                Assert.That(row.Angle.GetErrors(nameof(ExpressionCellVM.Input)).Cast<string>().Single(), Does.Contain("Saving failed"));
            });
        }
    }
}
