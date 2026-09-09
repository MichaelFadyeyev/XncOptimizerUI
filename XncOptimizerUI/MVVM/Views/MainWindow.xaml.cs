using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using XncOptimizerUI.MVVM.ViewModels;

namespace XncOptimizerUI.MVVM.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly AppViewModel _viewModel;

        public MainWindow(AppViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            PartCanvas.SizeChanged += (_, _) => RenderPart(_viewModel.SelectedPart);
            PartCanvas.Loaded += (_, _) => RenderPart(_viewModel.SelectedPart);
        }

        /// <summary>
        /// The range filter boxes rewrite their own text on the fly (","->".",
        /// trailing-separator trim, ".5"->"0.5"). When that happens while the box is focused
        /// the TwoWay binding resets the caret to the start; put it back at the end so the
        /// next keystroke appends. Only acts when the caret was actually reset to 0, so
        /// ordinary left-to-right typing and mid-text edits are untouched.
        /// </summary>
        private void RangeInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb)
            {
                return;
            }

            tb.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (tb.IsKeyboardFocused && tb.CaretIndex == 0 && tb.Text.Length > 0)
                {
                    tb.CaretIndex = tb.Text.Length;
                }
            }), DispatcherPriority.Background);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppViewModel.SelectedPart))
            {
                RenderPart(_viewModel.SelectedPart);
            }
        }

        /// <summary>
        /// Draws the selected part in <see cref="PartCanvas"/> as a plain rectangle
        /// (length x width) plus the four side views of the part. Each side view is a
        /// band whose thickness is the owning sheet's <c>t</c> value and whose length
        /// is the part's size in that direction, all drawn at the same uniform scale
        /// so the drawing fits the canvas and rescales per part. First iteration only:
        /// no bores, mills, groovings or pockets. Every shape carries a
        /// <see cref="FrameworkElement.Tag"/> ("Face"/"Top"/"Bottom"/"Left"/"Right")
        /// so a future click handler can tell which surface was hit.
        /// </summary>
        private void RenderPart(PartVM? part)
        {
            PartCanvas.Children.Clear();

            var canvasWidth = PartCanvas.ActualWidth;
            var canvasHeight = PartCanvas.ActualHeight;

            if (part is null
                || part.Length <= 0m
                || part.Width <= 0m
                || canvasWidth <= 0
                || canvasHeight <= 0)
            {
                return;
            }

            var faceBrush = (Brush)FindResource("PartFaceBrush");
            var edgeBrush = (Brush)FindResource("PartEdgeProjectionBrush");
            var outlineBrush = (Brush)FindResource("PartOutlineBrush");
            var outlineThickness = (double)FindResource("PartOutlineThickness");
            var minEdgeThickness = (double)FindResource("MinEdgeProjectionThickness");
            var gapMm = (double)FindResource("ProjectionGapMm");
            var margin = (double)FindResource("PartPreviewMargin");

            var partLength = (double)part.Length; // X, horizontal
            var partWidth = (double)part.Width;   // Y, vertical (points down)

            // Sheet thickness (mm) is the depth of every side view. Missing / zero
            // sheet -> the bands collapse to the min visible thickness below.
            var sheetThickness = (double)(_viewModel.Sheets
                .FirstOrDefault(s => s.Id == part.SheetId)?.Thickness ?? 0m);
            sheetThickness = Math.Max(sheetThickness, 0);

            var availableWidth = canvasWidth - (2 * margin);
            var availableHeight = canvasHeight - (2 * margin);

            if (availableWidth <= 0 || availableHeight <= 0)
            {
                return;
            }

            // Fit the face plus a 10 mm gap and one sheet-thickness band on each side,
            // everything in the same units. Uniform scale, recomputed per part -> every
            // part fills the viewport the same way, gap included.
            var perSideMm = gapMm + sheetThickness;
            var scale = Math.Min(
                availableWidth / (partLength + (2 * perSideMm)),
                availableHeight / (partWidth + (2 * perSideMm)));

            if (scale <= 0)
            {
                return;
            }

            var faceWidth = partLength * scale;
            var faceHeight = partWidth * scale;
            var gap = gapMm * scale;
            var bandThickness = Math.Max(sheetThickness * scale, minEdgeThickness);

            var slot = gap + bandThickness;
            var groupWidth = faceWidth + (2 * slot);
            var groupHeight = faceHeight + (2 * slot);

            // Top-left corner of the face rectangle, group centred in the canvas.
            var originX = ((canvasWidth - groupWidth) / 2) + slot;
            var originY = ((canvasHeight - groupHeight) / 2) + slot;

            Rectangle MakeRect(string tag, double x, double y, double w, double h, Brush fill)
            {
                var rect = new Rectangle
                {
                    Width = w,
                    Height = h,
                    Fill = fill,
                    Stroke = outlineBrush,
                    StrokeThickness = outlineThickness,
                    Tag = tag,
                };

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                PartCanvas.Children.Add(rect);

                return rect;
            }

            // Four side views: top/bottom are as long as the part's length, left/right
            // as long as its width; band thickness = scaled sheet t.
            MakeRect("Top", originX, originY - gap - bandThickness, faceWidth, bandThickness, edgeBrush);
            MakeRect("Bottom", originX, originY + faceHeight + gap, faceWidth, bandThickness, edgeBrush);
            MakeRect("Left", originX - gap - bandThickness, originY, bandThickness, faceHeight, edgeBrush);
            MakeRect("Right", originX + faceWidth + gap, originY, bandThickness, faceHeight, edgeBrush);

            MakeRect("Face", originX, originY, faceWidth, faceHeight, faceBrush);

            DrawAxisGlyph(margin);
        }

        /// <summary>
        /// Small X (right, red) / Y (down, green) axis marker in the top-left corner,
        /// mirroring the reference image. Purely decorative for now.
        /// </summary>
        private void DrawAxisGlyph(double margin)
        {
            var axisXBrush = (Brush)FindResource("AxisXBrush");
            var axisYBrush = (Brush)FindResource("AxisYBrush");
            var axisThickness = (double)FindResource("AxisThickness");

            var ox = margin * 0.5;
            var oy = margin * 0.5;
            const double armLength = 22;
            const double head = 5;

            void AddLine(double x1, double y1, double x2, double y2, Brush brush) =>
                PartCanvas.Children.Add(new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = brush,
                    StrokeThickness = axisThickness,
                });

            var origin = new Ellipse
            {
                Width = 6,
                Height = 6,
                Stroke = axisXBrush,
                StrokeThickness = axisThickness,
                Fill = Brushes.Transparent,
            };
            Canvas.SetLeft(origin, ox - 3);
            Canvas.SetTop(origin, oy - 3);
            PartCanvas.Children.Add(origin);

            // X arrow (right)
            AddLine(ox, oy, ox + armLength, oy, axisXBrush);
            AddLine(ox + armLength, oy, ox + armLength - head, oy - head, axisXBrush);
            AddLine(ox + armLength, oy, ox + armLength - head, oy + head, axisXBrush);

            // Y arrow (down)
            AddLine(ox, oy, ox, oy + armLength, axisYBrush);
            AddLine(ox, oy + armLength, ox - head, oy + armLength - head, axisYBrush);
            AddLine(ox, oy + armLength, ox + head, oy + armLength - head, axisYBrush);

            PartCanvas.Children.Add(new TextBlock
            {
                Text = "X",
                Foreground = axisXBrush,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                RenderTransform = new TranslateTransform(ox + armLength + 2, oy - 8),
            });
            PartCanvas.Children.Add(new TextBlock
            {
                Text = "Y",
                Foreground = axisYBrush,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                RenderTransform = new TranslateTransform(ox - 10, oy + armLength - 2),
            });
        }
    }
}
