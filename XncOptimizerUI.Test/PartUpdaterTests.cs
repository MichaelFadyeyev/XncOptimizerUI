using System.Xml.Linq;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="Services.Project.PartUpdater"/> through <see cref="IProjectEditor.UpdatePart"/>: sizes, rename, texture and banding.
    /// </summary>
    [TestFixture]
    public class PartUpdaterTests : ProjectServicesTestBase
    {
        [Test]
        public void UpdatePart_WithUnchangedValues_ReturnsFalse()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);

            var part = service.Reader.ReadParts().First();
            var log = string.Empty;

            Assert.That(service.Editor.UpdatePart(part).Into(ref log), Is.False,
                "an unmodified part is not a change and should not be written");
        }

        [Test]
        public void UpdatePart_WithNewDimensions_ReturnsTrueAndPersists()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);

            var part = service.Reader.ReadParts().First();
            var newLength = part.Length + 25m;
            part.Length = newLength;

            var log = string.Empty;

            Assert.That(service.Editor.UpdatePart(part).Into(ref log), Is.True);

            service.Session.Save();

            var reopened = OpenAndRead(CreateService(), ProjectPath);

            Assert.That(reopened.Reader.ReadParts().First(p => p.Id == part.Id).Length, Is.EqualTo(newLength));
        }

        /// <summary>
        /// Regression: sizes were written with the user's culture, so on a Ukrainian
        /// (comma-decimal) Windows a fractional length was saved as <c>l="1380,5"</c>.
        /// </summary>
        [Test]
        [SetCulture("uk-UA")]
        public void UpdatePart_WithFractionalSize_WritesInvariantDecimals()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);

            var part = service.Reader.ReadParts().First();
            part.Length += 0.5m;
            part.Width += 0.25m;

            var log = string.Empty;
            service.Editor.UpdatePart(part).Into(ref log);

            var saved = SaveAndLoadProductPart(service, part.Id);

            Assert.Multiple(() =>
            {
                foreach (var name in new[] { "l", "w", "dl", "dw", "jl", "jw" })
                {
                    var value = (string?)saved.Attribute(name);
                    Assert.That(value, Does.Not.Contain(","), $"{name}=\"{value}\" must use '.' as decimal separator");
                }
            });
        }

        [TestCase("Old name", "New name", "New name", TestName = "UpdatePart_Rename_PlainXncTypeNameFollowsPartName")]
        [TestCase("[03]Old name", "[03]New name", "[03]New name", TestName = "UpdatePart_Rename_GroupCodeKeptAndNameReplaced")]
        [TestCase("[03]Old name", "[07]New name", "[03]New name", TestName = "UpdatePart_Rename_XncKeepsItsOwnGroupCode")]
        public void UpdatePart_Rename_SyncsXncTypeName(string xncTypeName, string newPartName, string expectedTypeName)
        {
            var (service, part) = OpenWithXncTypeName(xncTypeName);
            part.Name = newPartName;

            var log = string.Empty;
            service.Editor.UpdatePart(part).Into(ref log);
            service.Session.Save();

            Assert.Multiple(() =>
            {
                Assert.That(XncTypeNamesOf(part.Id), Has.All.EqualTo(expectedTypeName));
                Assert.That(log, Does.Not.Contain("Cannot set new XNC typeName"));
            });
        }

        [TestCase("Old name", "[03]New name", TestName = "UpdatePart_Rename_GroupCodeOnPartOnly_LogsAndKeepsXncTypeName")]
        [TestCase("[03]Old name", "New name", TestName = "UpdatePart_Rename_GroupCodeOnXncOnly_LogsAndKeepsXncTypeName")]
        public void UpdatePart_Rename_MismatchedGroupCode_LogsAndKeepsXncTypeName(string xncTypeName, string newPartName)
        {
            var (service, part) = OpenWithXncTypeName(xncTypeName);
            part.Name = newPartName;

            var log = string.Empty;
            service.Editor.UpdatePart(part).Into(ref log);
            service.Session.Save();

            Assert.Multiple(() =>
            {
                Assert.That(XncTypeNamesOf(part.Id), Has.All.EqualTo(xncTypeName));
                Assert.That(log, Does.Contain($"""Cannot set new XNC typeName for part "{newPartName}" with old XNC typeName "{xncTypeName}"."""));
            });
        }

        private IEnumerable<string?> XncTypeNamesOf(int partId) =>
            XncOperationsOf(XDocument.Load(ProjectPath), partId).Select(o => (string?)o.Attribute("typeName"));

        [Test]
        public void UpdatePart_WithToggledTexture_ReturnsTrueAndPersists()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);

            var part = service.Reader.ReadParts().First();
            var newTexture = !part.ConsiderTexture;
            part.ConsiderTexture = newTexture;

            var log = string.Empty;

            Assert.That(service.Editor.UpdatePart(part).Into(ref log), Is.True);

            service.Session.Save();

            var reopened = OpenAndRead(CreateService(), ProjectPath);

            Assert.That(reopened.Reader.ReadParts().First(p => p.Id == part.Id).ConsiderTexture, Is.EqualTo(newTexture));
        }

        /// <summary>Saves, then returns the raw <c>&lt;part&gt;</c> of the product good with <paramref name="partId"/>.</summary>
        private XElement SaveAndLoadProductPart(ProjectServices service, int partId)
        {
            service.Session.Save();

            return XDocument.Load(ProjectPath).Root!.Elements("good")
                .Where(g => (string?)g.Attribute("typeId") == "product")
                .SelectMany(g => g.Elements("part"))
                .Single(p => (int)p.Attribute("id")! == partId);
        }

        private List<int> ElOperationPartIds(int operationId) =>
            XDocument.Load(ProjectPath).Root!.Elements("operation")
                .Single(o => (string?)o.Attribute("typeId") == "EL" && (int)o.Attribute("id")! == operationId)
                .Elements("part")
                .Select(p => (int)p.Attribute("id")!)
                .ToList();

        [Test]
        public void UpdatePart_WithReassignedBand_RewritesEdgeOperationListsAndJointLength()
        {
            // td.project part 2: l=2301, jl=2297, ell=op#1 (t=2), elr=op#2 (t=2), op#2 used only on elr.
            var service = OpenAndRead(CreateService(), ProjectPath);
            var part = service.Reader.ReadParts().Single(p => p.Id == 2);
            var band3 = service.Reader.ReadBands().Single(b => b.Id == 3);

            part.RightBandingId = 3; // t=1

            var log = string.Empty;

            Assert.That(service.Editor.UpdatePart(part).Into(ref log), Is.True);

            var saved = SaveAndLoadProductPart(service, 2);

            Assert.Multiple(() =>
            {
                Assert.That((string?)saved.Attribute("elr"), Is.EqualTo("@operation#3"));
                Assert.That((string?)saved.Attribute("elrMat"), Is.EqualTo(band3.Name));
                Assert.That(part.RightBandingMat, Is.EqualTo(band3.Name));
                Assert.That((string?)saved.Attribute("jl"), Is.EqualTo("2298"), "jl = 2301 - 2 - 1");
                Assert.That((string?)saved.Attribute("jw"), Is.EqualTo("613"), "top/bottom untouched");
                Assert.That(ElOperationPartIds(3), Does.Contain(2));
                Assert.That(ElOperationPartIds(3), Is.Ordered, "part reference inserted in id order");
                Assert.That(ElOperationPartIds(2), Does.Not.Contain(2), "no edge uses op#2 any more");
                Assert.That(ElOperationPartIds(1), Does.Contain(2), "op#1 still on top/bottom/left");
                Assert.That(log, Is.Empty);
            });

            var reopened = OpenAndRead(CreateService(), ProjectPath);
            Assert.That(reopened.Reader.ReadParts().Single(p => p.Id == 2).RightBandingId, Is.EqualTo(3));
        }

        [Test]
        public void UpdatePart_WithRemovedBand_DropsEdgeAttributesAndRecalculatesJointWidth()
        {
            // td.project part 1: w=263, jw=261, elt=op#1 (t=2), no elb.
            var service = OpenAndRead(CreateService(), ProjectPath);
            var part = service.Reader.ReadParts().Single(p => p.Id == 1);

            part.TopBandingId = null;

            var log = string.Empty;

            Assert.That(service.Editor.UpdatePart(part).Into(ref log), Is.True);

            var saved = SaveAndLoadProductPart(service, 1);

            Assert.Multiple(() =>
            {
                Assert.That(saved.Attribute("elt"), Is.Null);
                Assert.That(saved.Attribute("eltMat"), Is.Null);
                Assert.That(part.TopBandingMat, Is.Null);
                Assert.That((string?)saved.Attribute("jw"), Is.EqualTo("263"));
                Assert.That((string?)saved.Attribute("jl"), Is.EqualTo("2297"));
                Assert.That(ElOperationPartIds(1), Does.Contain(1), "left/right still use op#1");
            });
        }

        [Test]
        public void UpdatePart_WithBandOnUnbandedEdge_AddsEdgeAndListsPartInOperation()
        {
            // td.project part 1: w=263, elt=op#1 (t=2), no elb; op#3 (t=1) doesn't list part 1.
            var service = OpenAndRead(CreateService(), ProjectPath);
            var part = service.Reader.ReadParts().Single(p => p.Id == 1);

            part.BottomBandingId = 3;

            var log = string.Empty;

            Assert.That(service.Editor.UpdatePart(part).Into(ref log), Is.True);

            var saved = SaveAndLoadProductPart(service, 1);

            Assert.Multiple(() =>
            {
                Assert.That((string?)saved.Attribute("elb"), Is.EqualTo("@operation#3"));
                Assert.That((string?)saved.Attribute("jw"), Is.EqualTo("260"), "jw = 263 - 2 - 1");
                Assert.That(ElOperationPartIds(3), Does.Contain(1));
                Assert.That(ElOperationPartIds(3), Is.Ordered);
            });
        }

        [Test]
        public void UpdatePart_WithUnknownBand_LogsAndLeavesEdgeUntouched()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);
            var part = service.Reader.ReadParts().Single(p => p.Id == 1);

            part.TopBandingId = 999_999;

            var log = string.Empty;
            service.Editor.UpdatePart(part).Into(ref log);

            var saved = SaveAndLoadProductPart(service, 1);

            Assert.Multiple(() =>
            {
                Assert.That(log, Does.Contain("no such band"));
                Assert.That((string?)saved.Attribute("elt"), Is.EqualTo("@operation#1"));
            });
        }
    }
}
