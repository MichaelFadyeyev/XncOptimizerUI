using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Panels
{
    /// <summary>The "Optimize mill order" command: shortens the travel between the checked parts' mills.</summary>
    public sealed partial class MillOrderPanelVM(
        ProjectServices project,
        PartsListVM parts,
        BatchCommandRunner runner)
    {
        #region Commands
        [RelayCommand]
        private void OptimizeMillTraversal() =>
            runner.RunOnParts(
                parts.CheckedParts,
                "No parts checked for mill order optimization!",
                selected => project.Batch.OptimizeMillTraversal(selected));
        #endregion
    }
}
