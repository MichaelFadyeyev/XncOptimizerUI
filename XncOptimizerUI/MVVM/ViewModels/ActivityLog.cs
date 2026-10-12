using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>The text of the log box at the bottom of the window, written by every view model.</summary>
    public sealed partial class ActivityLog : ObservableObject
    {
        #region ObservableProperties
        [ObservableProperty]
        private string _text = string.Empty;
        #endregion

        #region Methods
        public void Append(string text) => Text += text;

        /// <summary>Appends what <paramref name="result"/> reports.</summary>
        public void Append(OperationResult result) => Append(result.LogText);

        public void Clear() => Text = string.Empty;
        #endregion
    }
}
