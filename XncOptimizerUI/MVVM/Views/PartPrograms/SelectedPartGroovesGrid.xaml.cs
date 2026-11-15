using System.Windows.Controls;

namespace XncOptimizerUI.MVVM.Views.PartPrograms
{
    /// <summary>
    /// Grooves table of the selected part (<c>SelectedPartMachiningVM.Grooves</c>) with in-place editing of start / end points, depth and width as numbers or expressions.
    /// </summary>
    public partial class SelectedPartGroovesGrid : UserControl
    {
        public SelectedPartGroovesGrid()
        {
            InitializeComponent();
        }
    }
}
