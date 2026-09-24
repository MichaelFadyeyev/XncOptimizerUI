using System.Xml.Linq;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// These were impossible before DI: GibLabProjectService popped modal
    /// MessageBox dialogs from inside its algorithms, so a headless test host hung
    /// rather than failed, and SawWidth was read from a static ConfigService that
    /// touched the developer's real %AppData% file.
    /// </summary>
    [TestFixture]
    public class GibLabProjectServiceTests
    {
        private string _directory = string.Empty;
        private string _projectPath = string.Empty;
        private IConfigService _config = null!;

        /// <summary>Frozen so the description timestamp written into saved XML is deterministic.</summary>
        private static readonly TimeProvider _clock =
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 30, 13, 45, 0, TimeSpan.Zero));

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<IConfigService>();
            _config.SawWidth.Returns(4.0m);
            _config.MillingToolDiams.Returns(new List<decimal> { 6.0m, 10.0m, 20.0m });

            _directory = Path.Combine(Path.GetTempPath(), "XncOptimizerUI.Test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);

            // Work on a copy so the fixture is never mutated.
            var source = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "td.project");
            _projectPath = Path.Combine(_directory, "td.project");
            File.Copy(source, _projectPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private GibLabProjectService CreateService() => new(_config, _clock);

        /// <summary>Copies a named fixture into the per-test temp dir and returns its path.</summary>
        private string CopyFixture(string name)
        {
            var source = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", name);
            var dest = Path.Combine(_directory, name);
            File.Copy(source, dest);

            return dest;
        }

        /// <summary>
        /// Opens the fixture and reads in the order the service requires: ReadParts
        /// resolves banding names and sheet ids against the caches that ReadBands and
        /// ReadSheets populate, so calling it first throws.
        /// </summary>
        private static GibLabProjectService OpenAndRead(GibLabProjectService service, string path)
        {
            service.OpenProject(path);
            service.ReadBands();
            service.ReadSheets();

            return service;
        }

        [Test]
        public void OpenProject_ReadsPartsBandsAndSheets()
        {
            var service = OpenAndRead(CreateService(), _projectPath);

            var parts = service.ReadParts();

            Assert.Multiple(() =>
            {
                Assert.That(service.FullPath, Is.EqualTo(_projectPath));
                Assert.That(service.ReadSheets(), Is.Not.Empty);
                Assert.That(parts, Is.Not.Empty);
                Assert.That(parts.Select(p => p.Id), Is.Unique);
            });
        }

        [Test]
        public void GroupIdenticalElements_RunsHeadlessAndReportsThroughTheLog()
        {
            var service = OpenAndRead(CreateService(), _projectPath);

            var log = string.Empty;

            // The point of the test: this call used to block on MessageBox.Show.
            var result = service.GroupIdenticalElements(ref log);

            Assert.That(log, Is.Not.Empty, "the service must report through the log, not a dialog");

            if (result)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(log, Does.Contain("Stored to:"));
                    Assert.That(service.FullPath, Does.EndWith("_opt.project"));
                    Assert.That(File.Exists(service.FullPath), Is.True);
                });
            }
            else
            {
                Assert.That(log, Does.Contain("already optimized").Or.Contains("no XNC"));
            }
        }

        [Test]
        public void GroupIdenticalElements_WhenRunTwice_ReportsAlreadyOptimized()
        {
            var service = OpenAndRead(CreateService(), _projectPath);

            var log = string.Empty;

            if (!service.GroupIdenticalElements(ref log))
            {
                Assert.Ignore("The fixture is already optimized; the second-pass case cannot be exercised.");
            }

            // Re-open the file the first pass produced and optimize it again.
            var second = OpenAndRead(CreateService(), service.FullPath);

            var secondLog = string.Empty;
            var result = second.GroupIdenticalElements(ref secondLog);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(secondLog, Does.Contain("File seems to be already optimized or contains no XNC."));
            });
        }

        [Test]
        public void GroupIdenticalElements_DoesNotTouchSourceFileOnDisk()
        {
            var path = CopyFixture("td-execute-optimize.project");
            var bytesBefore = File.ReadAllBytes(path);

            var service = OpenAndRead(CreateService(), path);
            var log = string.Empty;

            var result = service.GroupIdenticalElements(ref log);

            var bytesAfter = File.ReadAllBytes(path);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(bytesAfter, Is.EqualTo(bytesBefore),
                    "GroupIdenticalElements must save the grouped result to a new _opt file, never overwrite the source it read");
                Assert.That(service.FullPath, Does.EndWith("_opt.project"));
            });
        }

        [Test]
        public void GetPartsWithXncTurnDiscordance_ReturnsOnlyMultiProgramPartsWithDisagreeingTurn()
        {
            // Fixture: panel-A has two XNC ops with turn 0 and turn 1 (discordant);
            // panel-B has two ops both turn 2 (consistent); panel-C has a single op.
            var path = CopyFixture("td-turn-discordance.project");
            var service = OpenAndRead(CreateService(), path);

            Assert.That(service.GetPartsWithXncTurnDiscordance(), Is.EqualTo(new[] { "panel-A" }));
        }

        [Test]
        public void GetPartsWithXncTurnDiscordance_WithNoProjectOpen_ReturnsEmpty()
        {
            Assert.That(CreateService().GetPartsWithXncTurnDiscordance(), Is.Empty);
        }

        [Test]
        public void ReplaceXncPrograms_WithNoTargets_ReturnsFalseAndLogs()
        {
            var service = OpenAndRead(CreateService(), _projectPath);

            var sourcePart = service.ReadParts().First();
            var log = string.Empty;

            var result = service.ReplaceXncPrograms(ref log, sourcePart, []);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No target parts selected for XNC replacement."));
            });
        }

        [Test]
        public void UpdatePart_WithUnchangedValues_ReturnsFalse()
        {
            var service = OpenAndRead(CreateService(), _projectPath);

            var part = service.ReadParts().First();
            var log = string.Empty;

            Assert.That(service.UpdatePart(ref log, part), Is.False,
                "an unmodified part is not a change and should not be written");
        }

        [Test]
        public void UpdatePart_WithNewDimensions_ReturnsTrueAndPersists()
        {
            var service = OpenAndRead(CreateService(), _projectPath);

            var part = service.ReadParts().First();
            var newLength = part.Length + 25m;
            part.Length = newLength;

            var log = string.Empty;

            Assert.That(service.UpdatePart(ref log, part), Is.True);

            service.SaveProject();

            var reopened = OpenAndRead(CreateService(), _projectPath);

            Assert.That(reopened.ReadParts().First(p => p.Id == part.Id).Length, Is.EqualTo(newLength));
        }

        [Test]
        public void PrepForSplitAlongX_UsesInjectedSawWidth()
        {
            var service = OpenAndRead(CreateService(), _projectPath);

            var part = service.ReadParts().First();
            var originalWidth = part.Width;
            var log = string.Empty;

            service.PrepForSplitAlongX(ref log, [part.Id.ToString()]);

            var updated = service.ReadParts().First(p => p.Id == part.Id);

            Assert.Multiple(() =>
            {
                // Width is derived from dw, so assert the direction rather than an
                // exact value; the kerf itself is asserted via the injected config.
                Assert.That(updated.Width, Is.GreaterThan(originalWidth));
                Assert.That(updated.Count, Is.LessThanOrEqualTo(part.Count));
            });

            _ = _config.Received().SawWidth;
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_ConvertsGrooveAddsBoreToolAndDropsOrphan()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-grooving.project"));

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("_gm.project"));
                Assert.That(File.Exists(service.FullPath), Is.True);
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("1 tool(s) added"));
                Assert.That(log, Does.Contain("1 tool(s) removed"));
            });

            var program = service.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.Groovings, Is.Empty);
                Assert.That(program.MillingContours, Has.Count.EqualTo(1));
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(10d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Mill10"));
                // Cut3.2 was the groove's only user; after conversion nothing references it.
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Cut3.2"));
            });

            var contour = program.MillingContours.Single();
            var segment = contour.Segments.Single();

            Assert.Multiple(() =>
            {
                // groove ran along X at y=50; overshoot = tool.dia/2 = width/2 = 5
                Assert.That(contour.Entry.X, Is.EqualTo(-5d));    // x1=-10 -> -5
                Assert.That(contour.Entry.Y, Is.EqualTo(50d));
                Assert.That(segment.End.X, Is.EqualTo(10005d));   // x2=dx+10 -> dx+5
                Assert.That(segment.End.Y, Is.EqualTo(50d));
                Assert.That(contour.EntryDepth, Is.EqualTo(4d));
                Assert.That(contour.Position, Is.EqualTo(ToolPosition.Right)); // groove c="1"
            });
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_DoesNotReuseUnrelatedSameDiameterTool()
        {
            var service = CreateService();
            var path = CopyFixture("td-grooving.project");
            var xml = File.ReadAllText(path)
                .Replace("&lt;tool name=&quot;Cut3.2&quot; d=&quot;3.2&quot;/&gt;",
                    "&lt;tool name=&quot;Bore10&quot; d=&quot;10&quot;/&gt;&lt;tool name=&quot;Cut3.2&quot; d=&quot;3.2&quot;/&gt;");
            File.WriteAllText(path, xml);
            service.OpenProject(path);

            var log = string.Empty;
            Assert.That(service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }],
                GrooveMillDirection.GroovesToMills, processPockets: false), Is.True);

            var program = service.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours.Single().ToolName, Is.EqualTo("Mill10"));
                Assert.That(program.Tools.Single(t => t.Name == "Bore10").Diameter, Is.EqualTo(10d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Bore10"));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Mill10"));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_KeepsToolStillUsedByAnUnconvertedGroove()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-grooving-mixed.project"));

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("0 tool(s) removed"));
            });

            var program = service.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours, Has.Count.EqualTo(1));
                Assert.That(program.Groovings, Has.Count.EqualTo(1), "the diagonal groove is left untouched");
                // Cut3.2 is still referenced by that diagonal groove, so it must survive.
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Cut3.2"));
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(10d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_ConvertsShallowSingleSegmentMill()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-milling-shallow.project"));

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.MillsToGrooves, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("_gm.project"));
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Cut2.8
                Assert.That(log, Does.Contain("1 tool(s) removed")); // orphaned Mill6
            });

            var program = service.ReadXncPrograms(2).Single();

            Assert.That(program.MillingContours, Is.Empty);

            var groove = program.Groovings.Single();

            Assert.Multiple(() =>
            {
                // the groove is cut by the fixed 2.8 mm grooving tool, not the mill's Mill6
                Assert.That(groove.ToolName, Is.EqualTo("Cut2.8"));
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(2.8d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Mill6"));
                Assert.That(groove.Width, Is.EqualTo(6d));   // t = original mill (Mill6) diameter
                Assert.That(groove.Depth, Is.EqualTo(5d));
                Assert.That(groove.Position, Is.EqualTo(ToolPosition.Left)); // ms c="2"
                // entry x=-20 and end x=1020 lie outside [0,1000] -> clamped onto the edge
                Assert.That(groove.Start.X, Is.EqualTo(0d));
                Assert.That(groove.Start.Y, Is.EqualTo(80d));
                Assert.That(groove.End.X, Is.EqualTo(1000d));
                Assert.That(groove.End.Y, Is.EqualTo(80d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_DoesNotReuseUnrelatedSameDiameterTool()
        {
            var service = CreateService();
            var path = CopyFixture("td-milling-shallow.project");
            var xml = File.ReadAllText(path)
                .Replace("&lt;tool name=&quot;Mill6&quot; d=&quot;6&quot;/&gt;",
                    "&lt;tool name=&quot;Bore2.8&quot; d=&quot;2.8&quot;/&gt;&lt;tool name=&quot;Mill6&quot; d=&quot;6&quot;/&gt;");
            File.WriteAllText(path, xml);
            service.OpenProject(path);

            var log = string.Empty;
            Assert.That(service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }],
                GrooveMillDirection.MillsToGrooves, processPockets: false), Is.True);

            var program = service.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.Groovings.Single().ToolName, Is.EqualTo("Cut2.8"));
                Assert.That(program.Tools.Single(t => t.Name == "Bore2.8").Diameter, Is.EqualTo(2.8d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Bore2.8"));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Cut2.8"));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_IgnoresThroughMillAndRectangle()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-milling.project"));
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.MillsToGrooves, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mills converted"));
                Assert.That(log, Does.Contain("ignored 2")); // through-mill contour + <mr>
                Assert.That(service.FullPath, Is.EqualTo(pathBefore), "nothing converted => nothing saved");
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_WithProcessPockets_ConvertsAxisParallelRectangularPockets()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-milling-pocket.project"));

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.MillsToGrooves, processPockets: true);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("_gm.project"));
                Assert.That(File.Exists(service.FullPath), Is.True);
                Assert.That(log, Does.Contain("converted 2"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Cut2.8
                Assert.That(log, Does.Contain("ignored 3"));         // rotated + frame + through-depth
            });

            var program = service.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.Groovings, Has.Count.EqualTo(2));
                Assert.That(program.MillingRectangles, Has.Count.EqualTo(3), "rotated / non-pocket / through-depth rectangles are left alone");
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(2.8d));
            });

            // #1: l=200 >= w=30 -> groove along X, width = w, centred on y
            var alongX = program.Groovings[0];
            // #2: w=160 > l=25 -> groove along Y, width = l, centred on x
            var alongY = program.Groovings[1];

            Assert.Multiple(() =>
            {
                Assert.That(alongX.ToolName, Is.EqualTo("Cut2.8"));
                Assert.That(alongX.Start.X, Is.EqualTo(200d));
                Assert.That(alongX.Start.Y, Is.EqualTo(150d));
                Assert.That(alongX.End.X, Is.EqualTo(400d));
                Assert.That(alongX.End.Y, Is.EqualTo(150d));
                Assert.That(alongX.Width, Is.EqualTo(30d));
                Assert.That(alongX.Depth, Is.EqualTo(6d));
                Assert.That(alongX.Position, Is.EqualTo(ToolPosition.Center));

                Assert.That(alongY.Start.X, Is.EqualTo(700d));
                Assert.That(alongY.Start.Y, Is.EqualTo(70d));
                Assert.That(alongY.End.X, Is.EqualTo(700d));
                Assert.That(alongY.End.Y, Is.EqualTo(230d));
                Assert.That(alongY.Width, Is.EqualTo(25d));
                Assert.That(alongY.Depth, Is.EqualTo(5d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_WithoutProcessPockets_LeavesRectanglesAlone()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-milling-pocket.project"));
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.MillsToGrooves, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mills converted"));
                Assert.That(log, Does.Contain("ignored 5"));
                Assert.That(service.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.ReadXncPrograms(2).Single().MillingRectangles, Has.Count.EqualTo(5));
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_WithProcessPockets_ConvertsRectangularContourPockets()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-pocket-to-groove.project"));

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 1, Name = "402.07.01.ПАН-640" }], GrooveMillDirection.MillsToGrooves, processPockets: true);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("_gm.project"));
                Assert.That(File.Exists(service.FullPath), Is.True);
                Assert.That(log, Does.Contain("converted 2"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Cut2.8
                Assert.That(log, Does.Contain("1 tool(s) removed")); // orphaned Mill6
                Assert.That(log, Does.Contain("ignored 0"));
            });

            var program = service.ReadXncPrograms(1).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours, Is.Empty);
                Assert.That(program.Groovings, Has.Count.EqualTo(2));
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(2.8d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Mill6"));
            });

            // contour 1: rect X in [120,130], Y in [-5,1385] -> groove along Y, centred x=125,
            // ends clamped onto the part borders [0, dy=1380]
            var alongY = program.Groovings[0];
            // contour 2: rect X in [-5,645], Y in [550,560] -> groove along X, centred y=555,
            // ends clamped onto the part borders [0, dx=640]
            var alongX = program.Groovings[1];

            Assert.Multiple(() =>
            {
                Assert.That(alongY.ToolName, Is.EqualTo("Cut2.8"));
                Assert.That(alongY.Start.X, Is.EqualTo(125d));
                Assert.That(alongY.Start.Y, Is.EqualTo(0d));
                Assert.That(alongY.End.X, Is.EqualTo(125d));
                Assert.That(alongY.End.Y, Is.EqualTo(1380d));
                Assert.That(alongY.Width, Is.EqualTo(10d));
                Assert.That(alongY.Depth, Is.EqualTo(4d));
                Assert.That(alongY.Position, Is.EqualTo(ToolPosition.Center));
                Assert.That(alongY.Comment, Is.EqualTo("Виїмка G=4 (В4)"));

                Assert.That(alongX.Start.X, Is.EqualTo(0d));
                Assert.That(alongX.Start.Y, Is.EqualTo(555d));
                Assert.That(alongX.End.X, Is.EqualTo(640d));
                Assert.That(alongX.End.Y, Is.EqualTo(555d));
                Assert.That(alongX.Width, Is.EqualTo(10d));
                Assert.That(alongX.Depth, Is.EqualTo(4d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_WithoutProcessPockets_LeavesContourPocketsAlone()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-pocket-to-groove.project"));
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 1, Name = "402.07.01.ПАН-640" }], GrooveMillDirection.MillsToGrooves, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mills converted"));
                Assert.That(log, Does.Contain("ignored 2"));
                Assert.That(service.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.ReadXncPrograms(1).Single().MillingContours, Has.Count.EqualTo(2));
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_IgnoresSecondaryPassGroove()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-grooving-secondary-pass.project"));
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No grooves converted"));
                Assert.That(log, Does.Contain("ignored 1"));
                Assert.That(service.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.ReadXncPrograms(2).Single().Groovings, Has.Count.EqualTo(1),
                "a p!=0 (secondary-pass) groove must be left untouched");
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_IgnoresDiagonalGroove()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-grooving-diagonal.project"));
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No grooves converted"));
                Assert.That(log, Does.Contain("ignored 1"));
                Assert.That(service.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.ReadXncPrograms(2).Single().Groovings, Has.Count.EqualTo(1),
                "a diagonal groove must be left untouched");
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_OffSizeWidth_ConvertsToMillRectangleWithSmallestCutter()
        {
            var service = CreateService();
            // t=10 groove, but the shop only has Ø6 and Ø20 cutters -> milled out as a pocket.
            _config.MillingToolDiams.Returns(new List<decimal> { 6.0m, 20.0m });
            service.OpenProject(CopyFixture("td-grooving.project"));

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("_gm.project"));
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Mill6
                Assert.That(log, Does.Contain("1 tool(s) removed")); // orphaned Cut3.2
            });

            var program = service.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.Groovings, Is.Empty);
                Assert.That(program.MillingContours, Is.Empty);
                Assert.That(program.MillingRectangles, Has.Count.EqualTo(1));
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(6d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Cut3.2"));
            });

            var rect = program.MillingRectangles.Single();

            Assert.Multiple(() =>
            {
                Assert.That(rect.ToolName, Is.EqualTo("Mill6"));
                Assert.That(rect.Position, Is.EqualTo(ToolPosition.Pocket));   // c="3"
                // groove ran along X at y=50; ends x1=-10 / x2=dx+10 are outside [0,10000]
                // -> pushed a further toolDiam/2 = 3 past each edge: [-3, 10003], length 10006.
                Assert.That(rect.Length, Is.EqualTo(10006d));
                Assert.That(rect.Width, Is.EqualTo(10d));                     // short side = groove width t
                Assert.That(rect.Origin.X, Is.EqualTo(5000d));               // rectangle centre
                Assert.That(rect.Origin.Y, Is.EqualTo(50d));
                Assert.That(rect.Depth, Is.EqualTo(4d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_OffSizeWidth_InteriorGroove_RectangleIsNotExtended()
        {
            var service = CreateService();
            _config.MillingToolDiams.Returns(new List<decimal> { 6.0m, 20.0m });
            service.OpenProject(CopyFixture("td-grooving-interior.project"));

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false);

            Assert.That(result, Is.True);

            var rect = service.ReadXncPrograms(2).Single().MillingRectangles.Single();

            Assert.Multiple(() =>
            {
                // groove x1=100 / x2=900 are both inside [0,1000] -> no overshoot.
                Assert.That(rect.Length, Is.EqualTo(800d));
                Assert.That(rect.Width, Is.EqualTo(10d));
                Assert.That(rect.Origin.X, Is.EqualTo(500d));
                Assert.That(rect.Origin.Y, Is.EqualTo(250d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_SmallestCutterWiderThanGroove_IgnoresGroove()
        {
            var service = CreateService();
            // Only a Ø20 cutter available; the t=10 groove cannot be milled cleanly.
            _config.MillingToolDiams.Returns(new List<decimal> { 20.0m });
            service.OpenProject(CopyFixture("td-grooving.project"));
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No grooves converted"));
                Assert.That(log, Does.Contain("ignored 1"));
                Assert.That(service.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.ReadXncPrograms(2).Single().Groovings, Has.Count.EqualTo(1),
                "a groove with no fitting cutter must be left untouched");
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_NoMillingToolsConfigured_CancelsOperation()
        {
            var service = CreateService();
            _config.MillingToolDiams.Returns(new List<decimal>());
            service.OpenProject(CopyFixture("td-grooving.project"));
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No milling tools configured"));
                Assert.That(service.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.ReadXncPrograms(2).Single().Groovings, Has.Count.EqualTo(1));
        }

        [Test]
        public void ConvertGroovesAndMills_WithNoParts_ReturnsFalseAndLogs()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-grooving.project"));

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(ref log, [], GrooveMillDirection.GroovesToMills, processPockets: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No parts selected for groove/mill conversion."));
            });
        }

        [Test]
        public void OptimizeMillTraversal_ReordersParallelPassesIntoSerpentineOrder()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-mills-optimization.project"));

            var log = string.Empty;
            var result = service.OptimizeMillTraversal(ref log, [new Part { Id = 1, Name = "as-is" }]);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("_mo.project"));
                Assert.That(File.Exists(service.FullPath), Is.True);
                Assert.That(log, Does.Contain("reordered 3 pass(es)"));
                Assert.That(log, Does.Contain("Stored to:"));
            });

            var optimised = service.ReadXncPrograms(1).Single();
            var oracle = service.ReadXncPrograms(2).Single(); // the hand-authored "optimized" part

            Assert.That(optimised.MillingContours, Has.Count.EqualTo(6));

            // The pass order (by Y) is preserved; only the entry/exit ends of passes 2, 4 and 6
            // are swapped so every pass starts where the previous one finished.
            var expectedEntryX = new[] { -5d, 1385d, -5d, 1385d, -5d, 1385d };
            var expectedY = new[] { 5d, 45d, 85d, 125d, 165d, 205d };

            for (var i = 0; i < 6; i++)
            {
                var contour = optimised.MillingContours[i];
                var end = contour.Segments.Single().End;
                var index = i;

                Assert.Multiple(() =>
                {
                    Assert.That(contour.Entry.Y, Is.EqualTo(expectedY[index]));
                    Assert.That(contour.Entry.X, Is.EqualTo(expectedEntryX[index]));
                    Assert.That(end.X, Is.EqualTo(expectedEntryX[index] == -5d ? 1385d : -5d));
                    Assert.That(end.Y, Is.EqualTo(expectedY[index]));

                    // identical to the hand-authored oracle part in the same fixture
                    Assert.That(contour.Entry.X, Is.EqualTo(oracle.MillingContours[index].Entry.X));
                    Assert.That(end.X, Is.EqualTo(oracle.MillingContours[index].Segments.Single().End.X));
                });
            }
        }

        [Test]
        public void OptimizeMillTraversal_AlreadyOptimised_ReturnsFalseAndSavesNothing()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-mills-optimization.project"));
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.OptimizeMillTraversal(ref log, [new Part { Id = 2, Name = "optimized" }]);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mill passes reordered"));
                Assert.That(service.FullPath, Is.EqualTo(pathBefore), "nothing reordered => nothing saved");
            });
        }

        [Test]
        public void OptimizeMillTraversal_WithNoParts_ReturnsFalseAndLogs()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-mills-optimization.project"));

            var log = string.Empty;
            var result = service.OptimizeMillTraversal(ref log, []);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No parts selected for mill traversal optimization."));
            });
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_LargeBoreBecomesClosedTwoArcContour()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-bores-large.project"));

            var log = string.Empty;
            var result = service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("_bm.project"));
                Assert.That(File.Exists(service.FullPath), Is.True);
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Mill6
                Assert.That(log, Does.Contain("1 tool(s) removed")); // orphaned Bore40
            });

            var program = service.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours, Has.Count.EqualTo(1));
                Assert.That(program.Bores.Select(b => b.ToolName), Is.EqualTo(new[] { "Bore8" }), "the small bore is left alone");
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Mill6"));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Bore40"));
            });

            var contour = program.MillingContours.Single();

            Assert.Multiple(() =>
            {
                Assert.That(contour.ToolName, Is.EqualTo("Mill6"));
                Assert.That(contour.Position, Is.EqualTo(ToolPosition.Pocket)); // Bore40 dp=12 < dz=19 => blind => c="3"
                Assert.That(contour.EntryDepth, Is.EqualTo(12d));
                Assert.That(contour.Entry.X, Is.EqualTo(220d));  // cx + r = 200 + 20
                Assert.That(contour.Entry.Y, Is.EqualTo(300d));
                Assert.That(contour.Segments, Has.Count.EqualTo(2));
                Assert.That(contour.Segments, Is.All.InstanceOf<XncArcSegment>());
            });

            var arcs = contour.Segments.Cast<XncArcSegment>().ToList();

            Assert.Multiple(() =>
            {
                Assert.That(arcs[0].Center.X, Is.EqualTo(200d));
                Assert.That(arcs[0].Center.Y, Is.EqualTo(300d));
                Assert.That(arcs[0].Radius, Is.EqualTo(20d).Within(1e-6));
                Assert.That(arcs[0].End.X, Is.EqualTo(180d));             // opposite point
                Assert.That(arcs[1].End.X, Is.EqualTo(220d));             // closes onto the entry
                Assert.That(arcs[1].End.Y, Is.EqualTo(300d));
                Assert.That(arcs.Select(a => a.Depth), Is.All.EqualTo(12d));
            });

            // Curved paths are emitted clockwise as dir="true".
            Assert.That(File.ReadAllText(service.FullPath), Does.Contain("dir=&quot;true&quot;"));
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_ReusesExistingMill6Tool()
        {
            var service = CreateService();
            var path = CopyFixture("td-bores-large.project");
            var xml = File.ReadAllText(path)
                .Replace("&lt;tool name=&quot;Bore40&quot; d=&quot;40&quot;/&gt;",
                    "&lt;tool name=&quot;Mill6&quot; d=&quot;6&quot;/&gt;&lt;tool name=&quot;Bore40&quot; d=&quot;40&quot;/&gt;");
            File.WriteAllText(path, xml);
            service.OpenProject(path);

            var log = string.Empty;
            Assert.That(service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: false), Is.True);

            Assert.That(log, Does.Contain("0 tool(s) added"));

            var program = service.ReadXncPrograms(2).Single();
            Assert.That(program.Tools.Count(t => t.Name == "Mill6"), Is.EqualTo(1));
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_CustomVariableAsBoreDepth()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-bore-depth-variable.project"));

            var log = string.Empty;
            var result = service.ConvertBoresAndMills(
                ref log, [new Part { Id = 1, Name = "312.09.02.ПАН-1" }], BoreMillDirection.BoresToMills, useEllipse: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(log, Does.Contain("converted 1"));
            });

            var program = service.ReadXncPrograms(1).Single();
            var contour = program.MillingContours.Single();

            Assert.Multiple(() =>
            {
                Assert.That(contour.ToolName, Is.EqualTo("Mill6"));
                // dz=18, dp="throughBoreDepth" (var expr dz+2.00=20) >= dz => through, not a pocket.
                Assert.That(contour.Position, Is.EqualTo(ToolPosition.Right));
                Assert.That(contour.EntryDepth, Is.EqualTo(20d));
                Assert.That(contour.Entry.X, Is.EqualTo(334d)); // cx + r = 300 + 34 (DX/2=300, Bore68 r=34)
                Assert.That(contour.Entry.Y, Is.EqualTo(150d)); // DY/2 = 150
                Assert.That(contour.Segments, Has.Count.EqualTo(2));
                Assert.That(contour.Segments, Is.All.InstanceOf<XncArcSegment>());
            });

            var arcs = contour.Segments.Cast<XncArcSegment>().ToList();

            Assert.Multiple(() =>
            {
                Assert.That(arcs, Is.All.Matches<XncArcSegment>(a => a.Center.X == 300d && a.Center.Y == 150d));
                Assert.That(arcs[0].End.X, Is.EqualTo(266d)); // opposite point
                Assert.That(arcs[1].End.X, Is.EqualTo(334d)); // closes onto the entry
            });
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_UseEllipse_BlindBoreBecomesEllipticalPocket()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-bores-large.project")); // Bore40 dp="12", dz="19" => blind

            var log = string.Empty;
            Assert.That(service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: true), Is.True);

            var saved = File.ReadAllText(service.FullPath);

            Assert.Multiple(() =>
            {
                Assert.That(saved, Does.Contain("&lt;me "));
                Assert.That(saved, Does.Contain("l=&quot;20&quot;"), "l/w are semi-axes: Ø40 -> l = w = 20");
                Assert.That(saved, Does.Contain("w=&quot;20&quot;"));
                Assert.That(saved, Does.Contain("c=&quot;3&quot;"), "a blind bore is milled as a pocket");
                Assert.That(saved, Does.Contain("sxy=&quot;tool.dia/2&quot;"));
                Assert.That(saved, Does.Contain("fwd=&quot;true&quot;"));
                Assert.That(saved, Does.Contain("name=&quot;Mill6&quot;"));
                Assert.That(saved, Does.Not.Contain("Bore40"), "the orphaned bore tool is dropped");
            });

            var program = service.ReadXncPrograms(2).Single();
            Assert.That(program.Bores.Select(b => b.ToolName), Is.EqualTo(new[] { "Bore8" }));
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_UseEllipse_ThroughBoreKeepsRightPosition()
        {
            var service = CreateService();
            var path = CopyFixture("td-bores-large.project");
            // Push the large bore's depth through the panel (dz="19").
            var xml = File.ReadAllText(path).Replace("dp=&quot;12&quot;", "dp=&quot;19&quot;");
            File.WriteAllText(path, xml);
            service.OpenProject(path);

            var log = string.Empty;
            Assert.That(service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: true), Is.True);

            var saved = File.ReadAllText(service.FullPath);

            Assert.Multiple(() =>
            {
                Assert.That(saved, Does.Contain("&lt;me "));
                Assert.That(saved, Does.Contain("c=&quot;1&quot;"), "a through bore keeps the right-of-centre-line position");
                Assert.That(saved, Does.Not.Contain("c=&quot;3&quot;"));
            });
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_NoLargeBores_ReturnsFalseAndSavesNothing()
        {
            var service = CreateService();
            var path = CopyFixture("td-bores-large.project");
            var xml = File.ReadAllText(path).Replace("d=&quot;40&quot;", "d=&quot;30&quot;");
            File.WriteAllText(path, xml);
            service.OpenProject(path);
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No bores converted"));
                Assert.That(service.FullPath, Is.EqualTo(pathBefore));
            });
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_ClosedArcContourBecomesFaceBore()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-mill-circle.project"));

            var log = string.Empty;
            var result = service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("_bm.project"));
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Bore40
                Assert.That(log, Does.Contain("1 tool(s) removed")); // orphaned Mill6
            });

            var program = service.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours, Is.Empty);
                Assert.That(program.Bores, Has.Count.EqualTo(1));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Bore40"));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Mill6"));
            });

            var bore = program.Bores.Single();

            Assert.Multiple(() =>
            {
                Assert.That(bore.Surface, Is.EqualTo(BoreSurface.Face));
                Assert.That(bore.ToolName, Is.EqualTo("Bore40"));
                Assert.That(bore.X, Is.EqualTo(250d));
                Assert.That(bore.Y, Is.EqualTo(300d));
                Assert.That(bore.Depth, Is.EqualTo(15d));
                Assert.That(program.Tools.Single(t => t.Name == "Bore40").Diameter, Is.EqualTo(40d));
            });
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_RoundEllipseBecomesFaceBore()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-mill-ellipse.project"));

            var log = string.Empty;
            var result = service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false);

            Assert.That(result, Is.True);

            var program = service.ReadXncPrograms(2).Single();
            var bore = program.Bores.Single();

            Assert.Multiple(() =>
            {
                Assert.That(bore.ToolName, Is.EqualTo("Bore40"));
                Assert.That(bore.X, Is.EqualTo(250d));
                Assert.That(bore.Y, Is.EqualTo(300d));
                Assert.That(bore.Depth, Is.EqualTo(15d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Mill6"));
            });
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_NonClosedContourIsIgnored()
        {
            var service = CreateService();
            var path = CopyFixture("td-mill-circle.project");
            // Move the final arc endpoint off the entry point: the contour no longer closes.
            var xml = File.ReadAllText(path)
                .Replace("&lt;mac x=&quot;270&quot; y=&quot;300&quot; cx=&quot;250&quot; cy=&quot;300&quot; dp=&quot;15&quot; dir=&quot;false&quot;/&gt;&lt;/program&gt;",
                    "&lt;mac x=&quot;265&quot; y=&quot;300&quot; cx=&quot;250&quot; cy=&quot;300&quot; dp=&quot;15&quot; dir=&quot;false&quot;/&gt;&lt;/program&gt;");
            File.WriteAllText(path, xml);
            service.OpenProject(path);
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mills converted"));
                Assert.That(service.FullPath, Is.EqualTo(pathBefore));
            });
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_ContourWithStraightSegmentIsIgnored()
        {
            var service = CreateService();
            var path = CopyFixture("td-mill-circle.project");
            var xml = File.ReadAllText(path)
                .Replace("&lt;mac x=&quot;270&quot; y=&quot;300&quot; cx=&quot;250&quot; cy=&quot;300&quot; dp=&quot;15&quot; dir=&quot;false&quot;/&gt;&lt;/program&gt;",
                    "&lt;ml x=&quot;270&quot; y=&quot;300&quot; dp=&quot;15&quot;/&gt;&lt;/program&gt;");
            File.WriteAllText(path, xml);
            service.OpenProject(path);

            var log = string.Empty;
            Assert.That(service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false), Is.False);
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_UnderSizeCircleIsIgnored()
        {
            var service = CreateService();
            var path = CopyFixture("td-mill-circle.project");
            // Shrink the circle to radius 15 (Ø30, below the 35 mm threshold).
            var xml = File.ReadAllText(path)
                .Replace("x=&quot;270&quot;", "x=&quot;265&quot;")
                .Replace("x=&quot;230&quot;", "x=&quot;235&quot;");
            File.WriteAllText(path, xml);
            service.OpenProject(path);

            var log = string.Empty;
            Assert.That(service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false), Is.False);
        }

        [Test]
        public void ConvertBoresAndMills_RoundTrip_RestoresBoreCentreDiameterAndDepth()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-bores-large.project"));

            var log = string.Empty;
            Assert.That(service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: false), Is.True);

            service.OpenProject(service.FullPath);
            log = string.Empty;
            Assert.That(service.ConvertBoresAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false), Is.True);

            var program = service.ReadXncPrograms(2).Single();
            var restored = program.Bores.Single(b => b.ToolName == "Bore40");

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours, Is.Empty);
                Assert.That(restored.X, Is.EqualTo(200d));
                Assert.That(restored.Y, Is.EqualTo(300d));
                Assert.That(restored.Depth, Is.EqualTo(12d));
                Assert.That(program.Tools.Single(t => t.Name == "Bore40").Diameter, Is.EqualTo(40d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Mill6"));
                Assert.That(program.Bores.Select(b => b.ToolName), Does.Contain("Bore8"));
            });
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_RichFixture_EllipsesAndArcContoursBecomeBores()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-br-ml-conversion.project"));

            var log = string.Empty;
            var result = service.ConvertBoresAndMills(
                ref log,
                [new Part { Id = 2, Name = "Mill-Ellipse" }, new Part { Id = 5, Name = "Mill-Path" }],
                BoreMillDirection.MillsToBores,
                useEllipse: false);

            Assert.That(result, Is.True);

            var ellipsePart = service.ReadXncPrograms(2).SelectMany(p => p.Bores).ToList();
            var pathPart = service.ReadXncPrograms(5).SelectMany(p => p.Bores).ToList();

            Assert.Multiple(() =>
            {
                // Two <me> on the front face + one on the back.
                Assert.That(ellipsePart, Has.Count.EqualTo(3));
                Assert.That(ellipsePart, Is.All.Matches<XncBore>(b => b.ToolName == "Bore40" && b.Surface == BoreSurface.Face));
                // Two <mac> circles on the front face + one <ma> circle on the back.
                Assert.That(pathPart, Has.Count.EqualTo(3));
                Assert.That(pathPart, Is.All.Matches<XncBore>(b => b.ToolName == "Bore40"));
                Assert.That(service.ReadXncPrograms(2).SelectMany(p => p.MillingContours), Is.Empty);
                Assert.That(service.ReadXncPrograms(5).SelectMany(p => p.MillingContours), Is.Empty);
            });

            var backEllipse = service.ReadXncPrograms(2).Single(p => !p.Side).Bores.Single();
            Assert.Multiple(() =>
            {
                Assert.That(backEllipse.X, Is.EqualTo(300d));  // dx/2
                Assert.That(backEllipse.Y, Is.EqualTo(150d));  // dy/2
                Assert.That(backEllipse.Depth, Is.EqualTo(15d));
            });
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_RichFixture_EveryLargeBoreBecomesATwoArcContour()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-br-ml-conversion.project"));

            var log = string.Empty;
            var result = service.ConvertBoresAndMills(
                ref log, [new Part { Id = 3, Name = "Bore" }], BoreMillDirection.BoresToMills, useEllipse: false);

            Assert.That(result, Is.True);

            var contours = service.ReadXncPrograms(3).SelectMany(p => p.MillingContours).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(contours, Has.Count.EqualTo(3));  // 2 front + 1 back
                Assert.That(contours, Is.All.Matches<XncMillingContour>(
                    c => c.ToolName == "Mill6" && c.Segments.Count == 2));
                // dz=18: the dp=20 bore is through (c="1" Right), the two dp=15 bores are blind (c="3" Pocket).
                Assert.That(contours.Count(c => c.Position == ToolPosition.Right), Is.EqualTo(1));
                Assert.That(contours.Count(c => c.Position == ToolPosition.Pocket), Is.EqualTo(2));
                Assert.That(service.ReadXncPrograms(3).SelectMany(p => p.Bores), Is.Empty);
            });
        }

        [Test]
        public void ConvertBoresAndMills_WithNoParts_ReturnsFalseAndLogs()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-bores-large.project"));

            var log = string.Empty;
            var result = service.ConvertBoresAndMills(ref log, [], BoreMillDirection.BoresToMills, useEllipse: false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No parts selected for bore/mill conversion."));
            });
        }

        // --- Mill path offset (TestData/td-mill-offset.project: one part per case, ids 2..14) ---

        private const string MillOffsetFixture = "td-mill-offset.project";

        /// <summary>Offsets the given parts of the mill-offset fixture; returns the service, result and log.</summary>
        private (GibLabProjectService Service, bool Result, string Log) OffsetMills(
            double offset, MillOffsetSide side, params int[] partIds) =>
            OffsetMills(offset, side, MillPathKinds.All, partIds);

        private (GibLabProjectService Service, bool Result, string Log) OffsetMills(
            double offset, MillOffsetSide side, MillPathKinds kinds, params int[] partIds)
        {
            var service = CreateService();
            service.OpenProject(CopyFixture(MillOffsetFixture));

            var log = string.Empty;
            var result = service.OffsetMillPaths(
                ref log, partIds.Select(id => new Part { Id = id, Name = $"part-{id}" }).ToList(), offset, side, kinds);

            return (service, result, log);
        }

        /// <summary>Open-contour parts of the fixture; every other part (6..14) holds a closed contour, rectangle or ellipse.</summary>
        private static readonly int[] OpenMillParts = [2, 3, 4, 5, 9];

        [Test]
        public void OffsetMillPaths_OpenKindOnly_OffsetsOpenPathsAndSkipsClosedMills()
        {
            var (service, result, log) = OffsetMills(3, MillOffsetSide.Right, MillPathKinds.Open, Enumerable.Range(2, 13).ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(log, Does.Contain("offset 5 open mill(s) by 3 mm R, ignored 0, skipped 8"));
                Assert.That(ReadSavedProgram(service.FullPath, 2).Element("ms")!.Attribute("y")!.Value, Is.EqualTo("303"));
                Assert.That(Points(ReadSavedProgram(service.FullPath, 6)),
                    Is.EqualTo(new[] { "100,100", "300,100", "300,200", "100,200", "100,100" }), "closed contour untouched");
                Assert.That(ReadSavedProgram(service.FullPath, 10).Element("mr")!.Attribute("l")!.Value, Is.EqualTo("100"), "rectangle untouched");
                Assert.That(ReadSavedProgram(service.FullPath, 12).Element("me")!.Attribute("l")!.Value, Is.EqualTo("20"), "ellipse untouched");
            });
        }

        [Test]
        public void OffsetMillPaths_ClosedKindOnly_OffsetsClosedMillsAndSkipsOpenPaths()
        {
            var (service, result, log) = OffsetMills(3, MillOffsetSide.Right, MillPathKinds.Closed, Enumerable.Range(2, 13).ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(log, Does.Contain("offset 7 closed mill(s) by 3 mm R, ignored 1, skipped 5"));
                Assert.That(OpenMillParts.Select(id => ReadSavedProgram(service.FullPath, id).Element("ms")!.Attribute("y")!.Value),
                    Is.EqualTo(new[] { "300", "300", "100", "100", "100" }), "open paths untouched");
                Assert.That(Points(ReadSavedProgram(service.FullPath, 6))[0], Is.EqualTo("97,97"));
                Assert.That(ReadSavedProgram(service.FullPath, 10).Element("mr")!.Attribute("l")!.Value, Is.EqualTo("106"));
            });
        }

        [Test]
        public void OffsetMillPaths_OnlyUnselectedKind_ReturnsFalseAndSavesNothing()
        {
            var (_, result, log) = OffsetMills(3, MillOffsetSide.Right, MillPathKinds.Closed, OpenMillParts);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mill paths offset (ignored 0, skipped 5)."));
                Assert.That(Directory.GetFiles(_directory, "*_off*.project"), Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_NoKindSelected_ReturnsFalseAndLogs()
        {
            var (_, result, log) = OffsetMills(3, MillOffsetSide.Right, MillPathKinds.None, 2);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mill path types selected for offset."));
            });
        }

        /// <summary>The decoded <c>&lt;program&gt;</c> of a part's XNC operation, read back from the saved file.</summary>
        private static XElement ReadSavedProgram(string path, int partId)
        {
            var operation = XDocument.Load(path).Descendants("operation")
                .Single(o => (string?)o.Attribute("typeId") == "XNC"
                    && o.Elements("part").Any(p => (string?)p.Attribute("id") == partId.ToString()));

            return XDocument.Parse(operation.Attribute("program")!.Value).Root!;
        }

        private static string[] Points(XElement program) => program.Elements()
            .Where(e => e.Name.LocalName is "ms" or "ml" or "mac" or "ma")
            .Select(e => $"{e.Attribute("x")!.Value},{e.Attribute("y")!.Value}")
            .ToArray();

        [TestCase(MillOffsetSide.Right, 2, "303")]
        [TestCase(MillOffsetSide.Left, 2, "297")]
        [TestCase(MillOffsetSide.Right, 3, "297")] // fwd="false" travels the path in reverse
        [TestCase(MillOffsetSide.Left, 3, "303")]
        public void OffsetMillPaths_OpenHorizontalPass_ShiftsSidewaysAndKeepsEdgeOvershoot(
            MillOffsetSide side, int partId, string expectedY)
        {
            var (service, result, _) = OffsetMills(3, side, partId);

            Assert.That(result, Is.True);

            var program = ReadSavedProgram(service.FullPath, partId);
            var ms = program.Element("ms")!;
            var ml = program.Element("ml")!;

            Assert.Multiple(() =>
            {
                Assert.That(ms.Attribute("x")!.Value, Is.EqualTo("-10"), "entry keeps its 10 mm overshoot past the left edge");
                Assert.That(ml.Attribute("x")!.Value, Is.EqualTo("dx+10"), "an unchanged value keeps its authored expression");
                Assert.That(ms.Attribute("y")!.Value, Is.EqualTo(expectedY));
                Assert.That(ml.Attribute("y")!.Value, Is.EqualTo(expectedY));
                Assert.That(ms.Attribute("dp")!.Value, Is.EqualTo("5"));
            });
        }

        [Test]
        public void OffsetMillPaths_OpenDiagonalPass_EndsKeepTheirDistanceFromTheCrossedEdges()
        {
            var (service, result, _) = OffsetMills(3, MillOffsetSide.Right, 4);

            Assert.That(result, Is.True);

            // (-10,100) -> (510,620) at 45 deg; right of travel is up-left. The entry slides along
            // x=-10 (left edge overshoot), the end along y=620 (top edge overshoot): 3*sqrt(2) each.
            Assert.That(Points(ReadSavedProgram(service.FullPath, 4)),
                Is.EqualTo(new[] { "-10,104.2426", "505.7574,620" }));
        }

        [Test]
        public void OffsetMillPaths_OpenPathInsidePart_OffsetsEndsPerpendicularlyAndTrimsTheCorner()
        {
            var (service, result, _) = OffsetMills(3, MillOffsetSide.Right, 5);

            Assert.That(result, Is.True);
            Assert.That(Points(ReadSavedProgram(service.FullPath, 5)),
                Is.EqualTo(new[] { "100,103", "197,103", "197,200" }));
        }

        [TestCase(6, MillOffsetSide.Right, new[] { "97,97", "303,97", "303,203", "97,203", "97,97" })]    // CCW travel: right = outside
        [TestCase(6, MillOffsetSide.Left, new[] { "103,103", "297,103", "297,197", "103,197", "103,103" })]
        [TestCase(7, MillOffsetSide.Right, new[] { "103,103", "297,103", "297,197", "103,197", "103,103" })] // fwd="false": CW travel
        public void OffsetMillPaths_ClosedRectangleContour_GrowsOrShrinksPerTraversalDirection(
            int partId, MillOffsetSide side, string[] expected)
        {
            var (service, result, _) = OffsetMills(3, side, partId);

            Assert.That(result, Is.True);
            Assert.That(Points(ReadSavedProgram(service.FullPath, partId)), Is.EqualTo(expected));
        }

        [Test]
        public void OffsetMillPaths_ClosedArcCircle_ChangesRadiusAndKeepsCentres()
        {
            var (service, result, _) = OffsetMills(3, MillOffsetSide.Right, 8);

            Assert.That(result, Is.True);

            var program = ReadSavedProgram(service.FullPath, 8);

            Assert.Multiple(() =>
            {
                Assert.That(Points(program), Is.EqualTo(new[] { "250,277", "227,300", "250,323", "273,300", "250,277" }));
                Assert.That(program.Elements("mac").Select(m => $"{m.Attribute("cx")!.Value},{m.Attribute("cy")!.Value}"),
                    Is.All.EqualTo("250,300"));
                Assert.That(program.Elements("mac").Select(m => m.Attribute("dir")!.Value), Is.All.EqualTo("false"));
                Assert.That(program.Element("ms")!.Attribute("dp")!.Value, Is.EqualTo("contMillDepth"));
            });
        }

        [Test]
        public void OffsetMillPaths_RadiusArc_UpdatesRadiusAndEndPoints()
        {
            var (service, result, _) = OffsetMills(3, MillOffsetSide.Right, 9);

            Assert.That(result, Is.True);

            var program = ReadSavedProgram(service.FullPath, 9);

            // dir="true" is a clockwise sweep, so its centre (420,100) lies right of travel.
            Assert.Multiple(() =>
            {
                Assert.That(Points(program), Is.EqualTo(new[] { "403,100", "437,100" }));
                Assert.That(program.Element("ma")!.Attribute("r")!.Value, Is.EqualTo("17"));
            });
        }

        [TestCase(10, MillOffsetSide.Right, "106", "46", "3")] // fwd="true": CCW, right = outside
        [TestCase(10, MillOffsetSide.Left, "94", "34", "0")]   // corner radius never goes negative
        [TestCase(11, MillOffsetSide.Right, "94", "34", "2")]  // fwd="false": CW, right = inside
        public void OffsetMillPaths_Rectangle_ChangesSizeByTwiceTheOffsetAndRadiusByTheOffset(
            int partId, MillOffsetSide side, string l, string w, string r)
        {
            var (service, result, _) = OffsetMills(3, side, partId);

            Assert.That(result, Is.True);

            var mr = ReadSavedProgram(service.FullPath, partId).Element("mr")!;

            Assert.Multiple(() =>
            {
                Assert.That(mr.Attribute("l")!.Value, Is.EqualTo(l));
                Assert.That(mr.Attribute("w")!.Value, Is.EqualTo(w));
                Assert.That(mr.Attribute("r")!.Value, Is.EqualTo(r));
                Assert.That(mr.Attribute("x")!.Value, Is.EqualTo("500"));
                Assert.That(mr.Attribute("y")!.Value, Is.EqualTo("300"));
            });
        }

        [TestCase(MillOffsetSide.Right, "23", "13")]
        [TestCase(MillOffsetSide.Left, "17", "7")]
        public void OffsetMillPaths_Ellipse_ChangesSemiAxesByTheOffset(MillOffsetSide side, string l, string w)
        {
            var (service, result, _) = OffsetMills(3, side, 12);

            Assert.That(result, Is.True);

            var me = ReadSavedProgram(service.FullPath, 12).Element("me")!;

            Assert.Multiple(() =>
            {
                Assert.That(me.Attribute("l")!.Value, Is.EqualTo(l));
                Assert.That(me.Attribute("w")!.Value, Is.EqualTo(w));
            });
        }

        [Test]
        public void OffsetMillPaths_SharpConvexCorner_IsBridgedByARoundJoinArc()
        {
            var (service, result, _) = OffsetMills(3, MillOffsetSide.Right, 14);

            Assert.That(result, Is.True);

            var program = ReadSavedProgram(service.FullPath, 14);
            var names = program.Elements().Select(e => e.Name.LocalName).ToArray();
            var join = program.Element("mac")!;

            Assert.Multiple(() =>
            {
                Assert.That(names, Is.EqualTo(new[] { "tool", "ms", "ml", "mac", "ml", "ml" }));
                Assert.That(join.Attribute("cx")!.Value, Is.EqualTo("300"));
                Assert.That(join.Attribute("cy")!.Value, Is.EqualTo("420"));
                Assert.That(join.Attribute("dir")!.Value, Is.EqualTo("true"));
            });
        }

        [Test]
        public void OffsetMillPaths_CollapsingMill_IsIgnoredAndNothingIsSaved()
        {
            var (service, result, log) = OffsetMills(3, MillOffsetSide.Right, 13);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mill paths offset (ignored 1, skipped 0)."));
                Assert.That(service.FullPath, Does.EndWith(MillOffsetFixture));
                Assert.That(Directory.GetFiles(_directory, "*_off*.project"), Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_AllParts_SavesOffFileAndLogsCounts()
        {
            var (service, result, log) = OffsetMills(3, MillOffsetSide.Right, Enumerable.Range(2, 13).ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("td-mill-offset_off.project"));
                Assert.That(File.Exists(service.FullPath), Is.True);
                Assert.That(log, Does.Contain("offset 12 open and closed mill(s) by 3 mm R, ignored 1, skipped 0"));
                Assert.That(log, Does.Contain("Mill offset: \"part-13\" (id=13): 0 mill(s) offset, 1 ignored, 0 skipped"));
            });
        }

        [Test]
        public void OffsetMillPaths_RightThenLeft_RestoresTheOriginalContour()
        {
            var (service, _, _) = OffsetMills(3, MillOffsetSide.Right, 6);
            service.OpenProject(service.FullPath); // as the UI does after every operation

            var log = string.Empty;
            Assert.That(service.OffsetMillPaths(
                ref log, [new Part { Id = 6, Name = "closed-rect" }], 3, MillOffsetSide.Left, MillPathKinds.All), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(service.FullPath, Does.EndWith("td-mill-offset_off(1).project"));
                Assert.That(Points(ReadSavedProgram(service.FullPath, 6)),
                    Is.EqualTo(new[] { "100,100", "300,100", "300,200", "100,200", "100,100" }));
            });
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        public void OffsetMillPaths_NonPositiveOffset_ReturnsFalseAndLogs(double offset)
        {
            var (_, result, log) = OffsetMills(offset, MillOffsetSide.Right, 2);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("Mill path offset must be greater than 0"));
            });
        }

        [Test]
        public void OffsetMillPaths_WithNoParts_ReturnsFalseAndLogs()
        {
            var (_, result, log) = OffsetMills(3, MillOffsetSide.Right);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No parts selected for mill path offset."));
            });
        }

        /// <summary>Minimal fixed clock; .NET 9 ships no in-box fake TimeProvider.</summary>
        private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;

            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }
    }
}
