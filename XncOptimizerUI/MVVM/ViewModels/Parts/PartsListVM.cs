using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels.Parts
{
    /// <summary>
    /// The parts of the open project: all of them, the ones the Parts grid shows (see
    /// <see cref="PartsFilterVM"/>), the checked ones the batch commands act on, and the selected
    /// one, whose pending grid edits are saved when the selection moves on.
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

        private readonly PartEditSaver _saver;

        private List<PartVM> _allParts = [];

        public PartsListVM(PartEditSaver saver)
        {
            _saver = saver;
        }

        /// <summary>
        /// Raised when <see cref="SelectedPart"/> changed, before its <c>PropertyChanged</c>, so the
        /// views of the selected part are up to date once the grid and the preview react.
        /// </summary>
        public event Action<PartVM?>? SelectedPartChanged;

        #region Properties
        /// <summary>Every part of the project, in document order, filtered or not.</summary>
        public IReadOnlyList<PartVM> AllParts => _allParts;

        /// <summary>
        /// Number of parts currently checked ("Sel") across the whole project, including
        /// any hidden by the active filter - this is the set the batch commands act on.
        /// </summary>
        public int CheckedCount => _allParts.Count(p => p.IsSelected);
        #endregion

        #region ObservableProperties
        /// <summary>The parts the grid shows: those passing the filter.</summary>
        [ObservableProperty]
        private ObservableCollection<PartVM> _parts = [];

        [ObservableProperty]
        private PartVM? _selectedPart;

        partial void OnSelectedPartChanging(PartVM? value)
        {
            if (_selectedPart != null)
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
        public List<Part> CheckedParts() => [.. _allParts.Where(p => p.IsSelected).Select(p => p.Part)];

        /// <summary>The checked parts, or every part of the project when none is checked.</summary>
        public List<Part> CheckedOrAllParts()
        {
            var checkedParts = CheckedParts();

            return checkedParts.Count > 0 ? checkedParts : [.. _allParts.Select(p => p.Part)];
        }

        /// <summary>The parts the grid shows: every part passing the filter, checked or not.</summary>
        public List<Part> ShownParts() => [.. Parts.Select(p => p.Part)];

        private void ReplaceAll(List<PartVM> parts)
        {
            RebindCheckedCount(_allParts, parts);
            _allParts = parts;
            ShowAll();
        }

        // Acts on the currently displayed (filtered) parts only, mirroring the "Filtered no:"
        // label. Each IsSelected write raises PartVM.PropertyChanged, which refreshes CheckedCount.
        private void SetChecked(bool value)
        {
            foreach (var part in Parts)
            {
                part.IsSelected = value;
            }
        }

        /// <summary>
        /// Keeps <see cref="CheckedCount"/> live: the label must react to every "Sel" checkbox
        /// toggle in the grid, not just to Check all / Uncheck all. Detaches the handler from the
        /// old part VMs, attaches it to the new ones, and refreshes the count.
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

            OnPropertyChanged(nameof(CheckedCount));
        }

        private void OnPartPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PartVM.IsSelected))
            {
                OnPropertyChanged(nameof(CheckedCount));
            }
            else if (sender is PartVM part && e.PropertyName is { } name && ImmediatelySavedPartProperties.Contains(name))
            {
                _saver.SavePendingEdits(part);
            }
        }
        #endregion
    }
}
