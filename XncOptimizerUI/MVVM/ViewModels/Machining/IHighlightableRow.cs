using System.ComponentModel;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>
    /// A program-table row whose selection in its <c>DataGrid</c> is shared with the part preview:
    /// <see cref="IsHighlighted"/> is the row selected in the table, drawn red in the preview.
    /// (The row's "Check" box is a separate state.)
    /// </summary>
    public interface IHighlightableRow : INotifyPropertyChanged
    {
        /// <summary>Whether the row is selected in its table / its element in the preview.</summary>
        bool IsHighlighted { get; set; }

        /// <summary>The program element the row shows, as drawn by the preview (matched by reference).</summary>
        object Element { get; }
    }

    /// <summary>How a click on a preview element changes the selection.</summary>
    public enum HighlightMode
    {
        /// <summary>Select only the element (plain click).</summary>
        Replace,

        /// <summary>Add or remove the element (Ctrl+click).</summary>
        Toggle,

        /// <summary>Add the element, never remove it (Shift+click).</summary>
        Add
    }
}
