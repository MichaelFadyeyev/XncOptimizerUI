using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.ViewModels.Machining;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="SelectedPartMachiningVM"/>: machining tables, program text and in-cell bore, groove, mill and variable edits.
    /// </summary>
    [TestFixture]
    public class SelectedPartMachiningVMTests : AppViewModelTestBase
    {
        [Test]
        public void SelectingPart_RendersItsXncProgramsAsBriefLines()
        {
            SeedTwoParts();
            Project.XncPrograms =
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
            Project.XncPrograms =
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
            Project.XncPrograms =
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
                Assert.That(vm.Machining.Grooves[0].StartX.Display, Is.EqualTo("1"));
                Assert.That(vm.Machining.Grooves[0].EndY.Display, Is.EqualTo("4"));
                Assert.That(vm.Machining.Grooves[0].Depth.Display, Is.EqualTo("5"));
                Assert.That(vm.Machining.Grooves[0].Width.Display, Is.EqualTo("6"));
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
            Project.XncPrograms = [];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];

            Assert.That(vm.Machining.ProgramsText, Is.EqualTo("Programs: none"));
        }

        [Test]
        public void ChangingSelectedPart_RefreshesTheProgramsText()
        {
            SeedTwoParts();
            Project.XncPrograms = [new XncProgram { Side = false, Dx = 800, Dy = 300, Dz = 16 }];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            vm.PartsList.SelectedPart = vm.PartsList.Parts[0];
            Assert.That(vm.Machining.ProgramsText, Does.Contain("/xnc/back/dx800 dy300 dz16"));

            vm.PartsList.SelectedPart = null;
            Assert.That(vm.Machining.ProgramsText, Is.EqualTo("Programs: -"));
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
            Project.XncPrograms = [ProgramWithBores((100, "100"), (200, "200"))];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            return vm;
        }

        [Test]
        public void EditingBoreX_CommitsExpression_SavesAndUpdatesRowInPlace()
        {
            var vm = OpenWithBores();
            Project.Editor.UpdateBore(default, default, default, default!).ReturnsForAnyArgs(Project.ChangingProgramsTo([ProgramWithBores((368, "dx-32"), (200, "200"))]));
            var row = vm.Machining.Bores[0];
            vm.Machining.SelectedBore = row;
            row.IsSelected = true;
            var savesBefore = Project.SaveCount;

            row.BeginCellEdit();
            row.X.Input = "dx-32";
            var committed = row.TryCommitCellEdit();

            Project.Editor.Received(1).UpdateBore(5, 0, MachiningAttribute.X, "dx-32");
            Assert.Multiple(() =>
            {
                Assert.That(committed, Is.True);
                Assert.That(Project.SaveCount, Is.EqualTo(savesBefore + 1));
                Assert.That(vm.Machining.Bores[0], Is.SameAs(row));
                Assert.That(vm.Machining.SelectedBore, Is.SameAs(row));
                Assert.That(row.X.Display, Is.EqualTo("368"));
                Assert.That(row.X.Input, Is.EqualTo("dx-32"));
                Assert.That(vm.Machining.CheckedBores, Is.EqualTo(new[] { row.Model }));
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
            Project.XncPrograms = [ProgramWithVariable("dz+2", 21, 21)];

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
            Project.XncPrograms = [ProgramWithVariable("dz+2", 21, 21)];
            Project.Editor.UpdateVariable(default, default, default, default!).ReturnsForAnyArgs(Project.ChangingProgramsTo([ProgramWithVariable("dz+4", 23, 23)]));

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var row = vm.Machining.Variables.Single();
            var savesBefore = Project.SaveCount;

            row.BeginCellEdit();
            row.ExprInput = "dz+4";
            var committed = row.TryCommitCellEdit();

            Project.Editor.Received(1).UpdateVariable(5, 0, XncVariableAttribute.Expr, "dz+4");
            Assert.Multiple(() =>
            {
                Assert.That(committed, Is.True);
                Assert.That(Project.SaveCount, Is.EqualTo(savesBefore + 1));
                Assert.That(vm.Machining.Variables.Single(), Is.SameAs(row));
                Assert.That((row.Expr, row.Value), Is.EqualTo(("dz+4", "23")));
                Assert.That(vm.Machining.Bores.Single().Depth.Display, Is.EqualTo("23"));
                Assert.That(vm.Log.Text, Does.Contain("Variable #1 expr = \"dz+4\", saved"));
            });
        }

        [Test]
        public void EditingVariable_WhenServiceRejects_SavesNothing()
        {
            SeedTwoParts();
            Project.XncPrograms = [ProgramWithVariable("dz+2", 21, 21)];
            Project.Editor.UpdateVariable(default, default, default, default!).ReturnsForAnyArgs(_ => Project.Result(changed: false));

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);
            var row = vm.Machining.Variables.Single();
            var savesBefore = Project.SaveCount;

            row.BeginCellEdit();
            row.TypeInput = "int";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(Project.SaveCount, Is.EqualTo(savesBefore));
            });
        }

        [Test]
        public void EditingBore_InvalidInput_HasErrorAndRefusesCommit()
        {
            var vm = OpenWithBores();
            var row = vm.Machining.Bores[0];

            row.BeginCellEdit();
            row.X.Input = "2*-3";

            Assert.Multiple(() =>
            {
                Assert.That(row.HasErrors, Is.True);
                Assert.That(row.X.GetErrors(nameof(ExpressionCellVM.Input)).Cast<string>().Single(), Does.Contain("sign"));
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(Project.Calls, Does.Not.Contain(nameof(IProjectEditor.UpdateBore)));
            });
        }

        [Test]
        public void EditingBore_Cancel_RestoresCommittedTextAndClearsError()
        {
            var vm = OpenWithBores();
            var row = vm.Machining.Bores[0];

            row.BeginCellEdit();
            row.X.Input = "1,5";
            row.CancelCellEdit();

            Assert.Multiple(() =>
            {
                Assert.That(row.X.Input, Is.EqualTo("100"));
                Assert.That(row.HasErrors, Is.False);
            });
        }

        [Test]
        public void EditingBore_UnchangedText_CommitsWithoutWriting()
        {
            var vm = OpenWithBores();
            var row = vm.Machining.Bores[0];

            row.BeginCellEdit();
            row.X.Input = " 100 ";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.True);
                Assert.That(Project.Calls, Does.Not.Contain(nameof(IProjectEditor.UpdateBore)));
            });
        }

        [Test]
        public void EditingBore_WhenServiceRejects_KeepsEditAndSavesNothing()
        {
            var vm = OpenWithBores();
            Project.Editor.UpdateBore(default, default, default, default!).ReturnsForAnyArgs(_ => Project.Result(changed: false));
            var row = vm.Machining.Bores[0];
            var savesBefore = Project.SaveCount;

            row.BeginCellEdit();
            row.Depth.Input = "dz";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(row.HasErrors, Is.True);
                Assert.That(row.Depth.Display, Is.EqualTo("10"));
                Assert.That(Project.SaveCount, Is.EqualTo(savesBefore));
            });
        }

        private static XncProgram ProgramWithGroovesAndRectangle(string secondGrooveY2Text, double secondGrooveY2) => new()
        {
            OperationId = 5,
            Side = true,
            Dx = 400,
            Dy = 800,
            Dz = 19,
            Tools = [new XncTool { Name = "Cut4", Diameter = 4 }],
            Groovings =
            [
                new XncGrooving
                {
                    ToolName = "Cut4", Start = new XncPoint(0, 50), End = new XncPoint(400, 50), Depth = 4, Width = 4,
                    Source = new XncElementSource(0, new Dictionary<string, string> { ["y2"] = "50" })
                },
                new XncGrooving
                {
                    ToolName = "Cut4", Start = new XncPoint(0, 100), End = new XncPoint(400, secondGrooveY2), Depth = 4, Width = 4,
                    Source = new XncElementSource(1, new Dictionary<string, string> { ["y2"] = secondGrooveY2Text })
                }
            ],
            MillingRectangles =
            [
                new XncMillingRectangle
                {
                    ToolName = "Cut4", Origin = new XncPoint(200, 400), Length = 100, Width = 40, Depth = 8,
                    Source = new XncElementSource(0, new Dictionary<string, string> { ["dp"] = "8" })
                }
            ]
        };

        private AppViewModel OpenWithGrooves()
        {
            SeedTwoParts();
            Project.XncPrograms = [ProgramWithGroovesAndRectangle("100", 100)];

            var vm = CreateViewModel();
            vm.OpenFileCommand.Execute(null);

            return vm;
        }

        [Test]
        public void EditingGrooveEndY_CommitsExpression_SavesAndUpdatesRowInPlace()
        {
            var vm = OpenWithGrooves();
            Project.Editor.UpdateMachiningElement(default, default, default, default, default!)
                .ReturnsForAnyArgs(Project.ChangingProgramsTo([ProgramWithGroovesAndRectangle("dy-35", 765)]));
            var row = vm.Machining.Grooves[1];
            var rectangleRow = vm.Machining.MillingRectangles[0];
            vm.Machining.SelectedGroove = row;
            row.IsSelected = true;
            var savesBefore = Project.SaveCount;

            row.BeginCellEdit();
            row.EndY.Input = "dy-35";
            var committed = row.TryCommitCellEdit();

            Project.Editor.Received(1).UpdateMachiningElement(5, XncElementKind.Groove, 1, MachiningAttribute.Y2, "dy-35");
            Assert.Multiple(() =>
            {
                Assert.That(committed, Is.True);
                Assert.That(Project.SaveCount, Is.EqualTo(savesBefore + 1));
                Assert.That(vm.Machining.Grooves[1], Is.SameAs(row));
                Assert.That(vm.Machining.SelectedGroove, Is.SameAs(row));
                Assert.That(vm.Machining.MillingRectangles[0], Is.SameAs(rectangleRow), "other tables untouched");
                Assert.That(row.EndY.Display, Is.EqualTo("765"));
                Assert.That(row.EndY.Input, Is.EqualTo("dy-35"));
                Assert.That(vm.Machining.CheckedGrooves, Is.EqualTo(new[] { row.Model }));
                Assert.That(vm.Machining.Programs.Single().Groovings[1].End.Y, Is.EqualTo(765));
                Assert.That(vm.Log.Text, Does.Contain("Groove #2 y2 = \"dy-35\", saved"));
            });
        }

        [Test]
        public void EditingRectangleDepth_AfterGrooveEdit_StillFindsItsOperation()
        {
            var vm = OpenWithGrooves();
            Project.Editor.UpdateMachiningElement(default, default, default, default, default!)
                .ReturnsForAnyArgs(Project.ChangingProgramsTo([ProgramWithGroovesAndRectangle("dy-35", 765)]));
            var groove = vm.Machining.Grooves[1];
            var rectangle = vm.Machining.MillingRectangles[0];

            groove.BeginCellEdit();
            groove.EndY.Input = "dy-35";
            groove.TryCommitCellEdit();
            rectangle.BeginCellEdit();
            rectangle.Depth.Input = "dz";
            var committed = rectangle.TryCommitCellEdit();

            Project.Editor.Received(1).UpdateMachiningElement(5, XncElementKind.Rectangle, 0, MachiningAttribute.Depth, "dz");
            Assert.Multiple(() =>
            {
                Assert.That(committed, Is.True);
                Assert.That(vm.Log.Text, Does.Contain("Rectangle #1 dp = \"dz\", saved"));
            });
        }

        [Test]
        public void EditingGroove_WhenServiceRejects_KeepsEditAndSavesNothing()
        {
            var vm = OpenWithGrooves();
            Project.Editor.UpdateMachiningElement(default, default, default, default, default!)
                .ReturnsForAnyArgs(_ => Project.Result(changed: false));
            var row = vm.Machining.Grooves[0];
            var savesBefore = Project.SaveCount;

            row.BeginCellEdit();
            row.Width.Input = "tool.dia*2";

            Assert.Multiple(() =>
            {
                Assert.That(row.TryCommitCellEdit(), Is.False);
                Assert.That(row.Width.HasErrors, Is.True);
                Assert.That(row.Width.Display, Is.EqualTo("4"));
                Assert.That(Project.SaveCount, Is.EqualTo(savesBefore));
            });
        }
    }
}
