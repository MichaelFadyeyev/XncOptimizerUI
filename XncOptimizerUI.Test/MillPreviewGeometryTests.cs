using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.Views.PartPreview;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Exercises <see cref="MillPreviewGeometry"/>: which side of a mill is cut off, where the tool
    /// path runs and where it starts. Coordinates are raw program mm drawn Y-down, so "right of
    /// travel" for a left-to-right path is +Y (screen down).
    /// </summary>
    [TestFixture]
    public class MillPreviewGeometryTests
    {
        private const double Tolerance = 1e-6;
        private const double Diameter = 10;
        private static readonly Vec2 Outline = new(100, 80);

        private static XncMillingContour Line(double x1, double x2, double y, ToolPosition position, bool forward = true, double depth = 10) => new()
        {
            ToolName = "Mill10",
            Entry = new XncPoint(x1, y),
            EntryDepth = depth,
            Position = position,
            Forward = forward,
            Segments = [new XncLineSegment { End = new XncPoint(x2, y), Depth = depth }],
        };

        private static XncMillingRectangle Rectangle(ToolPosition position, bool forward = true, double angle = 0) => new()
        {
            ToolName = "Mill10",
            Origin = new XncPoint(50, 40),
            Length = 40,
            Width = 20,
            Angle = angle,
            Depth = 10,
            Position = position,
            Forward = forward,
        };

        private static MillPreviewGeometry.MillPath Path(XncMillingContour contour) =>
            MillPreviewGeometry.FromContour(contour, Diameter)!;

        private static Vec2 Start(MillPreviewGeometry.MillPath mill) =>
            MillPreviewGeometry.StartPoint(MillPreviewGeometry.ToolPath(mill, Outline));

        private static IEnumerable<Vec2> BoundaryPoints(MillPreviewGeometry.CutOffRegion region) =>
            region.Boundary.SelectMany(p => new[] { p.Start, p.End });

        private static void AssertPoint(Vec2 actual, double x, double y)
        {
            Assert.That(actual.X, Is.EqualTo(x).Within(Tolerance), "X");
            Assert.That(actual.Y, Is.EqualTo(y).Within(Tolerance), "Y");
        }

        [Test]
        public void OpenRightMill_shiftsToolPathRightOfTravel_andCutsOffThatSide()
        {
            var mill = Path(Line(-10, 110, 40, ToolPosition.Right));

            AssertPoint(Start(mill), -10, 45);

            var region = MillPreviewGeometry.RemovedRegion(mill, Outline)!;
            Assert.Multiple(() =>
            {
                Assert.That(region.Outside, Is.False);
                Assert.That(BoundaryPoints(region).Min(p => p.Y), Is.EqualTo(40).Within(Tolerance));
                Assert.That(BoundaryPoints(region).Max(p => p.Y), Is.GreaterThan(Outline.Y));
            });
        }

        [Test]
        public void OpenLeftMill_mirrorsToolPathAndCutOffSide()
        {
            var mill = Path(Line(-10, 110, 40, ToolPosition.Left));

            AssertPoint(Start(mill), -10, 35);

            var region = MillPreviewGeometry.RemovedRegion(mill, Outline)!;
            Assert.Multiple(() =>
            {
                Assert.That(BoundaryPoints(region).Max(p => p.Y), Is.EqualTo(40).Within(Tolerance));
                Assert.That(BoundaryPoints(region).Min(p => p.Y), Is.LessThan(0));
            });
        }

        [Test]
        public void OpenMill_notForward_travelsReversed_soSidesAndStartFlip()
        {
            var mill = Path(Line(-10, 110, 40, ToolPosition.Right, forward: false));

            AssertPoint(Start(mill), 110, 35);
            Assert.That(BoundaryPoints(MillPreviewGeometry.RemovedRegion(mill, Outline)!).Max(p => p.Y), Is.EqualTo(40).Within(Tolerance));
        }

        [Test]
        public void CenteredMill_keepsBothSides_andRunsOnItsCentreLine()
        {
            var mill = Path(Line(-10, 110, 40, ToolPosition.Center));

            Assert.That(MillPreviewGeometry.RemovedRegion(mill, Outline), Is.Null);
            AssertPoint(Start(mill), -10, 40);
        }

        [Test]
        public void OpenMill_endingInsideThePart_cannotSplitIt()
        {
            var mill = Path(Line(-10, 50, 40, ToolPosition.Right));

            Assert.That(MillPreviewGeometry.RemovedRegion(mill, Outline), Is.Null);
        }

        [TestCase(ToolPosition.Right, true, false, 35)]  // fwd="true": clockwise, right = inside
        [TestCase(ToolPosition.Left, true, true, 25)]
        [TestCase(ToolPosition.Right, false, true, 25)]  // fwd="false": counter-clockwise, right = outside
        [TestCase(ToolPosition.Left, false, false, 35)]
        [TestCase(ToolPosition.Pocket, true, false, 35)]
        [TestCase(ToolPosition.Pocket, false, false, 35)]
        public void Rectangle_cutsOffToolSide_andStartsMidOfLocalYMinusSide(ToolPosition position, bool forward, bool removesOutside, double startY)
        {
            var mill = MillPreviewGeometry.FromRectangle(Rectangle(position, forward), Diameter)!;

            Assert.That(MillPreviewGeometry.RemovedRegion(mill, Outline)!.Outside, Is.EqualTo(removesOutside));
            AssertPoint(Start(mill), 50, startY);
        }

        [Test]
        public void Rectangle_positiveAngle_rotatesClockwiseOnScreen()
        {
            var mill = MillPreviewGeometry.FromRectangle(Rectangle(ToolPosition.Center, angle: 90), Diameter)!;

            // The y- side midpoint (above the centre) turns a quarter clockwise to the right of it.
            AssertPoint(Start(mill), 60, 40);
        }

        [Test]
        public void CircleEllipse_staysExactArcs_withToolPathInsideForRightForward()
        {
            var ellipse = new XncMillingEllipse
            {
                ToolName = "Mill10",
                Center = new XncPoint(50, 40),
                Length = 10,
                Width = 10,
                Depth = 18,
                Position = ToolPosition.Right,
            };

            var mill = MillPreviewGeometry.FromEllipse(ellipse, Diameter)!;

            Assert.Multiple(() =>
            {
                Assert.That(mill.Travel, Has.All.Property(nameof(ContourOffsetGeometry.Primitive.IsArc)).True);
                Assert.That(MillPreviewGeometry.RemovedRegion(mill, Outline)!.Outside, Is.False); // fwd="true" <me>: clockwise, right = inside
                Assert.That(MillPreviewGeometry.IsThrough(mill, 18), Is.True);
            });
            AssertPoint(Start(mill), 50, 35);
        }

        [Test]
        public void ClosedContour_authoredClockwise_isTravelledCounterClockwiseWhenForward()
        {
            // Clockwise on screen: right along the top, down, left along the bottom, up.
            var square = new XncMillingContour
            {
                ToolName = "Mill10",
                Entry = new XncPoint(20, 20),
                EntryDepth = 5,
                Position = ToolPosition.Right,
                Segments =
                [
                    new XncLineSegment { End = new XncPoint(60, 20), Depth = 5 },
                    new XncLineSegment { End = new XncPoint(60, 60), Depth = 5 },
                    new XncLineSegment { End = new XncPoint(20, 60), Depth = 5 },
                    new XncLineSegment { End = new XncPoint(20, 20), Depth = 5 },
                ],
            };

            var mill = Path(square);

            Assert.Multiple(() =>
            {
                Assert.That(mill.Closed, Is.True);
                Assert.That(ContourOffsetGeometry.OperatorSignedArea(mill.Travel), Is.GreaterThan(0));
                Assert.That(MillPreviewGeometry.RemovedRegion(mill, Outline)!.Outside, Is.True);
            });
            AssertPoint(Start(mill), 15, 15);
        }

        [Test]
        public void Depth_isDeepestCut_andDecidesThroughVersusBlind()
        {
            var contour = new XncMillingContour
            {
                ToolName = "Mill10",
                Entry = new XncPoint(0, 0),
                EntryDepth = 4,
                Segments =
                [
                    new XncLineSegment { End = new XncPoint(10, 0), Depth = 19 },
                    new XncLineSegment { End = new XncPoint(20, 0), Depth = 6 },
                ],
            };

            var mill = Path(contour);

            Assert.Multiple(() =>
            {
                Assert.That(mill.Depth, Is.EqualTo(19));
                Assert.That(MillPreviewGeometry.IsThrough(mill, 18), Is.True);
                Assert.That(MillPreviewGeometry.IsThrough(mill, 20), Is.False);
            });
        }
    }
}
