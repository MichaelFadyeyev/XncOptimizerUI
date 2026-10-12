using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;
using MillPath = XncOptimizerUI.MVVM.Views.PartPreview.MillPreviewGeometry.MillPath;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws every mill (<c>&lt;ms&gt;</c> contour, <c>&lt;mr&gt;</c> rectangle, <c>&lt;me&gt;</c>
    /// ellipse) on the Face rectangle of the part preview built by <see cref="PartPreviewRenderer.DrawPart"/>
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

        /// <summary>The shapes of every mill in <paramref name="programs"/>, bottom layer first.</summary>
        public static IReadOnlyList<UIElement> DrawMills(IEnumerable<XncProgram> programs, FaceLayout layout, MillBrushes brushes)
        {
            var layers = new Layers();

            foreach (var program in programs)
            {
                foreach (var (mill, path) in MillPaths(program))
                {
                    AddMill(layers, program, mill, path, layout, brushes);
                }
            }

            return [.. layers.BottomToTop()];
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
            FaceLayout layout,
            MillBrushes brushes)
        {
            var lines = brushes.Lines;
            var sideBrush = program.Side ? lines.SideTrue : lines.SideFalse;
            var sink = new OverlayShapeSink(new MillTag(program, mill), sideBrush);
            var outline = new Vec2(program.Dx, program.Dy);
            var toolPath = MillPreviewGeometry.ToolPath(path, outline);
            var diameterPx = layout.MmToPx(path.Diameter);

            // 1. Cut-off area, clipped to the Face rectangle. Not clickable: it would swallow
            // clicks meant for the face or the machining drawn over it.
            if (MillPreviewGeometry.RemovedRegion(path, outline) is { } region)
            {
                var face = new RectangleGeometry(new Rect(layout.OriginX, layout.OriginY, layout.FaceWidth, layout.FaceHeight));
                var boundary = ToGeometry(region.Boundary, layout, filled: true);
                var mode = region.Outside ? GeometryCombineMode.Exclude : GeometryCombineMode.Intersect;

                sink.Add(layers.Regions, new Path
                {
                    Data = Geometry.Combine(face, boundary, mode, null),
                    Fill = MillPreviewGeometry.IsThrough(path, program.Dz) ? brushes.ThroughCutOff : brushes.BlindCutOff,
                    IsHitTestVisible = false,
                }, stroke: null, isClickTarget: false);
            }

            // 2. Swept strip: the tool path stroked one tool diameter wide; round caps make its
            // ends the tool's semicircles.
            sink.Add(layers.Strips, new Path
            {
                Data = ToGeometry(toolPath, layout, filled: false),
                Stroke = brushes.Face,
                StrokeThickness = diameterPx,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = brushes.StripOpacity,
            }, stroke: null, isClickTarget: true);

            // 3. Programmed centre line, drawn like the part's own ridges; a back-side mill's in the
            // back-side bore colour.
            sink.Add(layers.CentreLines, new Path
            {
                Data = ToGeometry(path.Travel, layout, filled: false),
                StrokeThickness = brushes.CentreLineThickness,
            }, stroke: program.Side ? brushes.CentreLine : lines.SideFalse, isClickTarget: true);

            // 4. Tool-centre path, dashed like a groove's reference lines.
            var toolLine = sink.Add(layers.ToolPaths, new Path { Data = ToGeometry(toolPath, layout, filled: false) }, sideBrush, isClickTarget: true);
            OverlayShapeSink.SetStroke(toolLine, lines.Dashed);

            // 5. Start marker: the tool's circle with crossed centre lines, like a face bore.
            var start = layout.ToPx(MillPreviewGeometry.StartPoint(toolPath));
            var armPx = (diameterPx / 2d) + layout.MmToPx(lines.CenterLineOvershootMm);
            var armStyle = lines.Thin(lines.CrossLineDashPx(path.Diameter, layout));

            sink.AddCircle(layers.Markers, start, diameterPx, lines.Bold);

            foreach (var arm in PartPreviewOverlayGeometry.CrossArms(start, armPx))
            {
                sink.AddLine(layers.Markers, arm, armStyle);
            }

            sink.AttachClickToggle(lines.Selected);
        }

        /// <summary>
        /// Converts a primitive chain (mm) into canvas geometry (px): one figure per connected run,
        /// arcs as true <see cref="ArcSegment"/>s (a full circle as two halves).
        /// <paramref name="filled"/> closes and fills each figure, for an area boundary.
        /// </summary>
        private static PathGeometry ToGeometry(IReadOnlyList<Primitive> chain, FaceLayout layout, bool filled)
        {
            var geometry = new PathGeometry();
            PathFigure? figure = null;
            Vec2? cursor = null;

            foreach (var primitive in chain)
            {
                if (figure == null || cursor is not { } at || at.DistanceTo(primitive.Start) > PlanarGeometry.Tolerance)
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

        private static IEnumerable<PathSegment> ToSegments(Primitive primitive, FaceLayout layout)
        {
            if (primitive.Centre is not { } centre)
            {
                yield return new LineSegment(ToPoint(primitive.End, layout), isStroked: true);
                yield break;
            }

            var radiusPx = layout.MmToPx(primitive.Radius);
            var size = new Size(radiusPx, radiusPx);

            // Screen and operator view share the Y-down frame, so operator clockwise is screen clockwise.
            var sweep = primitive.Clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

            if (primitive.Start.DistanceTo(primitive.End) <= PlanarGeometry.Tolerance)
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

        private static Point ToPoint(Vec2 mm, FaceLayout layout) =>
            new(layout.XPx(mm.X), layout.YPx(mm.Y));
    }
}
