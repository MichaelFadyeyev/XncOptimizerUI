using System.Windows;
using XncOptimizerUI.MVVM.ViewModels;

namespace XncOptimizerUI.MVVM.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
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
