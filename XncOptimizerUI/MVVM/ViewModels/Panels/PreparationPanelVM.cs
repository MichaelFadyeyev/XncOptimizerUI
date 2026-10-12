using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Panels
{
    /// <summary>The "Preparation" panel: grouping identical parts, and preparing the labelled parts for a split along X.</summary>
    public sealed partial class PreparationPanelVM(
        ProjectServices project,
        PartsListVM parts,
        LabelsVM labels,
        BatchCommandRunner runner)
    {
        #region Properties
        public LabelsVM Labels { get; } = labels;
        #endregion

        #region Commands
        [RelayCommand]
        private void ExecuteOptimize() =>
            runner.Run(() =>
            {
                // Flush the selected part's pending grid edits before the parts are regrouped.
                parts.SelectedPart = null;

                return project.Restructuring.GroupIdenticalElements();
            });

        [RelayCommand]
        private void ExecutePrepForSplitAlongX() =>
            runner.Run(() => project.Restructuring.PrepForSplitAlongX(LabelledPartIds()));
        #endregion

        #region Methods
        /// <summary>Ids of the shown parts whose name contains the selected label.</summary>
        private string[] LabelledPartIds() =>
        [
            .. parts.Parts
                .Where(p => p.Name.Contains(Labels.SelectedLabel))
                .Select(p => p.Id.ToString(CultureInfo.InvariantCulture))
        ];
        #endregion
    }
}
