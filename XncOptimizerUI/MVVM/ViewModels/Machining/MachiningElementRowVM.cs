using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>One editable cell of a row as read from its element: attribute, authored text and evaluated value.</summary>
    public readonly record struct ExpressionCellValue(MachiningAttribute Attribute, string? AuthoredText, double Value)
    {
        /// <summary>The cell of <paramref name="attribute"/>, its authored text taken from the element's <paramref name="source"/>.</summary>
        public static ExpressionCellValue Of(XncElementSource source, MachiningAttribute attribute, double value) =>
            new(attribute, source.Text(attribute.XmlName()), value);
    }

    /// <summary>
    /// A row of a program table (bores, grooves, mills): its number, the side it is machined on,
    /// the element it shows, whether it is selected (shared with the part preview) and its
    /// editable numeric <see cref="Cells"/>. Cells are committed one at a time
    /// (<see cref="ICellEditableRow"/>): a commit hands every changed, valid draft to the owner,
    /// which writes it to the program, and is refused while any cell is invalid.
    /// </summary>
    public abstract partial class MachiningElementRowVM<TModel> : ObservableObject, ICellEditableRow, ISelectableRow
        where TModel : IXncMachiningElement
    {
        private readonly Func<TModel, IEnumerable<ExpressionCellValue>> _cellValues;
        private readonly Func<MachiningElementRowVM<TModel>, MachiningAttribute, string, bool> _onEdited;

        /// <param name="cellValues">The editable cells of an element - one per attribute, the same attributes for every re-read of it.</param>
        protected MachiningElementRowVM(
            int number,
            string side,
            TModel model,
            XncProgram program,
            Func<TModel, IEnumerable<ExpressionCellValue>> cellValues,
            Func<MachiningElementRowVM<TModel>, MachiningAttribute, string, bool> onEdited)
        {
            Number = number;
            Side = side;
            OperationId = program.OperationId;
            _model = model;
            _cellValues = cellValues;
            _onEdited = onEdited;

            var symbols = XncSymbols.ElementSymbols(program, model);
            Cells = cellValues(model)
                .Select(cell => new ExpressionCellVM(cell.Attribute, cell.AuthoredText, cell.Value, symbols))
                .ToList();
        }

        #region Properties

        public int Number { get; }

        public string Side { get; }

        /// <summary>Id of the XNC operation whose program holds the element.</summary>
        public int OperationId { get; private set; }

        /// <summary>The editable cells, one per attribute.</summary>
        public IReadOnlyList<ExpressionCellVM> Cells { get; }

        /// <summary>Whether any cell holds an invalid draft.</summary>
        public bool HasErrors => Cells.Any(cell => cell.HasErrors);

        public object Element => Model;

        #endregion

        #region ObservableProperties

        /// <summary>The element as last read from the program; replaced by <see cref="Apply"/> after a save.</summary>
        [ObservableProperty]
        private TModel _model;

        /// <summary>Whether the row is selected in its table, which draws its element red in the preview.</summary>
        [ObservableProperty]
        private bool _isSelected;

        #endregion

        #region Methods

        public void BeginCellEdit() => ResetCells();

        public bool TryCommitCellEdit() =>
            !HasErrors
            && Cells
                .Where(cell => cell.IsChanged)
                .Select(cell => (Cell: cell, Text: cell.Draft))
                .ToList()
                .All(edit => Save(edit.Cell, edit.Text));

        public void CancelCellEdit() => ResetCells();

        /// <summary>Refreshes the row in place from the re-read program after a successful save.</summary>
        public void Apply(TModel model, XncProgram program)
        {
            var symbols = XncSymbols.ElementSymbols(program, model);

            OperationId = program.OperationId;
            Model = model;
            _cellValues(model)
                .ToList()
                .ForEach(value => CellOf(value.Attribute).Apply(value.AuthoredText, value.Value, symbols));

            // Every read-only column derives from the model.
            OnPropertyChanged(string.Empty);
        }

        /// <summary>The cell editing <paramref name="attribute"/>.</summary>
        protected ExpressionCellVM CellOf(MachiningAttribute attribute) =>
            Cells.First(cell => cell.Attribute == attribute);

        private bool Save(ExpressionCellVM cell, string text)
        {
            if (_onEdited(this, cell.Attribute, text))
            {
                return true;
            }

            cell.SetSaveFailed();
            return false;
        }

        private void ResetCells() => Cells.ToList().ForEach(cell => cell.Reset());

        #endregion
    }
}
