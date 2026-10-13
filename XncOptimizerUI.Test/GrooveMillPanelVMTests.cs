using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;
using static XncOptimizerUI.Test.Fakes.ProjectServicesStub;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="MVVM.ViewModels.Panels.GrooveMillPanelVM"/>.
    /// </summary>
    [TestFixture]
    public class GrooveMillPanelVMTests : AppViewModelTestBase
    {
        [Test]
        public void ConvertGroovesAndMills_PassesCheckedPartsAndDirectionToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            vm.GrooveMill.GrooveMillDirection = GrooveMillDirection.MillsToGrooves;
            vm.GrooveMill.ProcessPockets = true;
            Project.ClearCalls();

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

            Project.Batch.Received(1).ConvertGroovesAndMills(PartsWithIds(100), GrooveMillDirection.MillsToGrooves, true);
            Assert.That(Project.Calls, Does.Contain(nameof(IProjectSession.Open)),
                "a successful conversion reloads the project");
        }

        [Test]
        public void ConvertGroovesAndMills_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            Project.Batch.ConvertGroovesAndMills(default!, default, default).ReturnsForAnyArgs(_ => Project.Result(changed: false));
            Project.ResultMessage = "No grooves converted (ignored 2).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            Project.ClearCalls();

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

            Dialogs.Received(1).ShowWarning("No grooves converted (ignored 2).", Arg.Any<string>());
            AssertNotReloadedAfter(nameof(IXncBatchOperations.ConvertGroovesAndMills));
        }

        [Test]
        public void ConvertGroovesAndMills_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            Project.ClearCalls();

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }

        [Test]
        public void ConvertGroovesAndMills_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No file selected!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }
    }
}
