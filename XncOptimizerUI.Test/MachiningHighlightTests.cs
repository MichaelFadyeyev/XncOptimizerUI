using NSubstitute;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.ViewModels.Preview;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// The selection shared by the program tables and the part preview:
    /// <see cref="SelectedPartMachiningVM.Highlight"/> / <see cref="SelectedPartMachiningVM.ClearHighlight"/>,
    /// as forwarded by <see cref="PartPreviewVM"/>.
    /// </summary>
    [TestFixture]
    public class MachiningHighlightTests : AppViewModelTestBase
    {
        [Test]
        public void Highlight_Replace_SelectsOnlyTheElementAcrossEveryTable()
        {
            var vm = OpenWithBoresAndGrooves();
            var machining = vm.Machining;
            machining.Bores[0].IsHighlighted = true;
            machining.Grooves[0].IsHighlighted = true;
            var changes = CountHighlightChanges(vm);

            vm.Preview.HighlightElement(machining.Bores[1].Model, HighlightMode.Replace);

            Assert.Multiple(() =>
            {
                Assert.That(Highlighted(vm), Is.EqualTo(new[] { false, true, false, false }));
                Assert.That(changes(), Is.EqualTo(1));
            });
        }

        [Test]
        public void Highlight_Toggle_AddsOrRemovesOnlyTheElement()
        {
            var vm = OpenWithBoresAndGrooves();
            var machining = vm.Machining;
            machining.Highlight(machining.Bores[0].Model, HighlightMode.Replace);

            machining.Highlight(machining.Grooves[1].Model, HighlightMode.Toggle);
            var added = Highlighted(vm);
            machining.Highlight(machining.Bores[0].Model, HighlightMode.Toggle);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.EqualTo(new[] { true, false, false, true }));
                Assert.That(Highlighted(vm), Is.EqualTo(new[] { false, false, false, true }));
            });
        }

        [Test]
        public void Highlight_Add_NeverRemoves()
        {
            var vm = OpenWithBoresAndGrooves();
            var machining = vm.Machining;
            machining.Highlight(machining.Bores[0].Model, HighlightMode.Replace);

            machining.Highlight(machining.Grooves[0].Model, HighlightMode.Add);
            machining.Highlight(machining.Grooves[0].Model, HighlightMode.Add);

            Assert.That(Highlighted(vm), Is.EqualTo(new[] { true, false, true, false }));
        }

        [Test]
        public void Highlight_OfAnElementNoRowShows_ChangesNothing()
        {
            var vm = OpenWithBoresAndGrooves();
            vm.Machining.Bores[0].IsHighlighted = true;

            vm.Machining.Highlight(new XncBore(), HighlightMode.Replace);

            Assert.That(Highlighted(vm), Is.EqualTo(new[] { true, false, false, false }));
        }

        [Test]
        public void ClearHighlight_DeselectsEveryRowWithOneNotification()
        {
            var vm = OpenWithBoresAndGrooves();
            vm.Machining.Bores[1].IsHighlighted = true;
            vm.Machining.Grooves[0].IsHighlighted = true;
            var changes = CountHighlightChanges(vm);

            vm.Preview.ClearHighlight();

            Assert.Multiple(() =>
            {
                Assert.That(Highlighted(vm), Is.All.False);
                Assert.That(changes(), Is.EqualTo(1));
            });
        }

        [Test]
        public void IsElementHighlighted_MatchesTheProgramElementTheRowShows()
        {
            var vm = OpenWithBoresAndGrooves();
            var program = vm.Preview.Programs.Single();

            vm.Machining.Grooves[1].IsHighlighted = true;

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.IsElementHighlighted(program.Groovings[1]), Is.True);
                Assert.That(vm.Preview.IsElementHighlighted(program.Groovings[0]), Is.False);
                Assert.That(vm.Preview.IsElementHighlighted(program.Bores[1]), Is.False);
            });
        }

        [Test]
        public void TableSelection_NotifiesThePreview()
        {
            var vm = OpenWithBoresAndGrooves();
            var changes = CountHighlightChanges(vm);

            vm.Machining.Bores[0].IsHighlighted = true;
            vm.Machining.Bores[0].IsHighlighted = false;

            Assert.That(changes(), Is.EqualTo(2));
        }

        [Test]
        public void EditingABore_KeepsItHighlightedOnTheReReadElement()
        {
            var vm = OpenWithBoresAndGrooves();
            Project.Editor.UpdateBore(default, default, default, default!).ReturnsForAnyArgs(Project.ChangingProgramsTo([Program(firstBoreX: 120)]));
            var row = vm.Machining.Bores[0];
            row.IsHighlighted = true;
            var changes = CountHighlightChanges(vm);

            row.BeginCellEdit();
            row.X.Input = "120";
            row.TryCommitCellEdit();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Machining.Bores[0], Is.SameAs(row));
                Assert.That(row.IsHighlighted, Is.True);
                Assert.That(vm.Preview.IsElementHighlighted(vm.Preview.Programs.Single().Bores[0]), Is.True);
                Assert.That(changes(), Is.EqualTo(1));
            });
        }

        [Test]
        public void RotatingThePart_KeepsTheHighlightedRows()
        {
            var vm = OpenWithBoresAndGrooves();
            Project.Editor.RotatePart(default, default, default).ReturnsForAnyArgs(Project.ChangingProgramsTo([Program(firstBoreX: 300, turn: 1)]));
            vm.Machining.Bores[1].IsHighlighted = true;
            vm.Machining.Grooves[0].IsHighlighted = true;

            vm.Preview.Rotation = PartTurn.Deg90;

            var program = vm.Preview.Programs.Single();
            Assert.Multiple(() =>
            {
                Assert.That(program.Turn, Is.EqualTo(1));
                Assert.That(Highlighted(vm), Is.EqualTo(new[] { false, true, true, false }));
                Assert.That(vm.Preview.IsElementHighlighted(program.Bores[1]), Is.True);
                Assert.That(vm.Preview.IsElementHighlighted(program.Groovings[0]), Is.True);
            });
        }

        [Test]
        public void SelectingAnotherPart_StartsWithNothingHighlighted()
        {
            var vm = OpenWithBoresAndGrooves();
            vm.Machining.Bores[0].IsHighlighted = true;

            vm.PartsList.SelectedPart = vm.PartsList.Parts[1];

            Assert.That(Highlighted(vm), Is.All.False);
        }

        private AppViewModel OpenWithBoresAndGrooves()
        {
            SeedTwoParts();
            Project.XncPrograms = [Program(firstBoreX: 100)];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            return vm;
        }

        /// <summary>Two face bores, then two grooves, each with a valid tool.</summary>
        private static XncProgram Program(double firstBoreX, int turn = 0) => new()
        {
            OperationId = 5,
            Side = true,
            Dx = 400,
            Dy = 800,
            Dz = 19,
            Turn = turn,
            Tools = [new XncTool { Name = "Bore8", Diameter = 8 }, new XncTool { Name = "Cut4", Diameter = 4 }],
            Bores =
            [
                new XncBore { Surface = BoreSurface.Face, Index = 0, ToolName = "Bore8", X = firstBoreX, XText = $"{firstBoreX}", Y = 50, YText = "50", Depth = 10, DepthText = "10" },
                new XncBore { Surface = BoreSurface.Face, Index = 1, ToolName = "Bore8", X = 200, XText = "200", Y = 50, YText = "50", Depth = 10, DepthText = "10" }
            ],
            Groovings =
            [
                new XncGrooving
                {
                    ToolName = "Cut4", Start = new XncPoint(0, 100), End = new XncPoint(400, 100), Depth = 4, Width = 4,
                    Source = new XncElementSource(0, new Dictionary<string, string>())
                },
                new XncGrooving
                {
                    ToolName = "Cut4", Start = new XncPoint(0, 200), End = new XncPoint(400, 200), Depth = 4, Width = 4,
                    Source = new XncElementSource(1, new Dictionary<string, string>())
                }
            ]
        };

        /// <summary>Highlight of bore 1, bore 2, groove 1, groove 2.</summary>
        private static bool[] Highlighted(AppViewModel vm) =>
            [.. vm.Machining.Bores.Select(row => row.IsHighlighted).Concat(vm.Machining.Grooves.Select(row => row.IsHighlighted))];

        /// <summary>Counts <see cref="PartPreviewVM.HighlightChanged"/> from now on.</summary>
        private static Func<int> CountHighlightChanges(AppViewModel vm)
        {
            var count = 0;
            vm.Preview.HighlightChanged += () => count++;

            return () => count;
        }
    }
}
