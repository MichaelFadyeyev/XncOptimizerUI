using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace XncOptimizerUI.Helpers
{
    /// <summary>
    /// Attached behavior for DataGrid template columns whose editor is a <see cref="ComboBox"/>:
    /// the cell shows light read-only content and builds the ComboBox only while editing (entered
    /// natively: a click on the already selected cell, or F2).
    /// <list type="bullet">
    /// <item><c>OpenDropDownOnLoad</c> (on <see cref="ComboBox"/>) - the editor opens its list as
    /// soon as it appears, and closing the list commits the cell back to display mode.</item>
    /// </list>
    /// </summary>
    public static class DataGridCellEditBehavior
    {
        public static readonly DependencyProperty OpenDropDownOnLoadProperty = DependencyProperty.RegisterAttached(
            "OpenDropDownOnLoad", typeof(bool), typeof(DataGridCellEditBehavior),
            new PropertyMetadata(false, OnOpenDropDownOnLoadChanged));

        public static bool GetOpenDropDownOnLoad(DependencyObject element) => (bool)element.GetValue(OpenDropDownOnLoadProperty);
        public static void SetOpenDropDownOnLoad(DependencyObject element, bool value) => element.SetValue(OpenDropDownOnLoadProperty, value);

        private static void OnOpenDropDownOnLoadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ComboBox comboBox) return;

            comboBox.Loaded -= ComboBox_Loaded;
            comboBox.DropDownClosed -= ComboBox_DropDownClosed;

            if (e.NewValue is true)
            {
                comboBox.Loaded += ComboBox_Loaded;
                comboBox.DropDownClosed += ComboBox_DropDownClosed;
            }
        }

        private static void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            comboBox.Focus();
            comboBox.IsDropDownOpen = true;
        }

        private static void ComboBox_DropDownClosed(object? sender, EventArgs e)
        {
            if (sender is not ComboBox comboBox || FindAncestor<DataGrid>(comboBox) is not { } dataGrid) return;

            // Deferred: committing swaps the cell back to its display template, which would
            // tear down this ComboBox while it is still raising DropDownClosed.
            comboBox.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                () => dataGrid.CommitEdit(DataGridEditingUnit.Cell, true));
        }

        private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
        {
            while (element != null && element is not T)
            {
                element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
            }

            return element as T;
        }
    }
}
