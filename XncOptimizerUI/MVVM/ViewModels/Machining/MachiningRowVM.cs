using CommunityToolkit.Mvvm.ComponentModel;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>
    /// A checkable row of a program table: its number, the side it is machined on, the program
    /// element it shows and its check box, reported through <paramref name="onSelectionChanged"/>.
    /// </summary>
    public abstract partial class MachiningRowVM<TModel>(
        int number,
        string side,
        TModel model,
        Action<TModel, bool> onSelectionChanged) : ObservableObject
    {
        #region Properties
        public int Number { get; } = number;

        public string Side { get; } = side;

        public TModel Model { get; } = model;
        #endregion

        #region ObservableProperties
        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value) => onSelectionChanged(Model, value);
        #endregion
    }
}
