using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
using IOPath = System.IO.Path;
using ShapePath = System.Windows.Shapes.Path;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.Views.PartPreview;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Characterization tests for the part-preview overlays: for every part of a fixture, the
    /// shapes <see cref="MillPreviewRenderer"/>, <see cref="BorePreviewRenderer"/> and
    /// <see cref="GroovePreviewRenderer"/> add to a canvas (in the order <see cref="PartPreviewRenderer.DrawPart"/> calls
    /// them) are listed as text - kind, geometry rounded to 0.001 px, brushes, thickness, dashes -
    /// and compared with the snapshots in <c>Golden/Preview/</c>. They lock in the drawing while
    /// the renderers are refactored. See <see cref="GoldenSnapshot"/> for accepting changes.
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public partial class PreviewGoldenTests
    {
        private const string SnapshotFolder = "Preview";
        private const double Scale = 0.5;
        private const double Origin = 40;
        private const double GapMm = 10;

        private static readonly Brush SideTrue = Named(Colors.Navy);
        private static readonly Brush SideFalse = Named(Colors.Teal);
        private static readonly Brush Selected = Named(Colors.Red);
        private static readonly Brush ThroughFill = Named(Colors.Ivory);
        private static readonly Brush Face = Named(Colors.Wheat);
        private static readonly Brush ThroughCutOff = Named(Colors.SkyBlue);
        private static readonly Brush BlindCutOff = Named(Colors.Silver);
        private static readonly Brush CentreLine = Named(Colors.Black);

        private static readonly Dictionary<Brush, string> BrushNames = new()
        {
            [SideTrue] = nameof(SideTrue),
            [SideFalse] = nameof(SideFalse),
            [Selected] = nameof(Selected),
            [ThroughFill] = nameof(ThroughFill),
            [Face] = nameof(Face),
            [ThroughCutOff] = nameof(ThroughCutOff),
            [BlindCutOff] = nameof(BlindCutOff),
            [CentreLine] = nameof(CentreLine),
        };

        private static readonly BoreBrushes BoreBrushes = new(
            SideTrue, SideFalse, Selected, ThroughFill,
            Thickness2Px: 2, Thickness1Px: 1, CenterLineOvershootMm: 3, DashLengthPx: 3, CenterLineDashMm: 3);

        private static readonly MillBrushes MillBrushes = new(
            Face, ThroughCutOff, BlindCutOff, CentreLine, CentreLineThickness: 1, StripOpacity: 0.5, Lines: BoreBrushes);

        private static readonly string[] Fixtures =
        [
            "td-bores-displaying.project",
            "td-bl-65-bore.project",
            "td-grooving-preview.project",
            "td-grooving-diagonal-preview.project",
            "td-grooving-non-centered-preview.project",
            "td-side-groove.project",
            "td-mill-offset.project",
            "td-mill-circle.project",
            "td-mill-ellipse.project",
            "td-milling.project",
            "td-milling-pocket.project",
            "td-clean-outside.project",
            "td-rotation.project",
        ];

        [TestCaseSource(nameof(Fixtures))]
        public void Overlays_MatchGoldenSnapshot(string fixture)
        {
            var text = new StringBuilder();

            foreach (var (partId, programs) in ProgramsByPart(fixture))
            {
                text.Append(CultureInfo.InvariantCulture, $"## part {partId}: operations {string.Join(", ", programs.Select(p => p.OperationId))}\n");

                foreach (var element in Render(programs))
                {
                    text.Append(Describe(element)).Append('\n');
                }

                text.Append('\n');
            }

            GoldenSnapshot.AssertMatches(IOPath.Combine(SnapshotFolder, IOPath.ChangeExtension(fixture, ".txt")), text.ToString());
        }

        #region Rendering

        /// <summary>XNC programs of every part in <paramref name="fixture"/>, in operation order, keyed by part id.</summary>
        private static IEnumerable<(int PartId, List<XncProgram> Programs)> ProgramsByPart(string fixture)
        {
            var path = IOPath.Combine(TestContext.CurrentContext.TestDirectory, "TestData", fixture);

            return XDocument.Load(path).Root!
                .Elements("operation")
                .Where(o => o.GetTypeIdValue() == "XNC")
                .GroupBy(o => o.Element("part")!.GetIdIntValue())
                .OrderBy(g => g.Key)
                .Select(g => (g.Key, g.Select(XncProgramReader.Read).ToList()));
        }

        /// <summary>Draws like <see cref="PartPreviewRenderer.DrawPart"/>: mills first, then bores, then grooves, at a fixed scale.</summary>
        private static IEnumerable<UIElement> Render(List<XncProgram> programs)
        {
            var first = programs[0];
            var layout = new FaceLayout(
                Origin, Origin, first.Dx * Scale, first.Dy * Scale, Scale, GapMm * Scale, first.Dz * Scale);
            var canvas = new Canvas();

            MillPreviewRenderer.DrawMills(canvas, programs, layout, MillBrushes);
            BorePreviewRenderer.DrawBores(canvas, programs, layout, BoreBrushes);
            GroovePreviewRenderer.DrawGrooves(canvas, programs, layout, BoreBrushes);

            return canvas.Children.Cast<UIElement>();
        }

        #endregion

        #region Description

        private static string Describe(UIElement element) => element switch
        {
            Line line => $"Line ({N(line.X1)}, {N(line.Y1)}) -> ({N(line.X2)}, {N(line.Y2)}){Style(line)}",
            Rectangle rect => $"Rectangle {Box(rect)}{Style(rect)}",
            Ellipse ellipse => $"Ellipse {Box(ellipse)}{Style(ellipse)}",
            Polygon polygon => $"Polygon [{string.Join(" ", polygon.Points.Select(p => $"{N(p.X)},{N(p.Y)}"))}]{Style(polygon)}",
            ShapePath path => $"Path {Geometry(path.Data)}{Style(path)}",
            _ => element.GetType().Name,
        };

        private static string Box(FrameworkElement element) =>
            $"at ({N(Canvas.GetLeft(element))}, {N(Canvas.GetTop(element))}) size {N(element.Width)} x {N(element.Height)}";

        private static string Style(Shape shape)
        {
            var parts = new List<string>
            {
                $"stroke={BrushName(shape.Stroke)}",
                $"thickness={N(shape.StrokeThickness)}",
            };

            if (shape.Fill is not null) parts.Add($"fill={BrushName(shape.Fill)}");
            if (shape.StrokeDashArray is { Count: > 0 } dashes) parts.Add($"dash=[{string.Join(" ", dashes.Select(N))}]");
            if (shape.StrokeStartLineCap != PenLineCap.Flat) parts.Add($"caps={shape.StrokeStartLineCap}");
            if (shape.StrokeLineJoin != PenLineJoin.Miter) parts.Add($"join={shape.StrokeLineJoin}");
            if (shape.Opacity != 1d) parts.Add($"opacity={N(shape.Opacity)}");
            if (!shape.IsHitTestVisible) parts.Add("no-hit-test");
            if (shape.Tag is not null) parts.Add($"tag={shape.Tag.GetType().Name}");

            return " | " + string.Join(" ", parts);
        }

        private static string BrushName(Brush? brush) => brush switch
        {
            null => "none",
            _ when BrushNames.TryGetValue(brush, out var name) => name,
            SolidColorBrush solid => solid.Color.ToString(CultureInfo.InvariantCulture),
            _ => brush.GetType().Name,
        };

        /// <summary>Path markup of <paramref name="geometry"/> with every number rounded like <see cref="N"/>.</summary>
        private static string Geometry(Geometry? geometry) =>
            geometry is null
                ? "none"
                : NumberPattern().Replace(
                    PathGeometry.CreateFromGeometry(geometry).ToString(CultureInfo.InvariantCulture),
                    m => N(double.Parse(m.Value, CultureInfo.InvariantCulture)));

        /// <summary>A coordinate rounded to 0.001 px, invariant, without <c>-0</c>.</summary>
        private static string N(double value)
        {
            if (double.IsNaN(value)) return "NaN";

            var rounded = Math.Round(value, 3);

            return (rounded == 0d ? 0d : rounded).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static SolidColorBrush Named(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();

            return brush;
        }

        [GeneratedRegex(@"-?\d+(\.\d+)?([eE][-+]?\d+)?")]
        private static partial Regex NumberPattern();

        #endregion
    }
}
