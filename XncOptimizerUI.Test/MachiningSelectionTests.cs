using NSubstitute;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.ViewModels.Preview;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// The selection shared by the program tables and the part preview:
    /// <see cref="SelectedPartMachiningVM.SelectElement"/> / <see cref="SelectedPartMachiningVM.ClearSelection"/>,
    /// as forwarded by <see cref="PartPreviewVM"/>.
    /// </summary>
    [TestFixture]
    public class MachiningSelectionTests : AppViewModelTestBase
    {
        [Test]
        public void SelectElement_Replace_SelectsOnlyTheElementAcrossEveryTable()
        {
            var vm = OpenWithBoresAndGrooves();
            var machining = vm.Machining;
            machining.Bores[0].IsSelected = true;
            machining.Grooves[0].IsSelected = true;
            var changes = CountSelectionChanges(vm);

            vm.Preview.SelectElement(machining.Bores[1].Model, ElementSelectionMode.Replace);

            Assert.Multiple(() =>
            {
                Assert.That(Selected(vm), Is.EqualTo(new[] { false, true, false, false }));
                Assert.That(changes(), Is.EqualTo(1));
            });
        }

        [Test]
        public void SelectElement_Toggle_AddsOrRemovesOnlyTheElement()
        {
            var vm = OpenWithBoresAndGrooves();
            var machining = vm.Machining;
            machining.SelectElement(machining.Bores[0].Model, ElementSelectionMode.Replace);

            machining.SelectElement(machining.Grooves[1].Model, ElementSelectionMode.Toggle);
            var added = Selected(vm);
            machining.SelectElement(machining.Bores[0].Model, ElementSelectionMode.Toggle);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.EqualTo(new[] { true, false, false, true }));
                Assert.That(Selected(vm), Is.EqualTo(new[] { false, false, false, true }));
            });
        }

        [Test]
        public void SelectElement_Add_NeverRemoves()
        {
            var vm = OpenWithBoresAndGrooves();
            var machining = vm.Machining;
            machining.SelectElement(machining.Bores[0].Model, ElementSelectionMode.Replace);

            machining.SelectElement(machining.Grooves[0].Model, ElementSelectionMode.Add);
            machining.SelectElement(machining.Grooves[0].Model, ElementSelectionMode.Add);

            Assert.That(Selected(vm), Is.EqualTo(new[] { true, false, true, false }));
        }

        [Test]
        public void SelectElement_OfAnElementNoRowShows_ChangesNothing()
        {
            var vm = OpenWithBoresAndGrooves();
            vm.Machining.Bores[0].IsSelected = true;

            vm.Machining.SelectElement(new XncBore(), ElementSelectionMode.Replace);

            Assert.That(Selected(vm), Is.EqualTo(new[] { true, false, false, false }));
        }

        [Test]
        public void ClearSelection_DeselectsEveryRowWithOneNotification()
        {
            var vm = OpenWithBoresAndGrooves();
            vm.Machining.Bores[1].IsSelected = true;
            vm.Machining.Grooves[0].IsSelected = true;
            var changes = CountSelectionChanges(vm);

            vm.Preview.ClearSelection();

            Assert.Multiple(() =>
            {
                Assert.That(Selected(vm), Is.All.False);
                Assert.That(changes(), Is.EqualTo(1));
            });
        }

        [Test]
        public void IsElementSelected_MatchesTheProgramElementTheRowShows()
        {
            var vm = OpenWithBoresAndGrooves();
            var program = vm.Preview.Programs.Single();

            vm.Machining.Grooves[1].IsSelected = true;

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.IsElementSelected(program.Groovings[1]), Is.True);
                Assert.That(vm.Preview.IsElementSelected(program.Groovings[0]), Is.False);
                Assert.That(vm.Preview.IsElementSelected(program.Bores[1]), Is.False);
            });
        }

        [Test]
        public void TableSelection_NotifiesThePreview()
        {
            var vm = OpenWithBoresAndGrooves();
            var changes = CountSelectionChanges(vm);

            vm.Machining.Bores[0].IsSelected = true;
            vm.Machining.Bores[0].IsSelected = false;

            Assert.That(changes(), Is.EqualTo(2));
        }

        [Test]
        public void EditingABore_KeepsItSelectedOnTheReReadElement()
        {
            var vm = OpenWithBoresAndGrooves();
            Project.Editor.UpdateBore(default, default, default, default!).ReturnsForAnyArgs(Project.ChangingProgramsTo([Program(firstBoreX: 120)]));
            var row = vm.Machining.Bores[0];
            row.IsSelected = true;
            var changes = CountSelectionChanges(vm);

            row.BeginCellEdit();
            row.X.Input = "120";
            row.TryCommitCellEdit();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Machining.Bores[0], Is.SameAs(row));
                Assert.That(row.IsSelected, Is.True);
                Assert.That(vm.Preview.IsElementSelected(vm.Preview.Programs.Single().Bores[0]), Is.True);
                Assert.That(changes(), Is.EqualTo(1));
            });
        }

        [Test]
        public void RotatingThePart_KeepsTheSelectedRows()
        {
            var vm = OpenWithBoresAndGrooves();
            Project.Editor.RotatePart(default, default, default).ReturnsForAnyArgs(Project.ChangingProgramsTo([Program(firstBoreX: 300, turn: 1)]));
            vm.Machining.Bores[1].IsSelected = true;
            vm.Machining.Grooves[0].IsSelected = true;

            vm.Preview.Rotation = PartTurn.Deg90;

            var program = vm.Preview.Programs.Single();
            Assert.Multiple(() =>
            {
                Assert.That(program.Turn, Is.EqualTo(1));
                Assert.That(Selected(vm), Is.EqualTo(new[] { false, true, true, false }));
                Assert.That(vm.Preview.IsElementSelected(program.Bores[1]), Is.True);
                Assert.That(vm.Preview.IsElementSelected(program.Groovings[0]), Is.True);
            });
        }

        [Test]
        public void SelectingAnotherPart_StartsWithNothingSelected()
        {
            var vm = OpenWithBoresAndGrooves();
            vm.Machining.Bores[0].IsSelected = true;

            vm.PartsList.SelectedPart = vm.PartsList.Parts[1];

            Assert.That(Selected(vm), Is.All.False);
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

        /// <summary>Selection of bore 1, bore 2, groove 1, groove 2.</summary>
        private static bool[] Selected(AppViewModel vm) =>
            [.. vm.Machining.Bores.Select(row => row.IsSelected).Concat(vm.Machining.Grooves.Select(row => row.IsSelected))];

        /// <summary>Counts <see cref="PartPreviewVM.SelectionChanged"/> from now on.</summary>
        private static Func<int> CountSelectionChanges(AppViewModel vm)
        {
            var count = 0;
            vm.Preview.SelectionChanged += () => count++;

            return () => count;
        }
    }
}
