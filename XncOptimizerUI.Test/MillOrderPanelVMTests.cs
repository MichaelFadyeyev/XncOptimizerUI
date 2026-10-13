using NSubstitute;
using XncOptimizerUI.Test.Fakes;

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
            ProjectService.Calls.Clear();

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(ProjectService.Calls, Does.Contain(nameof(FakeProjectService.OptimizeMillTraversal)));
                Assert.That(ProjectService.LastMillTraversalParts!.Select(p => p.Id), Is.EqualTo(new[] { 100 }));
                Assert.That(ProjectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
                    "a successful optimization reloads the project");
            });
        }

        [Test]
        public void OptimizeMillTraversal_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            ProjectService.OptimizeMillTraversalResult = false;
            ProjectService.ResultMessage = "No mill passes reordered (ignored 0).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            ProjectService.Calls.Clear();

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

            Dialogs.Received(1).ShowWarning("No mill passes reordered (ignored 0).", Arg.Any<string>());
            AssertNotReloadedAfter(nameof(FakeProjectService.OptimizeMillTraversal));
        }

        [Test]
        public void OptimizeMillTraversal_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            ProjectService.Calls.Clear();

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked for mill order optimization!"));
                Assert.That(ProjectService.Calls, Is.Empty);
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
                Assert.That(ProjectService.Calls, Is.Empty);
            });
        }
    }
}
