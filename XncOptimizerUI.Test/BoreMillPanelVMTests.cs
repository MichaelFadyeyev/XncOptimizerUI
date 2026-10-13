using NSubstitute;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Test.Fakes;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="MVVM.ViewModels.Panels.BoreMillPanelVM"/>.
    /// </summary>
    [TestFixture]
    public class BoreMillPanelVMTests : AppViewModelTestBase
    {
        [Test]
        public void ConvertBoresAndMills_PassesCheckedPartsDirectionAndEllipseFlagToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            vm.BoreMill.BoreMillDirection = BoreMillDirection.MillsToBores;
            vm.BoreMill.ConvertBoresToEllipses = true;
            ProjectService.Calls.Clear();

            vm.BoreMill.ConvertBoresAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(ProjectService.Calls, Does.Contain(nameof(FakeProjectService.ConvertBoresAndMills)));
                Assert.That(ProjectService.LastBoreMillParts!.Select(p => p.Id), Is.EqualTo(new[] { 100 }));
                Assert.That(ProjectService.LastBoreMillDirection, Is.EqualTo(BoreMillDirection.MillsToBores));
                Assert.That(ProjectService.LastBoreMillUseEllipse, Is.True);
                Assert.That(ProjectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
                    "a successful conversion reloads the project");
            });
        }

        [Test]
        public void ConvertBoresAndMills_DefaultsToBoresToMillsWithNoEllipse()
        {
            var vm = CreateViewModel();

            Assert.Multiple(() =>
            {
                Assert.That(vm.BoreMill.BoreMillDirection, Is.EqualTo(BoreMillDirection.BoresToMills));
                Assert.That(vm.BoreMill.ConvertBoresToEllipses, Is.False);
            });
        }

        [Test]
        public void ConvertBoresAndMills_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            ProjectService.ConvertBoresAndMillsResult = false;
            ProjectService.ResultMessage = "No bores converted (ignored 0).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            ProjectService.Calls.Clear();

            vm.BoreMill.ConvertBoresAndMillsCommand.Execute(null);

            Dialogs.Received(1).ShowWarning("No bores converted (ignored 0).", Arg.Any<string>());
            AssertNotReloadedAfter(nameof(FakeProjectService.ConvertBoresAndMills));
        }

        [Test]
        public void ConvertBoresAndMills_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            ProjectService.Calls.Clear();

            vm.BoreMill.ConvertBoresAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked"));
                Assert.That(ProjectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void ConvertBoresAndMills_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.BoreMill.ConvertBoresAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No file selected!"));
                Assert.That(ProjectService.Calls, Is.Empty);
            });
        }
    }
}
