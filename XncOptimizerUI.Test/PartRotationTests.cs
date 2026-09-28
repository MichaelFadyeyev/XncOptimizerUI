using System.Globalization;
using System.Xml.Linq;
using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="GibLabProjectService.RotatePart"/> / <see cref="XncProgramRotator"/> against
    /// <c>TestData/td-rotation.project</c>: part 2 (op 10 = every element kind on
    /// <c>dx=500 dy=200</c>, op 10 back side = one face bore), part 3 (op 12 <c>turn=0</c> and
    /// op 13 <c>turn=1</c>, the same bore in both frames - a discordant part).
    /// A clockwise quarter step maps <c>(x, y)</c> to <c>(dy - y, x)</c>; plane names are visual
    /// (<c>bt</c>/<c>p=3</c> top, <c>bb</c>/<c>p=4</c> bottom).
    /// </summary>
    [TestFixture]
    public class PartRotationTests
    {
        private const string RotationFixture = "td-rotation.project";
        private const int RichPartId = 2;
        private const int DiscordantPartId = 3;

        private string _directory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "XncOptimizerUI.Test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void RotatePart_To90_RecalculatesEveryElementAndSwapsSize()
        {
            var service = Open(RotationFixture);

            var (result, log) = Rotate(service, RichPartId, 1);
            var program = SavedProgram(service, operationId: 10);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, log);
                Assert.That(SavedOperation(service, 10).Attribute("turn")!.Value, Is.EqualTo("1"));
                Assert.That(Attrs(program, "dx", "dy"), Is.EqualTo("200,500"));
                Assert.That(program.Element("var")!.Attribute("expr")!.Value, Is.EqualTo("dy/2"));

                var faceBores = program.Elements("bf").ToList();
                Assert.That(Attrs(faceBores[0], "x", "y"), Is.EqualTo("150,100"));
                Assert.That(Attrs(faceBores[1], "x", "y", "av", "as"), Is.EqualTo("170,20,true,32"), "array along X turns along Y");
                Assert.That(Attrs(faceBores[2], "x", "y", "av", "as"), Is.EqualTo("106,20,false,32"), "array now pointing -X restarts from its last hole");

                var edgeBores = program.Elements().Where(e => e.Name.LocalName is "bt" or "br" or "bb" or "bl").ToList();
                Assert.That(edgeBores.Select(e => e.Name.LocalName), Is.EqualTo(new[] { "br", "bb", "bl", "bt" }));
                Assert.That(Attrs(edgeBores[0], "y", "z"), Is.EqualTo("100,9"));
                Assert.That(Attrs(edgeBores[1], "x", "z"), Is.EqualTo("140,9"));
                Assert.That(Attrs(edgeBores[2], "y", "z"), Is.EqualTo("dy-100,9"), "same value 400: authored text kept, dx/dy swapped");
                Assert.That(Attrs(edgeBores[3], "x", "z", "dp"), Is.EqualTo("135,9,dy"));
                Assert.That(edgeBores[3].Attribute("y"), Is.Null);

                var grooves = program.Elements("gr").ToList();
                Assert.That(Attrs(grooves[0], "x1", "y1", "x2", "y2", "p", "c"), Is.EqualTo("150,0,150,500,0,1"));
                Assert.That(Attrs(grooves[1], "x1", "y1", "x2", "y2", "p", "z"), Is.EqualTo("200,0,200,500,2,4.5"), "top plane turns right");
                Assert.That(Attrs(grooves[2], "x1", "y1", "x2", "y2", "p", "z"), Is.EqualTo("0,500,200,500,4,6"), "right plane turns bottom, start <= end kept");

                Assert.That(Points(program), Is.EqualTo(new[] { "100,-10", "100,510", "140,250", "60,250", "140,250" }));
                Assert.That(Attrs(program.Element("mac")!, "cx", "cy", "dir"), Is.EqualTo("100,250,false"));
                Assert.That(Attrs(program.Element("ma")!, "r", "dir"), Is.EqualTo("40,false"));
                Assert.That(Attrs(program.Element("mr")!, "x", "y", "l", "w", "a", "sxy"), Is.EqualTo("50,250,20,100,0,tool.dia/2"));
                Assert.That(Attrs(program.Element("me")!, "x", "y", "l", "w", "a"), Is.EqualTo("100,400,20,30,0"));
            });
        }

        [Test]
        public void RotatePart_To90_RotatesBackSideProgramInTheSameFrame()
        {
            var service = Open(RotationFixture);

            Rotate(service, RichPartId, 1);

            Assert.Multiple(() =>
            {
                Assert.That(SavedOperation(service, 11).Attribute("turn")!.Value, Is.EqualTo("1"));
                Assert.That(Attrs(SavedProgram(service, 11).Element("bf")!, "x", "y"), Is.EqualTo("50,400"));
            });
        }

        [Test]
        public void RotatePart_To180_KeepsSizeAndTurnsEdgesTwice()
        {
            var service = Open(RotationFixture);

            Rotate(service, RichPartId, 2);
            var program = SavedProgram(service, 10);

            Assert.Multiple(() =>
            {
                Assert.That(Attrs(program, "dx", "dy"), Is.EqualTo("500,200"));
                Assert.That(program.Element("var")!.Attribute("expr")!.Value, Is.EqualTo("dx/2"));
                Assert.That(Attrs(program.Element("bf")!, "x", "y"), Is.EqualTo("400,150"));
                Assert.That(Attrs(program.Elements("br").Last(), "y", "dp"), Is.EqualTo("135,dx"), "left edge bore ends on the right edge");
                Assert.That(Attrs(program.Elements("gr").ElementAt(1), "p"), Is.EqualTo("4"), "top plane ends at bottom");
            });
        }

        [Test]
        public void RotatePart_FullCircleInQuarterSteps_RestoresGeometry()
        {
            var service = Open(RotationFixture);
            var before = Describe(service.ReadXncPrograms(RichPartId));

            foreach (var turn in new[] { 1, 2, 3, 0 })
            {
                Assert.That(Rotate(service, RichPartId, turn).Result, Is.True);
            }

            var program = SavedProgram(service, 10);

            Assert.Multiple(() =>
            {
                Assert.That(Describe(service.ReadXncPrograms(RichPartId)), Is.EqualTo(before));
                Assert.That(program.Element("var")!.Attribute("expr")!.Value, Is.EqualTo("dx/2"));
                Assert.That(Attrs(program.Elements("bf").ElementAt(1), "x", "y", "av"), Is.EqualTo("20,30,false"));
                Assert.That(Attrs(program.Elements("bf").ElementAt(2), "x", "y", "av"), Is.EqualTo("20,30,true"));
            });
        }

        [Test]
        public void RotatePart_ReadBack_ReportsTargetTurnAndSwappedSize()
        {
            var service = Open(RotationFixture);

            Rotate(service, RichPartId, 3);
            var programs = service.ReadXncPrograms(RichPartId);

            Assert.Multiple(() =>
            {
                Assert.That(programs.Select(p => p.Turn), Is.All.EqualTo(3));
                Assert.That(programs.Select(p => $"{p.Dx}x{p.Dy}"), Is.All.EqualTo("200x500"));
                // 270°: (x, y) -> (y, dx - x) = (50, 400) for the (100, 50) face bore.
                Assert.That(programs[0].Bores.First(b => b.Surface == BoreSurface.Face).X, Is.EqualTo(50));
                Assert.That(programs[0].Bores.First(b => b.Surface == BoreSurface.Face).Y, Is.EqualTo(400));
            });
        }

        [Test]
        public void RotatePart_DiscordantPart_TurnsEachOperationFromItsOwnTurn()
        {
            var service = Open(RotationFixture);
            var alreadyTurned = SavedOperation(service, 13).Attribute("program")!.Value;

            var (result, _) = Rotate(service, DiscordantPartId, 1);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(SavedOperation(service, 12).Attribute("turn")!.Value, Is.EqualTo("1"));
                Assert.That(Attrs(SavedProgram(service, 12).Element("bf")!, "x", "y"), Is.EqualTo("150,100"));
                Assert.That(Attrs(SavedProgram(service, 12), "dx", "dy"), Is.EqualTo("200,500"));
                Assert.That(SavedOperation(service, 13).Attribute("turn")!.Value, Is.EqualTo("1"));
                Assert.That(SavedOperation(service, 13).Attribute("program")!.Value, Is.EqualTo(alreadyTurned), "already at target: untouched");
            });
        }

        [Test]
        public void RotatePart_LeftEdgeBoreAt65_BecomesTopEdgeBoreAt135()
        {
            var service = Open("td-bl-65-bore.project");

            var (result, log) = Rotate(service, 2, 1);
            var program = SavedProgram(service, 2);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, log);
                Assert.That(Attrs(program, "dx", "dy"), Is.EqualTo("200,500"));
                Assert.That(program.Element("bl"), Is.Null);
                Assert.That(Attrs(program.Element("bt")!, "ver", "x", "dp", "ac", "m", "name"), Is.EqualTo("2,135,30,1,true,Bore8"));
            });
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void RotatePart_TurnOutOfRange_ReturnsFalse(int turn)
        {
            var service = Open(RotationFixture);

            var (result, log) = Rotate(service, RichPartId, turn);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("Turn must be 0..3"));
            });
        }

        [Test]
        public void RotatePart_PartWithoutProgram_ReturnsFalse()
        {
            var service = Open(RotationFixture);

            var (result, log) = Rotate(service, 99, 1);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("has no XNC program"));
            });
        }

        // Edge-plane grooves on 500 x 200. c is relative to the plane's fixed travel direction
        // (+X on top/bottom, -Y on left/right), so it flips where that direction reverses.
        [TestCase("0", "0", "dx", "0", "3", "1", 1, "200,0,200,500,2,2", TestName = "Top groove to right: travel reverses, c flips")]
        [TestCase("dx", "0", "dx", "dy", "2", "1", 1, "0,500,200,500,4,1", TestName = "Right groove to bottom: reversed ends swapped, c kept")]
        [TestCase("0", "dy", "dx", "dy", "4", "2", 1, "0,0,0,500,1,1", TestName = "Bottom groove to left: travel reverses, c flips")]
        [TestCase("0", "0", "0", "dy", "1", "2", 1, "0,0,200,0,3,2", TestName = "Left groove to top: reversed ends swapped, c kept")]
        [TestCase("0", "0", "dx", "0", "3", "1", 2, "0,200,500,200,4,2", TestName = "Top groove 180: ends swapped, c flips once")]
        [TestCase("0", "0", "dx", "0", "3", "1", 3, "0,0,0,500,1,1", TestName = "Top groove 270: two reversals, c kept")]
        public void RotateProgram_EdgeGroove_KeepsStartBeforeEndAndTravelSide(
            string x1, string y1, string x2, string y2, string plane, string position, int steps, string expected)
        {
            var program = XElement.Parse(
                "<program dx=\"500\" dy=\"200\" dz=\"18\"><tool name=\"Cut2.8\" d=\"2.8\"/>"
                + $"<gr x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" dp=\"10\" t=\"3\" z=\"6\" c=\"{position}\" p=\"{plane}\" name=\"Cut2.8\"/>"
                + "</program>");

            XncProgramRotator.RotateProgram(program, steps);
            var groove = program.Element("gr")!;
            var n = (string a) => XncExpressionEvaluator.Evaluate(groove.Attribute(a)!.Value, Symbols(program)).ToString(CultureInfo.InvariantCulture);

            Assert.That($"{n("x1")},{n("y1")},{n("x2")},{n("y2")},{groove.Attribute("p")!.Value},{groove.Attribute("c")!.Value}",
                Is.EqualTo(expected));
        }

        [TestCase("3", "1", 1)]
        [TestCase("4", "2", 1)]
        [TestCase("3", "1", 2)]
        public void RotateProgram_EdgeGroove_WithoutTclFlip_KeepsGibLabPosition(string plane, string position, int steps)
        {
            var program = XElement.Parse(
                "<program dx=\"500\" dy=\"200\" dz=\"18\"><tool name=\"Cut2.8\" d=\"2.8\"/>"
                + $"<gr x1=\"0\" y1=\"0\" x2=\"dx\" y2=\"0\" dp=\"10\" t=\"3\" z=\"6\" c=\"{position}\" p=\"{plane}\" name=\"Cut2.8\"/>"
                + "</program>");

            XncProgramRotator.RotateProgram(program, steps, flipEdgeGrooveTcl: false);

            Assert.That(program.Element("gr")!.Attribute("c")!.Value, Is.EqualTo(position));
        }

        [Test]
        public void RotatePart_WithoutTclFlip_KeepsEdgeGrooveTclAndStillSwapsEnds()
        {
            var service = Open(RotationFixture);
            var log = string.Empty;

            service.RotatePart(ref log, RichPartId, 1, flipEdgeGrooveTcl: false);
            service.SaveProject();
            var grooves = SavedProgram(service, 10).Elements("gr").ToList();

            Assert.Multiple(() =>
            {
                Assert.That(Attrs(grooves[0], "c"), Is.EqualTo("1"), "front groove");
                Assert.That(Attrs(grooves[2], "x1", "y1", "x2", "y2", "p"), Is.EqualTo("0,500,200,500,4"));
            });
        }

        [Test]
        public void RotateProgram_FrontGroove_KeepsReversedEnds()
        {
            var program = XElement.Parse(
                "<program dx=\"500\" dy=\"200\" dz=\"18\"><tool name=\"Cut2.8\" d=\"2.8\"/>"
                + "<gr x1=\"0\" y1=\"50\" x2=\"500\" y2=\"50\" dp=\"10\" t=\"3\" c=\"1\" p=\"0\" name=\"Cut2.8\"/></program>");

            XncProgramRotator.RotateProgram(program, 2);

            Assert.That(Attrs(program.Element("gr")!, "x1", "y1", "x2", "y2", "c"), Is.EqualTo("500,150,0,150,1"),
                "p=0: c is relative to start -> end, so the order is authored meaning and stays");
        }

        [TestCase("dx+10", "dy+10")]
        [TestCase("DX-dy", "DY-dx")]
        [TestCase("(dx)/2", "(dy)/2")]
        [TestCase("dxOffset+tool.dia", "dxOffset+tool.dia")]
        [TestCase("tool.dx", "tool.dx")]
        [TestCase("dz+2", "dz+2")]
        public void SwapDxDyIdentifiers_SwapsWholeIdentifiersOnly(string expression, string expected)
        {
            Assert.That(XncProgramRotator.SwapDxDyIdentifiers(expression), Is.EqualTo(expected));
        }

        #region Helpers

        private GibLabProjectService Open(string fixture)
        {
            var path = Path.Combine(_directory, fixture);
            File.Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", fixture), path);

            var service = new GibLabProjectService(Substitute.For<IConfigService>(), TimeProvider.System);
            service.OpenProject(path);
            service.ReadBands();
            service.ReadSheets();

            return service;
        }

        /// <summary>Rotates and saves in place, as the view model does.</summary>
        private static (bool Result, string Log) Rotate(GibLabProjectService service, int partId, int turn)
        {
            var log = string.Empty;
            var result = service.RotatePart(ref log, partId, turn, flipEdgeGrooveTcl: true);
            service.SaveProject();

            return (result, log);
        }

        private static XElement SavedOperation(GibLabProjectService service, int operationId) =>
            XDocument.Load(service.FullPath).Descendants("operation")
                .Single(o => (string?)o.Attribute("typeId") == "XNC" && (string?)o.Attribute("id") == operationId.ToString(CultureInfo.InvariantCulture));

        private static XElement SavedProgram(GibLabProjectService service, int operationId) =>
            XDocument.Parse(SavedOperation(service, operationId).Attribute("program")!.Value).Root!;

        private static string Attrs(XElement element, params string[] names) =>
            string.Join(",", names.Select(n => element.Attribute(n)?.Value ?? "<none>"));

        private static string[] Points(XElement program) => program.Elements()
            .Where(e => e.Name.LocalName is "ms" or "ml" or "mac" or "ma")
            .Select(e => $"{e.Attribute("x")!.Value},{e.Attribute("y")!.Value}")
            .ToArray();

        /// <summary>Resolved geometry of every program, rounded, as comparable text lines.</summary>
        private static string[] Describe(IReadOnlyList<XncProgram> programs) => programs
            .SelectMany(p => new[] { $"turn {p.Turn} size {N(p.Dx)}x{N(p.Dy)}" }
                .Concat(p.Bores.Select(b => $"bore {b.Surface} {N(b.X)},{N(b.Y)},{N(b.Z)} dp {N(b.Depth)}"))
                .Concat(p.Groovings.Select(g => $"groove p{g.SideCode} {N(g.Start.X)},{N(g.Start.Y)}-{N(g.End.X)},{N(g.End.Y)} z {N(g.Z)}"))
                .Concat(p.MillingContours.Select(c => $"contour {N(c.Entry.X)},{N(c.Entry.Y)} " +
                    string.Join(" ", c.Segments.Select(s => $"{N(s.End.X)},{N(s.End.Y)}"))))
                .Concat(p.MillingRectangles.Select(r => $"rect {N(r.Origin.X)},{N(r.Origin.Y)} {N(r.Length)}x{N(r.Width)}"))
                .Concat(p.MillingEllipses.Select(e => $"ellipse {N(e.Center.X)},{N(e.Center.Y)} {N(e.Length)}x{N(e.Width)}")))
            .ToArray();

        private static XncSymbolTable Symbols(XElement program)
        {
            var symbols = new XncSymbolTable();
            symbols.Set("dx", double.Parse(program.Attribute("dx")!.Value, CultureInfo.InvariantCulture));
            symbols.Set("dy", double.Parse(program.Attribute("dy")!.Value, CultureInfo.InvariantCulture));
            symbols.Set("dz", double.Parse(program.Attribute("dz")!.Value, CultureInfo.InvariantCulture));

            return symbols;
        }

        private static string N(double value) => Math.Round(value, 4).ToString(CultureInfo.InvariantCulture);

        #endregion
    }
}
