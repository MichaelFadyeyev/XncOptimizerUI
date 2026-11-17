using System.Text;
using System.Xml.Linq;
using XncOptimizerUI.Services.Project;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="CadExportPreparer"/>: a CAD export (windows-1251, no band symbols, no part <c>l</c>/<c>w</c>,
    /// no sheet part in its cut operations) is completed on open and then reads like any project.
    /// </summary>
    [TestFixture]
    public class CadExportPreparerTests : TempFixtureTestBase
    {
        private const string CadExport = """
            <?xml version="1.0" encoding="windows-1251"?>
            <project>
              <good typeId="band" id="20" name="Кромка 2" code="b2" t="2" w="23"><part id="21" /></good>
              <good typeId="band" id="30" name="Кромка 1" code="b1" t="1" w="22"><part id="31" /></good>
              <good typeId="sheet" id="40" name="ДСП 18" code="s18" t="18"><part id="41" l="2800" w="2070" /><part id="42" /></good>
              <good typeId="product" id="50" name="Шафа" count="1">
                <part id="1" name="Полиця" count="2" dl="600.5" dw="300" elt="@operation#3" />
                <part id="2" name="Кришка" count="1" dl="800" dw="400" elb="@operation#4" />
              </good>
              <operation typeId="EL" id="3"><material id="20" /><part id="1" /></operation>
              <operation typeId="EL" id="4"><material id="30" /><part id="2" /></operation>
              <operation typeId="CS" id="5"><material id="40" /><part id="1" /><part id="2" /></operation>
            </project>
            """;

        private static readonly Encoding Windows1251 = CodePagesEncodingProvider.Instance.GetEncoding(1251)!;

        [Test]
        public void Prepare_AssignsUniqueBandSymbolsPerElOperationId()
        {
            var project = PreparedProject(CadExport.Replace(
                """<operation typeId="CS" """,
                """<operation typeId="EL" id="3"><material id="20" /></operation><operation typeId="CS" """));

            Assert.That(Operations(project, "EL").Select(o => o.Attribute("elSymbol")?.Value), Is.EqualTo(new[] { "A", "B", "A" }),
                "operations sharing an id share their letter");
        }

        [Test]
        public void Prepare_KeepsExistingBandSymbolsAndSkipsTheirLetters()
        {
            var project = PreparedProject(CadExport.Replace("""typeId="EL" id="4">""", """typeId="EL" id="4" elSymbol="A">"""));

            Assert.That(Operations(project, "EL").Select(o => o.Attribute("elSymbol")?.Value), Is.EqualTo(new[] { "B", "A" }));
        }

        [Test]
        public void Prepare_MoreThan26ElOperations_Throws()
        {
            var extraOperations = string.Concat(Enumerable.Range(100, 25).Select(id => $"""<operation typeId="EL" id="{id}"><material id="20" /></operation>"""));
            var document = XDocument.Parse(CadExport.Replace("</project>", extraOperations + "</project>"));

            Assert.That(() => CadExportPreparer.Prepare(document.Root!), Throws.InstanceOf<InvalidDataException>());
        }

        [Test]
        public void Prepare_CopiesDetailSizesIntoPartLengthAndWidth()
        {
            var parts = PreparedProject(CadExport).Elements("good").Single(g => g.Attribute("typeId")?.Value == "product").Elements("part").ToList();

            Assert.Multiple(() =>
            {
                Assert.That(parts.Select(p => p.Attribute("l")?.Value), Is.EqualTo(new[] { "600.5", "800" }));
                Assert.That(parts.Select(p => p.Attribute("w")?.Value), Is.EqualTo(new[] { "300", "400" }));
            });
        }

        [Test]
        public void Prepare_AppendsFirstPartOfTheMaterialSheetToEachCutOperation()
        {
            var cut = Operations(PreparedProject(CadExport), "CS").Single();

            Assert.That(cut.Elements("part").Select(p => p.Attribute("id")?.Value), Is.EqualTo(new[] { "1", "2", "41" }));
        }

        [Test]
        public void Prepare_Twice_ChangesNothingMore()
        {
            var once = PreparedProject(CadExport);
            var twice = new XElement(once);

            CadExportPreparer.Prepare(twice);

            Assert.That(XNode.DeepEquals(once, twice), Is.True);
        }

        [Test]
        public void Prepare_CutOperationWithUnknownSheet_Throws()
        {
            var document = XDocument.Parse(CadExport.Replace("""<material id="40" />""", """<material id="99" />"""));

            Assert.That(() => CadExportPreparer.Prepare(document.Root!), Throws.InstanceOf<InvalidDataException>().With.Message.Contains("id=99"));
        }

        [Test]
        public void Open_Windows1251CadExport_ReadsPartsBandsAndSheets()
        {
            var path = Path.Combine(TempDirectory, "cad.project");
            File.WriteAllBytes(path, Windows1251.GetBytes(CadExport));
            var document = new ProjectDocument(TimeProvider.System);
            document.Open(path);
            var reader = new ProjectReader(document);

            var parts = reader.ReadParts();

            Assert.Multiple(() =>
            {
                Assert.That(parts.Select(p => (p.Name, p.Length, p.Width, p.SheetId)),
                    Is.EqualTo(new[] { ("Полиця", 600.5m, 300m, 40), ("Кришка", 800m, 400m, 40) }));
                Assert.That(reader.ReadBands().Select(b => (b.Name, b.InternalSymbol)), Is.EqualTo(new[] { ("Кромка 2", "A"), ("Кромка 1", "B") }));
                Assert.That(reader.ReadSheets().Select(s => s.Name), Is.EqualTo(new[] { "ДСП 18" }));
            });
        }

        [Test]
        public void Open_Utf8File_IsNotPrepared()
        {
            var path = Path.Combine(TempDirectory, "utf8.project");
            File.WriteAllText(path, CadExport.Replace("windows-1251", "UTF-8"), new UTF8Encoding(false));
            var document = new ProjectDocument(TimeProvider.System);
            document.Open(path);

            Assert.That(Operations(document.Project, "EL").Select(o => o.Attribute("elSymbol")), Is.All.Null);
        }

        private static XElement PreparedProject(string xml)
        {
            var project = XDocument.Parse(xml).Root!;
            CadExportPreparer.Prepare(project);

            return project;
        }

        private static List<XElement> Operations(XElement project, string typeId) =>
            project.Elements("operation").Where(o => o.Attribute("typeId")?.Value == typeId).ToList();
    }
}
