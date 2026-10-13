using System.Globalization;
using System.Xml.Linq;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.Test.Fakes;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Real project services on fixture copies. These were impossible before DI: the service popped modal
    /// MessageBox dialogs from inside its algorithms, so a headless test host hung
    /// rather than failed, and SawWidth was read from a static ConfigService that
    /// touched the developer's real %AppData% file.
    /// </summary>
    public abstract class ProjectServicesTestBase : TempFixtureTestBase
    {
        protected string ProjectPath { get; private set; } = string.Empty;

        protected IConfigService Config { get; private set; } = null!;

        /// <summary>Frozen so the description timestamp written into saved XML is deterministic.</summary>
        protected static TimeProvider Clock { get; } =
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 30, 13, 45, 0, TimeSpan.Zero));

        [SetUp]
        public void SetUp()
        {
            Config = Substitute.For<IConfigService>();
            Config.SawWidth.Returns(4.0m);
            Config.MillingToolDiams.Returns(new List<decimal> { 6.0m, 10.0m, 20.0m });
            Config.GroovingToolWidths.Returns(new List<decimal> { 2.8m, 3.2m });

            ProjectPath = CopyFixture("td.project");
        }

        protected ProjectServices CreateService() => TestProject.Create(Config, Clock);

        /// <summary>
        /// Opens the fixture and reads in the order the service requires: ReadParts
        /// resolves banding names and sheet ids against the caches that ReadBands and
        /// ReadSheets populate, so calling it first throws.
        /// </summary>
        protected static ProjectServices OpenAndRead(ProjectServices service, string path)
        {
            service.Session.Open(path);
            service.Reader.ReadBands();
            service.Reader.ReadSheets();

            return service;
        }

        /// <summary>Sets <c>typeName</c> of every XNC operation of the first machined part in the fixture copy, then opens it.</summary>
        protected (ProjectServices Service, Part Part) OpenWithXncTypeName(string xncTypeName)
        {
            var reader = OpenAndRead(CreateService(), ProjectPath);
            var partId = reader.Reader.ReadParts().First(p => reader.Reader.GetXncProgramsCount(p.Id) > 0).Id;

            var doc = XDocument.Load(ProjectPath);
            foreach (var operation in XncOperationsOf(doc, partId))
            {
                operation.SetAttributeValue("typeName", xncTypeName);
            }
            doc.Save(ProjectPath);

            var service = OpenAndRead(CreateService(), ProjectPath);

            return (service, service.Reader.ReadParts().Single(p => p.Id == partId));
        }


        protected static IEnumerable<XElement> XncOperationsOf(XDocument doc, int partId) =>
            doc.Root!.Elements("operation")
                .Where(o => (string?)o.Attribute("typeId") == "XNC"
                    && (string?)o.Element("part")?.Attribute("id") == partId.ToString(CultureInfo.InvariantCulture))
                .ToList();

        /// <summary>A two-segment contour (straight <c>ml</c> then radius arc <c>ma</c>) after a straight pass of the same tool.</summary>
        protected const string LineThenRadiusArcProgram = """
            <program dx="200" dy="100" dz="18">
              <tool name="Mill6" d="6" />
              <ms x="0" y="10" dp="5" c="0" name="Mill6" />
              <ml x="100" y="10" dp="5" />
              <ms x="0" y="20" dp="5" c="0" name="Mill6" />
              <ml x="100" y="20" dp="5" />
              <ma x="110" y="30" r="10" dir="true" dp="5" />
            </program>
            """;

        /// <summary>Writes a minimal project with one XNC operation for part 1 and opens it.</summary>
        protected ProjectServices OpenSingleProgramProject(string program)
        {
            var path = Path.Combine(TempDirectory, "single-program.project");
            new XDocument(new XElement("project",
                new XElement("operation",
                    new XAttribute("typeId", "XNC"),
                    new XAttribute("id", "1"),
                    new XAttribute("program", program),
                    new XElement("part", new XAttribute("id", "1"))))).Save(path);

            var service = CreateService();
            service.Session.Open(path);

            return service;
        }

        /// <summary>The decoded <c>&lt;program&gt;</c> of a part's XNC operation, read back from the saved file.</summary>
        protected static XElement ReadSavedProgram(string path, int partId)
        {
            var operation = XDocument.Load(path).Descendants("operation")
                .Single(o => (string?)o.Attribute("typeId") == "XNC"
                    && o.Elements("part").Any(p => (string?)p.Attribute("id") == partId.ToString(CultureInfo.InvariantCulture)));

            return XDocument.Parse(operation.Attribute("program")!.Value).Root!;
        }
    }
}
