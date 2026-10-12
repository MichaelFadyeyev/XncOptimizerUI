using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>What one preview render shows of the selected part, besides its layout.</summary>
    /// <param name="Programs">The part's XNC programs; the first one's <c>turn</c> orients the texture mark.</param>
    /// <param name="ConsiderTexture">Whether the texture mark is drawn.</param>
    /// <param name="TurnText">The turn label, e.g. "90°"; empty draws none.</param>
    internal readonly record struct PartPreviewScene(IReadOnlyList<XncProgram> Programs, bool ConsiderTexture, string TurnText);

    /// <summary>
    /// Draws the part preview: the Face rectangle and its four side views, the machining over
    /// them, and the viewport decorations.
    /// </summary>
    internal static class PartPreviewRenderer
    {
        /// <summary>
        /// Draws the part onto <paramref name="canvas"/>: the four side views, the Face rectangle,
        /// then mills, bores and grooves (<see cref="MillPreviewRenderer.DrawMills"/> first, since
        /// their cut-off fills cover the face) and the texture mark. Every side-view and Face
        /// rectangle carries its side ("Face"/"Top"/"Bottom"/"Left"/"Right") as
        /// <see cref="FrameworkElement.Tag"/>, so a future click handler can tell which surface
        /// was hit.
        /// </summary>
        public static void DrawPart(Canvas canvas, PartPreviewLayout layout, PartPreviewScene scene, PreviewPalette palette)
        {
            foreach (var (side, rect) in layout.Bands)
            {
                canvas.Children.Add(SurfaceRectangle(side, rect, palette.Part.Edge, palette.Part));
            }

            canvas.Children.Add(SurfaceRectangle("Face", layout.FaceRect, palette.Part.Face, palette.Part));

            MillPreviewRenderer.DrawMills(canvas, scene.Programs, layout.Face, palette.Mills);
            BorePreviewRenderer.DrawBores(canvas, scene.Programs, layout.Face, palette.Bores);
            GroovePreviewRenderer.DrawGrooves(canvas, scene.Programs, layout.Face, palette.Bores);

            if (scene.ConsiderTexture)
            {
                var programTurn = scene.Programs.Count > 0 ? scene.Programs[0].Turn : 0;
                AddAll(canvas, PreviewDecorations.TextureIndicator(layout.FaceRect, programTurn, palette.Part));
            }
        }

        /// <summary>
        /// Draws the decorations pinned to the <paramref name="viewport"/> onto
        /// <paramref name="overlay"/>: the axis glyph top-left and the turn label bottom-right.
        /// </summary>
        public static void DrawOverlay(Canvas overlay, Size viewport, string turnText, PreviewPalette palette)
        {
            AddAll(overlay, PreviewDecorations.AxisGlyph(palette.Metrics.Margin, palette.Decorations));
            AddAll(overlay, PreviewDecorations.TurnLabel(turnText, viewport, palette.Decorations));
        }

        private static Rectangle SurfaceRectangle(string side, PxRect rect, Brush fill, PartBrushes brushes)
        {
            var shape = new Rectangle
            {
                Width = rect.Width,
                Height = rect.Height,
                Fill = fill,
                Stroke = brushes.Outline,
                StrokeThickness = brushes.OutlineThickness,
                Tag = side,
            };

            Canvas.SetLeft(shape, rect.Left);
            Canvas.SetTop(shape, rect.Top);

            return shape;
        }

        private static void AddAll(Canvas canvas, IEnumerable<UIElement> elements)
        {
            foreach (var element in elements)
            {
                canvas.Children.Add(element);
            }
        }
    }
}
