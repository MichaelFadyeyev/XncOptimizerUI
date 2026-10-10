using System.Windows.Controls;

namespace XncOptimizerUI.MVVM.Views.PartPrograms
{
    /// <summary>
    /// Bores table of the selected part (<c>AppViewModel.SelectedPartBores</c>) with in-place
    /// editing of X / Y / Depth as numbers or <c>dx</c>/<c>dy</c>/<c>dz</c> expressions.
    /// </summary>
    public partial class SelectedPartBoresGrid : UserControl
    {
        public SelectedPartBoresGrid()
        {
            InitializeComponent();
        }
    }
}
