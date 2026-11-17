using System.Text;
using System.Xml.Linq;
using XncOptimizerUI.Services.Project;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="ProjectFileEncoding"/> and <see cref="ProjectDocument"/>: <c>.project</c> files written in
    /// windows-1251 (declared, undeclared or wrongly declared as UTF-8) open with their Cyrillic intact
    /// and are saved as UTF-8; UTF-8 files are left as they are.
    /// </summary>
    [TestFixture]
    public class ProjectFileEncodingTests : TempFixtureTestBase
    {
        private const string Fixture = "td.project";
        private const string CyrillicPartName = "401.01.00.01.СТН-ФРН";
        private const string Utf8Declaration = """<?xml version="1.0" encoding="UTF-8"?>""";

        private static readonly Encoding Windows1251 = CodePagesEncodingProvider.Instance.GetEncoding(1251)!;

        [TestCase("""<?xml version="1.0" encoding="windows-1251"?>""", TestName = "Declared windows-1251")]
        [TestCase("", TestName = "No declaration")]
        [TestCase(Utf8Declaration, TestName = "Declared UTF-8 but written in windows-1251")]
        public void Open_Windows1251File_ReadsCyrillicNames(string declaration)
        {
            var path = WriteWindows1251Copy(declaration);

            var document = OpenDocument(path);

            Assert.Multiple(() =>
            {
                Assert.That(PartNames(document), Does.Contain(CyrillicPartName));
                Assert.That(document.ConvertedFromEncoding, Is.EqualTo("windows-1251"));
            });
        }

        [Test]
        public void Open_Utf8File_IsNotConverted()
        {
            var document = OpenDocument(CopyFixture(Fixture));

            Assert.Multiple(() =>
            {
                Assert.That(PartNames(document), Does.Contain(CyrillicPartName));
                Assert.That(document.ConvertedFromEncoding, Is.Null);
            });
        }

        [Test]
        public void Save_Utf8File_KeepsFileByteIdentical()
        {
            var path = CopyFixture(Fixture);
            var reference = Path.Combine(TempDirectory, "reference.project");
            XDocument.Load(path).Save(reference);

            OpenDocument(path).Save();

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(File.ReadAllBytes(reference)));
        }

        [Test]
        public void Save_Windows1251File_WritesUtf8ThatReopensWithCyrillicIntact()
        {
            var path = WriteWindows1251Copy("""<?xml version="1.0" encoding="windows-1251"?>""");

            OpenDocument(path).Save();
            var saved = File.ReadAllBytes(path);
            var reopened = OpenDocument(path);

            Assert.Multiple(() =>
            {
                Assert.That(ProjectFileEncoding.DetectEncoding(saved).WebName, Is.EqualTo("utf-8"));
                Assert.That(File.ReadLines(path, Encoding.UTF8).First(), Does.StartWith(Utf8Declaration).IgnoreCase);
                Assert.That(reopened.ConvertedFromEncoding, Is.Null);
                Assert.That(PartNames(reopened), Does.Contain(CyrillicPartName));
            });
        }

        [Test]
        public void Close_ForgetsConvertedEncoding()
        {
            var document = OpenDocument(WriteWindows1251Copy(""));

            document.Close();

            Assert.That(document.ConvertedFromEncoding, Is.Null);
        }

        /// <summary>The fixture re-encoded in windows-1251 with <paramref name="declaration"/> in place of its own.</summary>
        private string WriteWindows1251Copy(string declaration)
        {
            var text = File.ReadAllText(FixturePath(Fixture), Encoding.UTF8);
            Assert.That(text, Does.StartWith(Utf8Declaration), "fixture precondition");

            var path = Path.Combine(TempDirectory, "cp1251.project");
            File.WriteAllBytes(path, Windows1251.GetBytes(declaration + text[Utf8Declaration.Length..]));

            return path;
        }

        private static ProjectDocument OpenDocument(string path)
        {
            var document = new ProjectDocument(TimeProvider.System);
            document.Open(path);

            return document;
        }

        private static List<string?> PartNames(ProjectDocument document) =>
            document.ProductParts().Select(part => part.Attribute("name")?.Value).ToList();
    }
}
