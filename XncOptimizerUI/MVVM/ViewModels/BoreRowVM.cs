using System;
using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>
    /// One row of the selected part's bores <c>DataGrid</c>: a flattened, display-ready view of
    /// an <see cref="XncBore"/> plus the checkbox state that feeds
    /// <see cref="AppViewModel.CheckedBores"/>.
    /// </summary>
    public partial class BoreRowVM : ObservableObject
    {
        private readonly Action<BoreRowVM, bool> _onSelectionChanged;

        public BoreRowVM(
            int number, string side, string x, string y, string? diameter, string depth,
            XncBore bore, Action<BoreRowVM, bool> onSelectionChanged)
        {
            Number = number;
            Side = side;
            X = x;
            Y = y;
            Diameter = diameter;
            Depth = depth;
            Bore = bore;
            _onSelectionChanged = onSelectionChanged;
        }

        public int Number { get; }

        public string Side { get; }

        public string X { get; }

        public string Y { get; }

        public string? Diameter { get; }

        public string Depth { get; }

        public XncBore Bore { get; }

        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value) => _onSelectionChanged(this, value);
    }
}
