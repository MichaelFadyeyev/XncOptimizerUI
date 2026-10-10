using System.Windows.Controls;

namespace XncOptimizerUI.MVVM.Views.PartPrograms
{
    /// <summary>
    /// Variables table of the selected part (<c>AppViewModel.SelectedPartVariables</c>): the
    /// program <c>&lt;var&gt;</c> declarations with in-place editing of name, type, expr and comment.
    /// </summary>
    public partial class SelectedPartVariablesGrid : UserControl
    {
        public SelectedPartVariablesGrid()
        {
            InitializeComponent();
        }
    }
}
