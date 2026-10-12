using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

        /// <summary>
        /// The range filter boxes rewrite their own text on the fly (","->".",
        /// trailing-separator trim, ".5"->"0.5"). When that happens while the box is focused
        /// the TwoWay binding resets the caret to the start; put it back at the end so the
        /// next keystroke appends. Only acts when the caret was actually reset to 0, so
        /// ordinary left-to-right typing and mid-text edits are untouched.
        /// </summary>
        private void RangeInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb)
            {
                return;
            }

            tb.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (tb.IsKeyboardFocused && tb.CaretIndex == 0 && tb.Text.Length > 0)
                {
                    tb.CaretIndex = tb.Text.Length;
                }
            }), DispatcherPriority.Background);
        }
    }
}
