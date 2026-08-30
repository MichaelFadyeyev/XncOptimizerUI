using XncOptimizerUI.Services;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// These were impossible before DI: ConfigService was a static class whose
    /// static constructor computed a %AppData% path and did disk I/O, so any test
    /// touching it read and wrote the developer's real configuration file.
    /// </summary>
    [TestFixture]
    public class ConfigServiceTests
    {
        private string _directory = string.Empty;
        private string _path = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "XncOptimizerUI.Test", Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "configuration.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void Constructor_WhenFileIsMissing_WritesDefaultsToDisk()
        {
            var config = new ConfigService(_path);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(_path), Is.True, "the default configuration should have been created");
                Assert.That(config.SawWidth, Is.EqualTo(4.0m));
                Assert.That(config.LabelsToProcess, Has.Count.EqualTo(1));
                Assert.That(config.GetLastLabelToProcessSelected(), Is.EqualTo("поріз.2х40"));
            });
        }

        [Test]
        public void AddLabelToProcess_RoundTripsThroughDisk()
        {
            var first = new ConfigService(_path);
            first.AddLabelToProcess("тест");

            var second = new ConfigService(_path);

            Assert.Multiple(() =>
            {
                Assert.That(second.LabelsToProcess, Does.Contain("тест"));
                Assert.That(second.GetLastLabelToProcessSelected(), Is.EqualTo("тест"),
                    "adding a label should also select it, and that selection should persist");
            });
        }

        [Test]
        public void DeleteLabelToProcess_WhenOnlyOneLabelRemains_KeepsIt()
        {
            var config = new ConfigService(_path);

            config.DeleteLabelToProcess("поріз.2х40");

            Assert.That(config.LabelsToProcess, Has.Count.EqualTo(1));
        }

        [Test]
        public void DeleteLabelToProcess_RemovesLabelAndResetsSelection()
        {
            var config = new ConfigService(_path);
            config.AddLabelToProcess("тест");

            config.DeleteLabelToProcess("тест");

            Assert.Multiple(() =>
            {
                Assert.That(config.LabelsToProcess, Does.Not.Contain("тест"));
                Assert.That(config.GetLastLabelToProcessSelected(), Is.EqualTo("поріз.2х40"));
            });
        }

        [Test]
        public void UpdateSawWidth_PersistsNewValue()
        {
            var first = new ConfigService(_path);

            first.UpdateSawWidth(3.2m);

            Assert.That(new ConfigService(_path).SawWidth, Is.EqualTo(3.2m));
        }

        [Test]
        public void UpdateLastLabelToProcessSelectedIndex_WithUnknownLabel_DoesNotPersistNegativeIndex()
        {
            var first = new ConfigService(_path);

            // WPF resets a ComboBox SelectedItem to null/"" when it is absent from
            // ItemsSource. Persisting the resulting IndexOf of -1 used to make the
            // next launch throw from GetLastLabelToProcessSelected().
            first.UpdateLastLabelToProcessSelectedIndex(string.Empty);

            Assert.That(() => new ConfigService(_path).GetLastLabelToProcessSelected(), Throws.Nothing);
        }

        [Test]
        public void Constructor_WhenFileIsMalformed_FallsBackToDefaults()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "{ this is not json");

            ConfigService config = null!;

            Assert.That(() => config = new ConfigService(_path), Throws.Nothing,
                "a corrupt configuration file used to throw a TypeInitializationException out of the static ctor");
            Assert.That(config.SawWidth, Is.EqualTo(4.0m));
        }
    }
}
