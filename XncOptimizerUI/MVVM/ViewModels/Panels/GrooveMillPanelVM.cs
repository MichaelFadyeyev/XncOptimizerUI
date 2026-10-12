using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Panels
{
    /// <summary>The "Grooves / Mills" panel: converts the checked parts' grooves to mills or back.</summary>
    public sealed partial class GrooveMillPanelVM(
        ProjectServices project,
        PartsListVM parts,
        BatchCommandRunner runner) : ObservableObject
    {
        #region ObservableProperties
        [ObservableProperty]
        private GrooveMillDirection _grooveMillDirection = GrooveMillDirection.GroovesToMills;

        /// <summary>
        /// When set, a Mills → Grooves conversion also turns axis-parallel rectangular pocket
        /// mills into grooves. Opt-in: off by default.
        /// </summary>
        [ObservableProperty]
        private bool _processPockets;
        #endregion

        #region Commands
        [RelayCommand]
        private void ConvertGroovesAndMills() =>
            runner.RunOnParts(
                parts.CheckedParts,
                "No parts checked for groove/mill conversion!",
                selected => project.Batch.ConvertGroovesAndMills(selected, GrooveMillDirection, ProcessPockets));
        #endregion
    }
}
