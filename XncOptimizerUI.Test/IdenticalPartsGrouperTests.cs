using System.Xml.Linq;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="Services.Project.IdenticalPartsGrouper"/> through <see cref="IProjectRestructuring.GroupIdenticalElements"/>.
    /// </summary>
    [TestFixture]
    public class IdenticalPartsGrouperTests : ProjectServicesTestBase
    {
        [Test]
        public void GroupIdenticalElements_RunsHeadlessAndReportsThroughTheLog()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);

            var log = string.Empty;

            // The point of the test: this call used to block on MessageBox.Show.
            var result = service.Restructuring.GroupIdenticalElements().Into(ref log);

            Assert.That(log, Is.Not.Empty, "the service must report through the log, not a dialog");

            if (result)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(log, Does.Contain("Stored to:"));
                    Assert.That(service.Session.FullPath, Does.EndWith("_opt.project"));
                    Assert.That(File.Exists(service.Session.FullPath), Is.True);
                });
            }
            else
            {
                Assert.That(log, Does.Contain("already optimized").Or.Contains("no XNC"));
            }
        }

        [Test]
        public void GroupIdenticalElements_WhenRunTwice_ReportsAlreadyOptimized()
        {
            var service = OpenAndRead(CreateService(), ProjectPath);

            var log = string.Empty;

            if (!service.Restructuring.GroupIdenticalElements().Into(ref log))
            {
                Assert.Ignore("The fixture is already optimized; the second-pass case cannot be exercised.");
            }

            // Re-open the file the first pass produced and optimize it again.
            var second = OpenAndRead(CreateService(), service.Session.FullPath);

            var secondLog = string.Empty;
            var result = second.Restructuring.GroupIdenticalElements().Into(ref secondLog);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(secondLog, Does.Contain("File seems to be already optimized or contains no XNC."));
            });
        }

        [Test]
        public void GroupIdenticalElements_WithDifferentBandMaterial_KeepsPartsInSeparateGroups()
        {
            // 301.07.03 and 301.08.03 share one program and are grouped as [01]; a different top
            // band material on the second must split them (it was ignored: `|| true`).
            var path = CopyFixture("td-execute-optimize.project");
            var doc = XDocument.Load(path);
            doc.Descendants("part")
                .Single(p => (string?)p.Attribute("name") == "301.08.03.підріз.47=ПАН-ВРХ")
                .SetAttributeValue("eltMat", "Крайка 42х2мм");
            doc.Save(path);

            var service = OpenAndRead(CreateService(), path);
            var log = string.Empty;
            Assert.That(service.Restructuring.GroupIdenticalElements().Into(ref log), Is.True, log);

            var names = XDocument.Load(service.Session.FullPath).Descendants("part")
                .Select(p => (string?)p.Attribute("name"))
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(names, Does.Contain("[01]301.07.03.підріз.47=ПАН-ВРХ"));
                Assert.That(names, Has.None.EqualTo("[01]301.08.03.підріз.47=ПАН-ВРХ"));
                Assert.That(names, Has.Some.EndsWith("]301.08.03.підріз.47=ПАН-ВРХ"));
            });
        }

        [Test]
        public void GroupIdenticalElements_ListsEachCutOperationSheetOnce()
        {
            var service = OpenAndRead(CreateService(), CopyFixture("td-execute-optimize.project"));
            var log = string.Empty;
            Assert.That(service.Restructuring.GroupIdenticalElements().Into(ref log), Is.True, log);

            var cutOperations = XDocument.Load(service.Session.FullPath).Root!.Elements("operation")
                .Where(o => (string?)o.Attribute("typeId") == "CS")
                .ToList();

            Assert.Multiple(() =>
            {
                foreach (var operation in cutOperations)
                {
                    var ids = operation.Elements("part").Select(p => (string?)p.Attribute("id")).ToList();
                    Assert.That(ids, Is.Unique, $"CS operation {(string?)operation.Attribute("id")} lists a part twice");
                }
            });
        }

        [Test]
        public void GroupIdenticalElements_DoesNotTouchSourceFileOnDisk()
        {
            var path = CopyFixture("td-execute-optimize.project");
            var bytesBefore = File.ReadAllBytes(path);

            var service = OpenAndRead(CreateService(), path);
            var log = string.Empty;

            var result = service.Restructuring.GroupIdenticalElements().Into(ref log);

            var bytesAfter = File.ReadAllBytes(path);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(bytesAfter, Is.EqualTo(bytesBefore),
                    "GroupIdenticalElements must save the grouped result to a new _opt file, never overwrite the source it read");
                Assert.That(service.Session.FullPath, Does.EndWith("_opt.project"));
            });
        }
    }
}
