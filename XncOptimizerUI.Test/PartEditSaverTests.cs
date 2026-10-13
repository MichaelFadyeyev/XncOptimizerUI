using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.ViewModels.Parts;
using XncOptimizerUI.Test.Fakes;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="PartEditSaver"/>: when part edits from the grid are written to the project.
    /// </summary>
    [TestFixture]
    public class PartEditSaverTests : AppViewModelTestBase
    {
        [Test]
        public void SelectingAnotherPart_SavesTheOutgoingPart()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];
            Project.ClearCalls();

            vm.PartsList.SelectedPart = vm.PartsList.Parts[1];

            Assert.Multiple(() =>
            {
                Assert.That(Project.Calls, Does.Contain(nameof(IProjectEditor.UpdatePart)));
                Assert.That(Project.SaveCount, Is.EqualTo(1));
                Assert.That(vm.Log.Text, Does.Contain("Updates saved"));
            });
        }

        [Test]
        public void SaveTimes_AreLoggedFromTheInjectedClock()
        {
            SeedTwoParts();
            var now = new DateTimeOffset(2026, 8, 30, 13, 45, 0, TimeSpan.Zero);

            var vm = CreateViewModel(new FakeTimeProvider(now));
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.SelectedPart = vm.PartsList.Parts[1];
            vm.SaveFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain($"Updates saved: {now.DateTime}\n"));
                Assert.That(vm.Log.Text, Does.Contain($"Saved file: {vm.Workspace.FullPath} at {now.DateTime}\n"));
            });
        }

        [Test]
        public void SelectingAnotherPart_WhenNothingChanged_DoesNotSave()
        {
            SeedTwoParts();
            Project.Editor.UpdatePart(default!).ReturnsForAnyArgs(_ => Project.Result(changed: false));

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];
            vm.PartsList.SelectedPart = vm.PartsList.Parts[1];

            Assert.That(Project.SaveCount, Is.Zero);
        }

        [TestCase(nameof(PartVM.TopBandingId))]
        [TestCase(nameof(PartVM.BottomBandingId))]
        [TestCase(nameof(PartVM.LeftBandingId))]
        [TestCase(nameof(PartVM.RightBandingId))]
        public void ReassigningBand_OfNotSelectedPart_SavesImmediately(string edge)
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];
            Project.ClearCalls();

            typeof(PartVM).GetProperty(edge)!.SetValue(vm.PartsList.Parts[1], (int?)2);

            Assert.Multiple(() =>
            {
                Assert.That(Project.Calls, Does.Contain(nameof(IProjectEditor.UpdatePart)));
                Assert.That(Project.SaveCount, Is.EqualTo(1));
                Assert.That(vm.Log.Text, Does.Contain("Updates saved"));
            });
        }

        [Test]
        public void RemovingBand_SavesImmediately()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            Project.ClearCalls();

            vm.PartsList.Parts[0].TopBandingId = null;

            Assert.That(Project.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void TogglingTxt_SavesImmediately()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            Project.ClearCalls();

            vm.PartsList.Parts[1].ConsiderTexture = !vm.PartsList.Parts[1].ConsiderTexture;

            Assert.That(Project.SaveCount, Is.EqualTo(1));
        }
    }
}
