using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>One row of the selected part's tools table.</summary>
    public sealed class ToolRowVM(int number, string side, XncTool tool)
    {
        public int Number { get; } = number;

        public string Side { get; } = side;

        public XncTool Tool { get; } = tool;

        public string Name => Tool.Name;

        public string Diameter => MachiningNumber.Format(Tool.Diameter);
    }

    /// <summary>One row of the selected part's milling contours table.</summary>
    public sealed class MillingContourRowVM(int number, string side, XncMillingContour contour, Action<XncMillingContour, bool> onSelectionChanged)
        : MachiningRowVM<XncMillingContour>(number, side, contour, onSelectionChanged)
    {
        public string ToolName => Model.ToolName;

        public string EntryX => MachiningNumber.Format(Model.Entry.X);

        public string EntryY => MachiningNumber.Format(Model.Entry.Y);

        public string ExitX => MachiningNumber.Format(Exit.X);

        public string ExitY => MachiningNumber.Format(Exit.Y);

        public string Depth => MachiningNumber.Format(Model.EntryDepth);

        public string ToolToCenterLine => MachiningDisplay.ToolPositionCode(Model.Position);

        public int Segments => Model.Segments.Count;

        /// <summary>Where the contour ends: its last segment's end, or the entry when it has no segment.</summary>
        private XncPoint Exit => Model.Segments.Count == 0 ? Model.Entry : Model.Segments[^1].End;
    }

    /// <summary>One row of the selected part's milling ellipses table.</summary>
    public sealed class MillingEllipseRowVM(int number, string side, XncMillingEllipse ellipse, Action<XncMillingEllipse, bool> onSelectionChanged)
        : MachiningRowVM<XncMillingEllipse>(number, side, ellipse, onSelectionChanged)
    {
        public string ToolName => Model.ToolName;

        public string CenterX => MachiningNumber.Format(Model.Center.X);

        public string CenterY => MachiningNumber.Format(Model.Center.Y);

        public string Length => MachiningNumber.Format(Model.Length);

        public string Width => MachiningNumber.Format(Model.Width);

        public string Angle => MachiningNumber.Format(Model.Angle);

        public string Depth => MachiningNumber.Format(Model.Depth);

        public string ToolToCenterLine => MachiningDisplay.ToolPositionCode(Model.Position);
    }

    /// <summary>One row of the selected part's milling rectangles table.</summary>
    public sealed class MillingRectangleRowVM(int number, string side, XncMillingRectangle rectangle, Action<XncMillingRectangle, bool> onSelectionChanged)
        : MachiningRowVM<XncMillingRectangle>(number, side, rectangle, onSelectionChanged)
    {
        public string CenterX => MachiningNumber.Format(Model.Origin.X);

        public string CenterY => MachiningNumber.Format(Model.Origin.Y);

        public string Length => MachiningNumber.Format(Model.Length);

        public string Width => MachiningNumber.Format(Model.Width);

        public string Angle => MachiningNumber.Format(Model.Angle);

        public string CornerRadius => MachiningNumber.Format(Model.CornerRadius);

        public string Depth => MachiningNumber.Format(Model.Depth);

        public string ToolToCenterLine => MachiningDisplay.ToolPositionCode(Model.Position);
    }
}
