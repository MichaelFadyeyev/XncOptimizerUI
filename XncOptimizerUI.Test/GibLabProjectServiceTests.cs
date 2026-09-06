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
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills);

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
        public void ConvertGroovesAndMills_GroovesToMills_KeepsToolStillUsedByAnUnconvertedGroove()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-grooving-mixed.project"));

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills);

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
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.MillsToGrooves);

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
        public void ConvertGroovesAndMills_MillsToGrooves_IgnoresThroughMillAndRectangle()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-milling.project"));
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.MillsToGrooves);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mills converted"));
                Assert.That(log, Does.Contain("ignored 2")); // through-mill contour + <mr>
                Assert.That(service.FullPath, Is.EqualTo(pathBefore), "nothing converted => nothing saved");
            });
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_IgnoresSecondaryPassGroove()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-grooving-secondary-pass.project"));
            var pathBefore = service.FullPath;

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills);

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
                ref log, [new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills);

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
        public void ConvertGroovesAndMills_WithNoParts_ReturnsFalseAndLogs()
        {
            var service = CreateService();
            service.OpenProject(CopyFixture("td-grooving.project"));

            var log = string.Empty;
            var result = service.ConvertGroovesAndMills(ref log, [], GrooveMillDirection.GroovesToMills);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No parts selected for groove/mill conversion."));
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
