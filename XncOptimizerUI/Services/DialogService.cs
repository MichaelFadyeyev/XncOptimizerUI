using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.Services
{
    /// <summary>
    /// Thin pass-through to the real dialogs. Intentionally holds no logic and
    /// no branching, so there is nothing here worth unit testing.
    /// </summary>
    public class DialogService : IDialogService
    {
        public string? ShowOpenProjectDialog()
        {
            var openDialog = new OpenFileDialog()
            {
                Filter = "GibLab project files (*.project)|*.project"
            };

            return openDialog.ShowDialog() == true ? openDialog.FileName : null;
        }

        public string? ShowSaveCsvDialog(string suggestedFileName)
        {
            var saveDialog = new SaveFileDialog()
            {
                Filter = "CSV file (*.csv)|*.csv",
                Title = "Save CSV File",
                FileName = suggestedFileName
            };

            return saveDialog.ShowDialog() == true ? saveDialog.FileName : null;
        }

        public void SaveTextFile(string path, string content)
        {
            File.WriteAllText(path, content, Encoding.UTF8);
        }

        public void SetClipboardText(string text)
        {
            Clipboard.SetText(text);
        }

        public void ShowInfo(string message, string title = "Success")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
