using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>
    /// One row of the selected part's bores table: X / Y / Depth are editable cells. X and Y show
    /// the bore's position in the part plane, so they edit <c>x</c>/<c>y</c> of a face bore, <c>x</c>/<c>z</c>
    /// of a Top/Bottom edge bore and <c>z</c>/<c>y</c> of a Left/Right one (<see cref="BoreAttributes"/>).
    /// </summary>
    public sealed class BoreRowVM(
        int number, string side, string? diameter, XncBore bore, XncProgram program,
        Func<MachiningElementRowVM<XncBore>, MachiningAttribute, string, bool> onEdited)
        : MachiningElementRowVM<XncBore>(number, side, bore, program, CellValues, onEdited)
    {
        #region Properties

        public string? Diameter { get; } = diameter;

        /// <summary>Attribute behind the X column (<c>z</c> for a Left/Right edge bore, else <c>x</c>).</summary>
        public MachiningAttribute HorizontalAttribute => BoreAttributes.HorizontalAttribute(Model.Surface);

        /// <summary>Attribute behind the Y column (<c>z</c> for a Top/Bottom edge bore, else <c>y</c>).</summary>
        public MachiningAttribute VerticalAttribute => BoreAttributes.VerticalAttribute(Model.Surface);

        public ExpressionCellVM X => CellOf(HorizontalAttribute);

        public ExpressionCellVM Y => CellOf(VerticalAttribute);

        public ExpressionCellVM Depth => CellOf(MachiningAttribute.Depth);

        #endregion

        #region Methods

        private static IEnumerable<ExpressionCellValue> CellValues(XncBore bore) =>
        [
            Cell(bore, BoreAttributes.HorizontalAttribute(bore.Surface)),
            Cell(bore, BoreAttributes.VerticalAttribute(bore.Surface)),
            Cell(bore, MachiningAttribute.Depth)
        ];

        private static ExpressionCellValue Cell(XncBore bore, MachiningAttribute attribute) => attribute switch
        {
            MachiningAttribute.X => new(attribute, bore.XText, bore.X),
            MachiningAttribute.Y => new(attribute, bore.YText, bore.Y),
            MachiningAttribute.Z => new(attribute, bore.ZText, bore.Z),
            _ => new(attribute, bore.DepthText, bore.Depth)
        };

        #endregion
    }
}
