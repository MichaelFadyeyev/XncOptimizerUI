using System.Windows;
using System.Windows.Controls;
using XncOptimizerUI.MVVM.ViewModels;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.Views.Parts
{
    /// <summary>
    /// Parts list with in-place editing of sizes, name, texture flag and edge bands (<c>AppViewModel.PartsList</c>; the band select reads <c>AppViewModel.Catalog</c>).
    /// Brings a part into view when the list asks (<see cref="PartsListVM.RevealRequested"/>).
    /// </summary>
    public partial class PartsGrid : UserControl
    {
        public PartsGrid()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AppViewModel oldViewModel)
            {
                oldViewModel.PartsList.RevealRequested -= Reveal;
            }

            if (e.NewValue is AppViewModel newViewModel)
            {
                newViewModel.PartsList.RevealRequested += Reveal;
            }
        }

        /// <summary>
        /// Scrolls just enough to show <paramref name="part"/>'s row; a row already fully visible
        /// keeps the scroll position. Layout is brought up to date first, so rows removed just
        /// before are no longer counted.
        /// </summary>
        private void Reveal(PartVM part)
        {
            PartsDataGrid.UpdateLayout();
            PartsDataGrid.ScrollIntoView(part);
        }
    }
}
