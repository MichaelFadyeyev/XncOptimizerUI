using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using XncOptimizerUI.MVVM.ViewModels;

namespace XncOptimizerUI.Helpers
{
    /// <summary>
    /// Attached behavior for DataGrid template columns whose editor is a <see cref="ComboBox"/>:
    /// the cell shows light read-only content and builds the ComboBox only while editing (entered
    /// natively: a click on the already selected cell, or F2).
    /// <list type="bullet">
    /// <item><c>OpenDropDownOnLoad</c> (on <see cref="ComboBox"/>) - the editor opens its list as
    /// soon as it appears, and closing the list commits the cell back to display mode.</item>
    /// <item><c>CommitToRow</c> (on <see cref="DataGrid"/>) - for <see cref="ICellEditableRow"/>
    /// items, each cell edit is begun, committed or cancelled through the row; a row refusing the
    /// commit keeps the cell in edit mode; a click outside the grid commits the edit, or reverts
    /// it when the row refuses the draft.</item>
    /// <item><c>FocusOnLoad</c> (on <see cref="TextBox"/>) - the editor takes focus with its
    /// text selected as soon as it appears.</item>
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

        public static readonly DependencyProperty CommitToRowProperty = DependencyProperty.RegisterAttached(
            "CommitToRow", typeof(bool), typeof(DataGridCellEditBehavior),
            new PropertyMetadata(false, OnCommitToRowChanged));

        public static bool GetCommitToRow(DependencyObject element) => (bool)element.GetValue(CommitToRowProperty);
        public static void SetCommitToRow(DependencyObject element, bool value) => element.SetValue(CommitToRowProperty, value);

        private static void OnCommitToRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            dataGrid.BeginningEdit -= DataGrid_BeginningEdit;
            dataGrid.CellEditEnding -= DataGrid_CellEditEnding;

            if (e.NewValue is true)
            {
                dataGrid.BeginningEdit += DataGrid_BeginningEdit;
                dataGrid.CellEditEnding += DataGrid_CellEditEnding;
            }
        }

        /// <summary>The outside-click subscription of a grid while one of its cells is in edit mode.</summary>
        private sealed record OutsideClickWatch(Window Window, MouseButtonEventHandler Handler);

        private static readonly DependencyProperty OutsideClickWatchProperty = DependencyProperty.RegisterAttached(
            "OutsideClickWatch", typeof(OutsideClickWatch), typeof(DataGridCellEditBehavior), new PropertyMetadata(null));

        private static bool IsWatchingOutside(DataGrid dataGrid) => dataGrid.GetValue(OutsideClickWatchProperty) is OutsideClickWatch;

        private static void DataGrid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
        {
            if (sender is not DataGrid dataGrid || e.Row.Item is not ICellEditableRow row) return;

            row.BeginCellEdit();
            WatchOutsideClicks(dataGrid);
        }

        private static void DataGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if (sender is not DataGrid dataGrid || e.Row.Item is not ICellEditableRow row) return;

            if (e.EditAction == DataGridEditAction.Cancel)
            {
                row.CancelCellEdit();
            }
            else if (!row.TryCommitCellEdit())
            {
                e.Cancel = true;
                return;
            }

            StopWatchingOutsideClicks(dataGrid);
        }

        // DataGrid itself ends an edit on Enter/Tab/Esc, a click on another of its cells, or keyboard
        // focus moving to another control - but not on a click on something non-focusable;
        // leaving it by clicking elsewhere in the window (e.g. the part preview)
        // would keep the cell open with the typed value unsaved. A click outside the grid ends
        // the edit as a commit - or, when the row refuses the draft, as a cancel (revert).
        private static void WatchOutsideClicks(DataGrid dataGrid)
        {
            if (IsWatchingOutside(dataGrid) || Window.GetWindow(dataGrid) is not { } window) return;

            MouseButtonEventHandler handler = (_, args) =>
            {
                if (!IsWithin(args.OriginalSource as DependencyObject, dataGrid))
                {
                    EndEditLeavingGrid(dataGrid);
                }
            };

            window.PreviewMouseDown += handler;
            dataGrid.SetValue(OutsideClickWatchProperty, new OutsideClickWatch(window, handler));
        }

        private static void StopWatchingOutsideClicks(DataGrid dataGrid)
        {
            if (dataGrid.GetValue(OutsideClickWatchProperty) is not OutsideClickWatch watch) return;

            watch.Window.PreviewMouseDown -= watch.Handler;
            dataGrid.ClearValue(OutsideClickWatchProperty);
        }

        private static void EndEditLeavingGrid(DataGrid dataGrid)
        {
            if (!dataGrid.CommitEdit(DataGridEditingUnit.Row, true))
            {
                dataGrid.CancelEdit(DataGridEditingUnit.Row);
            }

            StopWatchingOutsideClicks(dataGrid);
        }

        private static bool IsWithin(DependencyObject? element, DependencyObject container)
        {
            while (element != null && !ReferenceEquals(element, container))
            {
                element = Parent(element);
            }

            return element != null;
        }

        public static readonly DependencyProperty FocusOnLoadProperty = DependencyProperty.RegisterAttached(
            "FocusOnLoad", typeof(bool), typeof(DataGridCellEditBehavior),
            new PropertyMetadata(false, OnFocusOnLoadChanged));

        public static bool GetFocusOnLoad(DependencyObject element) => (bool)element.GetValue(FocusOnLoadProperty);
        public static void SetFocusOnLoad(DependencyObject element, bool value) => element.SetValue(FocusOnLoadProperty, value);

        private static void OnFocusOnLoadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox textBox) return;

            textBox.Loaded -= TextBox_Loaded;

            if (e.NewValue is true)
            {
                textBox.Loaded += TextBox_Loaded;
            }
        }

        private static void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            textBox.Focus();
            textBox.SelectAll();
        }

        private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
        {
            while (element != null && element is not T)
            {
                element = Parent(element);
            }

            return element as T;
        }

        private static DependencyObject? Parent(DependencyObject element) =>
            element is Visual
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
    }
}
