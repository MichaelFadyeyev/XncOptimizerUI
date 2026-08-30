namespace XncOptimizerUI.Contracts
{
    /// <summary>
    /// Wraps the modal Win32/WPF surfaces the ViewModel needs. These block or
    /// require an STA message pump, so a headless test host cannot run a command
    /// that touches them directly.
    /// </summary>
    public interface IDialogService
    {
        /// <returns>The chosen path, or <c>null</c> if the user cancelled.</returns>
        string? ShowOpenProjectDialog();

        /// <returns>The chosen path, or <c>null</c> if the user cancelled.</returns>
        string? ShowSaveCsvDialog(string suggestedFileName);

        void SaveTextFile(string path, string content);

        void SetClipboardText(string text);

        void ShowInfo(string message, string title = "Success");

        void ShowWarning(string message, string title = "Warning");

        void ShowError(string message, string title = "Error");
    }
}
