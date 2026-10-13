using NSubstitute;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Test.Fakes;

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
            ProjectService.Calls.Clear();

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(ProjectService.Calls, Does.Contain(nameof(FakeProjectService.ConvertGroovesAndMills)));
                Assert.That(ProjectService.LastGrooveMillParts!.Select(p => p.Id), Is.EqualTo(new[] { 100 }));
                Assert.That(ProjectService.LastGrooveMillDirection, Is.EqualTo(GrooveMillDirection.MillsToGrooves));
                Assert.That(ProjectService.LastGrooveMillProcessPockets, Is.True);
                Assert.That(ProjectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
                    "a successful conversion reloads the project");
            });
        }

        [Test]
        public void ConvertGroovesAndMills_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            ProjectService.ConvertGroovesAndMillsResult = false;
            ProjectService.ResultMessage = "No grooves converted (ignored 2).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            ProjectService.Calls.Clear();

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

            Dialogs.Received(1).ShowWarning("No grooves converted (ignored 2).", Arg.Any<string>());
            AssertNotReloadedAfter(nameof(FakeProjectService.ConvertGroovesAndMills));
        }

        [Test]
        public void ConvertGroovesAndMills_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            ProjectService.Calls.Clear();

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked"));
                Assert.That(ProjectService.Calls, Is.Empty);
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
                Assert.That(ProjectService.Calls, Is.Empty);
            });
        }
    }
}
