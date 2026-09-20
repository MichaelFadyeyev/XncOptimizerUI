using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws every groove on top of the part preview built by <c>MainWindow.RenderPart</c>,
    /// mirroring <see cref="BorePreviewRenderer"/>. A front-plane groove (<see cref="XncGrooving.SideCode"/>
    /// <c>0</c>) gets its true rectangle (length = distance between its start/end points, width =
    /// <c>t</c>, offset from its center line per <c>c</c>) on the Face rectangle, plus a
    /// depth (<c>dp</c>) rectangle projected onto each of the four Top/Bottom/Left/Right edge
    /// bands. An edge-plane groove (<c>1</c>/<c>2</c>/<c>3</c>/<c>4</c> = Left/Right/Top/Bottom)
    /// is the mirror image: its true rectangle sits on the one band it belongs to (offset from
    /// the band's inner edge by its own <c>Z</c>), a <c>dp</c> rectangle sits on the Face
    /// rectangle flush to that same edge and growing inward, and a small <c>t</c>-wide/<c>dp</c>-
    /// long marker sits on each of the two bands perpendicular to its own plane, flush at its
    /// fixed edge coordinate. Unlike a bore, a groove's perpendicular offset always measures from
    /// a band's inner (Face-adjacent) edge, regardless of the owning program's <c>Side</c>.
    /// </summary>
    internal static class GroovePreviewRenderer
    {
        /// <summary>Identifies the groove (and its owning program, for the <c>Side</c> colour) a shape belongs to.</summary>
        internal readonly record struct GrooveTag(XncProgram Program, XncGrooving Groove);

        /// <summary>
        /// Draws every groove in <paramref name="programs"/> onto <paramref name="canvas"/>. A
        /// front-plane groove's five projections (its Face rectangle + its four edge-band
        /// rectangles), or an edge-plane groove's four (its band rectangle + Face rectangle +
        /// two cross-band markers), toggle red together on click, since they represent the same
        /// physical groove.
        /// </summary>
        public static void DrawGrooves(
            Canvas canvas,
            IEnumerable<XncProgram> programs,
            BorePreviewRenderer.FaceLayout layout,
            BorePreviewRenderer.BoreBrushes brushes)
        {
            foreach (var program in programs)
            {
                foreach (var groove in program.Groovings)
                {
                    if (groove.SideCode == 0)
                    {
                        DrawFrontPlaneGroove(canvas, program, groove, layout, brushes);
                    }
                    else
                    {
                        DrawEdgePlaneGroove(canvas, program, groove, layout, brushes);
                    }
                }
            }
        }

        /// <summary>Orientation tolerance (mm) for telling an axis-parallel groove's constant coordinate from noise.</summary>
        private const double OrientationEpsilon = 1e-6;

        private static void DrawFrontPlaneGroove(
            Canvas canvas,
            XncProgram program,
            XncGrooving groove,
            BorePreviewRenderer.FaceLayout layout,
            BorePreviewRenderer.BoreBrushes brushes)
        {
            var normalBrush = program.Side ? brushes.SideTrue : brushes.SideFalse;
            var tag = new GrooveTag(program, groove);
            var widthPx = groove.Width * layout.Scale;
            var depthPx = groove.Depth * layout.Scale;
            var overshootPx = brushes.CenterLineOvershootMm * layout.Scale;

            var startPx = (X: layout.OriginX + (groove.Start.X * layout.Scale), Y: layout.OriginY + (groove.Start.Y * layout.Scale));
            var endPx = (X: layout.OriginX + (groove.End.X * layout.Scale), Y: layout.OriginY + (groove.End.Y * layout.Scale));

            var (clickTargets, allStroked, addShape, addCenterLine) = CreateShapeSink(canvas, tag, normalBrush, brushes.OutlineThickness, brushes.CenterLineThickness);
            void NoCenterLine(double x1, double y1, double x2, double y2)
            {
            }

            // Face (true) rectangle: length = distance between start/end, width = t, offset from
            // the start-end center line per the groove's tool-to-centre-line position (c).
            AddOffsetRectangle(addShape, addCenterLine, startPx, endPx, widthPx, groove.Position, overshootPx);

            var xLo = Math.Min(startPx.X, endPx.X);
            var xHi = Math.Max(startPx.X, endPx.X);
            var yLo = Math.Min(startPx.Y, endPx.Y);
            var yHi = Math.Max(startPx.Y, endPx.Y);

            var isXOriented = Math.Abs(groove.Start.Y - groove.End.Y) < OrientationEpsilon;
            var isYOriented = Math.Abs(groove.Start.X - groove.End.X) < OrientationEpsilon;

            // A groove's perpendicular offset always measures from the band's inner
            // (Face-adjacent) edge, regardless of program.Side - unlike a bore, which flips
            // near/far on Side. AddSideRectangleRange's side parameter is therefore always true here.
            if (isXOriented)
            {
                // Parallel bands (Top/Bottom, same run direction as the groove): a plain extent
                // projection, no center line - the groove's own center line is already on the Face.
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, NoCenterLine, xLo, xHi, depthPx, overshootPx, layout, true, horizontal: true, near: true);
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, NoCenterLine, xLo, xHi, depthPx, overshootPx, layout, true, horizontal: true, near: false);

                // Perpendicular bands (Left/Right): a point-anchored rectangle (width t, depth dp)
                // at the groove's constant Y, with its own center line - like a face bore's band rectangle.
                var yPx = layout.OriginY + (groove.Start.Y * layout.Scale);
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, addCenterLine, yPx - (widthPx / 2), yPx + (widthPx / 2), depthPx, overshootPx, layout, true, horizontal: false, near: true);
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, addCenterLine, yPx - (widthPx / 2), yPx + (widthPx / 2), depthPx, overshootPx, layout, true, horizontal: false, near: false);
            }
            else if (isYOriented)
            {
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, NoCenterLine, yLo, yHi, depthPx, overshootPx, layout, true, horizontal: false, near: true);
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, NoCenterLine, yLo, yHi, depthPx, overshootPx, layout, true, horizontal: false, near: false);

                var xPx = layout.OriginX + (groove.Start.X * layout.Scale);
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, addCenterLine, xPx - (widthPx / 2), xPx + (widthPx / 2), depthPx, overshootPx, layout, true, horizontal: true, near: true);
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, addCenterLine, xPx - (widthPx / 2), xPx + (widthPx / 2), depthPx, overshootPx, layout, true, horizontal: true, near: false);
            }
            else
            {
                // Diagonal groove: not covered by any known fixture, and the groove/mill conversion
                // pipeline only really supports axis-parallel grooves either. Fall back to the
                // simple extent projection (with center line) on all four bands as an approximation.
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, addCenterLine, xLo, xHi, depthPx, overshootPx, layout, true, horizontal: true, near: true);
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, addCenterLine, xLo, xHi, depthPx, overshootPx, layout, true, horizontal: true, near: false);
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, addCenterLine, yLo, yHi, depthPx, overshootPx, layout, true, horizontal: false, near: true);
                PartPreviewOverlayGeometry.AddSideRectangleRange(addShape, addCenterLine, yLo, yHi, depthPx, overshootPx, layout, true, horizontal: false, near: false);
            }

            AttachClickToggle(clickTargets, allStroked, normalBrush, brushes.Selected);
        }

        private static void DrawEdgePlaneGroove(
            Canvas canvas,
            XncProgram program,
            XncGrooving groove,
            BorePreviewRenderer.FaceLayout layout,
            BorePreviewRenderer.BoreBrushes brushes)
        {
            // Edge-plane grooves always share one color, regardless of the owning program's Side
            // - unlike a front-plane groove's Side-based coloring (mirrors DrawEdgeBore).
            var normalBrush = brushes.SideTrue;
            var tag = new GrooveTag(program, groove);
            var widthPx = groove.Width * layout.Scale;
            var depthPx = groove.Depth * layout.Scale;
            var overshootPx = brushes.CenterLineOvershootMm * layout.Scale;

            var surface = groove.SideCode switch
            {
                1 => BoreSurface.Left,
                2 => BoreSurface.Right,
                3 => BoreSurface.Top,
                _ => BoreSurface.Bottom,
            };
            var horizontal = surface is BoreSurface.Top or BoreSurface.Bottom;
            var near = surface is BoreSurface.Top or BoreSurface.Left;

            // The axis not carrying the groove's along-edge extent stays constant (Start ~ End)
            // at the panel edge (e.g. x1=x2=dx for a right-edge groove) - it's not the
            // through-thickness position; that comes from the groove's own Z attribute.
            var axisLo = horizontal ? Math.Min(groove.Start.X, groove.End.X) : Math.Min(groove.Start.Y, groove.End.Y);
            var axisHi = horizontal ? Math.Max(groove.Start.X, groove.End.X) : Math.Max(groove.Start.Y, groove.End.Y);
            var zMm = groove.Z;

            var axisLoPx = (horizontal ? layout.OriginX : layout.OriginY) + (axisLo * layout.Scale);
            var axisHiPx = (horizontal ? layout.OriginX : layout.OriginY) + (axisHi * layout.Scale);

            var (clickTargets, allStroked, addShape, addCenterLine) = CreateShapeSink(canvas, tag, normalBrush, brushes.OutlineThickness, brushes.CenterLineThickness);
            void NoCenterLine(double x1, double y1, double x2, double y2)
            {
            }

            // Band (true) rectangle: offset from the band's inner (Face-adjacent) edge by zMm -
            // always the inner edge, regardless of program.Side, unlike an edge bore's circle
            // (which flips near/far on Side).
            var (nearEdge, farEdge, direction) = PartPreviewOverlayGeometry.GetBandEdges(layout, horizontal, near);
            var zStart = nearEdge;
            var zDir = direction;
            var perpPx = zStart + (zDir * (zMm * layout.Scale));

            var bandStart = horizontal ? (X: axisLoPx, Y: perpPx) : (X: perpPx, Y: axisLoPx);
            var bandEnd = horizontal ? (X: axisHiPx, Y: perpPx) : (X: perpPx, Y: axisHiPx);
            AddOffsetRectangle(addShape, addCenterLine, bandStart, bandEnd, widthPx, groove.Position, overshootPx);

            // Face depth projection: flush to the groove's own physical Face edge, growing inward -
            // no center line, matching a front-plane groove's parallel-band projections.
            PartPreviewOverlayGeometry.AddFaceRectangleRange(addShape, NoCenterLine, axisLoPx, axisHiPx, depthPx, overshootPx, layout, surface, horizontal);

            // Cross-band depth markers: on the two bands perpendicular to this groove's own plane
            // (Top/Bottom for a Left/Right-plane groove, Left/Right for a Top/Bottom-plane groove),
            // a small t-wide/dp-long marker flush at the groove's own fixed edge coordinate, growing
            // inward along its intrinsic depth axis (X for Left/Right-plane, Y for Top/Bottom-plane).
            // Perpendicular to that, each marker is displaced by the groove's own Z from *that*
            // band's inner edge - the same nearEdge/direction/Z formula as the own-band rectangle
            // above, just evaluated per target band via GetBandEdges.
            var otherHorizontal = !horizontal;
            double markerLoPx, markerHiPx;
            if (otherHorizontal)
            {
                (markerLoPx, markerHiPx) = surface == BoreSurface.Left
                    ? (layout.OriginX, layout.OriginX + depthPx)
                    : (layout.OriginX + layout.FaceWidth - depthPx, layout.OriginX + layout.FaceWidth);
            }
            else
            {
                (markerLoPx, markerHiPx) = surface == BoreSurface.Top
                    ? (layout.OriginY, layout.OriginY + depthPx)
                    : (layout.OriginY + layout.FaceHeight - depthPx, layout.OriginY + layout.FaceHeight);
            }

            var (nearEdgeA, _, directionA) = PartPreviewOverlayGeometry.GetBandEdges(layout, otherHorizontal, near: true);
            var anchorA = nearEdgeA + (directionA * (zMm * layout.Scale));
            var markerStartA = otherHorizontal ? (X: markerLoPx, Y: anchorA) : (X: anchorA, Y: markerLoPx);
            var markerEndA = otherHorizontal ? (X: markerHiPx, Y: anchorA) : (X: anchorA, Y: markerHiPx);
            AddOffsetRectangle(addShape, addCenterLine, markerStartA, markerEndA, widthPx, groove.Position, overshootPx);

            var (nearEdgeB, _, directionB) = PartPreviewOverlayGeometry.GetBandEdges(layout, otherHorizontal, near: false);
            var anchorB = nearEdgeB + (directionB * (zMm * layout.Scale));
            var markerStartB = otherHorizontal ? (X: markerLoPx, Y: anchorB) : (X: anchorB, Y: markerLoPx);
            var markerEndB = otherHorizontal ? (X: markerHiPx, Y: anchorB) : (X: anchorB, Y: markerHiPx);
            AddOffsetRectangle(addShape, addCenterLine, markerStartB, markerEndB, widthPx, groove.Position, overshootPx);

            AttachClickToggle(clickTargets, allStroked, normalBrush, brushes.Selected);
        }

        /// <summary>
        /// Builds the shared <c>addShape</c>/<c>addCenterLine</c> closures + tracking lists used
        /// by both groove variants, mirroring <c>BorePreviewRenderer.DrawBore</c>'s local functions.
        /// </summary>
        private static (List<Shape> ClickTargets, List<Shape> AllStroked, Action<Shape, bool> AddShape, Action<double, double, double, double> AddCenterLine) CreateShapeSink(
            Canvas canvas, GrooveTag tag, Brush brush, double outlineThickness, double centerLineThickness)
        {
            var clickTargets = new List<Shape>(5);
            var allStroked = new List<Shape>(11);

            void AddShape(Shape shape, bool isClickTarget)
            {
                shape.Stroke = brush;
                shape.Fill = Brushes.Transparent;
                shape.StrokeThickness = outlineThickness;
                shape.Tag = tag;
                canvas.Children.Add(shape);
                allStroked.Add(shape);

                if (isClickTarget)
                {
                    clickTargets.Add(shape);
                }
            }

            void AddCenterLine(double x1, double y1, double x2, double y2)
            {
                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = brush,
                    StrokeThickness = centerLineThickness,
                };
                canvas.Children.Add(line);
                allStroked.Add(line);
            }

            return (clickTargets, allStroked, AddShape, AddCenterLine);
        }

        /// <summary>
        /// Draws a rectangle of width <paramref name="widthPx"/> running the length of the line
        /// from <paramref name="startPx"/> to <paramref name="endPx"/>, offset perpendicular to
        /// that line per <paramref name="position"/> (<see cref="ToolPosition.Center"/> straddles
        /// it; <see cref="ToolPosition.Right"/>/<see cref="ToolPosition.Left"/> put the full width
        /// to the physical right/left of the start-to-end travel direction; an unexpected
        /// <see cref="ToolPosition.Pocket"/> falls back to <see cref="ToolPosition.Center"/>),
        /// plus the line itself as the center line, extended by <paramref name="overshootPx"/> at
        /// each end. Works for any orientation (axis-parallel or diagonal), since it never assumes
        /// a horizontal/vertical split - unlike the band/Face helpers in
        /// <see cref="PartPreviewOverlayGeometry"/>.
        /// </summary>
        private static void AddOffsetRectangle(
            Action<Shape, bool> addShape,
            Action<double, double, double, double> addCenterLine,
            (double X, double Y) startPx,
            (double X, double Y) endPx,
            double widthPx,
            ToolPosition position,
            double overshootPx)
        {
            var dx = endPx.X - startPx.X;
            var dy = endPx.Y - startPx.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));

            if (length <= 0)
            {
                return;
            }

            var dirX = dx / length;
            var dirY = dy / length;

            // Right of the start-to-end travel direction, in this Y-down canvas frame.
            var normalX = -dirY;
            var normalY = dirX;

            var (offsetA, offsetB) = position switch
            {
                ToolPosition.Right => (0d, widthPx),
                ToolPosition.Left => (-widthPx, 0d),
                _ => (-widthPx / 2, widthPx / 2),
            };

            var pA1 = (X: startPx.X + (normalX * offsetA), Y: startPx.Y + (normalY * offsetA));
            var pA2 = (X: endPx.X + (normalX * offsetA), Y: endPx.Y + (normalY * offsetA));
            var pB2 = (X: endPx.X + (normalX * offsetB), Y: endPx.Y + (normalY * offsetB));
            var pB1 = (X: startPx.X + (normalX * offsetB), Y: startPx.Y + (normalY * offsetB));

            var polygon = new Polygon
            {
                Points =
                [
                    new System.Windows.Point(pA1.X, pA1.Y),
                    new System.Windows.Point(pA2.X, pA2.Y),
                    new System.Windows.Point(pB2.X, pB2.Y),
                    new System.Windows.Point(pB1.X, pB1.Y),
                ],
            };
            addShape(polygon, true);

            addCenterLine(
                startPx.X - (dirX * overshootPx), startPx.Y - (dirY * overshootPx),
                endPx.X + (dirX * overshootPx), endPx.Y + (dirY * overshootPx));
        }

        private static void AttachClickToggle(List<Shape> clickTargets, List<Shape> allStroked, Brush normalBrush, Brush selectedBrush)
        {
            var selected = false;

            void OnClick(object sender, MouseButtonEventArgs e)
            {
                selected = !selected;
                var brush = selected ? selectedBrush : normalBrush;

                foreach (var shape in allStroked)
                {
                    shape.Stroke = brush;
                }

                e.Handled = true;
            }

            foreach (var shape in clickTargets)
            {
                shape.MouseLeftButtonDown += OnClick;
            }
        }
    }
}
