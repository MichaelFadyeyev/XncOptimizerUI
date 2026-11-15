using System.Windows.Controls;

namespace XncOptimizerUI.MVVM.Views.PartPrograms
{
    /// <summary>
    /// Milling contours table of the selected part (<c>SelectedPartMachiningVM.MillingContours</c>) with in-place editing of the entry depth as a number or expression.
    /// </summary>
    public partial class MillingContoursGrid : UserControl
    {
        public MillingContoursGrid()
        {
            InitializeComponent();
        }
    }
}
