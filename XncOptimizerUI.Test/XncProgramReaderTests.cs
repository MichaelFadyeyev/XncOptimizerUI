using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Exercises <see cref="XncProgramReader"/> against the checked-in fixture
    /// <c>TestData/td-programs.project</c>. Expected numbers follow the worked example
    /// in <c>.claude/xnc-program-read.md</c> §7.
    /// </summary>
    [TestFixture]
    public class XncProgramReaderTests
    {
        private static List<XncProgram> ReadFixture() => ReadPrograms("td-programs.project");

        private static List<XncProgram> ReadPrograms(string fixtureFileName)
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", fixtureFileName);
            var doc = XDocument.Load(path);

            return doc.Root!
                .Elements("operation")
                .Where(o => o.GetTypeIdValue() == "XNC")
                .Select(XncProgramReader.Read)
                .ToList();
        }

        [Test]
        public void Reads_bothXncOperations_withProgramBoxAndSide()
        {
            var programs = ReadFixture();

            Assert.That(programs, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(programs[0].Side, Is.True);
                Assert.That(programs[1].Side, Is.False);
                Assert.That(programs[0].Dx, Is.EqualTo(1380));
                Assert.That(programs[0].Dy, Is.EqualTo(600));
                Assert.That(programs[0].Dz, Is.EqualTo(19));
            });
        }

        [Test]
        public void Reads_tools_withNameAndDiameter()
        {
            var program = ReadFixture()[0];

            Assert.That(
                program.Tools.Select(t => t.Name),
                Is.EqualTo(new[] { "Bore8", "Bore10", "Cut3.2", "Mill6" }));
            Assert.That(program.Tools.Single(t => t.Name == "Mill6").Diameter, Is.EqualTo(6));
        }

        [Test]
        public void Reads_edgeBores_withPinnedEdgeAndResolvedDepth()
        {
            var bores = ReadFixture()[0].Bores;

            Assert.That(bores, Has.Count.EqualTo(4));
            Assert.Multiple(() =>
            {
                Assert.That(bores, Has.All.Property(nameof(XncBore.Surface)).EqualTo(BoreSurface.Left));
                Assert.That(bores, Has.All.Property(nameof(XncBore.ToolName)).EqualTo("Bore8"));
                Assert.That(bores, Has.All.Property(nameof(XncBore.X)).EqualTo(0));   // left edge is pinned to x = 0
                Assert.That(bores, Has.All.Property(nameof(XncBore.Z)).EqualTo(10));
                Assert.That(bores.Select(b => b.Y), Is.EqualTo(new[] { 65d, 105d, 505d, 545d }));
                Assert.That(bores.Select(b => b.Depth), Is.EqualTo(new[] { 34d, 26d, 26d, 34d }));
            });
        }

        [Test]
        public void Reads_faceBores_fromTheSecondProgram()
        {
            var bores = ReadFixture()[1].Bores;

            Assert.That(bores, Has.Count.EqualTo(4));
            Assert.Multiple(() =>
            {
                Assert.That(bores, Has.All.Property(nameof(XncBore.Surface)).EqualTo(BoreSurface.Face));
                Assert.That(bores, Has.All.Property(nameof(XncBore.Through)).EqualTo(false));
                Assert.That(
                    bores.Select(b => (b.X, b.Y)),
                    Is.EqualTo(new[] { (1353d, 65d), (1353d, 545d), (34d, 65d), (34d, 545d) }));
                Assert.That(
                    bores.Select(b => b.ToolName),
                    Is.EqualTo(new[] { "Bore8", "Bore8", "Bore15", "Bore15" }));
                Assert.That(bores.Select(b => b.Depth), Is.EqualTo(new[] { 12d, 12d, 14d, 14d }));
            });
        }

        [Test]
        public void Reads_allThreeGroovings_coveringEveryToolPosition()
        {
            var groovings = ReadFixture()[0].Groovings;

            Assert.That(groovings, Has.Count.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(groovings, Has.All.Property(nameof(XncGrooving.ToolName)).EqualTo("Cut3.2"));
                Assert.That(groovings, Has.All.Property(nameof(XncGrooving.Depth)).EqualTo(4));
                Assert.That(groovings, Has.All.Property(nameof(XncGrooving.Width)).EqualTo(10));
                Assert.That(groovings, Has.All.Property(nameof(XncGrooving.Comment)).EqualTo("Паз15 ()"));

                // c = 0 / 2 / 1  ->  Center / Left / Right
                Assert.That(
                    groovings.Select(g => g.Position),
                    Is.EqualTo(new[] { ToolPosition.Center, ToolPosition.Left, ToolPosition.Right }));

                // y1 = 565 / "565-80" / "565-160"
                Assert.That(groovings.Select(g => g.Start.Y), Is.EqualTo(new[] { 565d, 485d, 405d }));
                Assert.That(groovings[0].Start, Is.EqualTo(new XncPoint(-10, 565)));
                Assert.That(groovings[0].End, Is.EqualTo(new XncPoint(1390, 565)));   // x2 = "dx+10"
            });
        }

        [Test]
        public void Reads_millingLineContours_withExpressionEntryPointsAndPositions()
        {
            var contours = ReadFixture()[0].MillingContours;

            Assert.That(contours, Has.Count.EqualTo(4));   // 3 line contours + 1 arc contour

            var lines = contours.Take(3).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(lines, Has.All.Property(nameof(XncMillingContour.ToolName)).EqualTo("Bore10"));
                Assert.That(lines, Has.All.Property(nameof(XncMillingContour.EntryDepth)).EqualTo(4));
                Assert.That(lines, Has.All.Property(nameof(XncMillingContour.StartOffsetXY)).EqualTo(5)); // tool.dia/2, d = 10

                // y = "dy-35-40" / "dy-35-120" / "dy-35-200"
                Assert.That(lines.Select(c => c.Entry.Y), Is.EqualTo(new[] { 525d, 445d, 365d }));
                // c = 0 / 2 / 2  ->  Center / Left / Left
                Assert.That(
                    lines.Select(c => c.Position),
                    Is.EqualTo(new[] { ToolPosition.Center, ToolPosition.Left, ToolPosition.Left }));
            });

            var firstMove = (XncLineSegment)lines[0].Segments.Single();
            Assert.Multiple(() =>
            {
                Assert.That(firstMove.End, Is.EqualTo(new XncPoint(1390, 525)));   // x = "dx+10"
                Assert.That(firstMove.Depth, Is.EqualTo(40));
            });
        }

        [Test]
        public void Reads_arcContour_resolvingCustomVariableDepthAndCircleGeometry()
        {
            var circle = ReadFixture()[0].MillingContours[3];

            Assert.Multiple(() =>
            {
                Assert.That(circle.ToolName, Is.EqualTo("Mill6"));
                Assert.That(circle.Entry, Is.EqualTo(new XncPoint(250, 382.5)));
                Assert.That(circle.EntryDepth, Is.EqualTo(21));               // dp = contMillDepth = dz + 2
                Assert.That(circle.Position, Is.EqualTo(ToolPosition.Left));  // c = 2
                Assert.That(circle.LeadOut, Is.EqualTo(1));
                Assert.That(circle.Segments, Has.Count.EqualTo(4));
                Assert.That(circle.Segments, Has.All.InstanceOf<XncArcSegment>());
            });

            foreach (var arc in circle.Segments.Cast<XncArcSegment>())
            {
                Assert.That(arc.Center, Is.EqualTo(new XncPoint(250, 400)));
                Assert.That(arc.Radius, Is.EqualTo(17.5).Within(1e-9));
                Assert.That(arc.Clockwise, Is.True);                          // dir = "false"
            }
        }

        [Test]
        public void Reads_millingRectangle_asPocketPrimitive()
        {
            var rect = ReadFixture()[0].MillingRectangles.Single();

            Assert.Multiple(() =>
            {
                Assert.That(rect.ToolName, Is.EqualTo("Mill6"));
                Assert.That(rect.Origin, Is.EqualTo(new XncPoint(100, 100)));
                Assert.That(rect.Length, Is.EqualTo(100));
                Assert.That(rect.Width, Is.EqualTo(20));
                Assert.That(rect.Angle, Is.EqualTo(0));
                Assert.That(rect.CornerRadius, Is.EqualTo(0));
                Assert.That(rect.Depth, Is.EqualTo(8));
                Assert.That(rect.Position, Is.EqualTo(ToolPosition.Pocket));  // c = 3
                Assert.That(rect.StartOffsetXY, Is.EqualTo(3));               // tool.dia/2, Mill6 d = 6
            });
        }

        [Test]
        public void Reads_millingSegmentsThatOmitDp_carryingTheContourDepthForward()
        {
            // Real export where every <ml>/<mac> has no dp — the cut stays at the entry depth.
            var programs = ReadPrograms("td-missing-ml-error.project");

            Assert.That(programs, Has.Count.EqualTo(2));

            var contour = programs[0].MillingContours.Single();
            Assert.Multiple(() =>
            {
                Assert.That(contour.EntryDepth, Is.EqualTo(38));          // contMillDepth = dz + 2, dz = 36
                Assert.That(contour.Segments, Has.Count.EqualTo(3));      // ml, mac, ml
                Assert.That(contour.Segments.Select(s => s.Depth), Is.EqualTo(new[] { 38d, 38d, 38d }));
                Assert.That(contour.Segments[0], Is.InstanceOf<XncLineSegment>());
                Assert.That(contour.Segments[1], Is.InstanceOf<XncArcSegment>());
                Assert.That(contour.Segments[2], Is.InstanceOf<XncLineSegment>());
            });
        }

        [Test]
        public void Exposes_customVariables_withCaseInsensitiveKeys()
        {
            var program = ReadFixture()[0];

            Assert.Multiple(() =>
            {
                Assert.That(program.Variables.ContainsKey("CONTMILLDEPTH"), Is.True);
                Assert.That(program.Variables["contMillDepth"], Is.EqualTo(21));
            });
        }
    }
}
