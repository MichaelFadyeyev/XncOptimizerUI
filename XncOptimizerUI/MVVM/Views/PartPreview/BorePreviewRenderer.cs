using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws face-drilled bores (<see cref="BoreSurface.Face"/>) on top of the part preview
    /// built by <c>MainWindow.RenderPart</c>: a circle with crossed center lines on the Face
    /// rectangle, plus a rectangle with one center line projected onto each of the four
    /// Top/Bottom/Left/Right edge bands. Kept separate from <c>MainWindow.xaml.cs</c> so the
    /// overlay logic and its geometry are independently reusable/testable.
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
        /// Draws every <see cref="BoreSurface.Face"/> bore in <paramref name="programs"/> onto
        /// <paramref name="canvas"/>. A bore whose tool can't be resolved (or has a non-positive
        /// diameter) is skipped. Clicking any of a bore's five projections (the circle + its four
        /// edge-band rectangles) toggles all of them red together, since they represent the same
        /// physical hole.
        /// </summary>
        public static void DrawBores(Canvas canvas, IEnumerable<XncProgram> programs, FaceLayout layout, BoreBrushes brushes)
        {
            foreach (var program in programs)
            {
                var normalBrush = program.Side ? brushes.SideTrue : brushes.SideFalse;

                foreach (var bore in program.Bores)
                {
                    if (bore.Surface != BoreSurface.Face)
                    {
                        continue;
                    }

                    var diameter = program.Tools
                        .FirstOrDefault(t => t.Name == bore.ToolName)?.Diameter ?? 0;

                    if (diameter <= 0)
                    {
                        continue;
                    }

                    DrawBore(canvas, program, bore, diameter, layout, brushes, normalBrush);
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
            double left, top, width, height;

            if (horizontal)
            {
                width = diameterPx;
                height = depthPx;
                left = alongAxisPos - (width / 2);

                // Band's own near/far edges, one `gap` short of the Face rectangle itself.
                var nearEdge = near ? layout.OriginY - layout.Gap : layout.OriginY + layout.FaceHeight + layout.Gap;
                var farEdge = near
                    ? layout.OriginY - layout.Gap - layout.BandThickness
                    : layout.OriginY + layout.FaceHeight + layout.Gap + layout.BandThickness;

                if (near)
                {
                    // Top band: near edge is the bottom of the band (nearest Face).
                    top = side ? nearEdge - height : farEdge;
                }
                else
                {
                    // Bottom band: near edge is the top of the band (nearest Face).
                    top = side ? nearEdge : farEdge - height;
                }
            }
            else
            {
                height = diameterPx;
                width = depthPx;
                top = alongAxisPos - (height / 2);

                var nearEdge = near ? layout.OriginX - layout.Gap : layout.OriginX + layout.FaceWidth + layout.Gap;
                var farEdge = near
                    ? layout.OriginX - layout.Gap - layout.BandThickness
                    : layout.OriginX + layout.FaceWidth + layout.Gap + layout.BandThickness;

                if (near)
                {
                    // Left band: near edge is the right of the band (nearest Face).
                    left = side ? nearEdge - width : farEdge;
                }
                else
                {
                    // Right band: near edge is the left of the band (nearest Face).
                    left = side ? nearEdge : farEdge - width;
                }
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
    }
}
