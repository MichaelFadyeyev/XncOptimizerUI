using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.Models.Xnc;
using static XncOptimizerUI.MVVM.ViewModels.Machining.ExpressionCellValue;

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

    /// <summary>
    /// One row of the selected part's milling contours table. Only the entry depth (<c>&lt;ms&gt;</c>
    /// <c>dp</c>) is editable: moving the entry or exit point would detach it from its arc segments.
    /// </summary>
    public sealed class MillingContourRowVM(
        int number, string side, XncMillingContour contour, XncProgram program,
        Func<MachiningElementRowVM<XncMillingContour>, MachiningAttribute, string, bool> onEdited)
        : MachiningElementRowVM<XncMillingContour>(number, side, contour, program, CellValues, onEdited)
    {
        #region Properties

        public string ToolName => Model.ToolName;

        public string EntryX => MachiningNumber.Format(Model.Entry.X);

        public string EntryY => MachiningNumber.Format(Model.Entry.Y);

        public string ExitX => MachiningNumber.Format(Exit.X);

        public string ExitY => MachiningNumber.Format(Exit.Y);

        public ExpressionCellVM Depth => CellOf(MachiningAttribute.Depth);

        public string ToolToCenterLine => MachiningDisplay.ToolPositionCode(Model.Position);

        public int Segments => Model.Segments.Count;

        /// <summary>Where the contour ends: its last segment's end, or the entry when it has no segment.</summary>
        private XncPoint Exit => Model.Segments.Count == 0 ? Model.Entry : Model.Segments[^1].End;

        #endregion

        #region Methods

        private static IEnumerable<ExpressionCellValue> CellValues(XncMillingContour contour) =>
            [Of(contour.Source, MachiningAttribute.Depth, contour.EntryDepth)];

        #endregion
    }

    /// <summary>One row of the selected part's milling ellipses table: center, semi-axes, angle and depth are editable cells.</summary>
    public sealed class MillingEllipseRowVM(
        int number, string side, XncMillingEllipse ellipse, XncProgram program,
        Func<MachiningElementRowVM<XncMillingEllipse>, MachiningAttribute, string, bool> onEdited)
        : MachiningElementRowVM<XncMillingEllipse>(number, side, ellipse, program, CellValues, onEdited)
    {
        #region Properties

        public string ToolName => Model.ToolName;

        public ExpressionCellVM CenterX => CellOf(MachiningAttribute.X);

        public ExpressionCellVM CenterY => CellOf(MachiningAttribute.Y);

        public ExpressionCellVM Length => CellOf(MachiningAttribute.Length);

        public ExpressionCellVM Width => CellOf(MachiningAttribute.Width);

        public ExpressionCellVM Angle => CellOf(MachiningAttribute.Angle);

        public ExpressionCellVM Depth => CellOf(MachiningAttribute.Depth);

        public string ToolToCenterLine => MachiningDisplay.ToolPositionCode(Model.Position);

        #endregion

        #region Methods

        private static IEnumerable<ExpressionCellValue> CellValues(XncMillingEllipse ellipse) =>
        [
            Of(ellipse.Source, MachiningAttribute.X, ellipse.Center.X),
            Of(ellipse.Source, MachiningAttribute.Y, ellipse.Center.Y),
            Of(ellipse.Source, MachiningAttribute.Length, ellipse.Length),
            Of(ellipse.Source, MachiningAttribute.Width, ellipse.Width),
            Of(ellipse.Source, MachiningAttribute.Angle, ellipse.Angle),
            Of(ellipse.Source, MachiningAttribute.Depth, ellipse.Depth)
        ];

        #endregion
    }

    /// <summary>One row of the selected part's milling rectangles table: center, size, angle, corner radius and depth are editable cells.</summary>
    public sealed class MillingRectangleRowVM(
        int number, string side, XncMillingRectangle rectangle, XncProgram program,
        Func<MachiningElementRowVM<XncMillingRectangle>, MachiningAttribute, string, bool> onEdited)
        : MachiningElementRowVM<XncMillingRectangle>(number, side, rectangle, program, CellValues, onEdited)
    {
        #region Properties

        public ExpressionCellVM CenterX => CellOf(MachiningAttribute.X);

        public ExpressionCellVM CenterY => CellOf(MachiningAttribute.Y);

        public ExpressionCellVM Length => CellOf(MachiningAttribute.Length);

        public ExpressionCellVM Width => CellOf(MachiningAttribute.Width);

        public ExpressionCellVM Angle => CellOf(MachiningAttribute.Angle);

        public ExpressionCellVM CornerRadius => CellOf(MachiningAttribute.CornerRadius);

        public ExpressionCellVM Depth => CellOf(MachiningAttribute.Depth);

        public string ToolToCenterLine => MachiningDisplay.ToolPositionCode(Model.Position);

        #endregion

        #region Methods

        private static IEnumerable<ExpressionCellValue> CellValues(XncMillingRectangle rectangle) =>
        [
            Of(rectangle.Source, MachiningAttribute.X, rectangle.Origin.X),
            Of(rectangle.Source, MachiningAttribute.Y, rectangle.Origin.Y),
            Of(rectangle.Source, MachiningAttribute.Length, rectangle.Length),
            Of(rectangle.Source, MachiningAttribute.Width, rectangle.Width),
            Of(rectangle.Source, MachiningAttribute.Angle, rectangle.Angle),
            Of(rectangle.Source, MachiningAttribute.CornerRadius, rectangle.CornerRadius),
            Of(rectangle.Source, MachiningAttribute.Depth, rectangle.Depth)
        ];

        #endregion
    }
}
