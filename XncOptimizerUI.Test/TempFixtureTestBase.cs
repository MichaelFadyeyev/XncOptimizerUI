namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Base for tests that work on <c>TestData</c> fixtures: each test gets its own temp
    /// directory (deleted afterwards), and fixtures are copied there so the originals are
    /// never mutated. Runs before a derived fixture's own <c>[SetUp]</c>.
    /// </summary>
    public abstract class TempFixtureTestBase
    {
        /// <summary>The per-test temp directory; fixture copies and saved results land here.</summary>
        protected string TempDirectory { get; private set; } = string.Empty;

        [SetUp]
        public void CreateTempDirectory()
        {
            TempDirectory = Path.Combine(Path.GetTempPath(), "XncOptimizerUI.Test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempDirectory);
        }

        [TearDown]
        public void DeleteTempDirectory()
        {
            if (Directory.Exists(TempDirectory))
            {
                Directory.Delete(TempDirectory, recursive: true);
            }
        }

        /// <summary>The original (read-only) fixture <paramref name="name"/> in the test output's <c>TestData</c>.</summary>
        protected static string FixturePath(string name) =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", name);

        /// <summary>Copies fixture <paramref name="name"/> into <see cref="TempDirectory"/> and returns the copy's path.</summary>
        protected string CopyFixture(string name)
        {
            var path = Path.Combine(TempDirectory, name);
            File.Copy(FixturePath(name), path);

            return path;
        }
    }
}
