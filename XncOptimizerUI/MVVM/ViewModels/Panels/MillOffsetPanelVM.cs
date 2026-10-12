using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Panels
{
    /// <summary>The "Offset mill paths" panel: moves the checked parts' mill paths sideways by a distance.</summary>
    public sealed partial class MillOffsetPanelVM(
        ProjectServices project,
        PartsListVM parts,
        BatchCommandRunner runner) : ObservableObject
    {
        #region Properties
        /// <summary>The offset distance typed, or <c>null</c> while it is not a number.</summary>
        private decimal? Offset => DecimalInput.TryParse(MillOffset, out var offset) ? offset : null;

        private MillPathKinds Kinds =>
            (OffsetOpenMillPaths ? MillPathKinds.Open : MillPathKinds.None)
            | (OffsetClosedMillPaths ? MillPathKinds.Closed : MillPathKinds.None);
        #endregion

        #region ObservableProperties
        /// <summary>Raw text of the mill path offset distance (mm); parsed when the command runs.</summary>
        [ObservableProperty]
        private string _millOffset = string.Empty;

        [ObservableProperty]
        private MillOffsetSide _millOffsetSide = MillOffsetSide.Right;

        /// <summary>Mill path offset processes open <c>&lt;ms&gt;</c> contours.</summary>
        [ObservableProperty]
        private bool _offsetOpenMillPaths = true;

        /// <summary>Mill path offset processes closed contours, rectangles and ellipses.</summary>
        [ObservableProperty]
        private bool _offsetClosedMillPaths = true;
        #endregion

        #region Commands
        [RelayCommand]
        private void OffsetMillPaths() =>
            runner.RunOnParts(
                parts.CheckedParts,
                "No parts checked for mill path offset!",
                selected => project.Batch.OffsetMillPaths(selected, (double)Offset!.Value, MillOffsetSide, Kinds),
                () => Offset is > 0m ? null : "Enter a mill path offset greater than 0!",
                () => Kinds == MillPathKinds.None ? "Select open and/or closed mill paths to offset!" : null);
        #endregion
    }
}
