using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;
using static XncOptimizerUI.Test.Fakes.ProjectServicesStub;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="MVVM.ViewModels.Panels.CleanOutsidePanelVM"/>.
    /// </summary>
    [TestFixture]
    public class CleanOutsidePanelVMTests : AppViewModelTestBase
    {
        [Test]
        public void CleanOutsideOperations_DefaultsToAllKinds()
        {
            var vm = CreateViewModel();

            Assert.Multiple(() =>
            {
                Assert.That(vm.CleanOutside.CleanOutsideBores, Is.True);
                Assert.That(vm.CleanOutside.CleanOutsideGrooves, Is.True);
                Assert.That(vm.CleanOutside.CleanOutsideMills, Is.True);
            });
        }

        [Test]
        public void CleanOutsideOperations_PassesCheckedPartsAndKindsToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[1].IsSelected = true;
            vm.CleanOutside.CleanOutsideGrooves = false;
            Project.ClearCalls();

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            Project.Batch.Received(1).CleanOutsideOperations(
                PartsWithIds(vm.PartsList.Parts[1].Id), OutsideOperationKinds.Bores | OutsideOperationKinds.Mills);
            Assert.That(Project.Calls, Does.Contain(nameof(IProjectSession.Open)),
                "a successful cleaning reloads the project");
        }

        [Test]
        public void CleanOutsideOperations_WithNoPartsChecked_ProcessesAllParts()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.Filter.FilterName = vm.PartsList.Parts[0].Name;

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            // All parts of the project, not only the filtered ones.
            Project.Batch.Received(1).CleanOutsideOperations(
                PartsWithIdsInAnyOrder([.. Project.Parts.Select(p => p.Id)]), Arg.Any<OutsideOperationKinds>());
        }

        [Test]
        public void CleanOutsideOperations_WithNoKindSelected_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.CleanOutside.CleanOutsideBores = false;
            vm.CleanOutside.CleanOutsideGrooves = false;
            vm.CleanOutside.CleanOutsideMills = false;
            Project.ClearCalls();

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("Select bores, grooves and/or mills to clean!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }

        [Test]
        public void CleanOutsideOperations_WhenServiceFails_DoesNotReload()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            Project.Batch.CleanOutsideOperations(default!, default).ReturnsForAnyArgs(_ => Project.Result(changed: false));
            Project.ClearCalls();

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            AssertNotReloadedAfter(nameof(IXncBatchOperations.CleanOutsideOperations));
        }
    }
}
