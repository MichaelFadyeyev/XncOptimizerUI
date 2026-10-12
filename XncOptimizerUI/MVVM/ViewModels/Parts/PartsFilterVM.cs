using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Helpers;

namespace XncOptimizerUI.MVVM.ViewModels.Parts
{
    /// <summary>
    /// The Parts grid filter: a name fragment and length / width ranges. Every change re-filters
    /// <see cref="PartsListVM.Parts"/> at once; an inverted range is snapped together a moment
    /// after the user stops typing.
    /// </summary>
    public sealed partial class PartsFilterVM : ObservableObject
    {
        private const int BoundsNormalizeDelayMs = 400;

        private readonly PartsListVM _parts;
        private readonly DispatcherTimer _boundsNormalizeTimer;

        private string _filterName = string.Empty;

        // Each range bound keeps the raw text the user typed AND its parsed value. The text
        // is what the TextBox shows, so a half-typed "18." or a "," separator is not
        // reformatted away mid-edit; the decimal is what the filter and the equalize use.
        private string _lengthMinText = string.Empty;
        private string _lengthMaxText = string.Empty;
        private string _widthMinText = string.Empty;
        private string _widthMaxText = string.Empty;
        private decimal? _lengthMin;
        private decimal? _lengthMax;
        private decimal? _widthMin;
        private decimal? _widthMax;

        private RangeEdge _lengthPendingEdge = RangeEdge.None;
        private RangeEdge _widthPendingEdge = RangeEdge.None;

        /// <summary>Open <see cref="SuspendFilter"/> scopes; the filter is applied only when none is open.</summary>
        private int _filterSuspensions;

        public PartsFilterVM(PartsListVM parts)
        {
            _parts = parts;
            _boundsNormalizeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(BoundsNormalizeDelayMs)
            };
            _boundsNormalizeTimer.Tick += (_, _) =>
            {
                _boundsNormalizeTimer.Stop();
                NormalizeRangeBounds();
            };
        }

        /// <summary>The bound of a range the user edited last, which an inverted range keeps.</summary>
        private enum RangeEdge { None, Min, Max }

        #region Properties
        public string FilterName
        {
            get => _filterName;
            set
            {
                _filterName = value;
                OnPropertyChanged();
                FilterParts();
            }
        }

        public string LengthMin
        {
            get => _lengthMinText;
            set => SetRangeBound(ref _lengthMinText, ref _lengthMin, value, ref _lengthPendingEdge, RangeEdge.Min);
        }

        public string LengthMax
        {
            get => _lengthMaxText;
            set => SetRangeBound(ref _lengthMaxText, ref _lengthMax, value, ref _lengthPendingEdge, RangeEdge.Max);
        }

        public string WidthMin
        {
            get => _widthMinText;
            set => SetRangeBound(ref _widthMinText, ref _widthMin, value, ref _widthPendingEdge, RangeEdge.Min);
        }

        public string WidthMax
        {
            get => _widthMaxText;
            set => SetRangeBound(ref _widthMaxText, ref _widthMax, value, ref _widthPendingEdge, RangeEdge.Max);
        }
        #endregion

        #region Commands
        [RelayCommand]
        private void ClearFilters()
        {
            Reset();
            _parts.ShowAll();
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            NormalizeRangeBounds();
            FilterParts();
        }
        #endregion

        #region Methods
        /// <summary>Empties the name and the ranges without re-filtering the parts.</summary>
        public void Reset()
        {
            using (SuspendFilter())
            {
                FilterName = string.Empty;
                LengthMin = string.Empty;
                LengthMax = string.Empty;
                WidthMin = string.Empty;
                WidthMax = string.Empty;
            }

            CancelBoundsNormalize();
        }

