using System.Windows.Controls;

namespace XncOptimizerUI.MVVM.Views.Parts
{
    /// <summary>
    /// Parts list with in-place editing of sizes, name, texture flag and edge bands (<c>AppViewModel.PartsList</c>; the band select reads <c>AppViewModel.Catalog</c>).
    /// </summary>
    public partial class PartsGrid : UserControl
    {
        public PartsGrid()
        {
            InitializeComponent();
        }
    }
}
