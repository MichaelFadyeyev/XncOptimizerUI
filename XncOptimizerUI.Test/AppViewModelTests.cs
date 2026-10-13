using System.Xml.Linq;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.Test.Fakes;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// The <see cref="AppViewModel"/> shell: window title, opening and closing a project, parts list export.
    /// </summary>
    [TestFixture]
    public class AppViewModelTests : AppViewModelTestBase
    {
        [Test]
        public void Constructor_UsesInjectedAssemblyNameInWindowTitle()
        {
            var vm = CreateViewModel();

            Assert.That(vm.WindowTitle, Is.EqualTo("TestAssembly - No file selected"));
        }

        [Test]
        public void CopyPartsList_WithNoParts_LogsAndDoesNotTouchClipboard()
        {
            var vm = CreateViewModel();

            vm.CopyPartsListCommand.Execute(null);

            Dialogs.DidNotReceive().SetClipboardText(Arg.Any<string>());
            Assert.That(vm.Log.Text, Does.Contain("No parts to copy!"));
        }

        [Test]
        public void CopyPartsList_WithParts_PutsTabSeparatedListOnClipboard()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.CopyPartsListCommand.Execute(null);

            var copied = (string?)Dialogs.ReceivedCalls()
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

            Dialogs.Received(1).ShowInfo(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void CopyPartsList_ForSplitSheet_AppendsSeparatorRow()
        {
            SeedTwoParts();
            ProjectService.Sheets[0].Name = "ДСП Сращ.(2)";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.CopyPartsListCommand.Execute(null);

            var copied = (string?)Dialogs.ReceivedCalls()
                .Single(c => c.GetMethodInfo().Name == nameof(IDialogService.SetClipboardText))
                .GetArguments()[0];

            Assert.That(copied!.Split('\n'), Has.Length.EqualTo(3),
                "the part on the Сращ.(2) sheet should be followed by a separator row");
        }

        [Test]
        public void CopyPartsList_ForSplitSheet_SeparatorRowHasDashInNameColumn()
        {
            SeedTwoParts();
            ProjectService.Sheets[0].Name = "ДСП Сращ.(2)";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.CopyPartsListCommand.Execute(null);

            Assert.That(CopiedText()!.Split('\n')[1], Is.EqualTo("\t\t\t\t\t\t\t-"));
        }

        /// <summary>Sizes are formatted with the user's culture on purpose, so a spreadsheet in that culture reads them as numbers.</summary>
        [Test]
        [SetCulture("uk-UA")]
        public void CopyPartsList_FormatsFractionalSizesWithCurrentCulture()
        {
            SeedTwoParts();
            ProjectService.Parts[0].Length = 600.5m;

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.CopyPartsListCommand.Execute(null);

            Assert.That(CopiedText()!.Split('\n')[0], Does.StartWith("600,5\t400\t2\t"));
        }

        [Test]
        public void CopyPartsList_ListsOnlyFilteredParts()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.Filter.FilterName = "Бокова";

            vm.CopyPartsListCommand.Execute(null);

            Assert.That(CopiedText(), Is.EqualTo("800\t300\t4\t\t\t\t\tБокова\t"));
        }

        private string? CopiedText() =>
            (string?)Dialogs.ReceivedCalls()
                .Single(c => c.GetMethodInfo().Name == nameof(IDialogService.SetClipboardText))
                .GetArguments()[0];

        /// <summary>
        /// Regression test for the bug where re-opening the file that was open before
        /// Execute Optimize ran (or any other file reusing the same part ids) got its
        /// first part silently renamed to match the just-produced "_opt" result.
        /// Uses the real <see cref="ProjectServices"/> (not the fake) because the
        /// bug lives in the SelectedPart-autosave interaction between AppViewModel and
        /// the service's shared, mutable document state, which the fake doesn't model.
        /// </summary>
        [Test]
        public void OpenFile_AfterExecuteOptimize_ReopeningSourceLeavesFirstPartNameUnchanged()
        {
            var sourcePath = CopyFixture("td-execute-optimize.project");

            var config = Substitute.For<IConfigService>();
            config.SawWidth.Returns(4.0m);
            config.MillingToolDiams.Returns(new List<decimal> { 6.0m, 10.0m, 20.0m });
            config.LabelsToProcess.Returns(["поріз.2х40"]);
            config.GetLastLabelToProcessSelected().Returns("поріз.2х40");

            var realService = TestProject.Create(config, TimeProvider.System);
            var dialogs = Substitute.For<IDialogService>();
            dialogs.ShowOpenProjectDialog().Returns(sourcePath);

            var vm = TestViewModels.Create(realService, config, dialogs);

            vm.OpenFileCommand.Execute(null);
            vm.Preparation.ExecuteOptimizeCommand.Execute(null);

            // Re-open the same original source file, as a user comparing source vs. result would.
            vm.OpenFileCommand.Execute(null);

            var firstPartName = XDocument.Load(sourcePath).Descendants("part").First().Attribute("name")!.Value;

            Assert.That(firstPartName, Is.EqualTo("301.07.03.підріз.47=ПАН-ВРХ"),
                "re-opening the source file after Execute Optimize must not rename its first part");
        }

        [Test]
        public void ExportPartsList_WhenUserCancelsDialog_WritesNothing()
        {
            SeedTwoParts();
            Dialogs.ShowSaveCsvDialog(Arg.Any<string>()).Returns((string?)null);

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.ExportPartsListCommand.Execute(null);

            Dialogs.DidNotReceive().SaveTextFile(Arg.Any<string>(), Arg.Any<string>());
            Dialogs.DidNotReceive().ShowInfo(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void ExportPartsList_WhenUserPicksPath_WritesSemicolonSeparatedFile()
        {
            SeedTwoParts();
            Dialogs.ShowSaveCsvDialog(Arg.Any<string>()).Returns(@"C:\out.csv");

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.ExportPartsListCommand.Execute(null);

            Dialogs.Received(1).SaveTextFile(@"C:\out.csv", Arg.Is<string>(s => s.Contains(';')));
            Dialogs.Received(1).ShowInfo(Arg.Any<string>(), Arg.Any<string>());
        }

        [Test]
        public void OpeningProject_SelectsTheFirstPart()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.That(vm.PartsList.SelectedPart, Is.SameAs(vm.PartsList.Parts[0]));
        }

        [Test]
        public void OpeningProjectWithNoParts_LeavesSelectionNull()
        {
            Dialogs.ShowOpenProjectDialog().Returns(@"C:\empty.project");

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.That(vm.PartsList.SelectedPart, Is.Null);
        }

        [Test]
        public void OpenFile_BuildsBandOptions_WithEmptyOptionFirst()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Catalog.BandOptions.Select(o => o.Id), Is.EqualTo(new int?[] { null, 1, 2 }));
                Assert.That(vm.Catalog.BandOptions.Select(o => o.InternalSymbol), Is.EqualTo(new[] { "", "i1", "i2" }),
                    "the select shows each band's internal symbol");
                Assert.That(vm.Catalog.BandOptions[1].Caption, Is.EqualTo("i1 " + vm.Catalog.Bands[0].Name),
                    "symbol and band name separated by a space");
                Assert.That(vm.Catalog.BandOptions[0].Caption, Is.Empty, "the remove option is an empty row");
            });
        }

        [Test]
        public void OpeningProject_WithXncTurnDiscordance_WarnsAndLogsEachPartName()
        {
            SeedTwoParts();
            ProjectService.PartsWithXncTurnDiscordance = ["Полиця", "Кришка"];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Dialogs.Received(1).ShowWarning(
                "Discordance in xnc programs turn corner for parts: Полиця, Кришка",
                Arg.Any<string>());

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("Discordance in xnc programs turn corner for parts: Полиця, Кришка"));
                Assert.That(vm.Log.Text, Does.Contain("turn discordance: Полиця"));
                Assert.That(vm.Log.Text, Does.Contain("turn discordance: Кришка"));
            });
        }

        [Test]
        public void OpeningProject_WithNoXncTurnDiscordance_DoesNotWarn()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Dialogs.DidNotReceive().ShowWarning(
                Arg.Is<string>(s => s.Contains("Discordance in xnc programs turn corner")),
                Arg.Any<string>());
        }

        [Test]
        public void CloseFile_ResetsCheckedCount()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.CheckAllCommand.Execute(null);

            vm.CloseFileCommand.Execute(null);

            Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(0));
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
                Assert.That(ProjectService.Calls, Does.Contain(nameof(FakeProjectService.Close)));
                Assert.That(vm.Workspace.FullPath, Is.Empty);
                Assert.That(vm.PartsList.Parts, Is.Empty);
                Assert.That(vm.Catalog.Bands, Is.Empty);
                Assert.That(vm.PartsList.SelectedPart, Is.Null);
                Assert.That(vm.WindowTitle, Does.EndWith("No file selected"));
            });
        }
    }
}
