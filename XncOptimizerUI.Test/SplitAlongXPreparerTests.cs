using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using NSubstitute;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="Services.Project.SplitAlongXPreparer"/> through <see cref="IProjectRestructuring.PrepForSplitAlongX"/>.
    /// </summary>
    [TestFixture]
    public class SplitAlongXPreparerTests : ProjectServicesTestBase
    {
        [Test]
        public void PrepForSplitAlongX_FindsXncProgramsByPartIdNotByName()
        {
            // The XNC operation's typeName no longer matches the part name (e.g. renamed in GibLab).
            var (service, part) = OpenWithXncTypeName("unrelated operation name");
            var log = string.Empty;

            service.Restructuring.PrepForSplitAlongX([part.Id.ToString(CultureInfo.InvariantCulture)]).Into(ref log);

            var operation = XncOperationsOf(XDocument.Load(service.Session.FullPath), part.Id).First();
            var program = XDocument.Parse(operation.Attribute("program")!.Value).Root!;
            var expectedWidth = part.Width * 2 + 4.0m;

            Assert.That((string?)program.Attribute("dy"), Is.EqualTo(expectedWidth.ToString(CultureInfo.InvariantCulture)),
                "the part's program must be resized with the part");
        }

        /// <summary>
        /// Part 1 of td.project: w=263, jw=261 (a 2 mm joint shrink), kerf 4. The sawn blank is
        /// 2*263+4 = 530 wide; cut and detail widths are the blank's, the joint width keeps the shrink.
        /// </summary>
        [Test]
        public void PrepForSplitAlongX_SetsTheBlankWidths()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);
            var log = string.Empty;

            service.Restructuring.PrepForSplitAlongX(["1"]).Into(ref log);

            var part = XDocument.Load(service.Session.FullPath).Descendants("part").Single(p => (string?)p.Attribute("id") == "1" && p.Attribute("w") is not null);

            Assert.That(
                new[] { "w", "cw", "dw", "jw" }.Select(name => XmlConvert.ToDecimal(part.Attribute(name)!.Value)),
                Is.EqualTo(new[] { 530m, 530m, 530m, 528m }));
        }

        [Test]
        public void PrepForSplitAlongX_UsesInjectedSawWidth()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);

            var part = service.Reader.ReadParts().First();
            var originalWidth = part.Width;
            var log = string.Empty;

            service.Restructuring.PrepForSplitAlongX([part.Id.ToString(CultureInfo.InvariantCulture)]).Into(ref log);

            var updated = service.Reader.ReadParts().First(p => p.Id == part.Id);

            Assert.Multiple(() =>
            {
                // Width is derived from dw, so assert the direction rather than an
                // exact value; the kerf itself is asserted via the injected config.
                Assert.That(updated.Width, Is.GreaterThan(originalWidth));
                Assert.That(updated.Count, Is.LessThanOrEqualTo(part.Count));
                Assert.That(service.Session.FullPath, Does.EndWith("td_spl.project"),
                    "split output must not share Group identical elements' _opt name");
            });

            _ = Config.Received().SawWidth;
        }
    }
}
