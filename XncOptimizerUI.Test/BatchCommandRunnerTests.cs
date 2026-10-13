using NSubstitute;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="MVVM.ViewModels.Panels.BatchCommandRunner"/>: the guard, warning and reload steps every batch command shares.
    /// </summary>
    [TestFixture]
    public class BatchCommandRunnerTests : AppViewModelTestBase
    {
        [Test]
        public void BatchCommand_WhenNothingChanged_KeepsTheSelectedPart()
        {
            SeedTwoParts();
            Project.Restructuring.GroupIdenticalElements().Returns(_ => Project.Result(changed: false));

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var selected = vm.PartsList.Parts[1];
            vm.PartsList.SelectedPart = selected;

            vm.Preparation.ExecuteOptimizeCommand.Execute(null);

            Assert.That(vm.PartsList.SelectedPart, Is.SameAs(selected));
        }

        [Test]
        public void ExecuteOptimize_WhenServiceFails_WarnsWithTheTextTheServiceLogged()
        {
            SeedTwoParts();
            Project.Restructuring.GroupIdenticalElements().Returns(_ => Project.Result(changed: false));
            Project.ResultMessage = "File seems to be already optimized or contains no XNC.";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            Project.ClearCalls();

            vm.Preparation.ExecuteOptimizeCommand.Execute(null);

            Dialogs.Received(1).ShowWarning(
                "File seems to be already optimized or contains no XNC.",
                Arg.Any<string>());

            // Releasing the SelectedPart before the service call may auto-save it
            // (UpdatePart/Save) — that's expected. What must NOT happen is a
            // reload of the project after a failed GroupIdenticalElements call.
            AssertNotReloadedAfter(nameof(IProjectRestructuring.GroupIdenticalElements));
        }

        [Test]
        public void ExecuteOptimize_WhenServiceSucceeds_DoesNotWarn()
        {
            SeedTwoParts();
            Project.ResultMessage = "Stored to: C:\\out_opt.project";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Preparation.ExecuteOptimizeCommand.Execute(null);

            Dialogs.DidNotReceive().ShowWarning(Arg.Any<string>(), Arg.Any<string>());
            Assert.That(vm.Log.Text, Does.Contain("Stored to:"));
        }

        [Test]
        public void ExecuteOptimize_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.Preparation.ExecuteOptimizeCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No file selected!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }
    }
}
