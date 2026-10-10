namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>
    /// A <c>DataGrid</c> row item whose cells are committed one at a time, driven by
    /// <see cref="Helpers.DataGridCellEditBehavior"/>'s <c>CommitToRow</c>: the editor writes
    /// into draft properties while typing, and the row decides on cell commit whether the draft
    /// is accepted.
    /// </summary>
    public interface ICellEditableRow
    {
        /// <summary>A cell of this row entered edit mode.</summary>
        void BeginCellEdit();

        /// <summary>
        /// The cell is being committed. Returns <c>false</c> to refuse - the cell then stays in
        /// edit mode with its (invalid) draft.
        /// </summary>
        bool TryCommitCellEdit();

        /// <summary>The cell edit was cancelled (Esc): drafts return to the committed values.</summary>
        void CancelCellEdit();
    }
}