        /// <summary>
        /// Snaps an inverted [min, max] pair together, moving the edge the user did NOT
        /// just edit. Runs off the debounce timer so it acts on the fully typed value
        /// rather than each intermediate digit. Public so the Filter button and tests
        /// can force it without waiting for the timer.
        /// </summary>
        public void NormalizeRangeBounds()
        {
            CanonicalizeBoundText(ref _lengthMinText, nameof(LengthMin));
            CanonicalizeBoundText(ref _lengthMaxText, nameof(LengthMax));
            CanonicalizeBoundText(ref _widthMinText, nameof(WidthMin));
            CanonicalizeBoundText(ref _widthMaxText, nameof(WidthMax));

            var changed = NormalizePair(
                ref _lengthMin, ref _lengthMinText, nameof(LengthMin),
                ref _lengthMax, ref _lengthMaxText, nameof(LengthMax), _lengthPendingEdge);
            changed |= NormalizePair(
                ref _widthMin, ref _widthMinText, nameof(WidthMin),
                ref _widthMax, ref _widthMaxText, nameof(WidthMax), _widthPendingEdge);

            _lengthPendingEdge = RangeEdge.None;
            _widthPendingEdge = RangeEdge.None;

            if (changed)
            {
                FilterParts();
            }
        }

        /// <summary>Keeps the parts as they are until the returned scope is disposed.</summary>
        private FilterSuspension SuspendFilter()
        {
            _filterSuspensions++;

            return new FilterSuspension(this);
        }

        private void FilterParts()
        {
            if (_filterSuspensions > 0)
            {
                return;
            }

            _parts.ShowMatching(Matches);
        }

        private bool Matches(PartVM part) =>
            (string.IsNullOrEmpty(_filterName) || part.Name.Contains(_filterName))
            && (_lengthMin == null || part.Length >= _lengthMin)
            && (_lengthMax == null || part.Length <= _lengthMax)
            && (_widthMin == null || part.Width >= _widthMin)
            && (_widthMax == null || part.Width <= _widthMax);

        private void SetRangeBound(ref string text, ref decimal? parsed, string? value,
            ref RangeEdge pendingEdge, RangeEdge edge, [CallerMemberName] string? propertyName = null)
        {
            // Store the raw text verbatim. "." is the only decimal separator; "," is a
            // validation error (DecimalValidationRule rejects it, so it never reaches here
            // through the binding). Trimming and canonicalization happen only on commit
            // (NormalizeRangeBounds), so they cannot eat a digit still being typed.
            text = value ?? string.Empty;
            parsed = DecimalInput.TryParse(text, out var result) ? result : null;
            OnPropertyChanged(propertyName);

            pendingEdge = edge;
            RestartBoundsNormalizeTimer();

            FilterParts();
        }

        private void RestartBoundsNormalizeTimer()
        {
            _boundsNormalizeTimer.Stop();
            _boundsNormalizeTimer.Start();
        }

        private void CancelBoundsNormalize()
        {
            _boundsNormalizeTimer.Stop();
            _lengthPendingEdge = RangeEdge.None;
            _widthPendingEdge = RangeEdge.None;
        }

        // Canonicalization (trim, trailing-separator strip) never changes the parsed value,
        // so the decimal? fields stay correct and this does not need to re-run the filter.
        private void CanonicalizeBoundText(ref string text, string propertyName)
        {
            var canonical = DecimalInput.Canonicalize(text);

            if (canonical != text)
            {
                text = canonical;
                OnPropertyChanged(propertyName);
            }
        }

        private bool NormalizePair(
            ref decimal? min, ref string minText, string minProp,
            ref decimal? max, ref string maxText, string maxProp,
            RangeEdge edited)
        {
            if (edited == RangeEdge.None || !min.HasValue || !max.HasValue || min <= max)
            {
                return false;
            }

            if (edited == RangeEdge.Min)
            {
                max = min;
                maxText = DecimalInput.Format(max.Value);
                OnPropertyChanged(maxProp);
            }
            else
            {
                min = max;
                minText = DecimalInput.Format(min.Value);
                OnPropertyChanged(minProp);
            }

            return true;
        }
        #endregion

        private sealed class FilterSuspension(PartsFilterVM owner) : IDisposable
        {
            public void Dispose() => owner._filterSuspensions--;
        }
    }
}
