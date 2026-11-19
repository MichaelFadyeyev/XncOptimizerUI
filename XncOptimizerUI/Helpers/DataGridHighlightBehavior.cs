using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using XncOptimizerUI.MVVM.ViewModels.Machining;

namespace XncOptimizerUI.Helpers
{
    /// <summary>
    /// Attached behavior <c>SyncsRowHighlight</c> (on <see cref="DataGrid"/>): keeps the grid's
    /// selected rows and its <see cref="IHighlightableRow"/> items' <see cref="IHighlightableRow.IsHighlighted"/>
    /// in step, both ways. Selecting rows in the grid highlights them; highlighting a row from the
    /// view model (a click on the part preview) selects it and scrolls it into view; a new items
    /// source shows the rows already highlighted as selected.
    /// </summary>
    public static class DataGridHighlightBehavior
    {
        public static readonly DependencyProperty SyncsRowHighlightProperty = DependencyProperty.RegisterAttached(
            "SyncsRowHighlight", typeof(bool), typeof(DataGridHighlightBehavior),
            new PropertyMetadata(false, OnSyncsRowHighlightChanged));

        private static readonly DependencyProperty SyncProperty = DependencyProperty.RegisterAttached(
            "Sync", typeof(HighlightSync), typeof(DataGridHighlightBehavior), new PropertyMetadata(null));

        public static bool GetSyncsRowHighlight(DependencyObject element) => (bool)element.GetValue(SyncsRowHighlightProperty);
        public static void SetSyncsRowHighlight(DependencyObject element, bool value) => element.SetValue(SyncsRowHighlightProperty, value);

        private static void OnSyncsRowHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            (dataGrid.GetValue(SyncProperty) as HighlightSync)?.Detach();
            dataGrid.ClearValue(SyncProperty);

            if (e.NewValue is true)
            {
                dataGrid.SetValue(SyncProperty, new HighlightSync(dataGrid));
            }
        }

        /// <summary>The two-way sync of one grid: its row subscriptions and the re-entrance guard.</summary>
        private sealed class HighlightSync
        {
            private readonly DataGrid _grid;
            private readonly List<IHighlightableRow> _rows = [];
            private bool _isSyncing;

            public HighlightSync(DataGrid grid)
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
                    e.RemovedItems.OfType<IHighlightableRow>()
                        .Where(_grid.Items.Contains)
                        .ToList()
                        .ForEach(row => row.IsHighlighted = false);
                    e.AddedItems.OfType<IHighlightableRow>()
                        .ToList()
                        .ForEach(row => row.IsHighlighted = true);
                });
            }

            private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                WatchRows();

                // Deferred: the grid applies a new items source's selection after this notification.
                _grid.Dispatcher.BeginInvoke(ShowHighlightedRows);
            }

            /// <summary>Rows to grid.</summary>
            private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (_isSyncing || sender is not IHighlightableRow row || e.PropertyName != nameof(IHighlightableRow.IsHighlighted)) return;

                Guarded(() =>
                {
                    if (!row.IsHighlighted)
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

            /// <summary>Selects exactly the highlighted rows of the grid.</summary>
            private void ShowHighlightedRows()
            {
                var rows = _grid.Items.OfType<IHighlightableRow>().ToList();

                Guarded(() =>
                {
                    rows.Where(row => !row.IsHighlighted).ToList().ForEach(row => _grid.SelectedItems.Remove(row));
                    rows.Where(row => row.IsHighlighted && !_grid.SelectedItems.Contains(row)).ToList().ForEach(row => _grid.SelectedItems.Add(row));
                });
            }

            private void WatchRows()
            {
                UnwatchRows();
                _rows.AddRange(_grid.Items.OfType<IHighlightableRow>());
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
