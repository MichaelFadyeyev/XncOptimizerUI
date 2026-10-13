using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="Services.Xnc.MillPathOffsetter"/> through <see cref="IXncBatchOperations.OffsetMillPaths"/>
    /// on <c>TestData/td-mill-offset.project</c> (one part per case, ids 2..14).
    /// </summary>
    [TestFixture]
    public class MillPathOffsetterTests : ProjectServicesTestBase
    {
        private const string MillOffsetFixture = "td-mill-offset.project";

        /// <summary>Offsets the given parts of the mill-offset fixture; returns the service, result and log.</summary>
        private (ProjectServices Service, bool Result, string Log) OffsetMills(
            double offset, MillOffsetSide side, params int[] partIds) =>
            OffsetMills(offset, side, MillPathKinds.All, partIds);

        private (ProjectServices Service, bool Result, string Log) OffsetMills(
            double offset, MillOffsetSide side, MillPathKinds kinds, params int[] partIds)
        {
            var service = CreateService();
            service.Session.Open(CopyFixture(MillOffsetFixture));

            var log = string.Empty;
            var result = service.Batch.OffsetMillPaths(partIds.Select(id => new Part { Id = id, Name = $"part-{id}" }).ToList(), offset, side, kinds).Into(ref log);

            return (service, result, log);
        }

        /// <summary>Open-contour parts of the fixture; every other part (6..14) holds a closed contour, rectangle or ellipse.</summary>
        private static readonly int[] OpenMillParts = [2, 3, 4, 5, 9];

        [Test]
        public void OffsetMillPaths_OpenKindOnly_OffsetsOpenPathsAndSkipsClosedMills()
        {
            var (service, result, log) = OffsetMills(3, MillOffsetSide.Right, MillPathKinds.Open, Enumerable.Range(2, 13).ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(log, Does.Contain("offset 5 open mill(s) by 3 mm R, ignored 0, skipped 8"));
                Assert.That(ReadSavedProgram(service.Session.FullPath, 2).Element("ms")!.Attribute("y")!.Value, Is.EqualTo("303"));
                Assert.That(Points(ReadSavedProgram(service.Session.FullPath, 6)),
                    Is.EqualTo(new[] { "100,100", "300,100", "300,200", "100,200", "100,100" }), "closed contour untouched");
                Assert.That(ReadSavedProgram(service.Session.FullPath, 10).Element("mr")!.Attribute("l")!.Value, Is.EqualTo("100"), "rectangle untouched");
                Assert.That(ReadSavedProgram(service.Session.FullPath, 12).Element("me")!.Attribute("l")!.Value, Is.EqualTo("20"), "ellipse untouched");
            });
        }

        [Test]
        public void OffsetMillPaths_ClosedKindOnly_OffsetsClosedMillsAndSkipsOpenPaths()
        {
            var (service, result, log) = OffsetMills(3, MillOffsetSide.Right, MillPathKinds.Closed, Enumerable.Range(2, 13).ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(log, Does.Contain("offset 7 closed mill(s) by 3 mm R, ignored 1, skipped 5"));
                Assert.That(OpenMillParts.Select(id => ReadSavedProgram(service.Session.FullPath, id).Element("ms")!.Attribute("y")!.Value),
                    Is.EqualTo(new[] { "300", "300", "100", "100", "100" }), "open paths untouched");
                Assert.That(Points(ReadSavedProgram(service.Session.FullPath, 6))[0], Is.EqualTo("97,97"));
                Assert.That(ReadSavedProgram(service.Session.FullPath, 10).Element("mr")!.Attribute("l")!.Value, Is.EqualTo("94"), "fwd=\"true\" <mr> travels clockwise, right = inside");
            });
        }

        [Test]
        public void OffsetMillPaths_OnlyUnselectedKind_ReturnsFalseAndSavesNothing()
        {
            var (_, result, log) = OffsetMills(3, MillOffsetSide.Right, MillPathKinds.Closed, OpenMillParts);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mill paths offset (ignored 0, skipped 5)."));
                Assert.That(Directory.GetFiles(TempDirectory, "*_off*.project"), Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_NoKindSelected_ReturnsFalseAndLogs()
        {
            var (_, result, log) = OffsetMills(3, MillOffsetSide.Right, MillPathKinds.None, 2);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mill path types selected for offset."));
            });
        }

        private static string[] Points(XElement program) => program.Elements()
            .Where(e => e.Name.LocalName is "ms" or "ml" or "mac" or "ma")
            .Select(e => $"{e.Attribute("x")!.Value},{e.Attribute("y")!.Value}")
            .ToArray();

        [TestCase(MillOffsetSide.Right, 2, "303")]
        [TestCase(MillOffsetSide.Left, 2, "297")]
        [TestCase(MillOffsetSide.Right, 3, "297")] // fwd="false" travels the path in reverse
        [TestCase(MillOffsetSide.Left, 3, "303")]
        public void OffsetMillPaths_OpenHorizontalPass_ShiftsSidewaysAndKeepsEdgeOvershoot(
            MillOffsetSide side, int partId, string expectedY)
        {
            var (service, result, _) = OffsetMills(3, side, partId);

            Assert.That(result, Is.True);

            var program = ReadSavedProgram(service.Session.FullPath, partId);
            var ms = program.Element("ms")!;
            var ml = program.Element("ml")!;

            Assert.Multiple(() =>
            {
                Assert.That(ms.Attribute("x")!.Value, Is.EqualTo("-10"), "entry keeps its 10 mm overshoot past the left edge");
                Assert.That(ml.Attribute("x")!.Value, Is.EqualTo("dx+10"), "an unchanged value keeps its authored expression");
                Assert.That(ms.Attribute("y")!.Value, Is.EqualTo(expectedY));
                Assert.That(ml.Attribute("y")!.Value, Is.EqualTo(expectedY));
                Assert.That(ms.Attribute("dp")!.Value, Is.EqualTo("5"));
            });
        }

        [Test]
        public void OffsetMillPaths_OpenDiagonalPass_EndsKeepTheirDistanceFromTheCrossedEdges()
        {
            var (service, result, _) = OffsetMills(3, MillOffsetSide.Right, 4);

            Assert.That(result, Is.True);

            // (-10,100) -> (510,620) at 45 deg; right of travel is up-left. The entry slides along
            // x=-10 (left edge overshoot), the end along y=620 (top edge overshoot): 3*sqrt(2) each.
            Assert.That(Points(ReadSavedProgram(service.Session.FullPath, 4)),
                Is.EqualTo(new[] { "-10,104.2426", "505.7574,620" }));
        }

        [Test]
        public void OffsetMillPaths_OpenPathInsidePart_OffsetsEndsPerpendicularlyAndTrimsTheCorner()
        {
            var (service, result, _) = OffsetMills(3, MillOffsetSide.Right, 5);

            Assert.That(result, Is.True);
            Assert.That(Points(ReadSavedProgram(service.Session.FullPath, 5)),
                Is.EqualTo(new[] { "100,103", "197,103", "197,200" }));
        }

        [TestCase(6, MillOffsetSide.Right, new[] { "97,97", "303,97", "303,203", "97,203", "97,97" })]    // CCW travel: right = outside
        [TestCase(6, MillOffsetSide.Left, new[] { "103,103", "297,103", "297,197", "103,197", "103,103" })]
        [TestCase(7, MillOffsetSide.Right, new[] { "103,103", "297,103", "297,197", "103,197", "103,103" })] // fwd="false": CW travel
        public void OffsetMillPaths_ClosedRectangleContour_GrowsOrShrinksPerTraversalDirection(
            int partId, MillOffsetSide side, string[] expected)
        {
            var (service, result, _) = OffsetMills(3, side, partId);

            Assert.That(result, Is.True);
            Assert.That(Points(ReadSavedProgram(service.Session.FullPath, partId)), Is.EqualTo(expected));
        }

        [Test]
        public void OffsetMillPaths_ClosedArcCircle_ChangesRadiusAndKeepsCentres()
        {
            var (service, result, _) = OffsetMills(3, MillOffsetSide.Right, 8);

            Assert.That(result, Is.True);

            var program = ReadSavedProgram(service.Session.FullPath, 8);

            Assert.Multiple(() =>
            {
                Assert.That(Points(program), Is.EqualTo(new[] { "250,277", "227,300", "250,323", "273,300", "250,277" }));
                Assert.That(program.Elements("mac").Select(m => $"{m.Attribute("cx")!.Value},{m.Attribute("cy")!.Value}"),
                    Is.All.EqualTo("250,300"));
                Assert.That(program.Elements("mac").Select(m => m.Attribute("dir")!.Value), Is.All.EqualTo("false"));
                Assert.That(program.Element("ms")!.Attribute("dp")!.Value, Is.EqualTo("contMillDepth"));
            });
        }

        [Test]
        public void OffsetMillPaths_RadiusArc_UpdatesRadiusAndEndPoints()
        {
            var (service, result, _) = OffsetMills(3, MillOffsetSide.Right, 9);

            Assert.That(result, Is.True);

            var program = ReadSavedProgram(service.Session.FullPath, 9);

            // dir="true" is a clockwise sweep, so its centre (420,100) lies right of travel.
            Assert.Multiple(() =>
            {
                Assert.That(Points(program), Is.EqualTo(new[] { "403,100", "437,100" }));
                Assert.That(program.Element("ma")!.Attribute("r")!.Value, Is.EqualTo("17"));
            });
        }

        [TestCase(10, MillOffsetSide.Right, "94", "34", "0")]   // fwd="true": CW, right = inside; corner radius never goes negative
        [TestCase(10, MillOffsetSide.Left, "106", "46", "3")]
        [TestCase(11, MillOffsetSide.Right, "106", "46", "8")]  // fwd="false": CCW, right = outside
        public void OffsetMillPaths_Rectangle_ChangesSizeByTwiceTheOffsetAndRadiusByTheOffset(
            int partId, MillOffsetSide side, string l, string w, string r)
        {
            var (service, result, _) = OffsetMills(3, side, partId);

            Assert.That(result, Is.True);

            var mr = ReadSavedProgram(service.Session.FullPath, partId).Element("mr")!;

            Assert.Multiple(() =>
            {
                Assert.That(mr.Attribute("l")!.Value, Is.EqualTo(l));
                Assert.That(mr.Attribute("w")!.Value, Is.EqualTo(w));
                Assert.That(mr.Attribute("r")!.Value, Is.EqualTo(r));
                Assert.That(mr.Attribute("x")!.Value, Is.EqualTo("500"));
                Assert.That(mr.Attribute("y")!.Value, Is.EqualTo("300"));
            });
        }

        [TestCase(MillOffsetSide.Right, "17", "7")]  // fwd="true": CW, right = inside
        [TestCase(MillOffsetSide.Left, "23", "13")]
        public void OffsetMillPaths_Ellipse_ChangesSemiAxesByTheOffset(MillOffsetSide side, string l, string w)
        {
            var (service, result, _) = OffsetMills(3, side, 12);

            Assert.That(result, Is.True);

            var me = ReadSavedProgram(service.Session.FullPath, 12).Element("me")!;

            Assert.Multiple(() =>
            {
                Assert.That(me.Attribute("l")!.Value, Is.EqualTo(l));
                Assert.That(me.Attribute("w")!.Value, Is.EqualTo(w));
            });
        }

        [Test]
        public void OffsetMillPaths_SharpConvexCorner_IsBridgedByARoundJoinArc()
        {
            var (service, result, _) = OffsetMills(3, MillOffsetSide.Right, 14);

            Assert.That(result, Is.True);

            var program = ReadSavedProgram(service.Session.FullPath, 14);
            var names = program.Elements().Select(e => e.Name.LocalName).ToArray();
            var join = program.Element("mac")!;

            Assert.Multiple(() =>
            {
                Assert.That(names, Is.EqualTo(new[] { "tool", "ms", "ml", "mac", "ml", "ml" }));
                Assert.That(join.Attribute("cx")!.Value, Is.EqualTo("300"));
                Assert.That(join.Attribute("cy")!.Value, Is.EqualTo("420"));
                Assert.That(join.Attribute("dir")!.Value, Is.EqualTo("true"));
            });
        }

        [Test]
        public void OffsetMillPaths_CollapsingMill_IsIgnoredAndNothingIsSaved()
        {
            var (service, result, log) = OffsetMills(3, MillOffsetSide.Right, 13);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mill paths offset (ignored 1, skipped 0)."));
                Assert.That(service.Session.FullPath, Does.EndWith(MillOffsetFixture));
                Assert.That(Directory.GetFiles(TempDirectory, "*_off*.project"), Is.Empty);
            });
        }

        [Test]
        public void OffsetMillPaths_AllParts_SavesOffFileAndLogsCounts()
        {
            var (service, result, log) = OffsetMills(3, MillOffsetSide.Right, Enumerable.Range(2, 13).ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Session.FullPath, Does.EndWith("td-mill-offset_off.project"));
                Assert.That(File.Exists(service.Session.FullPath), Is.True);
                Assert.That(log, Does.Contain("offset 12 open and closed mill(s) by 3 mm R, ignored 1, skipped 0"));
                Assert.That(log, Does.Contain("Mill offset: \"part-13\" (id=13): 0 mill(s) offset, 1 ignored, 0 skipped"));
            });
        }

        [Test]
        public void OffsetMillPaths_RightThenLeft_RestoresTheOriginalContour()
        {
            var (service, _, _) = OffsetMills(3, MillOffsetSide.Right, 6);
            service.Session.Open(service.Session.FullPath); // as the UI does after every operation

            var log = string.Empty;
            Assert.That(service.Batch.OffsetMillPaths([new Part { Id = 6, Name = "closed-rect" }], 3, MillOffsetSide.Left, MillPathKinds.All).Into(ref log), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(service.Session.FullPath, Does.EndWith("td-mill-offset_off(1).project"));
                Assert.That(Points(ReadSavedProgram(service.Session.FullPath, 6)),
                    Is.EqualTo(new[] { "100,100", "300,100", "300,200", "100,200", "100,100" }));
            });
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        public void OffsetMillPaths_NonPositiveOffset_ReturnsFalseAndLogs(double offset)
        {
            var (_, result, log) = OffsetMills(offset, MillOffsetSide.Right, 2);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("Mill path offset must be greater than 0"));
            });
        }

        [Test]
        public void OffsetMillPaths_WithNoParts_ReturnsFalseAndLogs()
        {
            var (_, result, log) = OffsetMills(3, MillOffsetSide.Right);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No parts selected for mill path offset."));
            });
        }
    }
}
