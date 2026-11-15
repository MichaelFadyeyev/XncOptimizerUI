using System.Windows.Controls;

namespace XncOptimizerUI.MVVM.Views.PartPrograms
{
    /// <summary>
    /// Milling ellipses table of the selected part (<c>SelectedPartMachiningVM.MillingEllipses</c>) with in-place editing of center, depth, semi-axes and angle as numbers or expressions.
    /// </summary>
    public partial class MillingEllipsesGrid : UserControl
    {
        public MillingEllipsesGrid()
        {
            InitializeComponent();
        }
    }
}
