using NSubstitute;
using XncOptimizerUI.Contracts;
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

        /// <summary>Minimal fixed clock; .NET 9 ships no in-box fake TimeProvider.</summary>
        private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;

            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }
    }
}
