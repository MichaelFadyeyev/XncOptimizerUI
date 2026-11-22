using System.Xml.Linq;
using XncOptimizerUI.Services.Project;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="ProjectDocument.SaveAs"/>: the project is written to a new file, which then becomes the
    /// open file for in-place saves and suffixed outputs; a failed write leaves the open file as it was.
    /// </summary>
    [TestFixture]
    public class ProjectDocumentTests : TempFixtureTestBase
    {
        private const string Fixture = "td.project";

        [Test]
        public void SaveAs_WritesTheProjectToTheNewFileAndMakesItTheOpenFile()
        {
            var source = CopyFixture(Fixture);
            var copy = Path.Combine(TempDirectory, "copy.project");
            var reference = Path.Combine(TempDirectory, "reference.project");
            XDocument.Load(source).Save(reference);
            var document = OpenDocument(source);

            document.SaveAs(copy);

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(copy), Is.EqualTo(File.ReadAllBytes(reference)), "written as an in-place save would write it");
                Assert.That(document.FullPath, Is.EqualTo(copy));
            });
        }

        [Test]
        public void SaveAs_LaterSavesGoToTheNewFile()
        {
            var source = CopyFixture(Fixture);
            var original = File.ReadAllBytes(source);
            var copy = Path.Combine(TempDirectory, "copy.project");
            var document = OpenDocument(source);

            document.SaveAs(copy);
            document.AppendDescription("edited after save as");
            document.Save();

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(source), Is.EqualTo(original), "the original file is not touched");
                Assert.That(XDocument.Load(copy).Root!.Attribute("description")!.Value, Does.Contain("edited after save as"));
            });
        }

        [Test]
        public void SaveAs_SuffixedOutputsAreNamedAfterTheNewFile()
        {
            var copiesDirectory = Directory.CreateDirectory(Path.Combine(TempDirectory, "copies")).FullName;
            var document = OpenDocument(CopyFixture(Fixture));

            document.SaveAs(Path.Combine(copiesDirectory, "copy.project"));
            var suffixed = document.SaveAsSuffixed("_opt");

            Assert.That(suffixed, Is.EqualTo(Path.Combine(copiesDirectory, "copy_opt.project")));
        }

        [Test]
        public void SaveAs_WhenWritingFails_StaysOnTheOpenFile()
        {
            var source = CopyFixture(Fixture);
            var document = OpenDocument(source);
            var unreachable = Path.Combine(TempDirectory, "missing", "copy.project");

            Assert.Multiple(() =>
            {
                Assert.That(() => document.SaveAs(unreachable), Throws.InstanceOf<IOException>());
                Assert.That(document.FullPath, Is.EqualTo(source));
                Assert.That(File.Exists(unreachable), Is.False);
            });
        }

        [Test]
        public void SaveAs_WithNoProjectOpen_Throws()
        {
            var document = new ProjectDocument(TimeProvider.System);

            Assert.That(() => document.SaveAs(Path.Combine(TempDirectory, "copy.project")), Throws.InvalidOperationException);
        }

        private static ProjectDocument OpenDocument(string path)
        {
            var document = new ProjectDocument(TimeProvider.System);
            document.Open(path);

            return document;
        }
    }
}
