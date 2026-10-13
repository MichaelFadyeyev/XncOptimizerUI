using NSubstitute;
using XncOptimizerUI.Contracts;
using static XncOptimizerUI.Test.Fakes.ProjectServicesStub;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="MVVM.ViewModels.Panels.MillOrderPanelVM"/>.
    /// </summary>
    [TestFixture]
    public class MillOrderPanelVMTests : AppViewModelTestBase
    {
        [Test]
        public void OptimizeMillTraversal_PassesCheckedPartsToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            Project.ClearCalls();

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

            Project.Batch.Received(1).OptimizeMillTraversal(PartsWithIds(100));
            Assert.That(Project.Calls, Does.Contain(nameof(IProjectSession.Open)),
                "a successful optimization reloads the project");
        }

        [Test]
        public void OptimizeMillTraversal_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            Project.Batch.OptimizeMillTraversal(default!).ReturnsForAnyArgs(_ => Project.Result(changed: false));
            Project.ResultMessage = "No mill passes reordered (ignored 0).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            Project.ClearCalls();

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

            Dialogs.Received(1).ShowWarning("No mill passes reordered (ignored 0).", Arg.Any<string>());
            AssertNotReloadedAfter(nameof(IXncBatchOperations.OptimizeMillTraversal));
        }

        [Test]
        public void OptimizeMillTraversal_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            Project.ClearCalls();

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked for mill order optimization!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }

        [Test]
        public void OptimizeMillTraversal_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No file selected!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }
    }
}
