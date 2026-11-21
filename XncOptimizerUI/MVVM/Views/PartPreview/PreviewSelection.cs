using System.Windows.Input;
using XncOptimizerUI.MVVM.ViewModels.Machining;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// The selection of bores, grooves and mills during one preview render: whether an element
    /// is selected and what a click on it does both come from the view model, so the selection is
    /// shared with the program tables and survives every re-render (zoom, pan, resize). Each
    /// drawn element registers how to restyle itself; <see cref="Refresh"/> restyles them all
    /// when the selection changes.
    /// </summary>
    internal sealed class PreviewSelection(Func<object, bool> isSelected, Action<object, ElementSelectionMode> select)
    {
        private readonly List<(object Element, Action<bool> Apply)> _elements = [];

        /// <summary>A selection with nothing selected that ignores clicks: a render without a view model behind it.</summary>
        public static PreviewSelection None() => new(_ => false, (_, _) => { });

        /// <summary>How a click with <paramref name="modifiers"/> held changes the selection: Ctrl toggles, Shift adds, else selects only the element.</summary>
        public static ElementSelectionMode ModeFor(ModifierKeys modifiers) =>
            modifiers.HasFlag(ModifierKeys.Control) ? ElementSelectionMode.Toggle
            : modifiers.HasFlag(ModifierKeys.Shift) ? ElementSelectionMode.Add
            : ElementSelectionMode.Replace;

        /// <summary>Remembers how to restyle <paramref name="element"/>'s shapes and applies its current state.</summary>
        public void Register(object element, Action<bool> apply)
        {
            _elements.Add((element, apply));
            apply(isSelected(element));
        }

        /// <summary>A click on <paramref name="element"/> with <paramref name="modifiers"/> held.</summary>
        public void Click(object element, ModifierKeys modifiers) => select(element, ModeFor(modifiers));

        /// <summary>Restyles every registered element to its current state.</summary>
        public void Refresh() => _elements.ForEach(entry => entry.Apply(isSelected(entry.Element)));
    }
}
