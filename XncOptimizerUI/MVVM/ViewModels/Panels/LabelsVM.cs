using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.MVVM.ViewModels.Panels
{
    /// <summary>
    /// The name labels that mark parts to prepare for a split along X, persisted in the
    /// application options, and the one selected for the next preparation.
    /// </summary>
    public sealed partial class LabelsVM : ObservableObject
    {
        private readonly IConfigService _config;

        private string _selectedLabel;

        public LabelsVM(IConfigService config)
        {
            _config = config;
            _selectedLabel = config.GetLastLabelToProcessSelected();
            LabelsToProcess = [.. config.LabelsToProcess];
        }

        #region Properties
        /// <summary>The label the preparation looks for; remembered in the options on every change.</summary>
        public string SelectedLabel
        {
            get => _selectedLabel;
            set
            {
                if (SetProperty(ref _selectedLabel, value))
                {
                    _config.UpdateLastLabelToProcessSelectedIndex(value);
                }
            }
        }
        #endregion

        #region ObservableProperties
        [ObservableProperty]
        private ObservableCollection<string> _labelsToProcess = [];

        [ObservableProperty]
        private string _newLabelToProcess = string.Empty;
        #endregion

        #region Commands
        [RelayCommand]
        private void AddNewLabel()
        {
            if (!string.IsNullOrEmpty(NewLabelToProcess))
            {
                _config.AddLabelToProcess(NewLabelToProcess);
                LabelsToProcess = [.. _config.LabelsToProcess];
                SelectedLabel = NewLabelToProcess;
                NewLabelToProcess = string.Empty;
            }
        }

        [RelayCommand]
        private void DeleteLabel()
        {
            if (LabelsToProcess.Count > 1)
            {
                _config.DeleteLabelToProcess(SelectedLabel);
                LabelsToProcess = [.. _config.LabelsToProcess];
                SelectedLabel = _config.LabelsToProcess[0];
            }
        }
        #endregion
    }
}
