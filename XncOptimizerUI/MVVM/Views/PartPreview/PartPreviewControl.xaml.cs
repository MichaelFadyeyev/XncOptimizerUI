using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XncOptimizerUI.MVVM.ViewModels.Parts;
using XncOptimizerUI.MVVM.ViewModels.Preview;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws the part a <see cref="PartPreviewVM"/> (the <see cref="FrameworkElement.DataContext"/>)
    /// shows: the part fitted into the viewport, zoomable with the mouse wheel from the fit-to-view
    /// scale up (keeping the point under the cursor), pannable with the pressed wheel while
    /// magnified, and refitted on resize keeping the relative scroll position. A new part or turn
    /// resets to fit-to-view. The axis glyph and turn label stay pinned to the viewport.
    /// </summary>
    public partial class PartPreviewControl : UserControl
    {
        private const double MaxZoom = 8;
        private const double ZoomStep = 1.1;

        private PartPreviewVM? _viewModel;
        private PartVM? _part;
        private PreviewPalette? _palette;
        private double _zoom = 1;
        private bool _isHandlingSizeChanged;
        private bool _isPanning;
        private Point _panStartPoint;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;

        public PartPreviewControl()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
            PartCanvas.Loaded += (_, _) => Render();
            PartPreviewScrollViewer.SizeChanged += OnViewportSizeChanged;
            PartPreviewScrollViewer.PreviewMouseWheel += OnMouseWheel;
            PartPreviewScrollViewer.PreviewMouseDown += OnMouseDown;
            PartPreviewScrollViewer.PreviewMouseMove += OnMouseMove;
            PartPreviewScrollViewer.PreviewMouseUp += OnMouseUp;
        }

        #region Properties
        private PreviewPalette Palette => _palette ??= PreviewPalette.Resolve(this);
        #endregion

        #region Methods
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = e.NewValue as PartPreviewVM;

            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            WatchPart(_viewModel?.Part);
            ResetToFit();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                // A new part or a new turn changes the drawing's aspect: back to fit-to-view.
                case nameof(PartPreviewVM.Part):
                    WatchPart(_viewModel?.Part);
                    ResetToFit();
                    break;
                case nameof(PartPreviewVM.Turn):
                    ResetToFit();
                    break;
                case nameof(PartPreviewVM.DisplayLength):
                case nameof(PartPreviewVM.DisplayWidth):
                case nameof(PartPreviewVM.Programs):
                case nameof(PartPreviewVM.SheetThickness):
                    Render();
                    break;
            }
        }

        /// <summary>Follows the shown part's texture flag, which the Parts grid edits in place.</summary>
        private void WatchPart(PartVM? part)
        {
            if (ReferenceEquals(_part, part))
            {
                return;
            }

            if (_part is not null)
            {
                _part.PropertyChanged -= OnPartPropertyChanged;
            }

            _part = part;

            if (_part is not null)
            {
                _part.PropertyChanged += OnPartPropertyChanged;
            }
        }

        private void OnPartPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PartVM.ConsiderTexture))
            {
                Render();
            }
        }

        private void ResetToFit()
        {
            _zoom = 1;
            Render();
            PartPreviewScrollViewer.ScrollToHome();
        }

        /// <summary>
        /// Redraws the part at the current zoom into the current viewport; draws nothing without
        /// a part or room to draw it in.
        /// </summary>
        private void Render()
        {
            PartCanvas.Children.Clear();
            PreviewOverlayCanvas.Children.Clear();

            if (_viewModel is not { Part: { } part } viewModel)
            {
                return;
            }

            var layout = PartPreviewLayout.Fit(
                PartPreviewScrollViewer.ViewportWidth,
                PartPreviewScrollViewer.ViewportHeight,
                viewModel.DisplayLength,
                viewModel.DisplayWidth,
                viewModel.SheetThickness,
                _zoom,
                Palette.Metrics);

            if (layout is null)
            {
                return;
            }

            PartCanvas.Width = layout.CanvasWidth;
            PartCanvas.Height = layout.CanvasHeight;

            var scene = new PartPreviewScene(viewModel.Programs, part.ConsiderTexture, viewModel.TurnText);
            PartPreviewRenderer.DrawPart(PartCanvas, layout, scene, Palette);
            PartPreviewRenderer.DrawOverlay(
                PreviewOverlayCanvas,
                new Size(PreviewOverlayCanvas.ActualWidth, PreviewOverlayCanvas.ActualHeight),
                scene.TurnText,
                Palette);
        }

        /// <summary>Refits the drawing to the resized viewport, keeping the same part point in its centre.</summary>
        private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isHandlingSizeChanged)
            {
                return;
            }

            var viewer = PartPreviewScrollViewer;
            var centerX = RelativeCenter(viewer.HorizontalOffset, viewer.ViewportWidth, viewer.ExtentWidth);
            var centerY = RelativeCenter(viewer.VerticalOffset, viewer.ViewportHeight, viewer.ExtentHeight);

            _isHandlingSizeChanged = true;
            try
            {
                Render();
                UpdateLayout();
                viewer.ScrollToHorizontalOffset((centerX * viewer.ExtentWidth) - (viewer.ViewportWidth / 2));
                viewer.ScrollToVerticalOffset((centerY * viewer.ExtentHeight) - (viewer.ViewportHeight / 2));
            }
            finally
            {
                _isHandlingSizeChanged = false;
            }
        }

        /// <summary>Where the viewport's centre is along one axis, as a fraction of the content (0.5 without content).</summary>
        private static double RelativeCenter(double offset, double viewport, double extent) =>
            extent > 0
                ? Math.Clamp((offset + (viewport / 2)) / extent, 0, 1)
                : 0.5;

        /// <summary>Zooms in or out one step, keeping the content point under the cursor in place.</summary>
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0)
            {
                return;
            }

            var viewer = PartPreviewScrollViewer;
            var oldCanvasWidth = PartCanvas.ActualWidth;
            var oldCanvasHeight = PartCanvas.ActualHeight;
            var mousePosition = e.GetPosition(viewer);
            var contentPositionX = viewer.HorizontalOffset + mousePosition.X;
            var contentPositionY = viewer.VerticalOffset + mousePosition.Y;
            var relativeX = oldCanvasWidth > 0 ? contentPositionX / oldCanvasWidth : 0.5;
            var relativeY = oldCanvasHeight > 0 ? contentPositionY / oldCanvasHeight : 0.5;

            var oldZoom = _zoom;
            var newZoom = oldZoom * (e.Delta > 0 ? ZoomStep : 1 / ZoomStep);
            _zoom = Math.Clamp(newZoom, 1, MaxZoom);

            if (Math.Abs(_zoom - oldZoom) > double.Epsilon)
            {
                Render();
                UpdateLayout();

                viewer.ScrollToHorizontalOffset((relativeX * PartCanvas.ActualWidth) - mousePosition.X);
                viewer.ScrollToVerticalOffset((relativeY * PartCanvas.ActualHeight) - mousePosition.Y);
            }

            e.Handled = true;
        }

        /// <summary>Starts panning a magnified drawing on a pressed mouse wheel.</summary>
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle || _zoom <= 1)
            {
                return;
            }

            var viewer = PartPreviewScrollViewer;
            _isPanning = true;
            _panStartPoint = e.GetPosition(viewer);
            _panStartHorizontalOffset = viewer.HorizontalOffset;
            _panStartVerticalOffset = viewer.VerticalOffset;
            viewer.Cursor = Cursors.SizeAll;
            viewer.CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning)
            {
                return;
            }

            var viewer = PartPreviewScrollViewer;
            var currentPoint = e.GetPosition(viewer);
            viewer.ScrollToHorizontalOffset(_panStartHorizontalOffset + _panStartPoint.X - currentPoint.X);
            viewer.ScrollToVerticalOffset(_panStartVerticalOffset + _panStartPoint.Y - currentPoint.Y);
            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning || e.ChangedButton != MouseButton.Middle)
            {
                return;
            }

            var viewer = PartPreviewScrollViewer;
            _isPanning = false;
            viewer.ReleaseMouseCapture();
            viewer.Cursor = Cursors.Arrow;
            e.Handled = true;
        }
        #endregion
    }
}
