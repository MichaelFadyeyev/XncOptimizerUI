using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Panels
{
    /// <summary>The "Bores / Mills" panel: converts the checked parts' large bores to mills or back.</summary>
    public sealed partial class BoreMillPanelVM(
        ProjectServices project,
        PartsListVM parts,
        BatchCommandRunner runner) : ObservableObject
    {
        #region ObservableProperties
        [ObservableProperty]
        private BoreMillDirection _boreMillDirection = BoreMillDirection.BoresToMills;

        /// <summary>
        /// When set, a Bores → Mills conversion emits an elliptical mill (<c>&lt;me&gt;</c>) per
        /// bore instead of a closed four-arc contour. Opt-in: off by default.
        /// </summary>
        [ObservableProperty]
        private bool _convertBoresToEllipses;
        #endregion

        #region Commands
        [RelayCommand]
        private void ConvertBoresAndMills() =>
            runner.RunOnParts(
                parts.CheckedParts,
                "No parts checked for bore/mill conversion!",
                selected => project.Batch.ConvertBoresAndMills(selected, BoreMillDirection, ConvertBoresToEllipses));
        #endregion
    }
}
