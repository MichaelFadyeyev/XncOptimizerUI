using NSubstitute;
using XncOptimizerUI.Contracts;
using static XncOptimizerUI.Test.Fakes.ProjectServicesStub;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="MVVM.ViewModels.Panels.SortToolsPanelVM"/>, including which parts it applies to.
    /// </summary>
    [TestFixture]
    public class SortToolsPanelVMTests : AppViewModelTestBase
    {
        [Test]
        public void SortToolDeclarations_PassesCheckedPartsToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            Project.ClearCalls();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            Project.Batch.Received(1).SortToolDeclarations(PartsWithIds(100));
            Assert.That(Project.Calls, Does.Contain(nameof(IProjectSession.Open)),
                "a successful sort reloads the project");
        }

        [Test]
        public void SortToolDeclarations_AppliedGloballyWithoutFilter_PassesAllPartsEvenUnchecked()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.SortTools.SortToolsGlobally = true;
            Project.ClearCalls();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            Project.Batch.Received(1).SortToolDeclarations(PartsWithIdsInAnyOrder(100, 101));
        }

        [Test]
        public void SortToolDeclarations_AppliedGloballyWithFilter_PassesOnlyFilteredPartsEvenUnchecked()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var hidden = vm.PartsList.Parts.Single(p => p.Id == 101);
            hidden.IsSelected = true;
            vm.Filter.FilterName = vm.PartsList.Parts.Single(p => p.Id == 100).Name;
            vm.SortTools.SortToolsGlobally = true;
            Project.ClearCalls();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            // The filter decides the scope; a checked part hidden by the filter is not sorted.
            Project.Batch.Received(1).SortToolDeclarations(PartsWithIds(100));
        }

        [Test]
        public void SortToolDeclarations_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            Project.Batch.SortToolDeclarations(default!).ReturnsForAnyArgs(_ => Project.Result(changed: false));
            Project.ResultMessage = "No tool declarations reordered (skipped 0 program(s)).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            Project.ClearCalls();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            Dialogs.Received(1).ShowWarning("No tool declarations reordered (skipped 0 program(s)).", Arg.Any<string>());
            AssertNotReloadedAfter(nameof(IXncBatchOperations.SortToolDeclarations));
        }

        [Test]
        public void SortToolDeclarations_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            Project.ClearCalls();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked for tool sorting!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }

        [Test]
        public void SortToolDeclarations_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No file selected!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }
    }
}
