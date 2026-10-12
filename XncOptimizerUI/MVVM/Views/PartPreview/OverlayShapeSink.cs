using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Collects the shapes of one machining element (a bore, groove or mill) into the given
    /// layers: each shape is tagged with the element, stroked in the element's normal brush and
    /// remembered, so that <see cref="AttachClickToggle"/> can make a click on any of its click
    /// targets switch all of its strokes to the selection brush and back.
    /// </summary>
    internal sealed class OverlayShapeSink(object tag, Brush normal)
    {
        private readonly List<Shape> _clickTargets = [];
        private readonly List<(Shape Shape, Brush Normal)> _stroked = [];

        /// <summary>
        /// Adds <paramref name="shape"/> to <paramref name="layer"/>, tagged with the element.
        /// A given <paramref name="stroke"/> is its normal brush, swapped for the selection brush
        /// while selected; without one the shape keeps its own stroke.
        /// </summary>
        public T Add<T>(ICollection<UIElement> layer, T shape, Brush? stroke, bool isClickTarget)
            where T : Shape
        {
            shape.Tag = tag;
            layer.Add(shape);

            if (stroke is not null)
            {
                shape.Stroke = stroke;
                _stroked.Add((shape, stroke));
            }

            if (isClickTarget)
            {
                _clickTargets.Add(shape);
            }

            return shape;
        }

        /// <summary>Adds <paramref name="shape"/> outlined in the normal brush, with a transparent (but clickable) inside.</summary>
        public T AddOutline<T>(ICollection<UIElement> layer, T shape, LineStyle style, bool isClickTarget = true)
            where T : Shape
        {
            shape.Fill = Brushes.Transparent;
            SetStroke(shape, style);

            return Add(layer, shape, normal, isClickTarget);
        }

        public Rectangle AddRectangle(ICollection<UIElement> layer, PxRect rect, LineStyle style)
        {
            var shape = new Rectangle { Width = rect.Width, Height = rect.Height };
            Canvas.SetLeft(shape, rect.Left);
            Canvas.SetTop(shape, rect.Top);

            return AddOutline(layer, shape, style);
        }

        public Polygon AddPolygon(ICollection<UIElement> layer, IEnumerable<Vec2> corners, LineStyle style) =>
            AddOutline(layer, new Polygon { Points = [.. corners.Select(c => new Point(c.X, c.Y))] }, style);

        public Ellipse AddCircle(ICollection<UIElement> layer, Vec2 centre, double diameterPx, LineStyle style)
        {
            var radiusPx = diameterPx / 2d;
            var shape = new Ellipse { Width = diameterPx, Height = diameterPx };
            Canvas.SetLeft(shape, centre.X - radiusPx);
            Canvas.SetTop(shape, centre.Y - radiusPx);

            return AddOutline(layer, shape, style);
        }

        /// <summary>Adds a line of the element, stroked in the normal brush; not a click target.</summary>
        public Line AddLine(ICollection<UIElement> layer, PxLine line, LineStyle style)
        {
            var shape = Add(layer, ToLine(line), normal, isClickTarget: false);
            SetStroke(shape, style);

            return shape;
        }

        /// <summary>
        /// Adds a reference center line: stroked in the normal brush and selected with the
        /// element, but untagged and not a click target.
        /// </summary>
        public Line AddCentreLine(ICollection<UIElement> layer, PxLine line, LineStyle style)
        {
            var shape = ToLine(line);
            shape.Stroke = normal;
            SetStroke(shape, style);
            layer.Add(shape);
            _stroked.Add((shape, normal));

            return shape;
        }

        /// <summary>Adds the crossed center lines of a circular symbol (see <see cref="PartPreviewOverlayGeometry.CrossArms"/>).</summary>
        public void AddCrossArms(ICollection<UIElement> layer, Vec2 centre, double armPx, LineStyle style)
        {
            foreach (var arm in PartPreviewOverlayGeometry.CrossArms(centre, armPx))
            {
                AddCentreLine(layer, arm, style);
            }
        }

        /// <summary>
        /// Makes every click target toggle the element's selection: each click switches all of its
        /// stroked shapes to <paramref name="selected"/> or back to each shape's own normal brush.
        /// Selection is local to the current render pass.
        /// </summary>
        public void AttachClickToggle(Brush selected)
        {
            var isSelected = false;
            var stroked = _stroked.ToArray();

            void OnClick(object sender, MouseButtonEventArgs e)
            {
                isSelected = !isSelected;

                foreach (var (shape, normalStroke) in stroked)
                {
                    shape.Stroke = isSelected ? selected : normalStroke;
                }

                e.Handled = true;
            }

            foreach (var shape in _clickTargets)
            {
                shape.MouseLeftButtonDown += OnClick;
            }
        }

        /// <summary>
        /// Sets <paramref name="shape"/>'s stroke thickness and dash pattern. WPF expresses
        /// <c>StrokeDashArray</c> as multiples of <c>StrokeThickness</c>, so the dash length is
        /// divided by the thickness to keep styles in plain px.
        /// </summary>
        public static void SetStroke(Shape shape, LineStyle style)
        {
            shape.StrokeThickness = style.Thickness;
            shape.StrokeDashArray = style.DashPx is double dash
                ? new DoubleCollection { dash / style.Thickness, dash / style.Thickness }
                : null;
        }

        private static Line ToLine(PxLine line) => new() { X1 = line.X1, Y1 = line.Y1, X2 = line.X2, Y2 = line.Y2 };
    }
}
