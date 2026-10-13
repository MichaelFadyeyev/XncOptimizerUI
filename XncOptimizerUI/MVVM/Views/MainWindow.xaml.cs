using System.Windows;
using XncOptimizerUI.MVVM.ViewModels;

namespace XncOptimizerUI.MVVM.Views
{
    /// <summary>
    /// The application window: lays out the menu panels, the parts and materials tabs and the log,
    /// all bound to <see cref="AppViewModel"/> and its child view models.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(AppViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
