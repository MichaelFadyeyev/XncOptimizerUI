using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="IXncBatchOperations.SortToolDeclarations"/> on a whole project; the sort itself is
    /// covered by <see cref="XncToolDeclarationSorterTests"/>.
    /// </summary>
    [TestFixture]
    public class ToolSortBatchTests : ProjectServicesTestBase
    {
        [Test]
        public void SortToolDeclarations_MovesToolsToProgramStart_OrderedByKindThenDiameter()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-programs.project"));
            var before = service.Reader.ReadXncPrograms(1).Single(p => p.Side);

            var log = string.Empty;
            var result = service.Batch.SortToolDeclarations([new Part { Id = 1, Name = "panel" }]).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Session.FullPath, Does.EndWith("_to.project"));
                Assert.That(File.Exists(service.Session.FullPath), Is.True);
                Assert.That(log, Does.Contain("sorted 2 program(s), 0 skipped"));
                Assert.That(log, Does.Contain("Stored to:"));
            });

            var programs = service.Reader.ReadXncPrograms(1);
            var front = programs.Single(p => p.Side);
            var back = programs.Single(p => !p.Side);

            Assert.Multiple(() =>
            {
                Assert.That(front.Tools.Select(t => t.Name), Is.EqualTo(new[] { "Bore8", "Bore10", "Cut3.2", "Mill6" }));
                Assert.That(back.Tools.Select(t => t.Name), Is.EqualTo(new[] { "Bore8", "Bore15" }));

                // the machining itself is unchanged, only the declarations moved
                Assert.That(front.Bores.Select(b => (b.X, b.Y, b.Depth)), Is.EqualTo(before.Bores.Select(b => (b.X, b.Y, b.Depth))));
                Assert.That(front.Groovings, Has.Count.EqualTo(before.Groovings.Count));
                Assert.That(front.MillingContours.Select(c => c.Segments.Count), Is.EqualTo(before.MillingContours.Select(c => c.Segments.Count)));
                Assert.That(front.MillingRectangles, Has.Count.EqualTo(before.MillingRectangles.Count));
            });
        }

        [Test]
        public void SortToolDeclarations_AlreadySorted_ReturnsFalseAndSavesNothing()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-programs.project"));

            var log = string.Empty;
            service.Batch.SortToolDeclarations([new Part { Id = 1, Name = "panel" }]).Into(ref log);
            var sortedPath = service.Session.FullPath;

            log = string.Empty;
            var result = service.Batch.SortToolDeclarations([new Part { Id = 1, Name = "panel" }]).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No tool declarations reordered (skipped 0 program(s))."));
                Assert.That(service.Session.FullPath, Is.EqualTo(sortedPath), "nothing reordered => nothing saved");
            });
        }

        [Test]
        public void SortToolDeclarations_WithNoParts_ReturnsFalseAndLogs()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-programs.project"));

            var log = string.Empty;
            var result = service.Batch.SortToolDeclarations([]).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No parts selected for tool declaration sorting."));
            });
        }
    }
}
