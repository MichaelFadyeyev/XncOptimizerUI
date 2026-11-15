using System.Windows.Controls;

namespace XncOptimizerUI.MVVM.Views.PartPrograms
{
    /// <summary>
    /// Milling rectangles table of the selected part (<c>SelectedPartMachiningVM.MillingRectangles</c>) with in-place editing of center, depth, size, angle and corner radius as numbers or expressions.
    /// </summary>
    public partial class MillingRectanglesGrid : UserControl
    {
        public MillingRectanglesGrid()
        {
            InitializeComponent();
        }
    }
}
