using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// <see cref="Services.Xnc.Conversions.BoreMillConverter"/> through <see cref="IXncBatchOperations.ConvertBoresAndMills"/>.
    /// </summary>
    [TestFixture]
    public class BoreMillConverterTests : ProjectServicesTestBase
    {
        [Test]
        public void ConvertBoresAndMills_BoresToMills_LargeBoreBecomesClosedFourArcContour()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-bores-large.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Session.FullPath, Does.EndWith("_bm.project"));
                Assert.That(File.Exists(service.Session.FullPath), Is.True);
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Mill6
                Assert.That(log, Does.Contain("1 tool(s) removed")); // orphaned Bore40
            });

            var program = service.Reader.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours, Has.Count.EqualTo(1));
                Assert.That(program.Bores.Select(b => b.ToolName), Is.EqualTo(new[] { "Bore8" }), "the small bore is left alone");
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Mill6"));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Bore40"));
            });

            var contour = program.MillingContours.Single();

            Assert.Multiple(() =>
            {
                Assert.That(contour.ToolName, Is.EqualTo("Mill6"));
                Assert.That(contour.Position, Is.EqualTo(ToolPosition.Pocket)); // Bore40 dp=12 < dz=19 => blind => c="3"
                Assert.That(contour.EntryDepth, Is.EqualTo(12d));
                Assert.That(contour.Entry, Is.EqualTo(new XncPoint(200, 280)));  // top: (cx, cy - r)
                Assert.That(contour.Forward, Is.True);
                Assert.That(contour.Segments, Has.Count.EqualTo(4));
                Assert.That(contour.Segments, Is.All.InstanceOf<XncArcSegment>());
            });

            var arcs = contour.Segments.Cast<XncArcSegment>().ToList();

            Assert.Multiple(() =>
            {
                Assert.That(arcs, Is.All.Matches<XncArcSegment>(a => a.Center == new XncPoint(200, 300)));
                Assert.That(arcs.Select(a => a.Radius), Is.All.EqualTo(20d).Within(1e-6));
                // Declared counter-clockwise on screen: left, bottom, right, back to the top.
                Assert.That(arcs.Select(a => a.End), Is.EqualTo(new[]
                {
                    new XncPoint(180, 300), new XncPoint(200, 320), new XncPoint(220, 300), new XncPoint(200, 280),
                }));
                Assert.That(arcs.Select(a => a.Clockwise), Is.All.False);  // dir="false"
                Assert.That(arcs.Select(a => a.Depth), Is.All.EqualTo(12d));
            });
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_ReusesExistingMill6Tool()
        {
            var service = CreateService();
            var path = CopyFixture("td-bores-large.project");
            var xml = File.ReadAllText(path)
                .Replace("&lt;tool name=&quot;Bore40&quot; d=&quot;40&quot;/&gt;",
                    "&lt;tool name=&quot;Mill6&quot; d=&quot;6&quot;/&gt;&lt;tool name=&quot;Bore40&quot; d=&quot;40&quot;/&gt;");
            File.WriteAllText(path, xml);
            service.Session.Open(path);

            var log = string.Empty;
            Assert.That(service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: false).Into(ref log), Is.True);

            Assert.That(log, Does.Contain("0 tool(s) added"));

            var program = service.Reader.ReadXncPrograms(2).Single();
            Assert.That(program.Tools.Count(t => t.Name == "Mill6"), Is.EqualTo(1));
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_CustomVariableAsBoreDepth()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-bore-depth-variable.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertBoresAndMills([new Part { Id = 1, Name = "312.09.02.ПАН-1" }], BoreMillDirection.BoresToMills, useEllipse: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(log, Does.Contain("converted 1"));
            });

            var program = service.Reader.ReadXncPrograms(1).Single();
            var contour = program.MillingContours.Single();

            Assert.Multiple(() =>
            {
                Assert.That(contour.ToolName, Is.EqualTo("Mill6"));
                // dz=18, dp="throughBoreDepth" (var expr dz+2.00=20) >= dz => through, not a pocket;
                // declared counter-clockwise, c="2" keeps the tool inside the hole.
                Assert.That(contour.Position, Is.EqualTo(ToolPosition.Left));
                Assert.That(contour.EntryDepth, Is.EqualTo(20d));
                Assert.That(contour.Entry, Is.EqualTo(new XncPoint(300, 116))); // (cx, cy - r): DX/2=300, DY/2=150, Bore68 r=34
                Assert.That(contour.Segments, Has.Count.EqualTo(4));
                Assert.That(contour.Segments, Is.All.InstanceOf<XncArcSegment>());
            });

            var arcs = contour.Segments.Cast<XncArcSegment>().ToList();

            Assert.Multiple(() =>
            {
                Assert.That(arcs, Is.All.Matches<XncArcSegment>(a => a.Center.X == 300d && a.Center.Y == 150d));
                Assert.That(arcs.Select(a => a.End), Is.EqualTo(new[]
                {
                    new XncPoint(266, 150), new XncPoint(300, 184), new XncPoint(334, 150), new XncPoint(300, 116),
                }));
            });
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_UseEllipse_BlindBoreBecomesEllipticalPocket()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-bores-large.project")); // Bore40 dp="12", dz="19" => blind

            var log = string.Empty;
            Assert.That(service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: true).Into(ref log), Is.True);

            var saved = File.ReadAllText(service.Session.FullPath);

            Assert.Multiple(() =>
            {
                Assert.That(saved, Does.Contain("&lt;me "));
                Assert.That(saved, Does.Contain("l=&quot;20&quot;"), "l/w are semi-axes: Ø40 -> l = w = 20");
                Assert.That(saved, Does.Contain("w=&quot;20&quot;"));
                Assert.That(saved, Does.Contain("c=&quot;3&quot;"), "a blind bore is milled as a pocket");
                Assert.That(saved, Does.Contain("sxy=&quot;tool.dia/2&quot;"));
                Assert.That(saved, Does.Contain("fwd=&quot;true&quot;"));
                Assert.That(saved, Does.Contain("name=&quot;Mill6&quot;"));
                Assert.That(saved, Does.Not.Contain("Bore40"), "the orphaned bore tool is dropped");
            });

            var program = service.Reader.ReadXncPrograms(2).Single();
            Assert.That(program.Bores.Select(b => b.ToolName), Is.EqualTo(new[] { "Bore8" }));
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_UseEllipse_ThroughBoreKeepsRightPosition()
        {
            var service = CreateService();
            var path = CopyFixture("td-bores-large.project");
            // Push the large bore's depth through the panel (dz="19").
            var xml = File.ReadAllText(path).Replace("dp=&quot;12&quot;", "dp=&quot;19&quot;");
            File.WriteAllText(path, xml);
            service.Session.Open(path);

            var log = string.Empty;
            Assert.That(service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: true).Into(ref log), Is.True);

            var saved = File.ReadAllText(service.Session.FullPath);

            Assert.Multiple(() =>
            {
                Assert.That(saved, Does.Contain("&lt;me "));
                Assert.That(saved, Does.Contain("c=&quot;1&quot;"), "a through bore keeps the right-of-centre-line position");
                Assert.That(saved, Does.Not.Contain("c=&quot;3&quot;"));
            });
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_NoLargeBores_ReturnsFalseAndSavesNothing()
        {
            var service = CreateService();
            var path = CopyFixture("td-bores-large.project");
            var xml = File.ReadAllText(path).Replace("d=&quot;40&quot;", "d=&quot;30&quot;");
            File.WriteAllText(path, xml);
            service.Session.Open(path);
            var pathBefore = service.Session.FullPath;

            var log = string.Empty;
            var result = service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No bores converted"));
                Assert.That(service.Session.FullPath, Is.EqualTo(pathBefore));
            });
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_ClosedArcContourBecomesFaceBore()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-mill-circle.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(service.Session.FullPath, Does.EndWith("_bm.project"));
                Assert.That(log, Does.Contain("converted 1"));
                Assert.That(log, Does.Contain("1 tool(s) added"));   // Bore40
                Assert.That(log, Does.Contain("1 tool(s) removed")); // orphaned Mill6
            });

            var program = service.Reader.ReadXncPrograms(2).Single();

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours, Is.Empty);
                Assert.That(program.Bores, Has.Count.EqualTo(1));
                Assert.That(program.Tools.Select(t => t.Name), Does.Contain("Bore40"));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Mill6"));
            });

            var bore = program.Bores.Single();

            Assert.Multiple(() =>
            {
                Assert.That(bore.Surface, Is.EqualTo(BoreSurface.Face));
                Assert.That(bore.ToolName, Is.EqualTo("Bore40"));
                Assert.That(bore.X, Is.EqualTo(250d));
                Assert.That(bore.Y, Is.EqualTo(300d));
                Assert.That(bore.Depth, Is.EqualTo(15d));
                Assert.That(program.Tools.Single(t => t.Name == "Bore40").Diameter, Is.EqualTo(40d));
            });
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_RoundEllipseBecomesFaceBore()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-mill-ellipse.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false).Into(ref log);

            Assert.That(result, Is.True);

            var program = service.Reader.ReadXncPrograms(2).Single();
            var bore = program.Bores.Single();

            Assert.Multiple(() =>
            {
                Assert.That(bore.ToolName, Is.EqualTo("Bore40"));
                Assert.That(bore.X, Is.EqualTo(250d));
                Assert.That(bore.Y, Is.EqualTo(300d));
                Assert.That(bore.Depth, Is.EqualTo(15d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Mill6"));
            });
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_NonClosedContourIsIgnored()
        {
            var service = CreateService();
            var path = CopyFixture("td-mill-circle.project");
            // Move the final arc endpoint off the entry point: the contour no longer closes.
            var xml = File.ReadAllText(path)
                .Replace("&lt;mac x=&quot;270&quot; y=&quot;300&quot; cx=&quot;250&quot; cy=&quot;300&quot; dp=&quot;15&quot; dir=&quot;false&quot;/&gt;&lt;/program&gt;",
                    "&lt;mac x=&quot;265&quot; y=&quot;300&quot; cx=&quot;250&quot; cy=&quot;300&quot; dp=&quot;15&quot; dir=&quot;false&quot;/&gt;&lt;/program&gt;");
            File.WriteAllText(path, xml);
            service.Session.Open(path);
            var pathBefore = service.Session.FullPath;

            var log = string.Empty;
            var result = service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No mills converted"));
                Assert.That(service.Session.FullPath, Is.EqualTo(pathBefore));
            });
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_ContourWithStraightSegmentIsIgnored()
        {
            var service = CreateService();
            var path = CopyFixture("td-mill-circle.project");
            var xml = File.ReadAllText(path)
                .Replace("&lt;mac x=&quot;270&quot; y=&quot;300&quot; cx=&quot;250&quot; cy=&quot;300&quot; dp=&quot;15&quot; dir=&quot;false&quot;/&gt;&lt;/program&gt;",
                    "&lt;ml x=&quot;270&quot; y=&quot;300&quot; dp=&quot;15&quot;/&gt;&lt;/program&gt;");
            File.WriteAllText(path, xml);
            service.Session.Open(path);

            var log = string.Empty;
            Assert.That(service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false).Into(ref log), Is.False);
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_UnderSizeCircleIsIgnored()
        {
            var service = CreateService();
            var path = CopyFixture("td-mill-circle.project");
            // Shrink the circle to radius 15 (Ø30, below the 35 mm threshold).
            var xml = File.ReadAllText(path)
                .Replace("x=&quot;270&quot;", "x=&quot;265&quot;")
                .Replace("x=&quot;230&quot;", "x=&quot;235&quot;");
            File.WriteAllText(path, xml);
            service.Session.Open(path);

            var log = string.Empty;
            Assert.That(service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false).Into(ref log), Is.False);
        }

        [Test]
        public void ConvertBoresAndMills_RoundTrip_RestoresBoreCentreDiameterAndDepth()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-bores-large.project"));

            var log = string.Empty;
            Assert.That(service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.BoresToMills, useEllipse: false).Into(ref log), Is.True);

            service.Session.Open(service.Session.FullPath);
            log = string.Empty;
            Assert.That(service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "panel-1" }], BoreMillDirection.MillsToBores, useEllipse: false).Into(ref log), Is.True);

            var program = service.Reader.ReadXncPrograms(2).Single();
            var restored = program.Bores.Single(b => b.ToolName == "Bore40");

            Assert.Multiple(() =>
            {
                Assert.That(program.MillingContours, Is.Empty);
                Assert.That(restored.X, Is.EqualTo(200d));
                Assert.That(restored.Y, Is.EqualTo(300d));
                Assert.That(restored.Depth, Is.EqualTo(12d));
                Assert.That(program.Tools.Single(t => t.Name == "Bore40").Diameter, Is.EqualTo(40d));
                Assert.That(program.Tools.Select(t => t.Name), Does.Not.Contain("Mill6"));
                Assert.That(program.Bores.Select(b => b.ToolName), Does.Contain("Bore8"));
            });
        }

        [Test]
        public void ConvertBoresAndMills_MillsToBores_RichFixture_EllipsesAndArcContoursBecomeBores()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-br-ml-conversion.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertBoresAndMills([new Part { Id = 2, Name = "Mill-Ellipse" }, new Part { Id = 5, Name = "Mill-Path" }],
                BoreMillDirection.MillsToBores,
                useEllipse: false).Into(ref log);

            Assert.That(result, Is.True);

            var ellipsePart = service.Reader.ReadXncPrograms(2).SelectMany(p => p.Bores).ToList();
            var pathPart = service.Reader.ReadXncPrograms(5).SelectMany(p => p.Bores).ToList();

            Assert.Multiple(() =>
            {
                // Two <me> on the front face + one on the back.
                Assert.That(ellipsePart, Has.Count.EqualTo(3));
                Assert.That(ellipsePart, Is.All.Matches<XncBore>(b => b.ToolName == "Bore40" && b.Surface == BoreSurface.Face));
                // Two <mac> circles on the front face + one <ma> circle on the back.
                Assert.That(pathPart, Has.Count.EqualTo(3));
                Assert.That(pathPart, Is.All.Matches<XncBore>(b => b.ToolName == "Bore40"));
                Assert.That(service.Reader.ReadXncPrograms(2).SelectMany(p => p.MillingContours), Is.Empty);
                Assert.That(service.Reader.ReadXncPrograms(5).SelectMany(p => p.MillingContours), Is.Empty);
            });

            var backEllipse = service.Reader.ReadXncPrograms(2).Single(p => !p.Side).Bores.Single();
            Assert.Multiple(() =>
            {
                Assert.That(backEllipse.X, Is.EqualTo(300d));  // dx/2
                Assert.That(backEllipse.Y, Is.EqualTo(150d));  // dy/2
                Assert.That(backEllipse.Depth, Is.EqualTo(15d));
            });
        }

        [Test]
        public void ConvertBoresAndMills_BoresToMills_RichFixture_EveryLargeBoreBecomesAFourArcContour()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-br-ml-conversion.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertBoresAndMills([new Part { Id = 3, Name = "Bore" }], BoreMillDirection.BoresToMills, useEllipse: false).Into(ref log);

            Assert.That(result, Is.True);

            var contours = service.Reader.ReadXncPrograms(3).SelectMany(p => p.MillingContours).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(contours, Has.Count.EqualTo(3));  // 2 front + 1 back
                Assert.That(contours, Is.All.Matches<XncMillingContour>(
                    c => c.ToolName == "Mill6" && c.Segments.Count == 4));
                // dz=18: the dp=20 bore is through (c="2" Left: tool inside the counter-clockwise hole), the two dp=15 bores are blind (c="3" Pocket).
                Assert.That(contours.Count(c => c.Position == ToolPosition.Left), Is.EqualTo(1));
                Assert.That(contours.Count(c => c.Position == ToolPosition.Pocket), Is.EqualTo(2));
                Assert.That(service.Reader.ReadXncPrograms(3).SelectMany(p => p.Bores), Is.Empty);
            });
        }

        [Test]
        public void ConvertBoresAndMills_WithNoParts_ReturnsFalseAndLogs()
        {
            var service = CreateService();
            service.Session.Open(CopyFixture("td-bores-large.project"));

            var log = string.Empty;
            var result = service.Batch.ConvertBoresAndMills([], BoreMillDirection.BoresToMills, useEllipse: false).Into(ref log);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(log, Does.Contain("No parts selected for bore/mill conversion."));
            });
        }
    }
}
