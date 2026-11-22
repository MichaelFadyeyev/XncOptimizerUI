using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels.Parts
{
    /// <summary>
    /// The parts of the open project: all of them, the ones the Parts grid shows (see
    /// <see cref="PartsFilterVM"/>), the checked ones the batch commands act on, and the selected
    /// one, whose pending grid edits are saved when the selection moves on. Deletes the checked parts.
    /// </summary>
    public sealed partial class PartsListVM : ObservableObject
    {
        /// <summary>
        /// Part properties edited through in-cell controls (Txt checkbox, band selects). Clicking
        /// the Txt checkbox doesn't select its row, and a band pick should not wait for the
        /// selection-change auto-save either; these are saved as soon as they change instead.
        /// </summary>
        private static readonly HashSet<string> ImmediatelySavedPartProperties =
        [
            nameof(PartVM.ConsiderTexture),
            nameof(PartVM.TopBandingId),
            nameof(PartVM.BottomBandingId),
            nameof(PartVM.LeftBandingId),
            nameof(PartVM.RightBandingId),
        ];

        private readonly ProjectServices _project;
        private readonly PartEditSaver _saver;
        private readonly IDialogService _dialogs;

        private List<PartVM> _allParts = [];

        public PartsListVM(ProjectServices project, PartEditSaver saver, IDialogService dialogs)
        {
            _project = project;
            _saver = saver;
            _dialogs = dialogs;
        }

        /// <summary>
        /// Raised when <see cref="SelectedPart"/> changed, before its <c>PropertyChanged</c>, so the
        /// views of the selected part are up to date once the grid and the preview react.
        /// </summary>
        public event Action<PartVM?>? SelectedPartChanged;

        /// <summary>
        /// Raised when the grid should bring <c>part</c> (the selected one) into view if it is off-screen,
        /// e.g. the part selected after a deletion; a visible part leaves the scroll position as it is.
        /// </summary>
        public event Action<PartVM>? RevealRequested;

        #region Properties
        /// <summary>Every part of the project, in document order, filtered or not.</summary>
        public IReadOnlyList<PartVM> AllParts => _allParts;

        /// <summary>
        /// Number of parts currently checked ("Sel") across the whole project, including
        /// any hidden by the active filter - this is the set the batch commands act on.
        /// </summary>
        public int CheckedCount => _allParts.Count(p => p.IsChecked);

        /// <summary>Whether any part is checked, shown or hidden by the filter (Delete is enabled).</summary>
        public bool HasCheckedParts => _allParts.Any(p => p.IsChecked);
        #endregion

        #region ObservableProperties
        /// <summary>The parts the grid shows: those passing the filter.</summary>
        [ObservableProperty]
        private ObservableCollection<PartVM> _parts = [];

        [ObservableProperty]
        private PartVM? _selectedPart;

        partial void OnSelectedPartChanging(PartVM? value)
        {
            // A deleted part is no longer in the project: nothing of it to save.
            if (_selectedPart != null && _allParts.Contains(_selectedPart))
            {
                _saver.SavePendingEdits(_selectedPart);
            }
        }

        partial void OnSelectedPartChanged(PartVM? value) => SelectedPartChanged?.Invoke(value);
        #endregion

        #region Commands
        [RelayCommand]
        private void CheckAll() => SetChecked(true);

        [RelayCommand]
        private void UncheckAll() => SetChecked(false);

        /// <summary>
        /// Deletes the checked parts (also those hidden by the filter) with their XNC operations once the
        /// user confirms, and saves the file in place. The grid keeps its rows and scroll position, less
        /// the deleted rows, renumbered; the part that followed the last deleted one is selected.
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasCheckedParts))]
        private void DeleteChecked()
        {
            var deleted = _allParts.Where(p => p.IsChecked).ToList();

            if (deleted.Count == 0 || !ConfirmDeletion(deleted))
            {
                return;
            }

            var deletedSet = deleted.ToHashSet();
            var keptSelection = SelectedPart is { } selected && !deletedSet.Contains(selected) ? selected : null;
            var ids = deleted.Select(p => p.Id).ToList();

            if (!_saver.SaveDescribedEdit(keptSelection, () => _project.Editor.DeleteParts(ids)))
            {
                return;
            }

            var next = NextAfterDeleted(Parts, deletedSet) ?? keptSelection;

            // The project's parts go first, so leaving a deleted selected part saves nothing; the shown
            // rows go after the new selection, as a removed selected row would make the grid select nothing.
            RemoveFromProject(deletedSet);
            SelectedPart = next;
            RemoveShownRows(deletedSet);
            Renumber();

            if (next != null)
            {
                RevealRequested?.Invoke(next);
            }
        }
        #endregion

        #region Methods
        /// <summary>Replaces the parts with <paramref name="parts"/>, numbered in order, and shows them all.</summary>
        public void Load(IEnumerable<Part> parts) =>
            ReplaceAll([.. parts.Select((part, i) => new PartVM(part) { Number = i + 1 })]);

        public void Clear() => ReplaceAll([]);

        /// <summary>Shows the parts matching <paramref name="filter"/>; nothing to show while the project has no part.</summary>
        public void ShowMatching(Func<PartVM, bool> filter)
        {
            if (_allParts.Count == 0) return;

            Parts = new ObservableCollection<PartVM>(_allParts.Where(filter));
        }

        public void ShowAll() => Parts = new ObservableCollection<PartVM>(_allParts);

        /// <summary>The checked parts, shown or hidden by the filter.</summary>
        public List<Part> CheckedParts() => [.. _allParts.Where(p => p.IsChecked).Select(p => p.Part)];

        /// <summary>The checked parts, or every part of the project when none is checked.</summary>
        public List<Part> CheckedOrAllParts()
        {
            var checkedParts = CheckedParts();

            return checkedParts.Count > 0 ? checkedParts : [.. _allParts.Select(p => p.Part)];
        }

        /// <summary>The parts the grid shows: every part passing the filter, checked or not.</summary>
        public List<Part> ShownParts() => [.. Parts.Select(p => p.Part)];

        /// <summary>
        /// The part to select once <paramref name="deleted"/> are gone: the first kept part shown after the
        /// last deleted one, else the last kept part shown before it; <c>null</c> when no deleted part is
        /// shown or no shown part is kept.
        /// </summary>
        private static PartVM? NextAfterDeleted(IReadOnlyList<PartVM> shown, HashSet<PartVM> deleted)
        {
            var lastDeletedIndex = shown.Select((part, index) => deleted.Contains(part) ? index : -1).DefaultIfEmpty(-1).Max();

            if (lastDeletedIndex < 0)
            {
                return null;
            }

            return shown.Skip(lastDeletedIndex + 1).FirstOrDefault(p => !deleted.Contains(p))
                ?? shown.Take(lastDeletedIndex).LastOrDefault(p => !deleted.Contains(p));
        }

        /// <summary>Asks before deleting <paramref name="deleted"/>, naming how many of them the filter hides.</summary>
        private bool ConfirmDeletion(IReadOnlyList<PartVM> deleted)
        {
            var hidden = deleted.Count(p => !Parts.Contains(p));
            var hiddenNote = hidden > 0 ? $"\n{hidden} of them hidden by the filter." : string.Empty;

            return _dialogs.Confirm($"Delete {deleted.Count} checked part(s)?{hiddenNote}\nTheir XNC operations are deleted too.", "Delete");
        }

        /// <summary>Removes <paramref name="deleted"/> from every part of the project.</summary>
        private void RemoveFromProject(HashSet<PartVM> deleted)
        {
            RebindCheckedCount(deleted, []);
            _allParts = [.. _allParts.Where(p => !deleted.Contains(p))];
            OnCheckedCountChanged();
        }

        /// <summary>
        /// Removes <paramref name="deleted"/> from the shown collection itself - not a new one, so the grid
        /// keeps its scroll position.
        /// </summary>
        private void RemoveShownRows(HashSet<PartVM> deleted) =>
            Parts.Where(deleted.Contains).ToList().ForEach(part => Parts.Remove(part));

        /// <summary>Numbers every part 1..N in document order, as a reopened file would.</summary>
        private void Renumber() =>
            _allParts.Select((part, index) => (part, index)).ToList().ForEach(p => p.part.Number = p.index + 1);

        private void ReplaceAll(List<PartVM> parts)
        {
            RebindCheckedCount(_allParts, parts);
            _allParts = parts;
            OnCheckedCountChanged();
            ShowAll();
        }

        // Acts on the currently displayed (filtered) parts only, mirroring the "Filtered no:"
        // label. Each IsChecked write raises PartVM.PropertyChanged, which refreshes CheckedCount.
        private void SetChecked(bool value)
        {
            foreach (var part in Parts)
            {
                part.IsChecked = value;
            }
        }

        /// <summary>
        /// Keeps <see cref="CheckedCount"/> live: the label must react to every "Sel" checkbox
        /// toggle in the grid, not just to Check all / Uncheck all. Detaches the handler from the
        /// old part VMs and attaches it to the new ones.
        /// </summary>
        private void RebindCheckedCount(IEnumerable<PartVM> oldParts, IEnumerable<PartVM> newParts)
        {
            foreach (var part in oldParts)
            {
                part.PropertyChanged -= OnPartPropertyChanged;
            }

            foreach (var part in newParts)
            {
                part.PropertyChanged += OnPartPropertyChanged;
            }
        }

        /// <summary>Refreshes the checked count and whether Delete is enabled.</summary>
        private void OnCheckedCountChanged()
        {
            OnPropertyChanged(nameof(CheckedCount));
            OnPropertyChanged(nameof(HasCheckedParts));
            DeleteCheckedCommand.NotifyCanExecuteChanged();
        }

        private void OnPartPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PartVM.IsChecked))
            {
                OnCheckedCountChanged();
            }
            else if (sender is PartVM part && e.PropertyName is { } name && ImmediatelySavedPartProperties.Contains(name))
            {
                _saver.SavePendingEdits(part);
            }
        }
        #endregion
    }
}
