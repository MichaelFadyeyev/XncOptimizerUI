using System.Xml.Linq;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="GibLabProjectService.CleanOutsideOperations"/> on <c>TestData/td-clean-outside.project</c>
    /// (1000 x 600 x 18 parts). Part 2 ("mixed") holds inside, outside and edge-case bores, grooves
    /// and mills; part 3 has one outside face bore; part 4 has nothing outside.
    /// </summary>
    [TestFixture]
    public class CleanOutsideOperationsTests
    {
        private const string Fixture = "td-clean-outside.project";

        private string _directory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "XncOptimizerUI.Test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            File.Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", Fixture), Path.Combine(_directory, Fixture));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private (GibLabProjectService Service, bool Result, string Log) Clean(OutsideOperationKinds kinds, params int[] partIds)
        {
            var service = new GibLabProjectService(Substitute.For<IConfigService>(), TimeProvider.System);
            service.OpenProject(Path.Combine(_directory, Fixture));

            var log = string.Empty;
            var result = service.CleanOutsideOperations(
                ref log, partIds.Select(id => new Part { Id = id, Name = $"part-{id}" }).ToList(), kinds);

            return (service, result, log);
        }

        /// <summary>The decoded <c>&lt;program&gt;</c> of a part's XNC operation, read back from the saved file.</summary>
        private static XElement ReadSavedProgram(string path, int partId)
        {
            var operation = XDocument.Load(path).Descendants("operation")
                .Single(o => (string?)o.Attribute("typeId") == "XNC"
                    && o.Elements("part").Any(p => (string?)p.Attribute("id") == partId.ToString()));

            return XDocument.Parse(operation.Attribute("program")!.Value).Root!;
        }

        private static string[] Tags(XElement program, params string[] names) => program.Elements()
            .Where(e => names.Contains(e.Name.LocalName))
            .Select(e => $"{e.Name.LocalName}:{(string?)e.Attribute("x") ?? (string?)e.Attribute("y1")}")
            .ToArray();

        [Test]
        public void CleanOutsideOperations_AllKinds_RemovesOnlyElementsThatNeverReachThePart()
        {
            var (service, result, log) = Clean(OutsideOperationKinds.All, 2);
            var program = ReadSavedProgram(service.FullPath, 2);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("td-clean-outside_cln.project"));
                Assert.That(log, Does.Contain("removed 17 (5 bore(s), 3 groove(s), 9 mill(s)), 1 tool(s) removed, unresolved 0, across 1 part(s)"));

                // Kept: inside bore, bore centred on the left side, middle top-edge bore, array whose third hole is inside
                // (a bore counts as outside when its centre is, so the one overlapping the edge by 1 mm goes).
                Assert.That(program.Elements().Where(e => e.Name.LocalName is "bf" or "bl" or "bt").Select(e => (string?)e.Attribute("x") ?? (string?)e.Attribute("y")),
                    Is.EqualTo(new[] { "100", "0", "500", "-50" }));

                // Kept: through groove and the right-of-travel groove reaching y = 3.
                Assert.That(program.Elements("gr").Select(e => (string?)e.Attribute("c")), Is.EqualTo(new[] { "0", "1" }));
                Assert.That(program.Elements("gr").First().Attribute("y1")!.Value, Is.EqualTo("300"));

                // Kept: contour with its tool on the part side, inside ellipse, pocket around the part, arc bulging into the part,
                // closed contour with its tool outside it (on the part side).
                Assert.That(Tags(program, "ms", "me", "mr"), Is.EqualTo(new[] { "ms:-8", "me:500", "ms:-50", "ms:1010", "ms:1000" }));
                Assert.That(program.Elements("ms").Single(e => e.Attribute("x")!.Value == "-50").Attribute("c")!.Value, Is.EqualTo("3"));
                Assert.That(program.Elements("mac").Single().Attribute("dir")!.Value, Is.EqualTo("false"));
                Assert.That(program.Elements("ml").Count(), Is.EqualTo(9), "removed contours take their segments along");

                Assert.That(program.Elements("tool").Select(t => t.Attribute("name")!.Value),
                    Is.EqualTo(new[] { "Bore8", "Cut4", "Mill10" }), "Mill6 was only used by removed mills");
            });
        }

        [Test]
        public void CleanOutsideOperations_ListsEveryRemovedElementInTheLog()
        {
            var (_, _, log) = Clean(OutsideOperationKinds.All, 2);

            Assert.Multiple(() =>
            {
                Assert.That(log, Does.Contain("Clean: \"part-2\" (id=2), XNC id=10: removed <bf x=\"-5\" y=\"100\" dp=\"10\" name=\"Bore8\" />"));
                Assert.That(log, Does.Contain("removed <ms x=\"-20\" y=\"100\" dp=\"5\" in=\"0\" out=\"1\" fwd=\"true\" c=\"0\" name=\"Mill10\" /> + 1 segment(s)"));
                Assert.That(log, Does.Contain("Clean: \"part-2\" (id=2): 17 removed (5 bore(s), 3 groove(s), 9 mill(s)), 1 tool(s) removed, 0 unresolved"));
                Assert.That(log.Split('\n').Count(l => l.StartsWith("Clean: \"part-2\" (id=2), XNC id=10: removed")), Is.EqualTo(17));
            });
        }

        [Test]
        public void CleanOutsideOperations_MillTouchingThePartFromOutside_IsRemoved()
        {
            var (_, _, log) = Clean(OutsideOperationKinds.Mills, 2);

            Assert.Multiple(() =>
            {
                Assert.That(log, Does.Contain("removed <ms x=\"-100\" y=\"100\" dp=\"5\" in=\"0\" out=\"1\" fwd=\"true\" c=\"3\" name=\"Mill10\" /> + 4 segment(s)"),
                    "pocket whose side lies on the part's left side");
                Assert.That(log, Does.Contain("removed <mr x=\"-25\" y=\"450\""), "rectangle pocket ending at x = 0");
                Assert.That(log, Does.Contain("removed <ms x=\"0\" y=\"100\" dp=\"5\" in=\"0\" out=\"1\" fwd=\"true\" c=\"1\" name=\"Mill10\" /> + 1 segment(s)"),
                    "open contour on the left side with its tool on the outer side");
                Assert.That(log, Does.Contain("removed <ms x=\"1000\" y=\"100\" dp=\"5\" in=\"0\" out=\"1\" fwd=\"true\" c=\"1\" name=\"Mill10\" /> + 4 segment(s)"),
                    "closed contour on the right side with its tool inside it");
                Assert.That(log, Does.Not.Contain("c=\"2\" name=\"Mill10\" /> +"), "the same contours with the tool on the part side stay");
            });
        }

        [Test]
        public void CleanOutsideOperations_OnlySelectedKinds_LeavesOtherKindsUntouched()
        {
            var (service, result, log) = Clean(OutsideOperationKinds.Bores, 2);
            var program = ReadSavedProgram(service.FullPath, 2);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(log, Does.Contain("removed 5 (5 bore(s), 0 groove(s), 0 mill(s)), 0 tool(s) removed"));
                Assert.That(program.Elements("gr").Count(), Is.EqualTo(5));
                Assert.That(program.Elements().Count(e => e.Name.LocalName is "ms" or "mr" or "me"), Is.EqualTo(14));
            });
        }

        [Test]
        public void CleanOutsideOperations_SeveralParts_CleansEachAndSkipsCleanParts()
        {
            var (service, result, log) = Clean(OutsideOperationKinds.All, 2, 3, 4);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(log, Does.Contain("removed 18 (6 bore(s), 3 groove(s), 9 mill(s))"));
                Assert.That(log, Does.Contain("across 2 part(s)"));
                Assert.That(log, Does.Not.Contain("(id=4)"), "a part with nothing outside is not reported");
                Assert.That(ReadSavedProgram(service.FullPath, 3).Elements("bf").Select(b => b.Attribute("x")!.Value), Is.EqualTo(new[] { "100" }));
                Assert.That(XDocument.Load(service.FullPath).Root!.Attribute("description")!.Value,
                    Does.Contain("cleaned outside bores, grooves, mills"));
            });
        }

        [Test]
        public void CleanOutsideOperations_NothingOutside_ReturnsFalseAndSavesNothing()
        {
            var (_, result, log) = Clean(OutsideOperationKinds.All, 4);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No outside bores, grooves, mills found (unresolved 0)."));
                Assert.That(Directory.GetFiles(_directory, "*_cln*.project"), Is.Empty);
            });
        }

        [Test]
        public void CleanOutsideOperations_NoKindSelected_ReturnsFalseAndLogs()
        {
            var (_, result, log) = Clean(OutsideOperationKinds.None, 2);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No operation types selected for outside cleaning."));
            });
        }

        [Test]
        public void CleanOutsideOperations_RerunOnCleanedFile_NumbersTheOutput()
        {
            var (service, _, _) = Clean(OutsideOperationKinds.Bores, 2);
            service.OpenProject(service.FullPath);

            var log = string.Empty;
            var result = service.CleanOutsideOperations(ref log, [new Part { Id = 2, Name = "part-2" }], OutsideOperationKinds.All);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.FullPath, Does.EndWith("td-clean-outside_cln(1).project"));
            });
        }
    }
}
