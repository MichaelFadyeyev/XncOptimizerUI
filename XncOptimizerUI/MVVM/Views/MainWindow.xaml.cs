using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.Views.PartPreview;

namespace XncOptimizerUI.MVVM.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double MaxPartPreviewZoom = 8;
        private const double PartPreviewZoomStep = 1.1;

        private readonly AppViewModel _viewModel;
        private double _partPreviewZoom = 1;
        private bool _isHandlingPreviewSizeChanged;
        private bool _isPanningPreview;
        private Point _panStartPoint;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;

        public MainWindow(AppViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            PartPreviewScrollViewer.SizeChanged += PartPreviewScrollViewer_SizeChanged;
            PartCanvas.Loaded += (_, _) => RenderPart(_viewModel.SelectedPart);
            PartPreviewScrollViewer.PreviewMouseWheel += PartCanvas_MouseWheel;
            PartPreviewScrollViewer.PreviewMouseDown += PartPreviewScrollViewer_MouseDown;
            PartPreviewScrollViewer.PreviewMouseMove += PartPreviewScrollViewer_MouseMove;
            PartPreviewScrollViewer.PreviewMouseUp += PartPreviewScrollViewer_MouseUp;
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
                _partPreviewZoom = 1;
                RenderPart(_viewModel.SelectedPart);
                PartPreviewScrollViewer.ScrollToHome();
            }
            else if (e.PropertyName == nameof(AppViewModel.SelectedPartDisplayLength)
                || e.PropertyName == nameof(AppViewModel.SelectedPartTurn)
                || e.PropertyName == nameof(AppViewModel.SelectedXncPrograms))
            {
                RenderPart(_viewModel.SelectedPart);
            }
        }

        private void PartPreviewScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isHandlingPreviewSizeChanged)
            {
                return;
            }

            var centerX = GetPreviewCenter(PartPreviewScrollViewer.HorizontalOffset,
                PartPreviewScrollViewer.ViewportWidth,
                PartPreviewScrollViewer.ExtentWidth);
            var centerY = GetPreviewCenter(PartPreviewScrollViewer.VerticalOffset,
                PartPreviewScrollViewer.ViewportHeight,
                PartPreviewScrollViewer.ExtentHeight);

            _isHandlingPreviewSizeChanged = true;
            try
            {
                RenderPart(_viewModel.SelectedPart);
                UpdateLayout();
                ScrollPreviewToCenter(centerX, centerY);
            }
            finally
            {
                _isHandlingPreviewSizeChanged = false;
            }
        }

        private static double GetPreviewCenter(double offset, double viewport, double extent)
        {
            return extent > 0
                ? Math.Clamp((offset + (viewport / 2)) / extent, 0, 1)
                : 0.5;
        }

        private void ScrollPreviewToCenter(double centerX, double centerY)
        {
            var horizontalOffset = (centerX * PartPreviewScrollViewer.ExtentWidth)
                - (PartPreviewScrollViewer.ViewportWidth / 2);
            var verticalOffset = (centerY * PartPreviewScrollViewer.ExtentHeight)
                - (PartPreviewScrollViewer.ViewportHeight / 2);

            PartPreviewScrollViewer.ScrollToHorizontalOffset(horizontalOffset);
            PartPreviewScrollViewer.ScrollToVerticalOffset(verticalOffset);
        }

        private void PartCanvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Delta == 0)
            {
                return;
            }

            var oldCanvasWidth = PartCanvas.ActualWidth;
            var oldCanvasHeight = PartCanvas.ActualHeight;
            var mousePosition = e.GetPosition(PartPreviewScrollViewer);
            var contentPositionX = PartPreviewScrollViewer.HorizontalOffset + mousePosition.X;
            var contentPositionY = PartPreviewScrollViewer.VerticalOffset + mousePosition.Y;
            var relativeX = oldCanvasWidth > 0 ? contentPositionX / oldCanvasWidth : 0.5;
            var relativeY = oldCanvasHeight > 0 ? contentPositionY / oldCanvasHeight : 0.5;

            var oldZoom = _partPreviewZoom;
            var newZoom = oldZoom * (e.Delta > 0 ? PartPreviewZoomStep : 1 / PartPreviewZoomStep);
            _partPreviewZoom = Math.Clamp(newZoom, 1, MaxPartPreviewZoom);
            if (Math.Abs(_partPreviewZoom - oldZoom) > double.Epsilon)
            {
                RenderPart(_viewModel.SelectedPart);
                UpdateLayout();

                var newOffsetX = relativeX * PartCanvas.ActualWidth - mousePosition.X;
                var newOffsetY = relativeY * PartCanvas.ActualHeight - mousePosition.Y;
                PartPreviewScrollViewer.ScrollToHorizontalOffset(newOffsetX);
                PartPreviewScrollViewer.ScrollToVerticalOffset(newOffsetY);
            }

            e.Handled = true;
        }

        private void PartPreviewScrollViewer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle || _partPreviewZoom <= 1)
            {
                return;
            }

            _isPanningPreview = true;
            _panStartPoint = e.GetPosition(PartPreviewScrollViewer);
            _panStartHorizontalOffset = PartPreviewScrollViewer.HorizontalOffset;
            _panStartVerticalOffset = PartPreviewScrollViewer.VerticalOffset;
            PartPreviewScrollViewer.Cursor = Cursors.SizeAll;
            PartPreviewScrollViewer.CaptureMouse();
            e.Handled = true;
        }

        private void PartPreviewScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanningPreview)
            {
                return;
            }

            var currentPoint = e.GetPosition(PartPreviewScrollViewer);
            PartPreviewScrollViewer.ScrollToHorizontalOffset(
                _panStartHorizontalOffset + _panStartPoint.X - currentPoint.X);
            PartPreviewScrollViewer.ScrollToVerticalOffset(
                _panStartVerticalOffset + _panStartPoint.Y - currentPoint.Y);
            e.Handled = true;
        }

        private void PartPreviewScrollViewer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanningPreview || e.ChangedButton != MouseButton.Middle)
            {
                return;
            }

            _isPanningPreview = false;
            PartPreviewScrollViewer.ReleaseMouseCapture();
            PartPreviewScrollViewer.Cursor = Cursors.Arrow;
            e.Handled = true;
        }

        /// <summary>
        /// Draws the selected part in <see cref="PartCanvas"/> as a plain rectangle
        /// (length x width) plus the four side views of the part. Each side view is a
        /// band whose thickness is the owning sheet's <c>t</c> value and whose length
        /// is the part's size in that direction, all drawn at the same uniform scale
        /// so the drawing fits the canvas and rescales per part. Bores and groovings are
        /// overlaid on top by <see cref="BorePreviewRenderer.DrawBores"/> and
        /// <see cref="GroovePreviewRenderer.DrawGrooves"/>; milling contours/rectangles and
        /// pockets are still not rendered. Every shape carries a
        /// <see cref="FrameworkElement.Tag"/> ("Face"/"Top"/"Bottom"/"Left"/"Right")
        /// so a future click handler can tell which surface was hit.
        /// </summary>
        private void RenderPart(PartVM? part)
        {
            PartCanvas.Children.Clear();
            PreviewOverlayCanvas.Children.Clear();

            var canvasWidth = PartPreviewScrollViewer.ViewportWidth;
            var canvasHeight = PartPreviewScrollViewer.ViewportHeight;

            // Display size follows the first applied XNC program's dx/dy (already in the turned
            // machine frame); with no program it falls back to the part's own length/width.
            var partLength = _viewModel.SelectedPartDisplayLength; // X, horizontal
            var partWidth = _viewModel.SelectedPartDisplayWidth;   // Y, vertical (points down)

            if (part is null
                || partLength <= 0
                || partWidth <= 0
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
            var zoomedScale = scale * _partPreviewZoom;
            var contentWidth = (partLength + (2 * perSideMm)) * zoomedScale + (2 * margin);
            var contentHeight = (partWidth + (2 * perSideMm)) * zoomedScale + (2 * margin);

            PartCanvas.Width = Math.Max(canvasWidth, contentWidth);
            PartCanvas.Height = Math.Max(canvasHeight, contentHeight);
            canvasWidth = PartCanvas.Width;
            canvasHeight = PartCanvas.Height;
            scale = zoomedScale;

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

            var boreLayout = new BorePreviewRenderer.FaceLayout(
                originX, originY, faceWidth, faceHeight, scale, gap, bandThickness);
            var boreBrushes = new BorePreviewRenderer.BoreBrushes(
                (Brush)FindResource("BoreSideTrueBrush"),
                (Brush)FindResource("BoreSideFalseBrush"),
                (Brush)FindResource("BoreSelectedBrush"),
                (Brush)FindResource("PartPreviewBackgroundBrush"),
                (double)FindResource("LineThickness2Px"),
                (double)FindResource("LineThickness1Px"),
                (double)FindResource("BoreCenterLineOvershootMm"),
                (double)FindResource("DashLengthPx"),
                (double)FindResource("CenterLineDashMm"));
            BorePreviewRenderer.DrawBores(PartCanvas, _viewModel.SelectedXncPrograms, boreLayout, boreBrushes);
            GroovePreviewRenderer.DrawGrooves(PartCanvas, _viewModel.SelectedXncPrograms, boreLayout, boreBrushes);

            DrawAxisGlyph(margin);
            DrawTurnLabel(_viewModel.SelectedPartTurnText);
        }

        /// <summary>
        /// Writes the selected part's XNC turn as degrees (e.g. "90°") in the bottom-right
        /// corner of the preview viewport. Empty text (part has no XNC program) draws nothing.
        /// </summary>
        private void DrawTurnLabel(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var brush = (Brush)FindResource("TurnLabelBrush");
            var fontSize = (double)FindResource("TurnLabelFontSize");

            var label = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var scrollbarSize = Math.Max(
                SystemParameters.VerticalScrollBarWidth,
                SystemParameters.HorizontalScrollBarHeight);
            var pad = scrollbarSize * 1.5;
            Canvas.SetLeft(label, PreviewOverlayCanvas.ActualWidth - label.DesiredSize.Width - pad);
            Canvas.SetTop(label, PreviewOverlayCanvas.ActualHeight - label.DesiredSize.Height - pad);
            PreviewOverlayCanvas.Children.Add(label);
        }

        /// <summary>
        /// Small X (right, red) / Y (down, green) axis marker in the top-left corner,
        /// mirroring the reference image. Deliberately NOT rotated by the part's XNC
        /// <c>turn</c>: the on-screen axes stay X-horizontal / Y-vertical regardless of turn,
        /// which is only reflected in the display dimensions and the bottom-right turn label.
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
                PreviewOverlayCanvas.Children.Add(new Line
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
            PreviewOverlayCanvas.Children.Add(origin);

            // X arrow (right)
            AddLine(ox, oy, ox + armLength, oy, axisXBrush);
            AddLine(ox + armLength, oy, ox + armLength - head, oy - head, axisXBrush);
            AddLine(ox + armLength, oy, ox + armLength - head, oy + head, axisXBrush);

            // Y arrow (down)
            AddLine(ox, oy, ox, oy + armLength, axisYBrush);
            AddLine(ox, oy + armLength, ox - head, oy + armLength - head, axisYBrush);
            AddLine(ox, oy + armLength, ox + head, oy + armLength - head, axisYBrush);

            PreviewOverlayCanvas.Children.Add(new TextBlock
            {
                Text = "X",
                Foreground = axisXBrush,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                RenderTransform = new TranslateTransform(ox + armLength + 2, oy - 8),
            });
            PreviewOverlayCanvas.Children.Add(new TextBlock
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
