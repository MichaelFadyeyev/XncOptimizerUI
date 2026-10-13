using NSubstitute;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="MVVM.ViewModels.Panels.LabelsVM"/>: labels seeded from and persisted to the config.
    /// </summary>
    [TestFixture]
    public class LabelsVMTests : AppViewModelTestBase
    {
        [Test]
        public void Constructor_SeedsLabelsFromConfig_WithoutWritingBack()
        {
            var vm = CreateViewModel();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preparation.Labels.LabelsToProcess, Is.EquivalentTo(new[] { "поріз.2х40" }));
                Assert.That(vm.Preparation.Labels.SelectedLabel, Is.EqualTo("поріз.2х40"));
            });

            // Seeding must assign the backing fields; going through the generated
            // setters would fire OnSelectedLabelChanged and save config on launch.
            Config.DidNotReceive().UpdateLastLabelToProcessSelectedIndex(Arg.Any<string>());
        }

        [Test]
        public void AddNewLabel_PersistsLabelAndSelectsIt()
        {
            Config.LabelsToProcess.Returns(["поріз.2х40"], ["поріз.2х40", "новий"]);

            var vm = CreateViewModel();
            vm.Preparation.Labels.NewLabelToProcess = "новий";

            vm.Preparation.Labels.AddNewLabelCommand.Execute(null);

            Config.Received(1).AddLabelToProcess("новий");

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preparation.Labels.LabelsToProcess, Does.Contain("новий"));
                Assert.That(vm.Preparation.Labels.SelectedLabel, Is.EqualTo("новий"));
                Assert.That(vm.Preparation.Labels.NewLabelToProcess, Is.Empty);
            });
        }

        [Test]
        public void AddNewLabel_WithEmptyInput_DoesNothing()
        {
            var vm = CreateViewModel();
            vm.Preparation.Labels.NewLabelToProcess = string.Empty;

            vm.Preparation.Labels.AddNewLabelCommand.Execute(null);

            Config.DidNotReceive().AddLabelToProcess(Arg.Any<string>());
        }
    }
}
