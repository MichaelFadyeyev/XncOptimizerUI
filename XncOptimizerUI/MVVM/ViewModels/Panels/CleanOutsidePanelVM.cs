using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Panels
{
    /// <summary>The "Clean operations" panel: removes the chosen kinds of operations lying outside their part.</summary>
    public sealed partial class CleanOutsidePanelVM(
        ProjectServices project,
        PartsListVM parts,
        BatchCommandRunner runner) : ObservableObject
    {
        #region Properties
        private OutsideOperationKinds Kinds =>
            (CleanOutsideBores ? OutsideOperationKinds.Bores : OutsideOperationKinds.None)
            | (CleanOutsideGrooves ? OutsideOperationKinds.Grooves : OutsideOperationKinds.None)
            | (CleanOutsideMills ? OutsideOperationKinds.Mills : OutsideOperationKinds.None);
        #endregion

        #region ObservableProperties
        /// <summary>"Clean operations" removes bores lying outside the part.</summary>
        [ObservableProperty]
        private bool _cleanOutsideBores = true;

        /// <summary>"Clean operations" removes grooves lying outside the part.</summary>
        [ObservableProperty]
        private bool _cleanOutsideGrooves = true;

        /// <summary>"Clean operations" removes mills (contours, rectangles, ellipses) lying outside the part.</summary>
        [ObservableProperty]
        private bool _cleanOutsideMills = true;
        #endregion

        #region Commands
        /// <summary>
        /// Removes the selected kinds of operations lying outside their part from the checked parts,
        /// or from every part of the project when none is checked.
        /// </summary>
        [RelayCommand]
        private void CleanOutsideOperations() =>
            runner.RunOnParts(
                parts.CheckedOrAllParts,
                "No parts to clean operations for!",
                selected => project.Batch.CleanOutsideOperations(selected, Kinds),
                () => Kinds == OutsideOperationKinds.None ? "Select bores, grooves and/or mills to clean!" : null);
        #endregion
    }
}
