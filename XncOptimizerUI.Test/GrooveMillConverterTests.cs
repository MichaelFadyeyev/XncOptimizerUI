using System.Xml.Linq;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="Services.Xnc.Conversions.GrooveMillConverter"/> through <see cref="IXncBatchOperations.ConvertGroovesAndMills"/>.
    /// </summary>
    [TestFixture]
    public class GrooveMillConverterTests : ProjectServicesTestBase
    {
        [Test]
        public void ConvertMillsToGrooves_ContourEndingWithRadiusArc_IsLeftUntouched()
        {
            var service = OpenSingleProgramProject(LineThenRadiusArcProgram);
            var log = string.Empty;

            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 1, Name = "panel" }], GrooveMillDirection.MillsToGrooves, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, log);
                Assert.That(log, Does.Contain("1 mill(s) -> groove(s), 0 tool(s) removed, 1 ignored"),
                    "only the straight one-segment pass converts; the ml+ma contour is ignored");
                Assert.That(ReadProgramOf(service.Session.FullPath).Elements("ma"), Has.Exactly(1).Items);
                Assert.That(ReadProgramOf(service.Session.FullPath).Elements("ml"), Has.Exactly(1).Items,
                    "the ma's own contour keeps its ms + ml");
            });
        }

        private const string ShallowStraightMillProgram = """
            <program dx="200" dy="100" dz="18">
              <tool name="Mill6" d="6" />
              <ms x="0" y="50" dp="5" c="0" name="Mill6" />
              <ml x="200" y="50" dp="5" />
            </program>
            """;

        [Test]
        public void ConvertMillsToGrooves_CutsWithFirstConfiguredGroovingWidth()
        {
            Config.GroovingToolWidths.Returns(new List<decimal> { 3.2m, 2.8m });
            var service = OpenSingleProgramProject(ShallowStraightMillProgram);
            var log = string.Empty;

            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 1, Name = "panel" }], GrooveMillDirection.MillsToGrooves, processPockets: false).Into(ref log);

            var program = ReadProgramOf(service.Session.FullPath);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, log);
                Assert.That(program.Elements("tool").Select(t => (string?)t.Attribute("name")), Is.EqualTo(new[] { "Cut3.2" }));
                Assert.That((string?)program.Element("gr")!.Attribute("name"), Is.EqualTo("Cut3.2"));
            });
        }

        [Test]
        public void ConvertMillsToGrooves_WithoutConfiguredGroovingWidth_IsCancelled()
        {
            Config.GroovingToolWidths.Returns(new List<decimal>());
            var service = OpenSingleProgramProject(ShallowStraightMillProgram);
            var log = string.Empty;

            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 1, Name = "panel" }], GrooveMillDirection.MillsToGrooves, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No grooving tools configured (AppOptions.GroovingToolWidths is empty)."));
            });
        }

        private static XElement ReadProgramOf(string path) =>
            XDocument.Parse(XDocument.Load(path).Descendants("operation").Single().Attribute("program")!.Value).Root!;

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_ConvertsGrooveAddsBoreToolAndDropsOrphan()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-grooving.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Session.FullPath, Does.EndWith("_gm.project"));
                Assert.That(File.Exists(service.Session.FullPath), Is.True);
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("1 tool(s) added"));
                Assert.That(log, Does.Contain("1 tool(s) removed"));
            });

            var program = service.Reader.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.Groovings, Is.Empty);
                Assert.That(program.MillingContours, Has.Count.EqualTo(1));
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(10d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Mill10"));
                // Cut3.2 was the groove's only user; after conversion nothing references it.
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Cut3.2"));
            });

            var contour = program.MillingContours.Single();
            var segment = contour.Segments.Single();

            Assert.Multiple(() =>
            {
                // groove ran along X at y=50; overshoot = tool.dia/2 = width/2 = 5
                Assert.That(contour.Entry.X, Is.EqualTo(-5d));    // x1=-10 -> -5
                Assert.That(contour.Entry.Y, Is.EqualTo(50d));
                Assert.That(segment.End.X, Is.EqualTo(10005d));   // x2=dx+10 -> dx+5
                Assert.That(segment.End.Y, Is.EqualTo(50d));
                Assert.That(contour.EntryDepth, Is.EqualTo(4d));
                Assert.That(contour.Position, Is.EqualTo(ToolPosition.Right)); // groove c="1"
            });
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_DoesNotReuseUnrelatedSameDiameterTool()
        {
            var service = CreateService();
            var path = CopyFixture("td-grooving.project");
            var xml = File.ReadAllText(path)
                .Replace("&lt;tool name=&quot;Cut3.2&quot; d=&quot;3.2&quot;/&gt;",
                    "&lt;tool name=&quot;Bore10&quot; d=&quot;10&quot;/&gt;&lt;tool name=&quot;Cut3.2&quot; d=&quot;3.2&quot;/&gt;");
            File.WriteAllText(path, xml);
            service.Session.Open(path);

            var log = string.Empty;
            Assert.That(service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }],
                GrooveMillDirection.GroovesToMills, processPockets: false).Into(ref log), Is.True);

            var program = service.Reader.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours.Single().ToolName, Is.EqualTo("Mill10"));
                Assert.That(program.Tools.Single(t => t.Name == "Bore10").Diameter, Is.EqualTo(10d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Bore10"));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Mill10"));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_KeepsToolStillUsedByAnUnconvertedGroove()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-grooving-mixed.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("0 tool(s) removed"));
            });

            var program = service.Reader.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours, Has.Count.EqualTo(1));
                Assert.That(program.Groovings, Has.Count.EqualTo(1), "the diagonal groove is left untouched");
                // Cut3.2 is still referenced by that diagonal groove, so it must survive.
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Cut3.2"));
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(10d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_ConvertsShallowSingleSegmentMill()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-milling-shallow.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.MillsToGrooves, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Session.FullPath, Does.EndWith("_gm.project"));
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Cut2.8
                Assert.That(log, Does.Contain("1 tool(s) removed")); // orphaned Mill6
            });

            var program = service.Reader.ReadXncPrograms(2).Single();

            Assert.That(program.MillingContours, Is.Empty);

            var groove = program.Groovings.Single();

            Assert.Multiple(() =>
            {
                // the groove is cut by the fixed 2.8 mm grooving tool, not the mill's Mill6
                Assert.That(groove.ToolName, Is.EqualTo("Cut2.8"));
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(2.8d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Mill6"));
                Assert.That(groove.Width, Is.EqualTo(6d));   // t = original mill (Mill6) diameter
                Assert.That(groove.Depth, Is.EqualTo(5d));
                Assert.That(groove.Position, Is.EqualTo(ToolPosition.Left)); // ms c="2"
                // entry x=-20 and end x=1020 lie outside [0,1000] -> clamped onto the edge
                Assert.That(groove.Start.X, Is.EqualTo(0d));
                Assert.That(groove.Start.Y, Is.EqualTo(80d));
                Assert.That(groove.End.X, Is.EqualTo(1000d));
                Assert.That(groove.End.Y, Is.EqualTo(80d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_DoesNotReuseUnrelatedSameDiameterTool()
        {
            var service = CreateService();
            var path = CopyFixture("td-milling-shallow.project");
            var xml = File.ReadAllText(path)
                .Replace("&lt;tool name=&quot;Mill6&quot; d=&quot;6&quot;/&gt;",
                    "&lt;tool name=&quot;Bore2.8&quot; d=&quot;2.8&quot;/&gt;&lt;tool name=&quot;Mill6&quot; d=&quot;6&quot;/&gt;");
            File.WriteAllText(path, xml);
            service.Session.Open(path);

            var log = string.Empty;
            Assert.That(service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }],
                GrooveMillDirection.MillsToGrooves, processPockets: false).Into(ref log), Is.True);

            var program = service.Reader.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.Groovings.Single().ToolName, Is.EqualTo("Cut2.8"));
                Assert.That(program.Tools.Single(t => t.Name == "Bore2.8").Diameter, Is.EqualTo(2.8d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Bore2.8"));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Cut2.8"));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_IgnoresThroughMillAndRectangle()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-milling.project"));
            var pathBefore = service.Session.FullPath;

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.MillsToGrooves, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mills converted"));
                Assert.That(log, Does.Contain("ignored 2")); // through-mill contour + <mr>
                Assert.That(service.Session.FullPath, Is.EqualTo(pathBefore), "nothing converted => nothing saved");
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_WithProcessPockets_ConvertsAxisParallelRectangularPockets()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-milling-pocket.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.MillsToGrooves, processPockets: true).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Session.FullPath, Does.EndWith("_gm.project"));
                Assert.That(File.Exists(service.Session.FullPath), Is.True);
                Assert.That(log, Does.Contain("converted 2"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Cut2.8
                Assert.That(log, Does.Contain("ignored 3"));         // rotated + frame + through-depth
            });

            var program = service.Reader.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.Groovings, Has.Count.EqualTo(2));
                Assert.That(program.MillingRectangles, Has.Count.EqualTo(3), "rotated / non-pocket / through-depth rectangles are left alone");
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(2.8d));
            });

            // #1: l=200 >= w=30 -> groove along X, width = w, centred on y
            var alongX = program.Groovings[0];
            // #2: w=160 > l=25 -> groove along Y, width = l, centred on x
            var alongY = program.Groovings[1];

            Assert.Multiple(() =>
            {
                Assert.That(alongX.ToolName, Is.EqualTo("Cut2.8"));
                Assert.That(alongX.Start.X, Is.EqualTo(200d));
                Assert.That(alongX.Start.Y, Is.EqualTo(150d));
                Assert.That(alongX.End.X, Is.EqualTo(400d));
                Assert.That(alongX.End.Y, Is.EqualTo(150d));
                Assert.That(alongX.Width, Is.EqualTo(30d));
                Assert.That(alongX.Depth, Is.EqualTo(6d));
                Assert.That(alongX.Position, Is.EqualTo(ToolPosition.Center));

                Assert.That(alongY.Start.X, Is.EqualTo(700d));
                Assert.That(alongY.Start.Y, Is.EqualTo(70d));
                Assert.That(alongY.End.X, Is.EqualTo(700d));
                Assert.That(alongY.End.Y, Is.EqualTo(230d));
                Assert.That(alongY.Width, Is.EqualTo(25d));
                Assert.That(alongY.Depth, Is.EqualTo(5d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_WithoutProcessPockets_LeavesRectanglesAlone()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-milling-pocket.project"));
            var pathBefore = service.Session.FullPath;

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.MillsToGrooves, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mills converted"));
                Assert.That(log, Does.Contain("ignored 5"));
                Assert.That(service.Session.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.Reader.ReadXncPrograms(2).Single().MillingRectangles, Has.Count.EqualTo(5));
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_WithProcessPockets_ConvertsRectangularContourPockets()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-pocket-to-groove.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 1, Name = "402.07.01.ПАН-640" }], GrooveMillDirection.MillsToGrooves, processPockets: true).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Session.FullPath, Does.EndWith("_gm.project"));
                Assert.That(File.Exists(service.Session.FullPath), Is.True);
                Assert.That(log, Does.Contain("converted 2"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Cut2.8
                Assert.That(log, Does.Contain("1 tool(s) removed")); // orphaned Mill6
                Assert.That(log, Does.Contain("ignored 0"));
            });

            var program = service.Reader.ReadXncPrograms(1).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours, Is.Empty);
                Assert.That(program.Groovings, Has.Count.EqualTo(2));
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(2.8d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Mill6"));
            });

            // contour 1: rect X in [120,130], Y in [-5,1385] -> groove along Y, centred x=125,
            // ends clamped onto the part borders [0, dy=1380]
            var alongY = program.Groovings[0];
            // contour 2: rect X in [-5,645], Y in [550,560] -> groove along X, centred y=555,
            // ends clamped onto the part borders [0, dx=640]
            var alongX = program.Groovings[1];

            Assert.Multiple(() =>
            {
                Assert.That(alongY.ToolName, Is.EqualTo("Cut2.8"));
                Assert.That(alongY.Start.X, Is.EqualTo(125d));
                Assert.That(alongY.Start.Y, Is.EqualTo(0d));
                Assert.That(alongY.End.X, Is.EqualTo(125d));
                Assert.That(alongY.End.Y, Is.EqualTo(1380d));
                Assert.That(alongY.Width, Is.EqualTo(10d));
                Assert.That(alongY.Depth, Is.EqualTo(4d));
                Assert.That(alongY.Position, Is.EqualTo(ToolPosition.Center));
                Assert.That(alongY.Comment, Is.EqualTo("Виїмка G=4 (В4)"));

                Assert.That(alongX.Start.X, Is.EqualTo(0d));
                Assert.That(alongX.Start.Y, Is.EqualTo(555d));
                Assert.That(alongX.End.X, Is.EqualTo(640d));
                Assert.That(alongX.End.Y, Is.EqualTo(555d));
                Assert.That(alongX.Width, Is.EqualTo(10d));
                Assert.That(alongX.Depth, Is.EqualTo(4d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_MillsToGrooves_WithoutProcessPockets_LeavesContourPocketsAlone()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-pocket-to-groove.project"));
            var pathBefore = service.Session.FullPath;

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 1, Name = "402.07.01.ПАН-640" }], GrooveMillDirection.MillsToGrooves, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mills converted"));
                Assert.That(log, Does.Contain("ignored 2"));
                Assert.That(service.Session.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.Reader.ReadXncPrograms(1).Single().MillingContours, Has.Count.EqualTo(2));
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_IgnoresSecondaryPassGroove()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-grooving-secondary-pass.project"));
            var pathBefore = service.Session.FullPath;

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No grooves converted"));
                Assert.That(log, Does.Contain("ignored 1"));
                Assert.That(service.Session.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.Reader.ReadXncPrograms(2).Single().Groovings, Has.Count.EqualTo(1),
                "a p!=0 (secondary-pass) groove must be left untouched");
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_IgnoresDiagonalGroove()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-grooving-diagonal.project"));
            var pathBefore = service.Session.FullPath;

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No grooves converted"));
                Assert.That(log, Does.Contain("ignored 1"));
                Assert.That(service.Session.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.Reader.ReadXncPrograms(2).Single().Groovings, Has.Count.EqualTo(1),
                "a diagonal groove must be left untouched");
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_OffSizeWidth_ConvertsToMillRectangleWithSmallestCutter()
        {
            var service = CreateService();
            // t=10 groove, but the shop only has Ø6 and Ø20 cutters -> milled out as a pocket.
            Config.MillingToolDiams.Returns(new List<decimal> { 6.0m, 20.0m });
            service.Session.Open(CopyFixture("td-grooving.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Session.FullPath, Does.EndWith("_gm.project"));
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Mill6
                Assert.That(log, Does.Contain("1 tool(s) removed")); // orphaned Cut3.2
            });

            var program = service.Reader.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.Groovings, Is.Empty);
                Assert.That(program.MillingContours, Is.Empty);
                Assert.That(program.MillingRectangles, Has.Count.EqualTo(1));
                Assert.That(program.Tools.Select(t => t.Diameter), Does.Contain(6d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Cut3.2"));
            });

            var rect = program.MillingRectangles.Single();

            Assert.Multiple(() =>
            {
                Assert.That(rect.ToolName, Is.EqualTo("Mill6"));
                Assert.That(rect.Position, Is.EqualTo(ToolPosition.Pocket));   // c="3"
                // groove ran along X at y=50; ends x1=-10 / x2=dx+10 are outside [0,10000]
                // -> pushed a further toolDiam/2 = 3 past each edge: [-3, 10003], length 10006.
                Assert.That(rect.Length, Is.EqualTo(10006d));
                Assert.That(rect.Width, Is.EqualTo(10d));                     // short side = groove width t
                Assert.That(rect.Origin.X, Is.EqualTo(5000d));               // rectangle centre
                Assert.That(rect.Origin.Y, Is.EqualTo(50d));
                Assert.That(rect.Depth, Is.EqualTo(4d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_OffSizeWidth_InteriorGroove_RectangleIsNotExtended()
        {
            var service = CreateService();
            Config.MillingToolDiams.Returns(new List<decimal> { 6.0m, 20.0m });
            service.Session.Open(CopyFixture("td-grooving-interior.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false).Into(ref log);

            Assert.That(result, Is.True);

            var rect = service.Reader.ReadXncPrograms(2).Single().MillingRectangles.Single();

            Assert.Multiple(() =>
            {
                // groove x1=100 / x2=900 are both inside [0,1000] -> no overshoot.
                Assert.That(rect.Length, Is.EqualTo(800d));
                Assert.That(rect.Width, Is.EqualTo(10d));
                Assert.That(rect.Origin.X, Is.EqualTo(500d));
                Assert.That(rect.Origin.Y, Is.EqualTo(250d));
            });
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_SmallestCutterWiderThanGroove_IgnoresGroove()
        {
            var service = CreateService();
            // Only a Ø20 cutter available; the t=10 groove cannot be milled cleanly.
            Config.MillingToolDiams.Returns(new List<decimal> { 20.0m });
            service.Session.Open(CopyFixture("td-grooving.project"));
            var pathBefore = service.Session.FullPath;

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No grooves converted"));
                Assert.That(log, Does.Contain("ignored 1"));
                Assert.That(service.Session.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.Reader.ReadXncPrograms(2).Single().Groovings, Has.Count.EqualTo(1),
                "a groove with no fitting cutter must be left untouched");
        }

        [Test]
        public void ConvertGroovesAndMills_GroovesToMills_NoMillingToolsConfigured_CancelsOperation()
        {
            var service = CreateService();
            Config.MillingToolDiams.Returns(new List<decimal>());
            service.Session.Open(CopyFixture("td-grooving.project"));
            var pathBefore = service.Session.FullPath;

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([new Part { Id = 2, Name = "panel-1" }], GrooveMillDirection.GroovesToMills, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No milling tools configured"));
                Assert.That(service.Session.FullPath, Is.EqualTo(pathBefore));
            });

            Assert.That(service.Reader.ReadXncPrograms(2).Single().Groovings, Has.Count.EqualTo(1));
        }

        [Test]
        public void ConvertGroovesAndMills_WithNoParts_ReturnsFalseAndLogs()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-grooving.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertGroovesAndMills([], GrooveMillDirection.GroovesToMills, processPockets: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No parts selected for groove/mill conversion."));
            });
        }
    }
}
