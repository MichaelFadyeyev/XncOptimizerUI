using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.Models;
using static XncOptimizerUI.Test.Fakes.ProjectServicesStub;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="MVVM.ViewModels.Panels.MillOffsetPanelVM"/>: offset input validation and kind selection.
    /// </summary>
    [TestFixture]
    public class MillOffsetPanelVMTests : AppViewModelTestBase
    {
        [Test]
        public void OffsetMillPaths_PassesCheckedPartsOffsetAndSideToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[1].IsSelected = true;
            vm.MillOffset.MillOffset = "2.5";
            vm.MillOffset.MillOffsetSide = MillOffsetSide.Left;
            Project.ClearCalls();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Project.Batch.Received(1).OffsetMillPaths(PartsWithIds(vm.PartsList.Parts[1].Id), 2.5d, MillOffsetSide.Left, MillPathKinds.All);
            Assert.That(Project.Calls, Does.Contain(nameof(IProjectSession.Open)),
                "a successful offset reloads the project");
        }

        [Test]
        public void OffsetMillPaths_DefaultsToEmptyOffsetOnTheRightForAllKinds()
        {
            var vm = CreateViewModel();

            Assert.Multiple(() =>
            {
                Assert.That(vm.MillOffset.MillOffset, Is.Empty);
                Assert.That(vm.MillOffset.MillOffsetSide, Is.EqualTo(MillOffsetSide.Right));
                Assert.That(vm.MillOffset.OffsetOpenMillPaths, Is.True);
                Assert.That(vm.MillOffset.OffsetClosedMillPaths, Is.True);
            });
        }

        [TestCase(true, false, MillPathKinds.Open)]
        [TestCase(false, true, MillPathKinds.Closed)]
        public void OffsetMillPaths_PassesSelectedKindsToService(bool open, bool closed, MillPathKinds expected)
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            vm.MillOffset.MillOffset = "3";
            vm.MillOffset.OffsetOpenMillPaths = open;
            vm.MillOffset.OffsetClosedMillPaths = closed;

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Project.Batch.Received(1).OffsetMillPaths(Arg.Any<IList<Part>>(), Arg.Any<double>(), Arg.Any<MillOffsetSide>(), expected);
        }

        [Test]
        public void OffsetMillPaths_WithNoKindSelected_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            vm.MillOffset.MillOffset = "3";
            vm.MillOffset.OffsetOpenMillPaths = false;
            vm.MillOffset.OffsetClosedMillPaths = false;
            Project.ClearCalls();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("Select open and/or closed mill paths to offset!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }

        [TestCase("")]
        [TestCase("abc")]
        [TestCase("0")]
        [TestCase("-3")]
        [TestCase("2,5")]
        public void OffsetMillPaths_WithInvalidOffset_LogsAndDoesNotCallService(string offset)
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            vm.MillOffset.MillOffset = offset;
            Project.ClearCalls();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("Enter a mill path offset greater than 0!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.MillOffset.MillOffset = "3";
            Project.ClearCalls();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked for mill path offset!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            Project.Batch.OffsetMillPaths(default!, default, default, default).ReturnsForAnyArgs(_ => Project.Result(changed: false));
            Project.ResultMessage = "No mill paths offset (ignored 2).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            vm.MillOffset.MillOffset = "3";
            Project.ClearCalls();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Dialogs.Received(1).ShowWarning("No mill paths offset (ignored 2).", Arg.Any<string>());
            AssertNotReloadedAfter(nameof(IXncBatchOperations.OffsetMillPaths));
        }

        [Test]
        public void OffsetMillPaths_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();
            vm.MillOffset.MillOffset = "3";

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No file selected!"));
                Assert.That(Project.Calls, Is.Empty);
            });
        }
    }
}
