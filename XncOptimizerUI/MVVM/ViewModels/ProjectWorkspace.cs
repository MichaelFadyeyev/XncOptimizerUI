using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>
    /// The project file open in the window: opens, reopens and closes it, and reads its catalog
    /// and parts into the view models.
    /// </summary>
    public sealed partial class ProjectWorkspace(
        ProjectServices project,
        ActivityLog log,
        ProjectCatalogVM catalog,
        PartsListVM parts,
        PartsFilterVM filter) : ObservableObject
    {
        #region Properties
        /// <summary>Whether a project file is open.</summary>
        public bool IsOpen => !string.IsNullOrEmpty(FullPath);
        #endregion

        #region ObservableProperties
        /// <summary>Path of the open project file; empty when none is open.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOpen))]
        private string _fullPath = string.Empty;
        #endregion

        #region Methods
        /// <summary>Opens <paramref name="fullPath"/> with a fresh log.</summary>
        public void Open(string fullPath)
        {
            project.Session.Open(fullPath);
            log.Clear();
            ShowOpened(fullPath);
        }

        /// <summary>
        /// Opens the file the last operation saved (the session's path) and reads it in full; the
        /// log keeps the operation's report.
        /// </summary>
        public void Reopen()
        {
            var fullPath = project.Session.FullPath;

            project.Session.Open(fullPath);
            log.Append("\n***\n");
            ShowOpened(fullPath);
            ReadItems();
        }

        /// <summary>
        /// Reads the catalog and the parts of the open project, resets the filter and selects the
        /// first part, so the preview has something to draw the moment a project opens.
        /// </summary>
        public void ReadItems()
        {
            catalog.Load(project.Reader);

            parts.SelectedPart = null;
            parts.Load(project.Reader.ReadParts());
            filter.Reset();

            parts.SelectedPart = parts.Parts.FirstOrDefault();
        }

        public void Close()
        {
            project.Session.Close();

            log.Clear();
            FullPath = string.Empty;
            filter.Reset();
            parts.Clear();
            catalog.Clear();
        }

        private void ShowOpened(string fullPath)
        {
            FullPath = fullPath;
            log.Append($"Opened file: {FullPath}\n");
        }
        #endregion
    }
}
