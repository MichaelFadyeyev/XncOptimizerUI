
namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="Services.Project.ProjectReader"/>: parts, bands, sheets and XNC turn checks read from a fixture.
    /// </summary>
    [TestFixture]
    public class ProjectReaderTests : ProjectServicesTestBase
    {
        [Test]
        public void OpenProject_ReadsPartsBandsAndSheets()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);

            var parts = service.Reader.ReadParts();

            Assert.Multiple(() =>
            {
                Assert.That(service.Session.FullPath, Is.EqualTo(ProjectPath));
                Assert.That(service.Reader.ReadSheets(), Is.Not.Empty);
                Assert.That(parts, Is.Not.Empty);
                Assert.That(parts.Select(p => p.Id), Is.Unique);
            });
        }

        [Test]
        public void GetPartsWithXncTurnDiscordance_ReturnsOnlyMultiProgramPartsWithDisagreeingTurn()
        {
            // Fixture: panel-A has two XNC ops with turn 0 and turn 1 (discordant);
            // panel-B has two ops both turn 2 (consistent); panel-C has a single op.
            var path = CopyFixture("td-turn-discordance.project");
            var service = OpenAndRead(CreateService(), path);

            Assert.That(service.Reader.GetPartsWithXncTurnDiscordance(), Is.EqualTo(new[] { "panel-A" }));
        }

        [Test]
        public void GetPartsWithXncTurnDiscordance_WithNoProjectOpen_ReturnsEmpty()
        {
            Assert.That(CreateService().Reader.GetPartsWithXncTurnDiscordance(), Is.Empty);
        }

        [Test]
        public void ReadParts_ReadsConsiderTextureFromTxt()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);

            Assert.That(service.Reader.ReadParts().First().ConsiderTexture, Is.True,
                "td.project parts are declared with txt=\"true\"");
        }
    }
}
