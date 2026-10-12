using System.Xml.Linq;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.ViewModels.Parts;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.Services;
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
    public class AppViewModelTests : TempFixtureTestBase
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

        private AppViewModel CreateViewModel(TimeProvider? clock = null) =>
            TestViewModels.Create(_projectService.Services, _config, _dialogs, clock);

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
            vm.Preparation.Labels.NewLabelToProcess = "новий";

            vm.Preparation.Labels.AddNewLabelCommand.Execute(null);

            _config.Received(1).AddLabelToProcess("новий");

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

            _config.DidNotReceive().AddLabelToProcess(Arg.Any<string>());
        }

        [Test]
        public void CopyPartsList_WithNoParts_LogsAndDoesNotTouchClipboard()
        {
            var vm = CreateViewModel();

            vm.CopyPartsListCommand.Execute(null);

            _dialogs.DidNotReceive().SetClipboardText(Arg.Any<string>());
            Assert.That(vm.Log.Text, Does.Contain("No parts to copy!"));
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
        public void CopyPartsList_ForSplitSheet_SeparatorRowHasDashInNameColumn()
        {
            SeedTwoParts();
            _projectService.Sheets[0].Name = "ДСП Сращ.(2)";

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
            _projectService.Parts[0].Length = 600.5m;

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
            (string?)_dialogs.ReceivedCalls()
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
        public void ExecuteOptimize_WhenServiceFails_WarnsWithTheTextTheServiceLogged()
        {
            SeedTwoParts();
            _projectService.GroupIdenticalElementsResult = false;
            _projectService.ResultMessage = "File seems to be already optimized or contains no XNC.";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            _projectService.Calls.Clear();

            vm.Preparation.ExecuteOptimizeCommand.Execute(null);

            _dialogs.Received(1).ShowWarning(
                "File seems to be already optimized or contains no XNC.",
                Arg.Any<string>());

            // Releasing the stale SelectedPart before the service call may auto-save it
            // (UpdatePart/SaveProject) — that's expected. What must NOT happen is a
            // reload of the project after a failed GroupIdenticalElements call.
            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls[^1], Is.EqualTo(nameof(FakeProjectService.GroupIdenticalElements)),
                    "nothing should run after the failed GroupIdenticalElements call");
                Assert.That(_projectService.Calls, Does.Not.Contain(nameof(FakeProjectService.Open)),
                    "a failed optimize should not reload the project");
            });
        }

        [Test]
        public void ExecuteOptimize_WhenServiceSucceeds_DoesNotWarn()
        {
            SeedTwoParts();
            _projectService.GroupIdenticalElementsResult = true;
            _projectService.ResultMessage = "Stored to: C:\\out_opt.project";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Preparation.ExecuteOptimizeCommand.Execute(null);

            _dialogs.DidNotReceive().ShowWarning(Arg.Any<string>(), Arg.Any<string>());
            Assert.That(vm.Log.Text, Does.Contain("Stored to:"));
        }

        [Test]
        public void ExecuteOptimize_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.Preparation.ExecuteOptimizeCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No file selected!"));
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

            Assert.That(vm.PartsList.SelectedPart, Is.SameAs(vm.PartsList.Parts[0]));
        }

        [Test]
        public void OpeningProjectWithNoParts_LeavesSelectionNull()
        {
            _dialogs.ShowOpenProjectDialog().Returns(@"C:\empty.project");

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.That(vm.PartsList.SelectedPart, Is.Null);
        }

        [Test]
        public void SelectingAnotherPart_SavesTheOutgoingPart()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];
            _projectService.Calls.Clear();

            vm.PartsList.SelectedPart = vm.PartsList.Parts[1];

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.UpdatePart)));
                Assert.That(_projectService.SaveCount, Is.EqualTo(1));
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
            _projectService.UpdatePartResult = false;

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];
            vm.PartsList.SelectedPart = vm.PartsList.Parts[1];

            Assert.That(_projectService.SaveCount, Is.Zero);
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
            _projectService.Calls.Clear();

            typeof(PartVM).GetProperty(edge)!.SetValue(vm.PartsList.Parts[1], (int?)2);

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.UpdatePart)));
                Assert.That(_projectService.SaveCount, Is.EqualTo(1));
                Assert.That(vm.Log.Text, Does.Contain("Updates saved"));
            });
        }

        [Test]
        public void RemovingBand_SavesImmediately()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            _projectService.Calls.Clear();

            vm.PartsList.Parts[0].TopBandingId = null;

            Assert.That(_projectService.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void TogglingTxt_SavesImmediately()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            _projectService.Calls.Clear();

            vm.PartsList.Parts[1].ConsiderTexture = !vm.PartsList.Parts[1].ConsiderTexture;

            Assert.That(_projectService.SaveCount, Is.EqualTo(1));
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

            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];

            Assert.Multiple(() =>
            {
                Assert.That(vm.Machining.ProgramsText, Does.StartWith("Programs: 1"));
                Assert.That(vm.Machining.ProgramsText, Does.Contain("/xnc/front/dx1380 dy600 dz19"));
                Assert.That(vm.Machining.ProgramsText, Does.Contain("/tool/front/Bore8 Ø8"));
                Assert.That(vm.Machining.ProgramsText, Does.Contain("/bore/front/Left Bore8 (0,65,10) dp34"));
                Assert.That(vm.Machining.ProgramsText, Does.Contain("/groove/front/Cut3.2 (-10,565)-(1390,565) dp4 w10 Center"));
                Assert.That(vm.Machining.ProgramsText, Does.Contain("/mill/front/Mill6 (250,382.5) dp21 Left 2 arc"));
            });
        }

        [Test]
        public void SelectingPart_BuildsRowsForAllMachiningTablesAndListEntries()
        {
            SeedTwoParts();
            _projectService.XncPrograms =
            [
                new XncProgram
                {
                    Side = true,
                    Tools = [new XncTool { Name = "Mill6", Diameter = 6 }],
                    MillingContours =
                    [
                        new XncMillingContour
                        {
                            ToolName = "Mill6",
                            Entry = new XncPoint(10, 20),
                            EntryDepth = 3,
                            Position = ToolPosition.Center,
                            Segments = [new XncLineSegment { End = new XncPoint(70, 80) }]
                        }
                    ],
                    MillingEllipses =
                    [
                        new XncMillingEllipse
                        {
                            ToolName = "Mill6",
                            Center = new XncPoint(30, 40),
                            Length = 20,
                            Width = 10,
                            Depth = 2
                        }
                    ],
                    MillingRectangles =
                    [
                        new XncMillingRectangle
                        {
                            ToolName = "Mill6",
                            Origin = new XncPoint(50, 60),
                            Length = 30,
                            Width = 15,
                            Depth = 4
                        }
                    ]
                }
            ];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Machining.Tools, Has.Count.EqualTo(1));
                Assert.That(vm.Machining.MillingContours, Has.Count.EqualTo(1));
                Assert.That(vm.Machining.MillingContours[0].ExitX, Is.EqualTo("70"));
                Assert.That(vm.Machining.MillingContours[0].ExitY, Is.EqualTo("80"));
                Assert.That(vm.Machining.MillingContours[0].ToolToCenterLine, Is.EqualTo("C"));
                Assert.That(vm.Machining.MillingEllipses, Has.Count.EqualTo(1));
                Assert.That(vm.Machining.MillingRectangles, Has.Count.EqualTo(1));
                Assert.That(vm.Machining.ProgramsText, Does.Contain("/ellipse/front/Mill6 (30,40) 20x10 a0 dp2 Center"));
            });

            vm.Machining.MillingContours[0].IsSelected = true;
            Assert.That(vm.Machining.CheckedMillingContours, Has.Count.EqualTo(1));
            vm.Machining.MillingContours[0].IsSelected = false;
            Assert.That(vm.Machining.CheckedMillingContours, Is.Empty);
        }

        [Test]
        public void SelectingPart_BuildsNumberedGrooveRowsWithMappedSides()
        {
            SeedTwoParts();
            _projectService.XncPrograms =
            [
                new XncProgram
                {
                    Side = true,
                    Groovings =
                    [
                        new XncGrooving
                        {
                            SideCode = 0,
                            Start = new XncPoint(1, 2),
                            End = new XncPoint(3, 4),
                            Depth = 5,
                            Width = 6,
                            Position = ToolPosition.Center
                        },
                        new XncGrooving { SideCode = 1, Position = ToolPosition.Left },
                        new XncGrooving { SideCode = 2, Position = ToolPosition.Right },
                        new XncGrooving { SideCode = 3 },
                        new XncGrooving { SideCode = 4 }
                    ]
                },
                new XncProgram
                {
                    Side = false,
                    Groovings = [new XncGrooving { SideCode = 0 }]
                }
            ];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Machining.Grooves, Has.Count.EqualTo(6));
                Assert.That(vm.Machining.Grooves.Select(g => g.Number),
                    Is.EqualTo(Enumerable.Range(1, 6)));
                Assert.That(vm.Machining.Grooves.Select(g => g.Side),
                    Is.EqualTo(new[] { "Front", "Left", "Right", "Top", "Bottom", "Back" }));
                Assert.That(vm.Machining.Grooves[0].StartX, Is.EqualTo("1"));
                Assert.That(vm.Machining.Grooves[0].EndY, Is.EqualTo("4"));
                Assert.That(vm.Machining.Grooves[0].Depth, Is.EqualTo("5"));
                Assert.That(vm.Machining.Grooves[0].Width, Is.EqualTo("6"));
                Assert.That(vm.Machining.Grooves[0].ToolToCenterLine, Is.EqualTo("C"));
                Assert.That(vm.Machining.Grooves[1].ToolToCenterLine, Is.EqualTo("L"));
                Assert.That(vm.Machining.Grooves[2].ToolToCenterLine, Is.EqualTo("R"));
                Assert.That(vm.Machining.HasGrooves, Is.True);
            });

            vm.Machining.Grooves[0].IsSelected = true;
            Assert.That(vm.Machining.CheckedGrooves, Has.Count.EqualTo(1));
            Assert.That(vm.Machining.CheckedGrooves[0], Is.SameAs(vm.Machining.Grooves[0].Model));

            vm.Machining.Grooves[0].IsSelected = false;
            Assert.That(vm.Machining.CheckedGrooves, Is.Empty);
        }

        [Test]
        public void SelectingPart_WithNoPrograms_ShowsNone()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];

            Assert.That(vm.Machining.ProgramsText, Is.EqualTo("Programs: none"));
        }

        [Test]
        public void ChangingSelectedPart_RefreshesTheProgramsText()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [new XncProgram { Side = false, Dx = 800, Dy = 300, Dz = 16 }];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];
            Assert.That(vm.Machining.ProgramsText, Does.Contain("/xnc/back/dx800 dy300 dz16"));

            vm.PartsList.SelectedPart = null;
            Assert.That(vm.Machining.ProgramsText, Is.EqualTo("Programs: -"));
        }

        [Test]
        public void SelectingPart_WithPrograms_TakesDisplayDimsAndTurnFromFirstProgram()
        {
            SeedTwoParts(); // Полиця is 600 x 400
            _projectService.XncPrograms =
            [
                new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 1 },
                new XncProgram { Side = false, Dx = 111, Dy = 222, Dz = 19, Turn = 3 }
            ];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null); // auto-selects the first part

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.DisplayLength, Is.EqualTo(400)); // first program's dx, not Part.Length
                Assert.That(vm.Preview.DisplayWidth, Is.EqualTo(800));  // first program's dy
                Assert.That(vm.Preview.Turn, Is.EqualTo(1));
                Assert.That(vm.Preview.TurnText, Is.EqualTo("90°"));
            });
        }

        [Test]
        public void SelectingPart_WithNoPrograms_FallsBackToPartDimensionsAndZeroTurn()
        {
            SeedTwoParts(); // Полиця is 600 x 400
            _projectService.XncPrograms = [];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.DisplayLength, Is.EqualTo(600));
                Assert.That(vm.Preview.DisplayWidth, Is.EqualTo(400));
                Assert.That(vm.Preview.Turn, Is.Null);       // no XNC program -> no turn code
                Assert.That(vm.Preview.TurnText, Is.EqualTo("0°")); // preview still reads 0°
            });
        }

        [Test]
        public void SelectingPart_SyncsRotationButtonsWithoutRotating()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 2 }];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.Rotation, Is.EqualTo(PartTurn.Deg180));
                Assert.That(vm.Preview.CanRotate, Is.True);
                Assert.That(_projectService.Calls, Does.Not.Contain(nameof(FakeProjectService.RotatePart)));
            });
        }

        [Test]
        public void SelectingPart_WithNoPrograms_DisablesRotation()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.CanRotate, Is.False);
                Assert.That(vm.Preview.Rotation, Is.EqualTo(PartTurn.Deg0));
            });
        }

        [Test]
        public void ChangingRotation_RotatesSavesAndRefreshesPreview()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 0 }];
            _projectService.XncProgramsAfterRotate = [new XncProgram { Side = true, Dx = 800, Dy = 400, Dz = 19, Turn = 1 }];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var savesBefore = _projectService.SaveCount;

            vm.Preview.Rotation = PartTurn.Deg90;

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.LastRotatePartId, Is.EqualTo(vm.PartsList.SelectedPart!.Id));
                Assert.That(_projectService.LastRotateTargetTurn, Is.EqualTo(1));
                Assert.That(_projectService.SaveCount, Is.EqualTo(savesBefore + 1));
                Assert.That(vm.Preview.DisplayLength, Is.EqualTo(800));
                Assert.That(vm.Preview.DisplayWidth, Is.EqualTo(400));
                Assert.That(vm.Preview.TurnText, Is.EqualTo("90°"));
                Assert.That(vm.Preview.Rotation, Is.EqualTo(PartTurn.Deg90));
                Assert.That(vm.Log.Text, Does.Contain("rotated to 90°, saved"));
            });
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void ChangingRotation_PassesTclFlipFromOption(bool neverFlip, bool expectedFlip)
        {
            SeedTwoParts();
            _projectService.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 0 }];
            _config.NeverFlipTclOnTurn.Returns(neverFlip);

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.Preview.Rotation = PartTurn.Deg90;

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.NeverFlipTclOnTurn, Is.EqualTo(neverFlip));
                Assert.That(_projectService.LastRotateFlipEdgeGrooveTcl, Is.EqualTo(expectedFlip));
            });
        }

        [Test]
        public void ChangingNeverFlipTclOnTurn_PersistsToOptions()
        {
            var vm = CreateViewModel();

            vm.Preview.NeverFlipTclOnTurn = true;

            _config.Received(1).UpdateNeverFlipTclOnTurn(true);
        }

        private static XncProgram ProgramWithBores(params (double X, string XText)[] bores) => new()
        {
            OperationId = 5,
            Side = true,
            Dx = 400,
            Dy = 800,
            Dz = 19,
            Bores = bores.Select((b, i) => new XncBore
            {
                Surface = BoreSurface.Face,
                Index = i,
                X = b.X,
                XText = b.XText,
                Y = 50,
                YText = "50",
                Depth = 10,
                DepthText = "10"
            }).ToList()
        };

        private AppViewModel OpenWithBores()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [ProgramWithBores((100, "100"), (200, "200"))];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            return vm;
        }

        [Test]
        public void EditingBoreX_CommitsExpression_SavesAndUpdatesRowInPlace()
        {
            var vm = OpenWithBores();
            _projectService.XncProgramsAfterUpdateBore = [ProgramWithBores((368, "dx-32"), (200, "200"))];
            var row = vm.Machining.Bores[0];
            vm.Machining.SelectedBore = row;
            row.IsSelected = true;
            var savesBefore = _projectService.SaveCount;

            row.BeginCellEdit();
            row.XInput = "dx-32";
            var committed = row.TryCommitCellEdit();

            Assert.Multiple(() =>
            {
                Assert.That(committed, Is.True);
                Assert.That(_projectService.LastUpdateBore, Is.EqualTo((5, 0, BoreAttribute.X, "dx-32")));
                Assert.That(_projectService.SaveCount, Is.EqualTo(savesBefore + 1));
                Assert.That(vm.Machining.Bores[0], Is.SameAs(row));
                Assert.That(vm.Machining.SelectedBore, Is.SameAs(row));
                Assert.That(row.X, Is.EqualTo("368"));
                Assert.That(row.XInput, Is.EqualTo("dx-32"));
                Assert.That(vm.Machining.CheckedBores, Is.EqualTo(new[] { row.Bore }));
                Assert.That(vm.Machining.Programs.Single().Bores[0].X, Is.EqualTo(368));
                Assert.That(vm.Log.Text, Does.Contain("Bore #1 x = \"dx-32\", saved"));
            });
        }

        private static XncProgram ProgramWithVariable(string expr, double value, double boreDepth) => new()
        {
            OperationId = 5,
            Side = true,
            Dx = 400,
            Dy = 800,
            Dz = 19,
            DeclaredVariables =
            [
                new XncVariable { Index = 0, Name = "depth", Type = XncVariableType.Double, Expr = expr, Comment = "Глибина", Value = value }
            ],
            Variables = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["depth"] = value },
            Bores = [new XncBore { Surface = BoreSurface.Face, X = 100, Y = 50, Depth = boreDepth, DepthText = "depth" }]
        };

        [Test]
        public void SelectingPart_ListsVariablesInTableAndXncList()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [ProgramWithVariable("dz+2", 21, 21)];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Machining.HasVariables, Is.True);
                Assert.That(vm.Machining.Variables.Single().Name, Is.EqualTo("depth"));
                Assert.That(vm.Machining.ProgramsText, Does.Contain("/var/front/depth double = dz+2 (= 21) // Глибина"));
            });
        }

        [Test]
        public void EditingVariableExpr_SavesUpdatesRowInPlaceAndRebuildsDependents()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [ProgramWithVariable("dz+2", 21, 21)];
            _projectService.XncProgramsAfterUpdateVariable = [ProgramWithVariable("dz+4", 23, 23)];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var row = vm.Machining.Variables.Single();
            var savesBefore = _projectService.SaveCount;

            row.BeginCellEdit();
            row.ExprInput = "dz+4";
            var committed = row.TryCommitCellEdit();

            Assert.Multiple(() =>
            {
                Assert.That(committed, Is.True);
                Assert.That(_projectService.LastUpdateVariable, Is.EqualTo((5, 0, XncVariableAttribute.Expr, "dz+4")));
                Assert.That(_projectService.SaveCount, Is.EqualTo(savesBefore + 1));
                Assert.That(vm.Machining.Variables.Single(), Is.SameAs(row));
                Assert.That((row.Expr, row.Value), Is.EqualTo(("dz+4", "23")));
                Assert.That(vm.Machining.Bores.Single().Depth, Is.EqualTo("23"));
                Assert.That(vm.Log.Text, Does.Contain("Variable #1 expr = \"dz+4\", saved"));
            });
        }

        [Test]
        public void EditingVariable_WhenServiceRejects_SavesNothing()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [ProgramWithVariable("dz+2", 21, 21)];
            _projectService.UpdateVariableResult = false;

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var row = vm.Machining.Variables.Single();
            var savesBefore = _projectService.SaveCount;

            row.BeginCellEdit();
            row.TypeInput = "int";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(_projectService.SaveCount, Is.EqualTo(savesBefore));
            });
        }

        [Test]
        public void EditingBore_InvalidInput_HasErrorAndRefusesCommit()
        {
            var vm = OpenWithBores();
            var row = vm.Machining.Bores[0];

            row.BeginCellEdit();
            row.XInput = "2*-3";

            Assert.Multiple(() =>
            {
                Assert.That(row.HasErrors, Is.True);
                Assert.That(row.GetErrors(nameof(BoreRowVM.XInput)).Cast<string>().Single(), Does.Contain("sign"));
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(_projectService.Calls, Does.Not.Contain(nameof(FakeProjectService.UpdateBore)));
            });
        }

        [Test]
        public void EditingBore_Cancel_RestoresCommittedTextAndClearsError()
        {
            var vm = OpenWithBores();
            var row = vm.Machining.Bores[0];

            row.BeginCellEdit();
            row.XInput = "1,5";
            row.CancelCellEdit();

            Assert.Multiple(() =>
            {
                Assert.That(row.XInput, Is.EqualTo("100"));
                Assert.That(row.HasErrors, Is.False);
            });
        }

        [Test]
        public void EditingBore_UnchangedText_CommitsWithoutWriting()
        {
            var vm = OpenWithBores();
            var row = vm.Machining.Bores[0];

            row.BeginCellEdit();
            row.XInput = " 100 ";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.True);
                Assert.That(_projectService.Calls, Does.Not.Contain(nameof(FakeProjectService.UpdateBore)));
            });
        }

        [Test]
        public void EditingBore_WhenServiceRejects_KeepsEditAndSavesNothing()
        {
            var vm = OpenWithBores();
            _projectService.UpdateBoreResult = false;
            var row = vm.Machining.Bores[0];
            var savesBefore = _projectService.SaveCount;

            row.BeginCellEdit();
            row.DepthInput = "dz";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(row.HasErrors, Is.True);
                Assert.That(row.Depth, Is.EqualTo("10"));
                Assert.That(_projectService.SaveCount, Is.EqualTo(savesBefore));
            });
        }

        [Test]
        public void ChangingRotation_WhenRotationFails_RestoresButtonsAndSavesNothing()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 0 }];
            _projectService.RotatePartResult = false;

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var savesBefore = _projectService.SaveCount;

            vm.Preview.Rotation = PartTurn.Deg270;

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.Rotation, Is.EqualTo(PartTurn.Deg0));
                Assert.That(_projectService.SaveCount, Is.EqualTo(savesBefore));
            });
        }

        [Test]
        public void SelectingPart_ToNull_ClearsDisplayDimsAndTurn()
        {
            SeedTwoParts();
            _projectService.XncPrograms = [new XncProgram { Side = true, Dx = 400, Dy = 800, Dz = 19, Turn = 2 }];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.SelectedPart = null;

            Assert.Multiple(() =>
            {
                Assert.That(vm.Preview.DisplayLength, Is.Zero);
                Assert.That(vm.Preview.DisplayWidth, Is.Zero);
                Assert.That(vm.Preview.Turn, Is.Null);
                Assert.That(vm.Preview.TurnText, Is.Empty);
            });
        }

        [Test]
        public void OpeningProject_WithXncTurnDiscordance_WarnsAndLogsEachPartName()
        {
            SeedTwoParts();
            _projectService.PartsWithXncTurnDiscordance = ["Полиця", "Кришка"];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            _dialogs.Received(1).ShowWarning(
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

            _dialogs.DidNotReceive().ShowWarning(
                Arg.Is<string>(s => s.Contains("Discordance in xnc programs turn corner")),
                Arg.Any<string>());
        }

        [Test]
        public void ConvertGroovesAndMills_PassesCheckedPartsAndDirectionToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            vm.GrooveMill.GrooveMillDirection = GrooveMillDirection.MillsToGrooves;
            vm.GrooveMill.ProcessPockets = true;
            _projectService.Calls.Clear();

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.ConvertGroovesAndMills)));
                Assert.That(_projectService.LastGrooveMillParts!.Select(p => p.Id), Is.EqualTo(new[] { 100 }));
                Assert.That(_projectService.LastGrooveMillDirection, Is.EqualTo(GrooveMillDirection.MillsToGrooves));
                Assert.That(_projectService.LastGrooveMillProcessPockets, Is.True);
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
                    "a successful conversion reloads the project");
            });
        }

        [Test]
        public void ConvertGroovesAndMills_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            _projectService.ConvertGroovesAndMillsResult = false;
            _projectService.ResultMessage = "No grooves converted (ignored 2).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            _projectService.Calls.Clear();

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

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

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void ConvertGroovesAndMills_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.GrooveMill.ConvertGroovesAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No file selected!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void ConvertBoresAndMills_PassesCheckedPartsDirectionAndEllipseFlagToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            vm.BoreMill.BoreMillDirection = BoreMillDirection.MillsToBores;
            vm.BoreMill.ConvertBoresToEllipses = true;
            _projectService.Calls.Clear();

            vm.BoreMill.ConvertBoresAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.ConvertBoresAndMills)));
                Assert.That(_projectService.LastBoreMillParts!.Select(p => p.Id), Is.EqualTo(new[] { 100 }));
                Assert.That(_projectService.LastBoreMillDirection, Is.EqualTo(BoreMillDirection.MillsToBores));
                Assert.That(_projectService.LastBoreMillUseEllipse, Is.True);
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
                    "a successful conversion reloads the project");
            });
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
            _projectService.ConvertBoresAndMillsResult = false;
            _projectService.ResultMessage = "No bores converted (ignored 0).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            _projectService.Calls.Clear();

            vm.BoreMill.ConvertBoresAndMillsCommand.Execute(null);

            _dialogs.Received(1).ShowWarning("No bores converted (ignored 0).", Arg.Any<string>());
            Assert.That(_projectService.Calls, Is.EqualTo(new[] { nameof(FakeProjectService.ConvertBoresAndMills) }),
                "a failed conversion should not reload the project");
        }

        [Test]
        public void ConvertBoresAndMills_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            _projectService.Calls.Clear();

            vm.BoreMill.ConvertBoresAndMillsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked"));
                Assert.That(_projectService.Calls, Is.Empty);
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
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void OptimizeMillTraversal_PassesCheckedPartsToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            _projectService.Calls.Clear();

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.OptimizeMillTraversal)));
                Assert.That(_projectService.LastMillTraversalParts!.Select(p => p.Id), Is.EqualTo(new[] { 100 }));
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
                    "a successful optimization reloads the project");
            });
        }

        [Test]
        public void OptimizeMillTraversal_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            _projectService.OptimizeMillTraversalResult = false;
            _projectService.ResultMessage = "No mill passes reordered (ignored 0).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            _projectService.Calls.Clear();

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

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

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked for mill order optimization!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void SortToolDeclarations_PassesCheckedPartsToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            _projectService.Calls.Clear();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.SortToolDeclarations)));
                Assert.That(_projectService.LastToolSortParts!.Select(p => p.Id), Is.EqualTo(new[] { 100 }));
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
                    "a successful sort reloads the project");
            });
        }

        [Test]
        public void SortToolDeclarations_AppliedGloballyWithoutFilter_PassesAllPartsEvenUnchecked()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.SortTools.SortToolsGlobally = true;
            _projectService.Calls.Clear();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            Assert.That(_projectService.LastToolSortParts!.Select(p => p.Id), Is.EquivalentTo(new[] { 100, 101 }));
        }

        [Test]
        public void SortToolDeclarations_AppliedGloballyWithFilter_PassesOnlyFilteredPartsEvenUnchecked()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var hidden = vm.PartsList.Parts.Single(p => p.Id == 101);
            hidden.IsSelected = true;
            vm.Filter.FilterName = vm.PartsList.Parts.Single(p => p.Id == 100).Name;
            vm.SortTools.SortToolsGlobally = true;
            _projectService.Calls.Clear();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            Assert.That(_projectService.LastToolSortParts!.Select(p => p.Id), Is.EqualTo(new[] { 100 }),
                "the filter decides the scope; a checked part hidden by the filter is not sorted");
        }

        [Test]
        public void SortToolDeclarations_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            _projectService.SortToolDeclarationsResult = false;
            _projectService.ResultMessage = "No tool declarations reordered (skipped 0 program(s)).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            _projectService.Calls.Clear();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            _dialogs.Received(1).ShowWarning("No tool declarations reordered (skipped 0 program(s)).", Arg.Any<string>());
            Assert.That(_projectService.Calls, Is.EqualTo(new[] { nameof(FakeProjectService.SortToolDeclarations) }),
                "a failed sort should not reload the project");
        }

        [Test]
        public void SortToolDeclarations_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            _projectService.Calls.Clear();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked for tool sorting!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void SortToolDeclarations_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.SortTools.SortToolDeclarationsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No file selected!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_PassesCheckedPartsOffsetAndSideToService_ThenReloads()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[1].IsSelected = true;
            vm.MillOffset.MillOffset = "2.5";
            vm.MillOffset.MillOffsetSide = MillOffsetSide.Left;
            _projectService.Calls.Clear();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.OffsetMillPaths)));
                Assert.That(_projectService.LastMillOffsetParts!.Select(p => p.Id), Is.EqualTo(new[] { vm.PartsList.Parts[1].Id }));
                Assert.That(_projectService.LastMillOffset, Is.EqualTo(2.5d));
                Assert.That(_projectService.LastMillOffsetSide, Is.EqualTo(MillOffsetSide.Left));
                Assert.That(_projectService.LastMillOffsetKinds, Is.EqualTo(MillPathKinds.All));
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
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
            _projectService.Calls.Clear();

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(_projectService.LastCleanOutsideParts!.Select(p => p.Id), Is.EqualTo(new[] { vm.PartsList.Parts[1].Id }));
                Assert.That(_projectService.LastCleanOutsideKinds, Is.EqualTo(OutsideOperationKinds.Bores | OutsideOperationKinds.Mills));
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.Open)),
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

            Assert.That(_projectService.LastCleanOutsideParts!.Select(p => p.Id),
                Is.EquivalentTo(_projectService.Parts.Select(p => p.Id)), "all parts of the project, not only the filtered ones");
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
            _projectService.Calls.Clear();

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("Select bores, grooves and/or mills to clean!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void CleanOutsideOperations_WhenServiceFails_DoesNotReload()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            _projectService.CleanOutsideOperationsResult = false;
            _projectService.Calls.Clear();

            vm.CleanOutside.CleanOutsideOperationsCommand.Execute(null);

            Assert.That(_projectService.Calls, Is.EqualTo(new[] { nameof(FakeProjectService.CleanOutsideOperations) }));
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

            Assert.That(_projectService.LastMillOffsetKinds, Is.EqualTo(expected));
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
            _projectService.Calls.Clear();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("Select open and/or closed mill paths to offset!"));
                Assert.That(_projectService.Calls, Is.Empty);
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
            _projectService.Calls.Clear();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("Enter a mill path offset greater than 0!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_WithNoPartsChecked_LogsAndDoesNotCallService()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.MillOffset.MillOffset = "3";
            _projectService.Calls.Clear();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No parts checked for mill path offset!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_WhenServiceFails_WarnsWithLoggedTextAndDoesNotReload()
        {
            SeedTwoParts();
            _projectService.OffsetMillPathsResult = false;
            _projectService.ResultMessage = "No mill paths offset (ignored 2).";

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.Parts[0].IsSelected = true;
            vm.MillOffset.MillOffset = "3";
            _projectService.Calls.Clear();

            vm.MillOffset.OffsetMillPathsCommand.Execute(null);

            _dialogs.Received(1).ShowWarning("No mill paths offset (ignored 2).", Arg.Any<string>());
            Assert.That(_projectService.Calls, Is.EqualTo(new[] { nameof(FakeProjectService.OffsetMillPaths) }),
                "a failed offset should not reload the project");
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
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void OptimizeMillTraversal_WithNoFileOpen_LogsAndDoesNothing()
        {
            var vm = CreateViewModel();

            vm.MillOrder.OptimizeMillTraversalCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.Log.Text, Does.Contain("No file selected!"));
                Assert.That(_projectService.Calls, Is.Empty);
            });
        }

        [Test]
        public void CheckAll_ChecksEveryVisiblePart_AndCheckedCountReflectsIt()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.CheckAllCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.Parts.Select(p => p.IsSelected), Is.All.True);
                Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void UncheckAll_UnchecksEveryVisiblePart()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            vm.PartsList.CheckAllCommand.Execute(null);

            vm.PartsList.UncheckAllCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.Parts.Select(p => p.IsSelected), Is.All.False);
                Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(0));
            });
        }

        [Test]
        public void CheckedCount_UpdatesOnIndividualToggle_AndRaisesPropertyChanged()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            var raised = 0;
            vm.PartsList.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PartsListVM.CheckedCount)) raised++;
            };

            vm.PartsList.Parts[0].IsSelected = true;
            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(1));
                Assert.That(raised, Is.GreaterThanOrEqualTo(1));
            });

            vm.PartsList.Parts[0].IsSelected = false;
            Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(0));
        }

        [Test]
        public void CheckAll_WithActiveFilter_OnlyChecksFilteredParts()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.FilterName = "Полиця"; // setter runs FilterParts -> only part 100 stays visible
            Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(1));

            vm.PartsList.CheckAllCommand.Execute(null);
            Assert.That(vm.PartsList.CheckedCount, Is.EqualTo(1), "the filtered-out part must not be checked");

            vm.Filter.ClearFiltersCommand.Execute(null);
            var reappeared = vm.PartsList.Parts.Single(p => p.Name == "Бокова");
            Assert.That(reappeared.IsSelected, Is.False);
        }

        [Test]
        public void LengthMin_ExcludesShorterParts()
        {
            SeedTwoParts(); // Полиця 600x400, Бокова 800x300

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "700";

            Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Бокова" }));
        }

        [Test]
        public void LengthRange_KeepsPartsInsideBounds()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "550";
            vm.Filter.LengthMax = "650";

            Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }));
        }

        [Test]
        public void WidthMax_ExcludesWiderParts()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.WidthMax = "350";

            Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Бокова" }));
        }

        [Test]
        public void RangeAndName_CombineAsAnd()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.FilterName = "Бокова";
            vm.Filter.LengthMin = "100";
            vm.Filter.LengthMax = "900";
            Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(1));

            vm.Filter.LengthMax = "500"; // Бокова is 800 long -> now excluded
            Assert.That(vm.PartsList.Parts, Is.Empty);
        }

        [Test]
        public void ClearFilters_ResetsRangeFields()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "550";
            vm.Filter.LengthMax = "650";
            vm.Filter.WidthMin = "100";
            vm.Filter.WidthMax = "450";
            Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(1));

            vm.Filter.ClearFiltersCommand.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(2));
                Assert.That(vm.Filter.LengthMin, Is.Empty);
                Assert.That(vm.Filter.LengthMax, Is.Empty);
                Assert.That(vm.Filter.WidthMin, Is.Empty);
                Assert.That(vm.Filter.WidthMax, Is.Empty);
            });
        }

        [Test]
        public void InvalidDecimalString_IsIgnored()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "abc";

            Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(2), "unparseable bound must not filter anything");
        }

        [Test]
        public void DotSeparator_ParsedAsDecimal()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "700.5";

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("700.5"), "typed text is kept verbatim");
                Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }),
                    "700.5 must parse as a decimal, so only the 600-long part passes");
            });
        }

        [Test]
        public void CommaSeparator_IsInvalid_NotConverted()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "700,5";

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("700,5"), "comma is left verbatim, never rewritten to a dot");
                Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(2), "\"700,5\" does not parse, so it filters nothing");
            });
        }

        [Test]
        public void HalfTypedDecimal_IsNotReformatted()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "18."; // mid-typing "18.5"

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("18."), "trailing separator must survive the setter so the next digit lands; truncation is commit-time only");
                Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(2), "\"18.\" parses as 18, both parts are longer");
            });
        }

        [Test]
        public void CommaSeparator_SurvivesCanonicalizationUnchanged()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "18,5";
            vm.Filter.NormalizeRangeBounds(); // canonicalization must not touch the comma either

            Assert.That(vm.Filter.LengthMin, Is.EqualTo("18,5"));
        }

        [Test]
        public void TrailingSeparator_TruncatedOnNormalize()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "18.";
            Assert.That(vm.Filter.LengthMin, Is.EqualTo("18."), "not yet - still mid-edit");

            vm.Filter.NormalizeRangeBounds();
            Assert.That(vm.Filter.LengthMin, Is.EqualTo("18"));
        }

        [Test]
        public void TrailingFractionalZeros_StrippedOnNormalize()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "0.500";
            vm.Filter.WidthMax = "350.00"; // -> "350", trailing "." also gone
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("0.5"));
                Assert.That(vm.Filter.WidthMax, Is.EqualTo("350"));
            });
        }

        [Test]
        public void SurroundingSpaces_TrimmedOnNormalize()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = " 700 ";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("700"));
                Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }));
            });
        }

        [Test]
        public void SpacesAndTrailingDot_CleanedOnNormalize()
        {
            SeedTwoParts(); // widths 400 and 300

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.WidthMax = " 350. ";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.WidthMax, Is.EqualTo("350"));
                Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Бокова" }));
            });
        }

        [Test]
        public void LeadingDot_FormattedWithLeadingZero()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = ".5";
            vm.Filter.NormalizeRangeBounds();

            Assert.That(vm.Filter.LengthMin, Is.EqualTo("0.5"));
        }

        [Test]
        public void NegativeLeadingDot_FormattedWithLeadingZero()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "-.5";
            vm.Filter.NormalizeRangeBounds();

            Assert.That(vm.Filter.LengthMin, Is.EqualTo("-0.5"));
        }

        [Test]
        public void RedundantLeadingZeros_Stripped()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "0700";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("700"));
                Assert.That(vm.PartsList.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Полиця" }));
            });
        }

        [Test]
        public void LeadingZeros_KeepSingleZeroBeforePoint()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            // one bound per pair, sibling blank -> canonicalization only, no equalize snap
            vm.Filter.LengthMin = "005";
            vm.Filter.WidthMax = "00.5";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("5"));
                Assert.That(vm.Filter.WidthMax, Is.EqualTo("0.5"));
            });
        }

        [Test]
        public void AllZeros_CollapseToSingleZero()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "000";
            vm.Filter.NormalizeRangeBounds();

            Assert.That(vm.Filter.LengthMin, Is.EqualTo("0"));
        }


        [Test]
        public void LengthMin_AboveLengthMax_RaisesMaxToMatch()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "500";
            vm.Filter.LengthMin = "700";
            vm.Filter.NormalizeRangeBounds(); // debounce timer would do this after typing pauses

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("700"));
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("700"));
            });
        }

        [Test]
        public void LengthMax_BelowLengthMin_LowersMinToMatch()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "500";
            vm.Filter.LengthMax = "300";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("300"));
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("300"));
            });
        }

        [Test]
        public void WidthMin_AboveWidthMax_RaisesMaxToMatch()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.WidthMax = "200";
            vm.Filter.WidthMin = "450";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.WidthMin, Is.EqualTo("450"));
                Assert.That(vm.Filter.WidthMax, Is.EqualTo("450"));
            });
        }

        [Test]
        public void Equalize_SkippedWhenSiblingBlank()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "700"; // LengthMax left blank -> no snap
            vm.Filter.NormalizeRangeBounds();

            Assert.That(vm.Filter.LengthMax, Is.Empty);
        }

        [Test]
        public void Equalize_ThenFilterUsesSnappedBounds()
        {
            SeedTwoParts(); // Полиця 600 long, Бокова 800 long

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "500";
            vm.Filter.LengthMin = "700"; // both snap to 700 -> nothing is exactly 700 long
            vm.Filter.NormalizeRangeBounds();

            Assert.That(vm.PartsList.Parts, Is.Empty);
        }

        [Test]
        public void Equalize_MaxTypedBelowMin_DigitByDigit_SnapsToFullValue()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "100";
            vm.Filter.LengthMax = "5";  // first digit while typing "50"
            vm.Filter.LengthMax = "50"; // full value
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("50"), "min must snap to the full max, not the first digit");
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("50"));
            });
        }

        [Test]
        public void Equalize_MinTypedAboveMax_DigitByDigit_SnapsToFullValue()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMax = "40";
            vm.Filter.LengthMin = "9";
            vm.Filter.LengthMin = "90";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("90"));
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("90"));
            });
        }

        [Test]
        public void NormalizeRangeBounds_LeavesValidRangeUntouched()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "100";
            vm.Filter.LengthMax = "500";
            vm.Filter.NormalizeRangeBounds();

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.EqualTo("100"));
                Assert.That(vm.Filter.LengthMax, Is.EqualTo("500"));
            });
        }

        [Test]
        public void ClearFilters_CancelsPendingEqualize()
        {
            SeedTwoParts();

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.Filter.LengthMin = "700";
            vm.Filter.LengthMax = "500"; // inverted, snap still pending (timer not fired)

            vm.Filter.ClearFiltersCommand.Execute(null);
            vm.Filter.NormalizeRangeBounds(); // pending edge was cleared -> no-op

            Assert.Multiple(() =>
            {
                Assert.That(vm.Filter.LengthMin, Is.Empty);
                Assert.That(vm.Filter.LengthMax, Is.Empty);
                Assert.That(vm.Filter.WidthMin, Is.Empty);
                Assert.That(vm.Filter.WidthMax, Is.Empty);
                Assert.That(vm.PartsList.Parts, Has.Count.EqualTo(2));
            });
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
                Assert.That(_projectService.Calls, Does.Contain(nameof(FakeProjectService.Close)));
                Assert.That(vm.Workspace.FullPath, Is.Empty);
                Assert.That(vm.PartsList.Parts, Is.Empty);
                Assert.That(vm.Catalog.Bands, Is.Empty);
                Assert.That(vm.PartsList.SelectedPart, Is.Null);
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
