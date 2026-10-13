using NSubstitute;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Test.Fakes;

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
            ProjectService.Calls.Clear();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(ProjectService.Calls, Does.Contain(nameof(FakeProjectService.OffsetMillPaths)));
                Assert.That(ProjectService.LastMillOffsetParts!.Select(p => p.Id), Is.EqualTo(new[] { vm.PartsList.Parts[1].Id }));
                Assert.That(ProjectService.LastMillOffset, Is.EqualTo(2.5d));
                Assert.That(ProjectService.LastMillOffsetSide, Is.EqualTo(MillOffsetSide.Left));
                Assert.That(ProjectService.LastMillOffsetKinds, Is.EqualTo(MillPathKinds.All));
                Assert.That(ProjectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
                    "a successful offset reloads the project");
            });
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

            Assert.That(ProjectService.LastMillOffsetKinds, Is.EqualTo(expected));
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
            ProjectService.Calls.Clear();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("Select open and/or closed mill paths to offset!"));
                Assert.That(ProjectService.Calls, Is.Empty);
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
            ProjectService.Calls.Clear();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("Enter a mill path offset greater than 0!"));
                Assert.That(ProjectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.MillOffset.MillOffset = "3";
            ProjectService.Calls.Clear();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked for mill path offset!"));
                Assert.That(ProjectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            ProjectService.OffsetMillPathsResult = false;
            ProjectService.ResultMessage = "No mill paths offset (ignored 2).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            vm.MillOffset.MillOffset = "3";
            ProjectService.Calls.Clear();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Dialogs.Received(1).ShowWarning("No mill paths offset (ignored 2).", Arg.Any<string>());
            AssertNotReloadedAfter(nameof(FakeProjectService.OffsetMillPaths));
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
                Assert.That(ProjectService.Calls, Is.Empty);
            });
        }
    }
}
