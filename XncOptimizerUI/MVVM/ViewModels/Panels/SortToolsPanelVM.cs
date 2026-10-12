using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Panels
{
    /// <summary>The "Sort tools" panel: sorts the tool declarations of the checked or the shown parts' programs.</summary>
    public sealed partial class SortToolsPanelVM(
        ProjectServices project,
        PartsListVM parts,
        BatchCommandRunner runner) : ObservableObject
    {
        #region ObservableProperties
        /// <summary>
        /// Tool sorting processes every part passing the current filter (all parts when no filter
        /// is set), checked or not, instead of only the checked ones. Opt-in: off by default.
        /// </summary>
        [ObservableProperty]
        private bool _sortToolsGlobally;
        #endregion

        #region Commands
        [RelayCommand]
        private void SortToolDeclarations() =>
            runner.RunOnParts(
                SelectedParts,
                SortToolsGlobally ? "No parts pass the filter for tool sorting!" : "No parts checked for tool sorting!",
                selected => project.Batch.SortToolDeclarations(selected));
        #endregion

        #region Methods
        /// <summary>Globally: every part the grid shows - the filtered set, or all parts when unfiltered.</summary>
        private List<Part> SelectedParts() => SortToolsGlobally ? parts.ShownParts() : parts.CheckedParts();
        #endregion
    }
}
