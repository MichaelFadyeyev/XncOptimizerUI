using System.Xml.Linq;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="IProjectEditor.DeleteParts"/> on <c>td-gr-ml-conversion.project</c>: nine two-sided parts
    /// (XNC ids 4..21, two per part), one CS listing parts 1..9 and the sheet part 10, EL id=1 listing
    /// parts 1..9 and EL id=2 listing parts 5 and 6.
    /// </summary>
    [TestFixture]
    public class PartDeleterTests : ProjectServicesTestBase
    {
        private const string FixtureName = "td-gr-ml-conversion.project";

        [Test]
        public void DeleteParts_RemovesThePartsTheirXncOperationsAndTheirCsAndElReferences()
        {
            var path = CopyFixture(FixtureName);
            var service = OpenAndRead(CreateService(), path);

            var log = string.Empty;
            var result = service.Editor.DeleteParts([6, 5]).Into(ref log);
            service.Session.Save();

            var saved = XDocument.Load(path).Root!;

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, log);
                Assert.That(ProductPartIds(saved), Is.EqualTo(new[] { 1, 2, 3, 4, 7, 8, 9 }));
                Assert.That(XncPartIds(saved), Is.EqualTo(new[] { 1, 1, 2, 2, 3, 3, 4, 4, 7, 7, 8, 8, 9, 9 }));
                Assert.That(ListedPartIds(saved, "CS", 3), Is.EqualTo(new[] { 1, 2, 3, 4, 7, 8, 9, 10 }), "the sheet part stays last");
                Assert.That(ListedPartIds(saved, "EL", 1), Is.EqualTo(new[] { 1, 2, 3, 4, 7, 8, 9 }));
                Assert.That(ListedPartIds(saved, "EL", 2), Is.Empty, "the EL operation itself is kept");
                Assert.That(log, Is.EqualTo(
                    "Deleted 2 part(s):\n"
                    + "\"512.08.01.ПАН-640\" (id=5),\n"
                    + "\"512.08.02.ПАН-600\" (id=6),\n"
                    + "4 XNC operation(s) removed\n"));
            });
        }

        [Test]
        public void DeleteParts_UnknownId_FailsAndLeavesTheDocumentUnchanged()
        {
            var path = CopyFixture(FixtureName);
            var before = File.ReadAllText(path);
            var service = OpenAndRead(CreateService(), path);

            var log = string.Empty;
            var result = service.Editor.DeleteParts([5, 99]).Into(ref log);
            service.Session.Save();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("part id=99 not found"));
                Assert.That(ProductPartIds(XDocument.Load(path).Root!), Has.Member(5));
                Assert.That(XDocument.Load(path).ToString(), Is.EqualTo(XDocument.Parse(before).ToString()));
            });
        }

        [Test]
        public void DeleteParts_NoIds_Fails()
        {
            var service = OpenAndRead(CreateService(), CopyFixture(FixtureName));

            var log = string.Empty;

            Assert.Multiple(() =>
            {
                Assert.That(service.Editor.DeleteParts([]).Into(ref log), Is.False);
                Assert.That(log, Does.Contain("No parts to delete."));
            });
        }

        [Test]
        public void DeleteParts_NoProjectOpen_Fails()
        {
            var log = string.Empty;

            Assert.Multiple(() =>
            {
                Assert.That(CreateService().Editor.DeleteParts([1]).Into(ref log), Is.False);
                Assert.That(log, Does.Contain("No project is opened."));
            });
        }

        private static IEnumerable<int> ProductPartIds(XElement project) =>
            project.Elements("good")
                .Where(g => (string?)g.Attribute("typeId") == "product")
                .Elements("part")
                .Select(p => (int)p.Attribute("id")!);

        private static IEnumerable<int> XncPartIds(XElement project) =>
            Operations(project, "XNC").Select(o => (int)o.Element("part")!.Attribute("id")!);

        private static IEnumerable<int> ListedPartIds(XElement project, string typeId, int operationId) =>
            Operations(project, typeId)
                .Single(o => (int)o.Attribute("id")! == operationId)
                .Elements("part")
                .Select(p => (int)p.Attribute("id")!);

        private static IEnumerable<XElement> Operations(XElement project, string typeId) =>
            project.Elements("operation").Where(o => (string?)o.Attribute("typeId") == typeId);
    }
}
