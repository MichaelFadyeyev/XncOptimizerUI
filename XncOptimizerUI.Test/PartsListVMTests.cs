using System.Text.RegularExpressions;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="PartsListVM"/>: checking parts, the checked count and deleting the checked parts.
    /// </summary>
    [TestFixture]
    public class PartsListVMTests : AppViewModelTestBase
    {
        private const string DeletedLog = "Deleted 2 part(s):\n\"P2\" (id=2),\n\"P3\" (id=3),\n2 XNC operation(s) removed";

        [Test]
        public void CheckAll_ChecksEveryVisiblePart_AndCheckedCountReflectsIt()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.CheckAllCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.Parts.Select(p => p.IsChecked), Is.All.True);
                Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void UncheckAll_UnchecksEveryVisiblePart()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.CheckAllCommand.Execute(null);

            vm.PartsList.UncheckAllCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.Parts.Select(p => p.IsChecked), Is.All.False);
                Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void CheckedCount_UpdatesOnIndividualToggle_AndRaisesPropertyChanged()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            var raised = 0;
            vm.PartsList.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PartsListVM.CheckedCount)) raised++;
            };

            vm.PartsList.Parts[0].IsChecked = true;
            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(1));
                Assert.That(raised, Is.GreaterThanOrEqualTo(1));
            });

            vm.PartsList.Parts[0].IsChecked = false;
            Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(0));
        }

        [Test]
        public void CheckAll_WithActiveFilter_OnlyChecksFilteredParts()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.FilterName = "Полиця"; // setter runs FilterParts -> only part 100 stays visible
            Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(1));

            vm.PartsList.CheckAllCommand.Execute(null);
            Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(1), "the filtered-out part must not be checked");

            vm.Filter.ClearFiltersCommand.Execute(null);
            var reappeared = vm.PartsList.Parts.Single(p => p.Name == "Бокова");
            Assert.That(reappeared.IsChecked, Is.False);
        }

        [Test]
        public void DeleteChecked_NothingChecked_IsDisabled()
        {
            var vm = OpenFiveParts();

            Assert.That(vm.PartsList.DeleteCheckedCommand.CanExecute(null), Is.False);

            vm.PartsList.Parts[1].IsChecked = true;

            Assert.That(vm.PartsList.DeleteCheckedCommand.CanExecute(null), Is.True);
        }

        [Test]
        public void DeleteChecked_Declined_DeletesAndSavesNothing()
        {
            var vm = OpenFiveParts();
            Check(vm, 2, 3);
            Dialogs.Confirm(default!, default!).ReturnsForAnyArgs(false);
            var savesBefore = Project.SaveCount;

            vm.PartsList.DeleteCheckedCommand.Execute(null);

            Project.Editor.DidNotReceiveWithAnyArgs().DeleteParts(default!);
            Assert.Multiple(() =>
            {
                Assert.That(Project.SaveCount, Is.EqualTo(savesBefore));
                Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(5));
            });
        }

        [Test]
        public void DeleteChecked_Confirmed_DeletesSavesAndLogsOneRecord()
        {
            var vm = OpenFiveParts();
            Check(vm, 2, 3);
            ConfirmDeletion();
            Project.Editor.UpdatePart(default!).ReturnsForAnyArgs(Project.Result(changed: false)); // no pending grid edit
            var savesBefore = Project.SaveCount;
            var logBefore = vm.Log.Text;

            vm.PartsList.DeleteCheckedCommand.Execute(null);

            Dialogs.Received(1).Confirm(Arg.Is<string>(m => m.StartsWith("Delete 2 checked part(s)?\nTheir XNC operations")), Arg.Any<string>());
            Project.Editor.Received(1).DeleteParts(Arg.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 2, 3 })));
            Assert.Multiple(() =>
            {
                Assert.That(Project.SaveCount, Is.EqualTo(savesBefore + 1));
                Assert.That(vm.Log.Text[logBefore.Length..], Does.Match($"^{Regex.Escape(DeletedLog)}, saved: [^\n]+\n$"));
            });
        }

        [Test]
        public void DeleteChecked_RemovesTheRowsInPlace_RenumbersAndSelectsThePartAfterTheLastDeleted()
        {
            var vm = OpenFiveParts();
            var shown = vm.PartsList.Parts;
            vm.PartsList.SelectedPart = shown[1];
            Check(vm, 2, 3);
            ConfirmDeletion();

            vm.PartsList.DeleteCheckedCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.Parts, Is.SameAs(shown), "the grid keeps its collection, so its scroll position");
                Assert.That(vm.PartsList.Parts.Select(p => p.Id), Is.EqualTo(new[] { 1, 4, 5 }));
                Assert.That(vm.PartsList.AllParts.Select(p => p.Number), Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(vm.PartsList.SelectedPart?.Id, Is.EqualTo(4));
                Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(0));
                Assert.That(vm.PartsList.DeleteCheckedCommand.CanExecute(null), Is.False);
            });
        }

        [Test]
        public void DeleteChecked_LastRowsDeleted_SelectsThePartBeforeThem()
        {
            var vm = OpenFiveParts();
            Check(vm, 4, 5);
            ConfirmDeletion();

            vm.PartsList.DeleteCheckedCommand.Execute(null);

            Assert.That(vm.PartsList.SelectedPart?.Id, Is.EqualTo(3));
        }

        [Test]
        public void DeleteChecked_EveryShownPart_SelectsNothing()
        {
            var vm = OpenFiveParts();
            vm.PartsList.CheckAllCommand.Execute(null);
            ConfirmDeletion();

            vm.PartsList.DeleteCheckedCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.Parts, Is.Empty);
                Assert.That(vm.PartsList.SelectedPart, Is.Null);
            });
        }

        [Test]
        public void DeleteChecked_OnlyHiddenParts_KeepsTheSelectionAndNamesThemInTheQuestion()
        {
            var vm = OpenFiveParts();
            Check(vm, 5);
            vm.Filter.FilterName = "P1";
            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];
            ConfirmDeletion();

            vm.PartsList.DeleteCheckedCommand.Execute(null);

            Dialogs.Received(1).Confirm(Arg.Is<string>(m => m.Contains("\n1 of them hidden by the filter.")), Arg.Any<string>());
            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.SelectedPart?.Id, Is.EqualTo(1));
                Assert.That(vm.PartsList.AllParts.Select(p => p.Id), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            });
        }

        [Test]
        public void DeleteChecked_SelectedPartDeleted_DoesNotSaveItsPendingEdit()
        {
            var vm = OpenFiveParts();
            vm.PartsList.SelectedPart = vm.PartsList.Parts[2];
            Check(vm, 3);
            ConfirmDeletion();
            Project.Editor.ClearReceivedCalls();

            vm.PartsList.DeleteCheckedCommand.Execute(null);

            Project.Editor.DidNotReceive().UpdatePart(Arg.Is<Part>(p => p.Id == 3));
        }

        [Test]
        public void DeleteChecked_SelectedPartKept_SavesItsPendingEditFirst()
        {
            var vm = OpenFiveParts();
            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];
            Check(vm, 3);
            ConfirmDeletion();
            Project.Editor.ClearReceivedCalls();

            vm.PartsList.DeleteCheckedCommand.Execute(null);

            Assert.That(
                Project.Calls.SkipWhile(call => call != nameof(IProjectEditor.UpdatePart)).Take(2),
                Is.EqualTo(new[] { nameof(IProjectEditor.UpdatePart), nameof(IProjectEditor.DeleteParts) }));
        }

        [Test]
        public void DeleteChecked_Failed_KeepsEveryRow()
        {
            var vm = OpenFiveParts();
            Check(vm, 2);
            Dialogs.Confirm(default!, default!).ReturnsForAnyArgs(true);
            Project.Editor.DeleteParts(default!).ReturnsForAnyArgs(Project.Result(changed: false));

            vm.PartsList.DeleteCheckedCommand.Execute(null);

            Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(5));
        }

        [Test]
        public void DeleteChecked_RequestsTheSelectedPartRevealed()
        {
            var vm = OpenFiveParts();
            Check(vm, 2);
            ConfirmDeletion();
            PartVM? revealed = null;
            vm.PartsList.RevealRequested += part => revealed = part;

            vm.PartsList.DeleteCheckedCommand.Execute(null);

            Assert.That(revealed, Is.SameAs(vm.PartsList.SelectedPart));
        }

        private AppViewModel OpenFiveParts()
        {
            Dialogs.ShowOpenProjectDialog().Returns(@"C:\project.project");
            Project.Parts = [.. Enumerable.Range(1, 5).Select(id => new Part { Id = id, Name = $"P{id}", Count = 1, Length = 100 * id, Width = 50 })];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            return vm;
        }

        private static void Check(AppViewModel vm, params int[] ids) =>
            vm.PartsList.AllParts.Where(p => ids.Contains(p.Id)).ToList().ForEach(p => p.IsChecked = true);

        private void ConfirmDeletion()
        {
            Dialogs.Confirm(default!, default!).ReturnsForAnyArgs(true);
            Project.Editor.DeleteParts(default!).ReturnsForAnyArgs(new OperationResult(true, [new LogEntry(LogLevel.Info, DeletedLog)]));
        }
    }
}
