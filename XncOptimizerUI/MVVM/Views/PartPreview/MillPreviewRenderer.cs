using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;
using MillPath = XncOptimizerUI.MVVM.Views.PartPreview.MillPreviewGeometry.MillPath;
using Primitive = XncOptimizerUI.Services.Xnc.ContourOffsetGeometry.Primitive;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws every mill (<c>&lt;ms&gt;</c> contour, <c>&lt;mr&gt;</c> rectangle, <c>&lt;me&gt;</c>
    /// ellipse) on the Face rectangle of the part preview built by <c>MainWindow.RenderPart</c>
    /// (edge bands get no mill projection yet). Per mill, bottom to top:
    /// <list type="number">
    /// <item>the part area the mill cuts off (<c>c</c> = right/left/pocket), filled with the
    /// through or blind cut-off brush (through when the mill's depth reaches <c>dz</c>);
    /// <c>c="0"</c> keeps both sides, so nothing is filled;</item>
    /// <item>the strip the tool sweeps - its centre path stroked one tool diameter wide with
    /// semicircular ends, in the face brush at reduced opacity;</item>
    /// <item>the programmed centre line, styled like the part outline (a back-side mill's in
    /// the back-side bore colour);</item>
    /// <item>the tool-centre path (centre line shifted by half the diameter for <c>c</c> =
    /// right/left/pocket), dashed;</item>
    /// <item>a tool-diameter circle with crossed centre lines at the tool path's start, styled like
    /// a face bore.</item>
    /// </list>
    /// Each layer is stacked across all mills, so one mill's cut-off fill never hides another's
    /// lines. Clicking a mill's strip, centre line, tool path or start marker toggles all its
    /// lines red, like a bore or a groove.
    /// </summary>
    internal static class MillPreviewRenderer
    {
        /// <summary>Identifies the mill (<see cref="XncMillingContour"/>, <see cref="XncMillingRectangle"/> or <see cref="XncMillingEllipse"/>) and its owning program a shape belongs to.</summary>
        internal readonly record struct MillTag(XncProgram Program, object Mill);

        /// <summary>Mill styling, sourced from <c>Window.Resources</c> by the caller.</summary>
        internal readonly record struct MillBrushes(
            Brush Face,
            Brush ThroughCutOff,
            Brush BlindCutOff,
            Brush CentreLine,
            double CentreLineThickness,
            double StripOpacity,
            BorePreviewRenderer.BoreBrushes Lines);

        /// <summary>The shapes of every mill, one list per stacking layer.</summary>
        private sealed class Layers
        {
            public List<UIElement> Regions { get; } = [];
            public List<UIElement> Strips { get; } = [];
            public List<UIElement> CentreLines { get; } = [];
            public List<UIElement> ToolPaths { get; } = [];
            public List<UIElement> Markers { get; } = [];

            public IEnumerable<UIElement> BottomToTop() =>
                Regions.Concat(Strips).Concat(CentreLines).Concat(ToolPaths).Concat(Markers);
        }

        public static void DrawMills(
            Canvas canvas,
            IEnumerable<XncProgram> programs,
            BorePreviewRenderer.FaceLayout layout,
            MillBrushes brushes)
        {
            var layers = new Layers();

            foreach (var program in programs)
            {
                foreach (var (mill, path) in MillPaths(program))
                {
                    AddMill(layers, program, mill, path, layout, brushes);
                }
            }

            foreach (var element in layers.BottomToTop())
            {
                canvas.Children.Add(element);
            }
        }

        /// <summary>Every drawable mill of <paramref name="program"/> with its tool resolved; mills whose tool is unknown or has no diameter are skipped, like bores.</summary>
        private static IEnumerable<(object Mill, MillPath Path)> MillPaths(XncProgram program)
        {
            double Diameter(string toolName) => program.Tools
                .FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase))?.Diameter ?? 0d;

            return program.MillingContours
                .Select(c => ((object)c, MillPreviewGeometry.FromContour(c, Diameter(c.ToolName))))
                .Concat(program.MillingRectangles.Select(r => ((object)r, MillPreviewGeometry.FromRectangle(r, Diameter(r.ToolName)))))
                .Concat(program.MillingEllipses.Select(e => ((object)e, MillPreviewGeometry.FromEllipse(e, Diameter(e.ToolName)))))
                .Where(m => m.Item2 is { Diameter: > 0d })
                .Select(m => (m.Item1, m.Item2!));
        }

        private static void AddMill(
            Layers layers,
            XncProgram program,
            object mill,
            MillPath path,
            BorePreviewRenderer.FaceLayout layout,
            MillBrushes brushes)
        {
            var lines = brushes.Lines;
            var sideBrush = program.Side ? lines.SideTrue : lines.SideFalse;
            var tag = new MillTag(program, mill);
            var outline = new Vec2(program.Dx, program.Dy);
            var toolPath = MillPreviewGeometry.ToolPath(path, outline);
            var diameterPx = path.Diameter * layout.Scale;

            var clickTargets = new List<Shape>();
            var stroked = new List<(Shape Shape, Brush Normal)>();

            T Add<T>(List<UIElement> layer, T shape, Brush? normalStroke, bool isClickTarget)
                where T : Shape
            {
                shape.Tag = tag;
                layer.Add(shape);

                if (normalStroke != null)
                {
                    shape.Stroke = normalStroke;
                    stroked.Add((shape, normalStroke));
                }

                if (isClickTarget)
                {
                    clickTargets.Add(shape);
                }

                return shape;
            }

            // 1. Cut-off area, clipped to the Face rectangle. Not clickable: it would swallow
            // clicks meant for the face or the machining drawn over it.
            if (MillPreviewGeometry.RemovedRegion(path, outline) is { } region)
            {
                var face = new RectangleGeometry(new Rect(layout.OriginX, layout.OriginY, layout.FaceWidth, layout.FaceHeight));
                var boundary = ToGeometry(region.Boundary, layout, filled: true);
                var mode = region.Outside ? GeometryCombineMode.Exclude : GeometryCombineMode.Intersect;

                Add(layers.Regions, new Path
                {
                    Data = Geometry.Combine(face, boundary, mode, null),
                    Fill = MillPreviewGeometry.IsThrough(path, program.Dz) ? brushes.ThroughCutOff : brushes.BlindCutOff,
                    IsHitTestVisible = false,
                }, normalStroke: null, isClickTarget: false);
            }

            // 2. Swept strip: the tool path stroked one tool diameter wide; round caps make its
            // ends the tool's semicircles.
            Add(layers.Strips, new Path
            {
                Data = ToGeometry(toolPath, layout, filled: false),
                Stroke = brushes.Face,
                StrokeThickness = diameterPx,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = brushes.StripOpacity,
            }, normalStroke: null, isClickTarget: true);

            // 3. Programmed centre line, drawn like the part's own ridges; a back-side mill's in the
            // back-side bore colour.
            var centreLineBrush = program.Side ? brushes.CentreLine : lines.SideFalse;
            var centreLine = Add(layers.CentreLines, new Path { Data = ToGeometry(path.Travel, layout, filled: false) }, centreLineBrush, isClickTarget: true);
            centreLine.StrokeThickness = brushes.CentreLineThickness;

            // 4. Tool-centre path, dashed like a groove's reference lines.
            var toolLine = Add(layers.ToolPaths, new Path { Data = ToGeometry(toolPath, layout, filled: false) }, sideBrush, isClickTarget: true);
            PartPreviewOverlayGeometry.SetStroke(toolLine, lines.Thickness1Px, lines.DashLengthPx);

            // 5. Start marker: the tool's circle with crossed centre lines, like a face bore.
            var start = ToPoint(MillPreviewGeometry.StartPoint(toolPath), layout);
            var radiusPx = diameterPx / 2d;
            var overshootPx = lines.CenterLineOvershootMm * layout.Scale;
            var crossDashPx = path.Diameter > 5d ? lines.CenterLineDashMm * layout.Scale : (double?)null;

            var circle = Add(layers.Markers, new Ellipse { Width = diameterPx, Height = diameterPx, Fill = Brushes.Transparent }, sideBrush, isClickTarget: true);
            PartPreviewOverlayGeometry.SetStroke(circle, lines.Thickness2Px);
            Canvas.SetLeft(circle, start.X - radiusPx);
            Canvas.SetTop(circle, start.Y - radiusPx);

            void AddCrossArm(double x1, double y1, double x2, double y2)
            {
                var arm = Add(layers.Markers, new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 }, sideBrush, isClickTarget: false);
                PartPreviewOverlayGeometry.SetStroke(arm, lines.Thickness1Px, crossDashPx);
            }

            PartPreviewOverlayGeometry.AddCrossArms(AddCrossArm, start.X, start.Y, radiusPx + overshootPx);

            PartPreviewOverlayGeometry.AttachClickToggle(clickTargets, stroked, lines.Selected);
        }

        /// <summary>
        /// Converts a primitive chain (mm) into canvas geometry (px): one figure per connected run,
        /// arcs as true <see cref="ArcSegment"/>s (a full circle as two halves).
        /// <paramref name="filled"/> closes and fills each figure, for an area boundary.
        /// </summary>
        private static PathGeometry ToGeometry(IReadOnlyList<Primitive> chain, BorePreviewRenderer.FaceLayout layout, bool filled)
        {
            var geometry = new PathGeometry();
            PathFigure? figure = null;
            Vec2? cursor = null;

            foreach (var primitive in chain)
            {
                if (figure == null || cursor is not { } at || at.DistanceTo(primitive.Start) > ContourOffsetGeometry.Tolerance)
                {
                    figure = new PathFigure { StartPoint = ToPoint(primitive.Start, layout), IsClosed = filled, IsFilled = filled };
                    geometry.Figures.Add(figure);
                }

                foreach (var segment in ToSegments(primitive, layout))
                {
                    figure.Segments.Add(segment);
                }

                cursor = primitive.End;
            }

            return geometry;
        }

        private static IEnumerable<PathSegment> ToSegments(Primitive primitive, BorePreviewRenderer.FaceLayout layout)
        {
            if (primitive.Centre is not { } centre)
            {
                yield return new LineSegment(ToPoint(primitive.End, layout), isStroked: true);
                yield break;
            }

            var size = new Size(primitive.Radius * layout.Scale, primitive.Radius * layout.Scale);

            // Screen and operator view share the Y-down frame, so operator clockwise is screen clockwise.
            var sweep = primitive.Clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

            if (primitive.Start.DistanceTo(primitive.End) <= ContourOffsetGeometry.Tolerance)
            {
                // A full circle: WPF cannot draw an arc back onto its own start, so go via the antipode.
                var antipode = (centre * 2d) - primitive.Start;
                yield return new ArcSegment(ToPoint(antipode, layout), size, 0d, false, sweep, isStroked: true);
                yield return new ArcSegment(ToPoint(primitive.End, layout), size, 0d, false, sweep, isStroked: true);
                yield break;
            }

            var isLargeArc = Math.Abs(ContourOffsetGeometry.OperatorSweep(primitive)) > Math.PI;
            yield return new ArcSegment(ToPoint(primitive.End, layout), size, 0d, isLargeArc, sweep, isStroked: true);
        }

        private static Point ToPoint(Vec2 mm, BorePreviewRenderer.FaceLayout layout) =>
            new(layout.OriginX + (mm.X * layout.Scale), layout.OriginY + (mm.Y * layout.Scale));
    }
}
