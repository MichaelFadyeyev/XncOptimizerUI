using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;
using static XncOptimizerUI.Test.Fakes.ProjectServicesStub;

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
            Project.ClearCalls();

            vm.BoreMill.ConvertBoresAndMillsCommand.Execute(null);

            Project.Batch.Received(1).ConvertBoresAndMills(PartsWithIds(100), BoreMillDirection.MillsToBores, true);
            Assert.That(Project.Calls, Does.Contain(nameof(IProjectSession.Open)),
                "a successful conversion reloads the project");
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
            Project.Batch.ConvertBoresAndMills(default!, default, default).ReturnsForAnyArgs(_ => Project.Result(changed: false));
            Project.ResultMessage = "No bores converted (ignored 0).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            Project.ClearCalls();

            vm.BoreMill.ConvertBoresAndMillsCommand.Execute(null);

            Dialogs.Received(1).ShowWarning("No bores converted (ignored 0).", Arg.Any<string>());
            AssertNotReloadedAfter(nameof(IXncBatchOperations.ConvertBoresAndMills));
        }

        [Test]
        public void ConvertBoresAndMills_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            Project.ClearCalls();

            vm.BoreMill.ConvertBoresAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked"));
                Assert.That(Project.Calls, Is.Empty);
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
                Assert.That(Project.Calls, Is.Empty);
            });
        }
    }
}
