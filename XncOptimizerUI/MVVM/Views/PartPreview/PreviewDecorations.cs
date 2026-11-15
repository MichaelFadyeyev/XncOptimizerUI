using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.ViewModels.Preview;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// The non-machining marks of the part preview: the texture mark on the Face rectangle, the
    /// edge-band symbols beside the side views, and the axis glyph and turn label pinned to the
    /// viewport.
    /// </summary>
    internal static class PreviewDecorations
    {
        /// <summary>Parallel lines in the texture mark.</summary>
        private const int TextureLineCount = 3;

        /// <summary>The texture lines are the face's size along the grain divided by this.</summary>
        private const double TextureLineLengthDivisor = 3;

        /// <summary>The outer texture lines are the face's size across the grain divided by this apart.</summary>
        private const double TextureSpanDivisor = 5;

        /// <summary>Length of each axis arrow (px).</summary>
        private const double AxisArmLength = 22;

        /// <summary>Length of the arrow-head strokes, along both axes (px).</summary>
        private const double AxisArrowHead = 5;

        /// <summary>Diameter of the circle marking the axis origin (px).</summary>
        private const double AxisOriginDiameter = 6;

        private const double AxisLabelFontSize = 10;

        /// <summary>Offset of the "X" label from the X arrow's tip (px).</summary>
        private static readonly Vector AxisXLabelOffset = new(2, -8);

        /// <summary>Offset of the "Y" label from the Y arrow's tip (px).</summary>
        private static readonly Vector AxisYLabelOffset = new(-10, -2);

        /// <summary>Gap between the turn label and the viewport's bottom-right corner, in scrollbar widths.</summary>
        private const double TurnLabelPaddingInScrollbars = 1.5;

        /// <summary>
        /// Three centred, parallel lines over the face marking the grain: along the program's X
        /// at turn 0, turned with the first applied program's <paramref name="programTurn"/>.
        /// </summary>
        public static IEnumerable<UIElement> TextureIndicator(PxRect face, int programTurn, PartBrushes brushes)
        {
            var centerX = face.Left + (face.Width / 2);
            var centerY = face.Top + (face.Height / 2);
            var isVertical = Math.Abs(programTurn % 2) == 1;
            var lineLength = (isVertical ? face.Height : face.Width) / TextureLineLengthDivisor;
            var blockWidth = (isVertical ? face.Width : face.Height) / TextureSpanDivisor;

            // Line i sits (i - middle) half-spans off the centre.
            const int middle = TextureLineCount / 2;

            for (var i = 0; i < TextureLineCount; i++)
            {
                var offset = (i - middle) * (blockWidth / 2);
                var line = new Line
                {
                    X1 = 0,
                    X2 = isVertical ? 0 : lineLength,
                    Y1 = 0,
                    Y2 = isVertical ? lineLength : 0,
                    Stroke = brushes.Texture,
                    StrokeThickness = brushes.TextureThickness,
                };

                Canvas.SetLeft(line, centerX + (isVertical ? offset : -lineLength / 2));
                Canvas.SetTop(line, centerY + (isVertical ? -lineLength / 2 : offset));

                yield return line;
            }
        }

        /// <summary>
        /// The edge-band symbol of each banded side, facing the centre of that side's view from
        /// outside: centred along the side view, <see cref="BandSymbolStyle.Gap"/> beyond its outer
        /// edge. Unbanded sides get nothing.
        /// </summary>
        public static IEnumerable<UIElement> BandSymbols(PartPreviewLayout layout, EdgeBandSymbols symbols, BandSymbolStyle style) =>
            layout.Bands
                .Select(band => (band.Rect, Side: BandSide(band.Side, symbols)))
                .Where(band => !string.IsNullOrEmpty(band.Side.Symbol))
                .Select(band => BandSymbol(band.Side.Symbol, band.Rect, band.Side.Outward, style));

        /// <summary>The symbol shown at <paramref name="side"/> and the unit vector pointing away from the face there.</summary>
        private static (string Symbol, Vector Outward) BandSide(string side, EdgeBandSymbols symbols) => side switch
        {
            "Top" => (symbols.Top, new Vector(0, -1)),
            "Right" => (symbols.Right, new Vector(1, 0)),
            "Bottom" => (symbols.Bottom, new Vector(0, 1)),
            "Left" => (symbols.Left, new Vector(-1, 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown side view."),
        };

        /// <summary>
        /// <paramref name="symbol"/> centred on the centre of <paramref name="band"/> pushed
        /// <paramref name="outward"/> by half the band, the gap and half the label.
        /// </summary>
        private static TextBlock BandSymbol(string symbol, PxRect band, Vector outward, BandSymbolStyle style)
        {
            var label = new TextBlock
            {
                Text = symbol,
                Foreground = style.Foreground,
                FontSize = style.FontSize,
                FontWeight = FontWeights.Bold,
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var size = label.DesiredSize;
            var halfAcross = (Math.Abs(outward.X) * (band.Width + size.Width) / 2)
                + (Math.Abs(outward.Y) * (band.Height + size.Height) / 2);
            var bandCenter = new Point(band.Left + (band.Width / 2), band.Top + (band.Height / 2));
            var labelCenter = bandCenter + (outward * (halfAcross + style.Gap));

            Canvas.SetLeft(label, labelCenter.X - (size.Width / 2));
            Canvas.SetTop(label, labelCenter.Y - (size.Height / 2));

            return label;
        }

        /// <summary>
        /// The selected part's XNC turn as degrees (e.g. "90°") in the bottom-right corner of a
        /// <paramref name="viewport"/>-sized overlay; nothing for empty text.
        /// </summary>
        public static IEnumerable<UIElement> TurnLabel(string text, Size viewport, DecorationBrushes brushes)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            var label = new TextBlock
            {
                Text = text,
                Foreground = brushes.TurnLabel,
                FontSize = brushes.TurnLabelFontSize,
                FontWeight = FontWeights.Bold,
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var scrollbarSize = Math.Max(
                SystemParameters.VerticalScrollBarWidth,
                SystemParameters.HorizontalScrollBarHeight);
            var pad = scrollbarSize * TurnLabelPaddingInScrollbars;
            Canvas.SetLeft(label, viewport.Width - label.DesiredSize.Width - pad);
            Canvas.SetTop(label, viewport.Height - label.DesiredSize.Height - pad);

            yield return label;
        }

        /// <summary>
        /// Small X (right) / Y (down) axis marker in the top-left corner, half a
        /// <paramref name="margin"/> in. Deliberately NOT rotated by the part's XNC <c>turn</c>: the
        /// on-screen axes stay X-horizontal / Y-vertical regardless of turn, which is only reflected
        /// in the display dimensions and the turn label.
        /// </summary>
        public static IEnumerable<UIElement> AxisGlyph(double margin, DecorationBrushes brushes)
        {
            var ox = margin * 0.5;
            var oy = margin * 0.5;
            const double arm = AxisArmLength;
            const double head = AxisArrowHead;

            Line Stroke(double x1, double y1, double x2, double y2, Brush brush) => new()
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = brush,
                StrokeThickness = brushes.AxisThickness,
            };

            var origin = new Ellipse
            {
                Width = AxisOriginDiameter,
                Height = AxisOriginDiameter,
                Stroke = brushes.AxisX,
                StrokeThickness = brushes.AxisThickness,
                Fill = Brushes.Transparent,
            };
            Canvas.SetLeft(origin, ox - (AxisOriginDiameter / 2));
            Canvas.SetTop(origin, oy - (AxisOriginDiameter / 2));

            yield return origin;

            // X arrow (right)
            yield return Stroke(ox, oy, ox + arm, oy, brushes.AxisX);
            yield return Stroke(ox + arm, oy, ox + arm - head, oy - head, brushes.AxisX);
            yield return Stroke(ox + arm, oy, ox + arm - head, oy + head, brushes.AxisX);

            // Y arrow (down)
            yield return Stroke(ox, oy, ox, oy + arm, brushes.AxisY);
            yield return Stroke(ox, oy + arm, ox - head, oy + arm - head, brushes.AxisY);
            yield return Stroke(ox, oy + arm, ox + head, oy + arm - head, brushes.AxisY);

            yield return AxisLabel("X", brushes.AxisX, ox + arm + AxisXLabelOffset.X, oy + AxisXLabelOffset.Y);
            yield return AxisLabel("Y", brushes.AxisY, ox + AxisYLabelOffset.X, oy + arm + AxisYLabelOffset.Y);
        }

        private static TextBlock AxisLabel(string text, Brush brush, double x, double y) => new()
        {
            Text = text,
            Foreground = brush,
            FontSize = AxisLabelFontSize,
            FontWeight = FontWeights.Bold,
            RenderTransform = new TranslateTransform(x, y),
        };
    }
}
