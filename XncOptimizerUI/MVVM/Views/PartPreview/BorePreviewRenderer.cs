using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws every bore on top of the part preview built by <c>MainWindow.RenderPart</c>.
    /// A face-drilled bore (<see cref="BoreSurface.Face"/>) gets a circle with crossed
    /// center lines on the Face rectangle, plus a rectangle with one center line projected
    /// onto each of the four Top/Bottom/Left/Right edge bands. An edge-drilled bore
    /// (<see cref="BoreSurface.Top"/>/<see cref="BoreSurface.Bottom"/>/
    /// <see cref="BoreSurface.Left"/>/<see cref="BoreSurface.Right"/>) is the mirror image:
    /// its true cross-section (circle) sits on the one band it was drilled from, and its
    /// depth reading (rectangle) sits on the Face rectangle, flush to that same edge and
    /// growing inward toward the panel center. Kept separate from <c>MainWindow.xaml.cs</c>
    /// so the overlay logic and its geometry are independently reusable/testable.
    /// </summary>
    internal static class BorePreviewRenderer
    {
        /// <summary>Face/band geometry already computed by <c>RenderPart</c>, reused as-is.</summary>
        internal readonly record struct FaceLayout(
            double OriginX,
            double OriginY,
            double FaceWidth,
            double FaceHeight,
            double Scale,
            double Gap,
            double BandThickness);

        /// <summary>Identifies the bore (and its owning program, for the <c>Side</c> colour) a shape belongs to.</summary>
        internal readonly record struct BoreTag(XncProgram Program, XncBore Bore);

        /// <summary>Bore styling, sourced from <c>Window.Resources</c> by the caller.</summary>
        internal readonly record struct BoreBrushes(
            Brush SideTrue,
            Brush SideFalse,
            Brush Selected,
            Brush ThroughFill,
            double OutlineThickness,
            double CenterLineThickness,
            double CenterLineOvershootMm);

        /// <summary>
        /// Draws every bore in <paramref name="programs"/> onto <paramref name="canvas"/>. A
        /// bore whose tool can't be resolved (or has a non-positive diameter) is skipped.
        /// A face bore's five projections (the circle + its four edge-band rectangles), or an
        /// edge bore's two (its band circle + Face rectangle), toggle red together on click,
        /// since they represent the same physical hole.
        /// </summary>
        public static void DrawBores(Canvas canvas, IEnumerable<XncProgram> programs, FaceLayout layout, BoreBrushes brushes)
        {
            foreach (var program in programs)
            {
                var normalBrush = program.Side ? brushes.SideTrue : brushes.SideFalse;

                foreach (var bore in program.Bores)
                {
                    var diameter = program.Tools
                        .FirstOrDefault(t => t.Name == bore.ToolName)?.Diameter ?? 0;

                    if (diameter <= 0)
                    {
                        continue;
                    }

                    if (bore.Surface == BoreSurface.Face)
                    {
                        DrawBore(canvas, program, bore, diameter, layout, brushes, normalBrush);
                    }
                    else
                    {
                        // Edge bores (bt/bb/bl/br) always share one color, regardless of the
                        // owning program's Side - unlike a face bore's Side-based coloring.
                        DrawEdgeBore(canvas, program, bore, diameter, layout, brushes);
                    }
                }
            }
        }

        private static void DrawBore(
            Canvas canvas,
            XncProgram program,
            XncBore bore,
            double diameter,
            FaceLayout layout,
            BoreBrushes brushes,
            Brush normalBrush)
        {
            var tag = new BoreTag(program, bore);
            var diameterPx = diameter * layout.Scale;
            var radiusPx = diameterPx / 2;
            var depthPx = bore.Depth * layout.Scale;
            var overshootPx = brushes.CenterLineOvershootMm * layout.Scale;
            var cx = layout.OriginX + (bore.X * layout.Scale);
            var cy = layout.OriginY + (bore.Y * layout.Scale);

            var clickTargets = new List<Shape>(5);
            var allStroked = new List<Shape>(11);

            void AddShape(Shape shape, bool isClickTarget)
            {
                shape.Stroke = normalBrush;
                shape.Fill = Brushes.Transparent;
                shape.StrokeThickness = brushes.OutlineThickness;
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
                    Stroke = normalBrush,
                    StrokeThickness = brushes.CenterLineThickness,
                };
                canvas.Children.Add(line);
                allStroked.Add(line);
            }

            // Front (circle) projection: full disc + horizontal/vertical center lines.
            var circle = new Ellipse { Width = diameterPx, Height = diameterPx };
            Canvas.SetLeft(circle, cx - radiusPx);
            Canvas.SetTop(circle, cy - radiusPx);
            AddShape(circle, isClickTarget: true);

            // Only the front (circle) projection shows the through-bore background; the side
            // (rectangle) projections stay transparent regardless of Through.
            if (bore.Through)
            {
                circle.Fill = brushes.ThroughFill;
            }

            AddCenterLine(cx - radiusPx - overshootPx, cy, cx + radiusPx + overshootPx, cy);
            AddCenterLine(cx, cy - radiusPx - overshootPx, cx, cy + radiusPx + overshootPx);

            // Edge-band (side) projections: one rectangle + one center line per band.
            AddSideRectangle(AddShape, AddCenterLine, cx, depthPx, diameterPx, overshootPx, layout, program.Side, horizontal: true, near: true);
            AddSideRectangle(AddShape, AddCenterLine, cx, depthPx, diameterPx, overshootPx, layout, program.Side, horizontal: true, near: false);
            AddSideRectangle(AddShape, AddCenterLine, cy, depthPx, diameterPx, overshootPx, layout, program.Side, horizontal: false, near: true);
            AddSideRectangle(AddShape, AddCenterLine, cy, depthPx, diameterPx, overshootPx, layout, program.Side, horizontal: false, near: false);

            var selected = false;

            void OnClick(object sender, MouseButtonEventArgs e)
            {
                selected = !selected;
                var brush = selected ? brushes.Selected : normalBrush;

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

        /// <summary>
        /// Draws one edge-drilled bore (<see cref="BoreSurface.Top"/>/<see cref="BoreSurface.Bottom"/>/
        /// <see cref="BoreSurface.Left"/>/<see cref="BoreSurface.Right"/>): a circle with crossed
        /// center lines on the one band it was drilled from, plus a rectangle with one center
        /// line on the Face rectangle, flush to that same edge and growing inward. Mirrors
        /// <see cref="DrawBore"/>'s circle/rectangle roles but swaps which projection carries
        /// which shape, and always colors with <see cref="BoreBrushes.SideTrue"/> regardless of
        /// <paramref name="program"/>'s own <c>Side</c> (which still drives the band circle's
        /// near/far depth anchor, per <see cref="GetBandEdges"/>). Neither shape uses
        /// <see cref="BoreBrushes.ThroughFill"/> - both stay outline-only.
        /// </summary>
        private static void DrawEdgeBore(
            Canvas canvas,
            XncProgram program,
            XncBore bore,
            double diameter,
            FaceLayout layout,
            BoreBrushes brushes)
        {
            var normalBrush = brushes.SideTrue;
            var tag = new BoreTag(program, bore);
            var diameterPx = diameter * layout.Scale;
            var radiusPx = diameterPx / 2;
            var depthPx = bore.Depth * layout.Scale;
            var overshootPx = brushes.CenterLineOvershootMm * layout.Scale;
            var horizontal = bore.Surface is BoreSurface.Top or BoreSurface.Bottom;
            var near = bore.Surface is BoreSurface.Top or BoreSurface.Left;

            var alongAxisPx = horizontal
                ? layout.OriginX + (bore.X * layout.Scale)
                : layout.OriginY + (bore.Y * layout.Scale);

            var clickTargets = new List<Shape>(2);
            var allStroked = new List<Shape>(5);

            void AddShape(Shape shape, bool isClickTarget)
            {
                shape.Stroke = normalBrush;
                shape.Fill = Brushes.Transparent;
                shape.StrokeThickness = brushes.OutlineThickness;
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
                    Stroke = normalBrush,
                    StrokeThickness = brushes.CenterLineThickness,
                };
                canvas.Children.Add(line);
                allStroked.Add(line);
            }

            // Side-band (circle) projection: the bore's true cross-section. Its perpendicular
            // (into-band) position comes from bore.Z, offset from the band edge nearest the
            // Face when this program's Side is the working face, else from the band's far/outer
            // edge - same near/far/side rule AddSideRectangle applies to a rectangle extent.
            var (nearEdge, farEdge, direction) = GetBandEdges(layout, horizontal, near);
            var zStart = program.Side ? nearEdge : farEdge;
            var zDir = program.Side ? direction : -direction;
            var perpPx = zStart + (zDir * (bore.Z * layout.Scale));

            var cx = horizontal ? alongAxisPx : perpPx;
            var cy = horizontal ? perpPx : alongAxisPx;

            var circle = new Ellipse { Width = diameterPx, Height = diameterPx };
            Canvas.SetLeft(circle, cx - radiusPx);
            Canvas.SetTop(circle, cy - radiusPx);
            AddShape(circle, isClickTarget: true);

            AddCenterLine(cx - radiusPx - overshootPx, cy, cx + radiusPx + overshootPx, cy);
            AddCenterLine(cx, cy - radiusPx - overshootPx, cx, cy + radiusPx + overshootPx);

            // Front (rectangle) projection: the depth reading, always flush to the bore's own
            // physical Face edge and growing inward toward the panel center - no Side
            // branching, since the edge is fixed regardless of which face the program machines.
            AddFaceRectangle(AddShape, AddCenterLine, alongAxisPx, depthPx, diameterPx, overshootPx, layout, bore.Surface, horizontal);

            var selected = false;

            void OnClick(object sender, MouseButtonEventArgs e)
            {
                selected = !selected;
                var brush = selected ? brushes.Selected : normalBrush;

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

        /// <summary>
        /// Returns the near/far pixel edges of one Top/Bottom/Left/Right band and the
        /// near-to-far direction sign (-1 for Top/Left, +1 for Bottom/Right), factored out of
        /// <see cref="AddSideRectangle"/> so <see cref="DrawEdgeBore"/> can place a point (not
        /// just a rectangle extent) against the same edges.
        /// </summary>
        private static (double NearEdge, double FarEdge, double Direction) GetBandEdges(FaceLayout layout, bool horizontal, bool near)
        {
            var direction = near ? -1d : 1d;
            var nearEdge = horizontal
                ? (near ? layout.OriginY - layout.Gap : layout.OriginY + layout.FaceHeight + layout.Gap)
                : (near ? layout.OriginX - layout.Gap : layout.OriginX + layout.FaceWidth + layout.Gap);
            var farEdge = nearEdge + (direction * layout.BandThickness);

            return (nearEdge, farEdge, direction);
        }

        /// <summary>
        /// Draws one edge-band rectangle for a face bore, plus its center line.
        /// <paramref name="horizontal"/> selects the Top/Bottom band pair (true, band spans the
        /// part's X axis) vs the Left/Right pair (false, spans Y); <paramref name="near"/>
        /// selects which of that pair (Top/Left = true, Bottom/Right = false).
        /// <paramref name="alongAxisPos"/> is the bore's scaled X or Y (matching
        /// <paramref name="horizontal"/>). The rectangle sits flush against the band edge
        /// closest to the Face rectangle when <paramref name="side"/> is true (front), or the
        /// band's far/outer edge when false (back) - see plan Context for the rationale.
        /// </summary>
        private static void AddSideRectangle(
            Action<Shape, bool> addShape,
            Action<double, double, double, double> addCenterLine,
            double alongAxisPos,
            double depthPx,
            double diameterPx,
            double overshootPx,
            FaceLayout layout,
            bool side,
            bool horizontal,
            bool near)
        {
            var (nearEdge, farEdge, direction) = GetBandEdges(layout, horizontal, near);

            // Rectangle spans depthPx from the reference edge (the band edge nearest the Face
            // when side is true, else the band's far/outer edge) toward the other edge.
            var start = side ? nearEdge : farEdge;
            var dir = side ? direction : -direction;
            var end = start + (dir * depthPx);
            var lo = Math.Min(start, end);

            double left, top, width, height;

            if (horizontal)
            {
                width = diameterPx;
                height = depthPx;
                left = alongAxisPos - (width / 2);
                top = lo;
            }
            else
            {
                height = diameterPx;
                width = depthPx;
                top = alongAxisPos - (height / 2);
                left = lo;
            }

            var rect = new Rectangle { Width = width, Height = height };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            addShape(rect, true);

            // Center line runs along the depth axis (the bore's drilling direction), overshooting
            // the rectangle by overshootPx at each end - not along the diameter axis.
            if (horizontal)
            {
                var midX = left + (width / 2);
                addCenterLine(midX, top - overshootPx, midX, top + height + overshootPx);
            }
            else
            {
                var midY = top + (height / 2);
                addCenterLine(left - overshootPx, midY, left + width + overshootPx, midY);
            }
        }

        /// <summary>
        /// Draws one Face-rectangle depth projection for an edge bore, plus its center line.
        /// Unlike <see cref="AddSideRectangle"/> (which anchors to a band edge and flips
        /// direction on <c>side</c>), this always starts flush at the bore's own physical Face
        /// edge - given by <paramref name="surface"/> - and grows inward toward the panel
        /// center by <paramref name="depthPx"/>, since that edge is fixed regardless of which
        /// face the program machines.
        /// </summary>
        private static void AddFaceRectangle(
            Action<Shape, bool> addShape,
            Action<double, double, double, double> addCenterLine,
            double alongAxisPos,
            double depthPx,
            double diameterPx,
            double overshootPx,
            FaceLayout layout,
            BoreSurface surface,
            bool horizontal)
        {
            double left, top, width, height;

            if (horizontal)
            {
                width = diameterPx;
                height = depthPx;
                left = alongAxisPos - (width / 2);
                top = surface == BoreSurface.Top
                    ? layout.OriginY
                    : layout.OriginY + layout.FaceHeight - height;
            }
            else
            {
                height = diameterPx;
                width = depthPx;
                top = alongAxisPos - (height / 2);
                left = surface == BoreSurface.Left
                    ? layout.OriginX
                    : layout.OriginX + layout.FaceWidth - width;
            }

            var rect = new Rectangle { Width = width, Height = height };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            addShape(rect, true);

            if (horizontal)
            {
                var midX = left + (width / 2);
                addCenterLine(midX, top - overshootPx, midX, top + height + overshootPx);
            }
            else
            {
                var midY = top + (height / 2);
                addCenterLine(left - overshootPx, midY, left + width + overshootPx, midY);
            }
        }
    }
}
