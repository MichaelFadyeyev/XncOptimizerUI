using System.Collections.ObjectModel;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
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
        public void OpeningProject_SelectsTheFirstPart()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.That(vm.SelectedPart, Is.SameAs(vm.Parts[0]));
        }

        [Test]
        public void OpeningProjectWithNoParts_LeavesSelectionNull()
        {
            _dialogs.ShowOpenProjectDialog().Returns(@"C:\empty.project");

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.That(vm.SelectedPart, Is.Null);
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
        public void SelectingPart_RendersItsXncProgramsAsBriefLines()
        {
            SeedTwoParts();
            _projectService.XncPrograms =
            [
                new XncProgram
                {
                    Side = true,
                    Dx = 1380, Dy = 600, Dz = 19,
                    Tools = [new XncTool { Name = "Bore8", Diameter = 8 }],
                    Bores =
                    [
                        new XncBore
                        {
                            Surface = BoreSurface.Left, ToolName = "Bore8",
                            X = 0, Y = 65, Z = 10, Depth = 34
                        }
                    ],
                    Groovings =
                    [
                        new XncGrooving
                        {
                            ToolName = "Cut3.2",
                            Start = new XncPoint(-10, 565), End = new XncPoint(1390, 565),
                            Depth = 4, Width = 10, Position = ToolPosition.Center
                        }
                    ],
                    MillingContours =
                    [
                        new XncMillingContour
                        {
                            ToolName = "Mill6", Entry = new XncPoint(250, 382.5),
                            EntryDepth = 21, Position = ToolPosition.Left,
                            Segments = [new XncArcSegment(), new XncArcSegment()]
                        }
                    ]
                }
            ];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.SelectedPart = vm.Parts[0];

            Assert.Multiple(() =>
            {
                Assert.That(vm.SelectedPartPrograms, Does.StartWith("Programs: 1"));
                Assert.That(vm.SelectedPartPrograms, Does.Contain("/xnc/front/dx1380 dy600 dz19"));
                Assert.That(vm.SelectedPartPrograms, Does.Contain("/tool/front/Bore8 Ø8"));
                Assert.That(vm.SelectedPartPrograms, Does.Contain("/bore/front/Left Bore8 (0,65,10) dp34"));
                Assert.That(vm.SelectedPartPrograms, Does.Contain("/groove/front/Cut3.2 (-10,565)-(1390,565) dp4 w10 Center"));
                Assert.That(vm.SelectedPartPrograms, Does.Contain("/mill/front/Mill6 (250,382.5) dp21 Left 2 arc"));
            });
        }

        [Test]
        public void SelectingPart_WithNoPrograms_ShowsNone()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.SelectedPart = vm.Parts[0];

            Assert.That(vm.SelectedPartPrograms, Is.EqualTo("Programs: none"));
        }

        [Test]
        public void ChangingSelectedPart_RefreshesTheProgramsText()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [new XncProgram { Side = false, Dx = 800, Dy = 300, Dz = 16 }];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.SelectedPart = vm.Parts[0];
            Assert.That(vm.SelectedPartPrograms, Does.Contain("/xnc/back/dx800 dy300 dz16"));

            vm.SelectedPart = null;
            Assert.That(vm.SelectedPartPrograms, Is.EqualTo("Programs: -"));
        }

        [Test]
        public void ConvertGroovesAndMills_PassesCheckedPartsAndDirectionToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.Parts[0].IsSelected = true;
            vm.GrooveMillDirection = GrooveMillDirection.MillsToGrooves;
            vm.ProcessPockets = true;
            _projectService.Calls.Clear();

            vm.ConvertGroovesAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.ConvertGroovesAndMills)));
                Assert.That(_projectService.LastGrooveMillParts!.Select(p => p.Id), Is.EqualTo(new[] { 100 }));
                Assert.That(_projectService.LastGrooveMillDirection, Is.EqualTo(GrooveMillDirection.MillsToGrooves));
                Assert.That(_projectService.LastGrooveMillProcessPockets, Is.True);
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.OpenProject)),
                    "a successful conversion reloads the project");
            });
        }

        [Test]
        public void ConvertGroovesAndMills_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            _projectService.ConvertGroovesAndMillsResult = false;
            _projectService.LogToAppend = "***\nNo grooves converted (ignored 2).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.Parts[0].IsSelected = true;
            _projectService.Calls.Clear();

            vm.ConvertGroovesAndMillsCommand.Execute(null);

            _dialogs.Received(1).ShowWarning("No grooves converted (ignored 2).", Arg.Any<string>());
            Assert.That(_projectService.Calls, Is.EqualTo(new[] { nameof(FakeProjectService.ConvertGroovesAndMills) }),
                "a failed conversion should not reload the project");
        }

        [Test]
        public void ConvertGroovesAndMills_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            _projectService.Calls.Clear();

            vm.ConvertGroovesAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log, Does.Contain("No parts checked"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void ConvertGroovesAndMills_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.ConvertGroovesAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log, Does.Contain("No file selected!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void OptimizeMillTraversal_PassesCheckedPartsToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.Parts[0].IsSelected = true;
            _projectService.Calls.Clear();

            vm.OptimizeMillTraversalCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.OptimizeMillTraversal)));
                Assert.That(_projectService.LastMillTraversalParts!.Select(p => p.Id), Is.EqualTo(new[] { 100 }));
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.OpenProject)),
                    "a successful optimization reloads the project");
            });
        }

        [Test]
        public void OptimizeMillTraversal_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            _projectService.OptimizeMillTraversalResult = false;
            _projectService.LogToAppend = "***\nNo mill passes reordered (ignored 0).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.Parts[0].IsSelected = true;
            _projectService.Calls.Clear();

            vm.OptimizeMillTraversalCommand.Execute(null);

            _dialogs.Received(1).ShowWarning("No mill passes reordered (ignored 0).", Arg.Any<string>());
            Assert.That(_projectService.Calls, Is.EqualTo(new[] { nameof(FakeProjectService.OptimizeMillTraversal) }),
                "a failed optimization should not reload the project");
        }

        [Test]
        public void OptimizeMillTraversal_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            _projectService.Calls.Clear();

            vm.OptimizeMillTraversalCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log, Does.Contain("No parts checked for mill order optimization!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void OptimizeMillTraversal_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.OptimizeMillTraversalCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log, Does.Contain("No file selected!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void CheckAll_ChecksEveryVisiblePart_AndCheckedCountReflectsIt()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.CheckAllCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Parts.Select(p => p.IsSelected), Is.All.True);
                Assert.That(vm.CheckedCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void UncheckAll_UnchecksEveryVisiblePart()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.CheckAllCommand.Execute(null);

            vm.UncheckAllCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Parts.Select(p => p.IsSelected), Is.All.False);
                Assert.That(vm.CheckedCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void CheckedCount_UpdatesOnIndividualToggle_AndRaisesPropertyChanged()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            var raised = 0;
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppViewModel.CheckedCount)) raised++;
            };

            vm.Parts[0].IsSelected = true;
            Assert.Multiple(() =>
            {
                Assert.That(vm.CheckedCount, Is.EqualTo(1));
                Assert.That(raised, Is.GreaterThanOrEqualTo(1));
            });

            vm.Parts[0].IsSelected = false;
            Assert.That(vm.CheckedCount, Is.EqualTo(0));
        }

        [Test]
        public void CheckAll_WithActiveFilter_OnlyChecksFilteredParts()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.FilterName = "Полиця"; // setter runs FilterParts -> only part 100 stays visible
            Assert.That(vm.Parts, Has.Count.EqualTo(1));

            vm.CheckAllCommand.Execute(null);
            Assert.That(vm.CheckedCount, Is.EqualTo(1), "the filtered-out part must not be checked");

            vm.ClearFiltersCommand.Execute(null);
            var reappeared = vm.Parts.Single(p => p.Name == "Бокова");
            Assert.That(reappeared.IsSelected, Is.False);
        }

        [Test]
        public void LengthMin_ExcludesShorterParts()
        {
            SeedTwoParts(); // Полиця 600x400, Бокова 800x300

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "700";

            Assert.That(vm.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Бокова" }));
        }

        [Test]
        public void LengthRange_KeepsPartsInsideBounds()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "550";
            vm.LengthMax = "650";

            Assert.That(vm.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }));
        }

        [Test]
        public void WidthMax_ExcludesWiderParts()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.WidthMax = "350";

            Assert.That(vm.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Бокова" }));
        }

        [Test]
        public void RangeAndName_CombineAsAnd()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.FilterName = "Бокова";
            vm.LengthMin = "100";
            vm.LengthMax = "900";
            Assert.That(vm.Parts, Has.Count.EqualTo(1));

            vm.LengthMax = "500"; // Бокова is 800 long -> now excluded
            Assert.That(vm.Parts, Is.Empty);
        }

        [Test]
        public void ClearFilters_ResetsRangeFields()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "550";
            vm.LengthMax = "650";
            vm.WidthMin = "100";
            vm.WidthMax = "450";
            Assert.That(vm.Parts, Has.Count.EqualTo(1));

            vm.ClearFiltersCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Parts, Has.Count.EqualTo(2));
                Assert.That(vm.LengthMin, Is.Empty);
                Assert.That(vm.LengthMax, Is.Empty);
                Assert.That(vm.WidthMin, Is.Empty);
                Assert.That(vm.WidthMax, Is.Empty);
            });
        }

        [Test]
        public void InvalidDecimalString_IsIgnored()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "abc";

            Assert.That(vm.Parts, Has.Count.EqualTo(2), "unparseable bound must not filter anything");
        }

        [Test]
        public void DotSeparator_ParsedAsDecimal()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMax = "700.5";

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMax, Is.EqualTo("700.5"), "typed text is kept verbatim");
                Assert.That(vm.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }),
                    "700.5 must parse as a decimal, so only the 600-long part passes");
            });
        }

        [Test]
        public void CommaSeparator_IsInvalid_NotConverted()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMax = "700,5";

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMax, Is.EqualTo("700,5"), "comma is left verbatim, never rewritten to a dot");
                Assert.That(vm.Parts, Has.Count.EqualTo(2), "\"700,5\" does not parse, so it filters nothing");
            });
        }

        [Test]
        public void HalfTypedDecimal_IsNotReformatted()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "18."; // mid-typing "18.5"

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMin, Is.EqualTo("18."), "trailing separator must survive the setter so the next digit lands; truncation is commit-time only");
                Assert.That(vm.Parts, Has.Count.EqualTo(2), "\"18.\" parses as 18, both parts are longer");
            });
        }

        [Test]
        public void CommaSeparator_SurvivesCanonicalizationUnchanged()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "18,5";
            vm.NormalizeRangeBounds(); // canonicalization must not touch the comma either

            Assert.That(vm.LengthMin, Is.EqualTo("18,5"));
        }

        [Test]
        public void TrailingSeparator_TruncatedOnNormalize()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "18.";
            Assert.That(vm.LengthMin, Is.EqualTo("18."), "not yet - still mid-edit");

            vm.NormalizeRangeBounds();
            Assert.That(vm.LengthMin, Is.EqualTo("18"));
        }

        [Test]
        public void TrailingFractionalZeros_StrippedOnNormalize()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "0.500";
            vm.WidthMax = "350.00"; // -> "350", trailing "." also gone
            vm.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMin, Is.EqualTo("0.5"));
                Assert.That(vm.WidthMax, Is.EqualTo("350"));
            });
        }

        [Test]
        public void SurroundingSpaces_TrimmedOnNormalize()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMax = " 700 ";
            vm.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMax, Is.EqualTo("700"));
                Assert.That(vm.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }));
            });
        }

        [Test]
        public void SpacesAndTrailingDot_CleanedOnNormalize()
        {
            SeedTwoParts(); // widths 400 and 300

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.WidthMax = " 350. ";
            vm.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.WidthMax, Is.EqualTo("350"));
                Assert.That(vm.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Бокова" }));
            });
        }

        [Test]
        public void LeadingDot_FormattedWithLeadingZero()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = ".5";
            vm.NormalizeRangeBounds();

            Assert.That(vm.LengthMin, Is.EqualTo("0.5"));
        }

        [Test]
        public void NegativeLeadingDot_FormattedWithLeadingZero()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "-.5";
            vm.NormalizeRangeBounds();

            Assert.That(vm.LengthMin, Is.EqualTo("-0.5"));
        }

        [Test]
        public void RedundantLeadingZeros_Stripped()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMax = "0700";
            vm.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMax, Is.EqualTo("700"));
                Assert.That(vm.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }));
            });
        }

        [Test]
        public void LeadingZeros_KeepSingleZeroBeforePoint()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            // one bound per pair, sibling blank -> canonicalization only, no equalize snap
            vm.LengthMin = "005";
            vm.WidthMax = "00.5";
            vm.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMin, Is.EqualTo("5"));
                Assert.That(vm.WidthMax, Is.EqualTo("0.5"));
            });
        }

        [Test]
        public void AllZeros_CollapseToSingleZero()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "000";
            vm.NormalizeRangeBounds();

            Assert.That(vm.LengthMin, Is.EqualTo("0"));
        }


        [Test]
        public void LengthMin_AboveLengthMax_RaisesMaxToMatch()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMax = "500";
            vm.LengthMin = "700";
            vm.NormalizeRangeBounds(); // debounce timer would do this after typing pauses

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMin, Is.EqualTo("700"));
                Assert.That(vm.LengthMax, Is.EqualTo("700"));
            });
        }

        [Test]
        public void LengthMax_BelowLengthMin_LowersMinToMatch()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "500";
            vm.LengthMax = "300";
            vm.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMin, Is.EqualTo("300"));
                Assert.That(vm.LengthMax, Is.EqualTo("300"));
            });
        }

        [Test]
        public void WidthMin_AboveWidthMax_RaisesMaxToMatch()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.WidthMax = "200";
            vm.WidthMin = "450";
            vm.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.WidthMin, Is.EqualTo("450"));
                Assert.That(vm.WidthMax, Is.EqualTo("450"));
            });
        }

        [Test]
        public void Equalize_SkippedWhenSiblingBlank()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "700"; // LengthMax left blank -> no snap
            vm.NormalizeRangeBounds();

            Assert.That(vm.LengthMax, Is.Empty);
        }

        [Test]
        public void Equalize_ThenFilterUsesSnappedBounds()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMax = "500";
            vm.LengthMin = "700"; // both snap to 700 -> nothing is exactly 700 long
            vm.NormalizeRangeBounds();

            Assert.That(vm.Parts, Is.Empty);
        }

        [Test]
        public void Equalize_MaxTypedBelowMin_DigitByDigit_SnapsToFullValue()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "100";
            vm.LengthMax = "5";  // first digit while typing "50"
            vm.LengthMax = "50"; // full value
            vm.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMin, Is.EqualTo("50"), "min must snap to the full max, not the first digit");
                Assert.That(vm.LengthMax, Is.EqualTo("50"));
            });
        }

        [Test]
        public void Equalize_MinTypedAboveMax_DigitByDigit_SnapsToFullValue()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMax = "40";
            vm.LengthMin = "9";
            vm.LengthMin = "90";
            vm.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMin, Is.EqualTo("90"));
                Assert.That(vm.LengthMax, Is.EqualTo("90"));
            });
        }

        [Test]
        public void NormalizeRangeBounds_LeavesValidRangeUntouched()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "100";
            vm.LengthMax = "500";
            vm.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMin, Is.EqualTo("100"));
                Assert.That(vm.LengthMax, Is.EqualTo("500"));
            });
        }

        [Test]
        public void ClearFilters_CancelsPendingEqualize()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.LengthMin = "700";
            vm.LengthMax = "500"; // inverted, snap still pending (timer not fired)

            vm.ClearFiltersCommand.Execute(null);
            vm.NormalizeRangeBounds(); // pending edge was cleared -> no-op

            Assert.Multiple(() =>
            {
                Assert.That(vm.LengthMin, Is.Empty);
                Assert.That(vm.LengthMax, Is.Empty);
                Assert.That(vm.WidthMin, Is.Empty);
                Assert.That(vm.WidthMax, Is.Empty);
                Assert.That(vm.Parts, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public void CloseFile_ResetsCheckedCount()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.CheckAllCommand.Execute(null);

            vm.CloseFileCommand.Execute(null);

            Assert.That(vm.CheckedCount, Is.EqualTo(0));
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
