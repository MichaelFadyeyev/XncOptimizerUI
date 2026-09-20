using System;
using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public sealed class ToolRowVM
    {
        public ToolRowVM(int number, string side, XncTool tool)
        {
            Number = number;
            Side = side;
            Tool = tool;
        }

        public int Number { get; }
        public string Side { get; }
        public XncTool Tool { get; }
        public string Name => Tool.Name;
        public string Diameter => Tool.Diameter.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }

    public sealed partial class MillingContourRowVM : ObservableObject
    {
        private readonly Action<MillingContourRowVM, bool> _onSelectionChanged;

        public MillingContourRowVM(
            int number,
            string side,
            XncMillingContour contour,
            Action<MillingContourRowVM, bool> onSelectionChanged)
        {
            Number = number;
            Side = side;
            Contour = contour;
            _onSelectionChanged = onSelectionChanged;
        }

        public int Number { get; }
        public string Side { get; }
        public XncMillingContour Contour { get; }
        public string ToolName => Contour.ToolName;
        public string EntryX => Format(Contour.Entry.X);
        public string EntryY => Format(Contour.Entry.Y);
        public string ExitX => Format(Contour.Segments.Count == 0
            ? Contour.Entry.X
            : Contour.Segments[^1].End.X);
        public string ExitY => Format(Contour.Segments.Count == 0
            ? Contour.Entry.Y
            : Contour.Segments[^1].End.Y);
        public string Depth => Format(Contour.EntryDepth);
        public string ToolToCenterLine => Contour.Position switch
        {
            ToolPosition.Center => "C",
            ToolPosition.Left => "L",
            ToolPosition.Right => "R",
            ToolPosition.Pocket => "P",
            _ => Contour.Position.ToString()
        };
        public int Segments => Contour.Segments.Count;

        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value) => _onSelectionChanged(this, value);

        private static string Format(double value) =>
            value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }

    public sealed partial class MillingEllipseRowVM : ObservableObject
    {
        private readonly Action<MillingEllipseRowVM, bool> _onSelectionChanged;

        public MillingEllipseRowVM(
            int number,
            string side,
            XncMillingEllipse ellipse,
            Action<MillingEllipseRowVM, bool> onSelectionChanged)
        {
            Number = number;
            Side = side;
            Ellipse = ellipse;
            _onSelectionChanged = onSelectionChanged;
        }

        public int Number { get; }
        public string Side { get; }
        public XncMillingEllipse Ellipse { get; }
        public string ToolName => Ellipse.ToolName;
        public string CenterX => Format(Ellipse.Center.X);
        public string CenterY => Format(Ellipse.Center.Y);
        public string Length => Format(Ellipse.Length);
        public string Width => Format(Ellipse.Width);
        public string Angle => Format(Ellipse.Angle);
        public string Depth => Format(Ellipse.Depth);
        public string ToolToCenterLine => FormatToolToCenterLine(Ellipse.Position);

        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value) => _onSelectionChanged(this, value);

        private static string Format(double value) =>
            value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);

        internal static string FormatToolToCenterLine(ToolPosition position) => position switch
        {
            ToolPosition.Center => "C",
            ToolPosition.Left => "L",
            ToolPosition.Right => "R",
            ToolPosition.Pocket => "P",
            _ => position.ToString()
        };
    }

    public sealed partial class MillingRectangleRowVM : ObservableObject
    {
        private readonly Action<MillingRectangleRowVM, bool> _onSelectionChanged;

        public MillingRectangleRowVM(
            int number,
            string side,
            XncMillingRectangle rectangle,
            Action<MillingRectangleRowVM, bool> onSelectionChanged)
        {
            Number = number;
            Side = side;
            Rectangle = rectangle;
            _onSelectionChanged = onSelectionChanged;
        }

        public int Number { get; }
        public string Side { get; }
        public XncMillingRectangle Rectangle { get; }
        public string CenterX => Format(Rectangle.Origin.X);
        public string CenterY => Format(Rectangle.Origin.Y);
        public string Length => Format(Rectangle.Length);
        public string Width => Format(Rectangle.Width);
        public string Angle => Format(Rectangle.Angle);
        public string CornerRadius => Format(Rectangle.CornerRadius);
        public string Depth => Format(Rectangle.Depth);
        public string ToolToCenterLine => MillingEllipseRowVM.FormatToolToCenterLine(Rectangle.Position);

        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value) => _onSelectionChanged(this, value);

        private static string Format(double value) =>
            value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }
}
