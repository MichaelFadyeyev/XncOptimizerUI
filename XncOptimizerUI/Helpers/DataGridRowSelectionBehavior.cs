using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using XncOptimizerUI.MVVM.ViewModels.Machining;

namespace XncOptimizerUI.Helpers
{
    /// <summary>
    /// Attached behavior <c>SyncsRowSelection</c> (on <see cref="DataGrid"/>): keeps the grid's
    /// selected rows and its <see cref="ISelectableRow"/> items' <see cref="ISelectableRow.IsSelected"/>
    /// in step, both ways. Selecting rows in the grid selects their items; selecting an item from the
    /// view model (a click on the part preview) selects it and scrolls it into view; a new items
    /// source shows the items already selected as selected rows.
    /// </summary>
    public static class DataGridRowSelectionBehavior
    {
        public static readonly DependencyProperty SyncsRowSelectionProperty = DependencyProperty.RegisterAttached(
            "SyncsRowSelection", typeof(bool), typeof(DataGridRowSelectionBehavior),
            new PropertyMetadata(false, OnSyncsRowSelectionChanged));

        private static readonly DependencyProperty SyncProperty = DependencyProperty.RegisterAttached(
            "Sync", typeof(RowSelectionSync), typeof(DataGridRowSelectionBehavior), new PropertyMetadata(null));

        public static bool GetSyncsRowSelection(DependencyObject element) => (bool)element.GetValue(SyncsRowSelectionProperty);
        public static void SetSyncsRowSelection(DependencyObject element, bool value) => element.SetValue(SyncsRowSelectionProperty, value);

        private static void OnSyncsRowSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            (dataGrid.GetValue(SyncProperty) as RowSelectionSync)?.Detach();
            dataGrid.ClearValue(SyncProperty);

            if (e.NewValue is true)
            {
                dataGrid.SetValue(SyncProperty, new RowSelectionSync(dataGrid));
            }
        }

        /// <summary>The two-way sync of one grid: its row subscriptions and the re-entrance guard.</summary>
        private sealed class RowSelectionSync
        {
            private readonly DataGrid _grid;
            private readonly List<ISelectableRow> _rows = [];
            private bool _isSyncing;

            public RowSelectionSync(DataGrid grid)
            {
                _grid = grid;
                _grid.SelectionChanged += OnGridSelectionChanged;
                ((INotifyCollectionChanged)_grid.Items).CollectionChanged += OnItemsChanged;
                WatchRows();
            }

            public void Detach()
            {
                _grid.SelectionChanged -= OnGridSelectionChanged;
                ((INotifyCollectionChanged)_grid.Items).CollectionChanged -= OnItemsChanged;
                UnwatchRows();
            }

            /// <summary>Grid to rows. Rows dropped by a new items source keep their state.</summary>
            private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                // Selection changes of nested selectors (an editing cell's ComboBox) bubble here too.
                if (_isSyncing || !ReferenceEquals(e.OriginalSource, _grid)) return;

                Guarded(() =>
                {
                    e.RemovedItems.OfType<ISelectableRow>()
                        .Where(_grid.Items.Contains)
                        .ToList()
                        .ForEach(row => row.IsSelected = false);
                    e.AddedItems.OfType<ISelectableRow>()
                        .ToList()
                        .ForEach(row => row.IsSelected = true);
                });
            }

            private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                WatchRows();

                // Deferred: the grid applies a new items source's selection after this notification.
                _grid.Dispatcher.BeginInvoke(ShowSelectedRows);
            }

            /// <summary>Rows to grid.</summary>
            private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (_isSyncing || sender is not ISelectableRow row || e.PropertyName != nameof(ISelectableRow.IsSelected)) return;

                Guarded(() =>
                {
                    if (!row.IsSelected)
                    {
                        _grid.SelectedItems.Remove(row);
                        return;
                    }

                    if (!_grid.SelectedItems.Contains(row))
                    {
                        _grid.SelectedItems.Add(row);
                    }

                    _grid.ScrollIntoView(row);
                });
            }

            /// <summary>Selects exactly the grid rows whose items are selected.</summary>
            private void ShowSelectedRows()
            {
                var rows = _grid.Items.OfType<ISelectableRow>().ToList();

                Guarded(() =>
                {
                    rows.Where(row => !row.IsSelected).ToList().ForEach(row => _grid.SelectedItems.Remove(row));
                    rows.Where(row => row.IsSelected && !_grid.SelectedItems.Contains(row)).ToList().ForEach(row => _grid.SelectedItems.Add(row));
                });
            }

            private void WatchRows()
            {
                UnwatchRows();
                _rows.AddRange(_grid.Items.OfType<ISelectableRow>());
                _rows.ForEach(row => row.PropertyChanged += OnRowPropertyChanged);
            }

            private void UnwatchRows()
            {
                _rows.ForEach(row => row.PropertyChanged -= OnRowPropertyChanged);
                _rows.Clear();
            }

            private void Guarded(Action sync)
            {
                _isSyncing = true;

                try
                {
                    sync();
                }
                finally
                {
                    _isSyncing = false;
                }
            }
        }
    }
}
