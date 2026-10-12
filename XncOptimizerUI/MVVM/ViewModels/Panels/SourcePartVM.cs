using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Panels
{
    /// <summary>
    /// The "Replace XNC" panel: a source part, set from the selected one, whose XNC programs
    /// replace those of the checked parts.
    /// </summary>
    public sealed partial class SourcePartVM(
        ProjectServices project,
        PartsListVM parts,
        ProjectCatalogVM catalog,
        BatchCommandRunner runner,
        ActivityLog log) : ObservableObject
    {
        private int _sourceXncCount;

        #region Properties
        /// <summary>Name, size, bands and program count of the source part, one per line.</summary>
        public string SourcePartInfo
        {
            get
            {
                var p = SourcePart;

                string Band(int? id) => id == null ? "-" : catalog.BandSymbol(id);

                if (p == null)
                    return "-\n"
                        + "-\n"
                        + "- | - | - | -\n"
                        + "Programs: -";

                return $"{p.Name}\n"
                    + $"{p.Length} x {p.Width}\n"
                    + $"{Band(p.TopBandingId)} | {Band(p.BottomBandingId)} "
                    + $"| {Band(p.LeftBandingId)} | {Band(p.RightBandingId)}\n"
                    + $"Programs: {_sourceXncCount}";
            }
        }
        #endregion

        #region ObservableProperties
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SourcePartInfo))]
        private PartVM? _sourcePart;

        partial void OnSourcePartChanged(PartVM? value)
        {
            _sourceXncCount = value == null ? 0 : project.Reader.GetXncProgramsCount(value.Id);
        }
        #endregion

        #region Commands
        [RelayCommand]
        private void SetSourcePart()
        {
            if (parts.SelectedPart == null)
            {
                log.Append("No part selected to set as source!\n");
                return;
            }

            SourcePart = parts.SelectedPart;
            log.Append($"Source part set: {SourcePart.Name}\n");
        }

        [RelayCommand]
        private void ResetSourcePart()
        {
            SourcePart = null;
        }

        [RelayCommand]
        private void ReplaceXNCs()
        {
            var replaced = runner.RunOnParts(
                TargetParts,
                "No parts selected to replace XNCs for!",
                targets => project.Restructuring.ReplaceXncPrograms(SourcePart!.Part, targets),
                () => SourcePart == null ? "No source part set!" : null);

            if (replaced)
            {
                SourcePart = null;
            }
        }
        #endregion

        #region Methods
        /// <summary>The checked parts but the source part itself.</summary>
        private List<Part> TargetParts() => [.. parts.CheckedParts().Where(p => p != SourcePart?.Part)];
        #endregion
    }
}
