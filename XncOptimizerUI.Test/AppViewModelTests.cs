using System.Collections.ObjectModel;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.Test.Fakes;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// These were impossible before DI: constructing AppViewModel triggered
    /// ConfigService's static constructor (real disk I/O), and the clipboard and
    /// MessageBox commands either needed an STA message pump or blocked the test
    /// host indefinitely.
    /// </summary>
    [TestFixture]
    public class AppViewModelTests
    {
        private FakeProjectService _projectService = null!;
        private IConfigService _config = null!;
        private IDialogService _dialogs = null!;

        [SetUp]
        public void SetUp()
        {
            _projectService = new FakeProjectService();
            _dialogs = Substitute.For<IDialogService>();

            _config = Substitute.For<IConfigService>();
            _config.LabelsToProcess.Returns(["поріз.2х40"]);
            _config.GetLastLabelToProcessSelected().Returns("поріз.2х40");
        }

        private AppViewModel CreateViewModel() => new(
            _projectService,
            _config,
            _dialogs,
            "TestAssembly",
            new ObservableCollection<string>(_config.LabelsToProcess),
            _config.GetLastLabelToProcessSelected());

        [Test]
        public void Constructor_SeedsLabelsFromConfig_WithoutWritingBack()
        {
            var vm = CreateViewModel();

            Assert.Multiple(() =>
            {
                Assert.That(vm.LabelsToProcess, Is.EquivalentTo(new[] { "поріз.2х40" }));
                Assert.That(vm.SelectedLabel, Is.EqualTo("поріз.2х40"));
            });

            // Seeding must assign the backing fields; going through the generated
            // setters would fire OnSelectedLabelChanged and save config on launch.
            _config.DidNotReceive().UpdateLastLabelToProcessSelectedIndex(Arg.Any<string>());
        }

        [Test]
        public void Constructor_UsesInjectedAssemblyNameInWindowTitle()
        {
            var vm = CreateViewModel();

            Assert.That(vm.WindowTitle, Is.EqualTo("TestAssembly - No file selected"));
        }

        [Test]
        public void AddNewLabel_PersistsLabelAndSelectsIt()
        {
            _config.LabelsToProcess.Returns(["поріз.2х40"], ["поріз.2х40", "новий"]);

            var vm = CreateViewModel();
            vm.NewLabelToProcess = "новий";

            vm.AddNewLabelCommand.Execute(null);

            _config.Received(1).AddLabelToProcess("новий");

            Assert.Multiple(() =>
            {
                Assert.That(vm.LabelsToProcess, Does.Contain("новий"));
                Assert.That(vm.SelectedLabel, Is.EqualTo("новий"));
                Assert.That(vm.NewLabelToProcess, Is.Empty);
            });
        }

        [Test]
        public void AddNewLabel_WithEmptyInput_DoesNothing()
        {
            var vm = CreateViewModel();
            vm.NewLabelToProcess = string.Empty;

            vm.AddNewLabelCommand.Execute(null);

            _config.DidNotReceive().AddLabelToProcess(Arg.Any<string>());
        }

        [Test]
        public void CopyPartsList_WithNoParts_LogsAndDoesNotTouchClipboard()
        {
            var vm = CreateViewModel();

            vm.CopyPartsListCommand.Execute(null);

            _dialogs.DidNotReceive().SetClipboardText(Arg.Any<string>());
            Assert.That(vm.Log, Does.Contain("No parts to copy!"));
        }

        [Test]
        public void CopyPartsList_WithParts_PutsTabSeparatedListOnClipboard()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.CopyPartsListCommand.Execute(null);

            var copied = (string?)_dialogs.ReceivedCalls()
                .Single(c => c.GetMethodInfo().Name == nameof(IDialogService.SetClipboardText))
                .GetArguments()[0];

            Assert.That(copied, Is.Not.Null);

            var lines = copied!.Split('\n');

            Assert.Multiple(() =>
            {
                Assert.That(lines, Has.Length.EqualTo(2), "one line per part, with no trailing newline");
                // Length, Width, Count, 4 banding symbols, Name - and a trailing
                // separator after the name, which is what the format string emits.
                Assert.That(lines[0], Is.EqualTo("600\t400\t2\tS1\tS2\t\t\tПолиця\t"));
                Assert.That(lines[1], Is.EqualTo("800\t300\t4\t\t\t\t\tБокова\t"));
            });

            _dialogs.Received(1).ShowInfo(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void CopyPartsList_ForSplitSheet_AppendsSeparatorRow()
        {
            SeedTwoParts();
            _projectService.Sheets[0].Name = "ДСП Сращ.(2)";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.CopyPartsListCommand.Execute(null);

            var copied = (string?)_dialogs.ReceivedCalls()
                .Single(c => c.GetMethodInfo().Name == nameof(IDialogService.SetClipboardText))
                .GetArguments()[0];

            Assert.That(copied!.Split('\n'), Has.Length.EqualTo(3),
                "the part on the Сращ.(2) sheet should be followed by a separator row");
        }

        [Test]
        public void ExecuteOptimize_WhenServiceFails_WarnsWithTheTextTheServiceLogged()
        {
            SeedTwoParts();
            _projectService.GroupIdenticalElementsResult = false;
            _projectService.LogToAppend = "***\nFile seems to be already optimized or contains no XNC.";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            _projectService.Calls.Clear();

            vm.ExecuteOptimizeCommand.Execute(null);

            _dialogs.Received(1).ShowWarning(
                "File seems to be already optimized or contains no XNC.",
                Arg.Any<string>());

            Assert.That(_projectService.Calls, Is.EqualTo(new[] { nameof(FakeProjectService.GroupIdenticalElements) }),
                "a failed optimize should not reload the project");
        }

        [Test]
        public void ExecuteOptimize_WhenServiceSucceeds_DoesNotWarn()
        {
            SeedTwoParts();
            _projectService.GroupIdenticalElementsResult = true;
            _projectService.LogToAppend = "***\nStored to: C:\\out_opt.project";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.ExecuteOptimizeCommand.Execute(null);

            _dialogs.DidNotReceive().ShowWarning(Arg.Any<string>(), Arg.Any<string>());
            Assert.That(vm.Log, Does.Contain("Stored to:"));
        }

        [Test]
        public void ExecuteOptimize_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.ExecuteOptimizeCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log, Does.Contain("No file selected!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void ExportPartsList_WhenUserCancelsDialog_WritesNothing()
        {
            SeedTwoParts();
            _dialogs.ShowSaveCsvDialog(Arg.Any<string>()).Returns((string?)null);

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.ExportPartsListCommand.Execute(null);

            _dialogs.DidNotReceive().SaveTextFile(Arg.Any<string>(), Arg.Any<string>());
            _dialogs.DidNotReceive().ShowInfo(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void ExportPartsList_WhenUserPicksPath_WritesSemicolonSeparatedFile()
        {
            SeedTwoParts();
            _dialogs.ShowSaveCsvDialog(Arg.Any<string>()).Returns(@"C:\out.csv");

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.ExportPartsListCommand.Execute(null);

            _dialogs.Received(1).SaveTextFile(@"C:\out.csv", Arg.Is<string>(s => s.Contains(';')));
            _dialogs.Received(1).ShowInfo(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void SelectingAnotherPart_SavesTheOutgoingPart()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.SelectedPart = vm.Parts[0];
            _projectService.Calls.Clear();

            vm.SelectedPart = vm.Parts[1];

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.UpdatePart)));
                Assert.That(_projectService.SaveProjectCount, Is.EqualTo(1));
                Assert.That(vm.Log, Does.Contain("Updates saved"));
            });
        }

        [Test]
        public void SelectingAnotherPart_WhenNothingChanged_DoesNotSave()
        {
            SeedTwoParts();
            _projectService.UpdatePartResult = false;

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.SelectedPart = vm.Parts[0];
            vm.SelectedPart = vm.Parts[1];

            Assert.That(_projectService.SaveProjectCount, Is.Zero);
        }

        [Test]
        public void CloseFile_ClearsProjectState()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.CloseFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.CloseProject)));
                Assert.That(vm.FullPath, Is.Empty);
                Assert.That(vm.Parts, Is.Empty);
                Assert.That(vm.Bands, Is.Empty);
                Assert.That(vm.SelectedPart, Is.Null);
                Assert.That(vm.WindowTitle, Does.EndWith("No file selected"));
            });
        }

        /// <summary>
        /// Two parts on one sheet, banded top and bottom, reachable through
        /// OpenFileCommand once the open dialog is stubbed to return a path.
        /// </summary>
        private void SeedTwoParts()
        {
            _dialogs.ShowOpenProjectDialog().Returns(@"C:\project.project");

            // Two sheets so a test can mark exactly one of them as a split sheet.
            _projectService.Sheets =
            [
                new Sheet { Id = 10, Name = "ДСП 18мм" },
                new Sheet { Id = 11, Name = "ДСП 16мм" }
            ];

            _projectService.Bands =
            [
                new Band { Id = 1, ExternalSymbol = "S1", InternalSymbol = "i1" },
                new Band { Id = 2, ExternalSymbol = "S2", InternalSymbol = "i2" }
            ];

            _projectService.Parts =
            [
                new Part
                {
                    Id = 100, Name = "Полиця", Count = 2, Length = 600, Width = 400,
                    SheetId = 10, TopBandingId = 1, BottomBandingId = 2
                },
                new Part
                {
                    Id = 101, Name = "Бокова", Count = 4, Length = 800, Width = 300,
                    SheetId = 11
                }
            ];
        }
    }
}
