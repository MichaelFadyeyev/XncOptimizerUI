using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="Services.Project.XncProgramReplacer"/> through <see cref="IProjectRestructuring.ReplaceXncPrograms"/>.
    /// </summary>
    [TestFixture]
    public class XncProgramReplacerTests : ProjectServicesTestBase
    {
        [Test]
        public void ReplaceXncPrograms_WithNoTargets_ReturnsFalseAndLogs()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);

            var sourcePart = service.Reader.ReadParts().First();
            var log = string.Empty;

            var result = service.Restructuring.ReplaceXncPrograms(sourcePart, []).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No target parts selected for XNC replacement."));
            });
        }
    }
}
