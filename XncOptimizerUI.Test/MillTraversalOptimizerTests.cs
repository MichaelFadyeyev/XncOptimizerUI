using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="Services.Xnc.MillTraversalOptimizer"/> through <see cref="IXncBatchOperations.OptimizeMillTraversal"/>.
    /// </summary>
    [TestFixture]
    public class MillTraversalOptimizerTests : ProjectServicesTestBase
    {
        [Test]
        public void OptimizeMillTraversal_ContourEndingWithRadiusArc_IsNotTreatedAsStraightPass()
        {
            var service = OpenSingleProgramProject(LineThenRadiusArcProgram);
            var log = string.Empty;

            var result = service.Batch.OptimizeMillTraversal([new Part { Id = 1, Name = "panel" }]).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False, "the ml+ma contour is not a straight pass, so no pair is left to reorder");
                Assert.That(log, Does.Contain("No mill passes reordered (ignored 1)"));
            });
        }

        [Test]
        public void OptimizeMillTraversal_ReordersParallelPassesIntoSerpentineOrder()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-mills-optimization.project"));

            var log = string.Empty;
            var result = service.Batch.OptimizeMillTraversal([new Part { Id = 1, Name = "as-is" }]).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Session.FullPath, Does.EndWith("_mo.project"));
                Assert.That(File.Exists(service.Session.FullPath), Is.True);
                Assert.That(log, Does.Contain("reordered 3 pass(es)"));
                Assert.That(log, Does.Contain("Stored to:"));
            });

            var optimised = service.Reader.ReadXncPrograms(1).Single();
            var oracle = service.Reader.ReadXncPrograms(2).Single(); // the hand-authored "optimized" part

            Assert.That(optimised.MillingContours, Has.Count.EqualTo(6));

            // The pass order (by Y) is preserved; only the entry/exit ends of passes 2, 4 and 6
            // are swapped so every pass starts where the previous one finished.
            var expectedEntryX = new[] { -5d, 1385d, -5d, 1385d, -5d, 1385d };
            var expectedY = new[] { 5d, 45d, 85d, 125d, 165d, 205d };

            for (var i = 0; i < 6; i++)
            {
                var contour = optimised.MillingContours[i];
                var end = contour.Segments.Single().End;
                var index = i;

                Assert.Multiple(() =>
                {
                    Assert.That(contour.Entry.Y, Is.EqualTo(expectedY[index]));
                    Assert.That(contour.Entry.X, Is.EqualTo(expectedEntryX[index]));
                    Assert.That(end.X, Is.EqualTo(expectedEntryX[index] == -5d ? 1385d : -5d));
                    Assert.That(end.Y, Is.EqualTo(expectedY[index]));

                    // identical to the hand-authored oracle part in the same fixture
                    Assert.That(contour.Entry.X, Is.EqualTo(oracle.MillingContours[index].Entry.X));
                    Assert.That(end.X, Is.EqualTo(oracle.MillingContours[index].Segments.Single().End.X));
                });
            }
        }

        [Test]
        public void OptimizeMillTraversal_AlreadyOptimised_ReturnsFalseAndSavesNothing()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-mills-optimization.project"));
            var pathBefore = service.Session.FullPath;

            var log = string.Empty;
            var result = service.Batch.OptimizeMillTraversal([new Part { Id = 2, Name = "optimized" }]).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mill passes reordered"));
                Assert.That(service.Session.FullPath, Is.EqualTo(pathBefore), "nothing reordered => nothing saved");
            });
        }

        [Test]
        public void OptimizeMillTraversal_WithNoParts_ReturnsFalseAndLogs()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-mills-optimization.project"));

            var log = string.Empty;
            var result = service.Batch.OptimizeMillTraversal([]).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No parts selected for mill traversal optimization."));
            });
        }
    }
}
