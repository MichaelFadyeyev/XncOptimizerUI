using System.Windows;
using XncOptimizerUI.Services;

namespace XncOptimizerUI.MVVM.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                ConfigService.LoadConfiguration();
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}: {ex.InnerException?.Message}\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}