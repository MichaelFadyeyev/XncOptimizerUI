using System.Windows.Controls;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Four toggle-style radio buttons (0°/90°/180°/270°) bound to
    /// <c>AppViewModel.SelectedPartRotation</c>; checking one turns the selected part.
    /// </summary>
    public partial class PartRotationPanel : UserControl
    {
        public PartRotationPanel()
        {
            InitializeComponent();
        }
    }
}
