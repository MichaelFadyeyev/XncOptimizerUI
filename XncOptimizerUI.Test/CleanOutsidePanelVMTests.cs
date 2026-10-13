using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Test.Fakes;

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
            ProjectService.Calls.Clear();

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(ProjectService.LastCleanOutsideParts!.Select(p => p.Id), Is.EqualTo(new[] { vm.PartsList.Parts[1].Id }));
                Assert.That(ProjectService.LastCleanOutsideKinds, Is.EqualTo(OutsideOperationKinds.Bores | OutsideOperationKinds.Mills));
                Assert.That(ProjectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
                    "a successful cleaning reloads the project");
            });
        }

        [Test]
        public void CleanOutsideOperations_WithNoPartsChecked_ProcessesAllParts()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.Filter.FilterName = vm.PartsList.Parts[0].Name;

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            Assert.That(ProjectService.LastCleanOutsideParts!.Select(p => p.Id),
                Is.EquivalentTo(ProjectService.Parts.Select(p => p.Id)), "all parts of the project, not only the filtered ones");
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
            ProjectService.Calls.Clear();

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("Select bores, grooves and/or mills to clean!"));
                Assert.That(ProjectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void CleanOutsideOperations_WhenServiceFails_DoesNotReload()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            ProjectService.CleanOutsideOperationsResult = false;
            ProjectService.Calls.Clear();

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            AssertNotReloadedAfter(nameof(FakeProjectService.CleanOutsideOperations));
        }
    }
}
