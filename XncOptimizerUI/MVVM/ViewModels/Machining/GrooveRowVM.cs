using XncOptimizerUI.MVVM.Models.Xnc;
using static XncOptimizerUI.MVVM.ViewModels.Machining.ExpressionCellValue;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>
    /// One row of the selected part's grooves table: start / end point (<c>x1</c>/<c>y1</c>/<c>x2</c>/<c>y2</c>),
    /// depth (<c>dp</c>) and width (<c>t</c>) are editable cells.
    /// </summary>
    public sealed class GrooveRowVM(
        int number, string side, XncGrooving groove, XncProgram program,
        Func<MachiningElementRowVM<XncGrooving>, MachiningAttribute, string, bool> onEdited)
        : MachiningElementRowVM<XncGrooving>(number, side, groove, program, CellValues, onEdited)
    {
        #region Properties

        public ExpressionCellVM StartX => CellOf(MachiningAttribute.X1);

        public ExpressionCellVM StartY => CellOf(MachiningAttribute.Y1);

        public ExpressionCellVM EndX => CellOf(MachiningAttribute.X2);

        public ExpressionCellVM EndY => CellOf(MachiningAttribute.Y2);

        public ExpressionCellVM Depth => CellOf(MachiningAttribute.Depth);

        public ExpressionCellVM Width => CellOf(MachiningAttribute.GrooveWidth);

        public string ToolToCenterLine => MachiningDisplay.ToolPositionCode(Model.Position);

        #endregion

        #region Methods

        private static IEnumerable<ExpressionCellValue> CellValues(XncGrooving groove) =>
        [
            Of(groove.Source, MachiningAttribute.X1, groove.Start.X),
            Of(groove.Source, MachiningAttribute.Y1, groove.Start.Y),
            Of(groove.Source, MachiningAttribute.X2, groove.End.X),
            Of(groove.Source, MachiningAttribute.Y2, groove.End.Y),
            Of(groove.Source, MachiningAttribute.Depth, groove.Depth),
            Of(groove.Source, MachiningAttribute.GrooveWidth, groove.Width)
        ];

        #endregion
    }
}
