using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.ViewModels.Panels;
using XncOptimizerUI.MVVM.ViewModels.Parts;
using XncOptimizerUI.MVVM.ViewModels.Preview;

namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>
    /// The main window: the project file's lifecycle (open, save, close, export the parts list),
    /// the log, and the view models of the window's areas.
    /// </summary>
    public partial class AppViewModel : ObservableObject
    {
        private readonly ApplicationInfo _application;
        private readonly ProjectServices _project;
        private readonly IDialogService _dialogs;
        private readonly TimeProvider _clock;

        public AppViewModel(
            ApplicationInfo application,
            ProjectServices project,
            IDialogService dialogs,
            TimeProvider clock,
            ActivityLog log,
            ProjectWorkspace workspace,
            ProjectCatalogVM catalog,
            PartsListVM partsList,
            PartsFilterVM filter,
            SelectedPartMachiningVM machining,
            PartPreviewVM preview,
            SourcePartVM sourcePart,
            PreparationPanelVM preparation,
            CleanOutsidePanelVM cleanOutside,
            SortToolsPanelVM sortTools,
            GrooveMillPanelVM grooveMill,
            BoreMillPanelVM boreMill,
            MillOffsetPanelVM millOffset,
            MillOrderPanelVM millOrder)
        {
            _application = application;
            _project = project;
            _dialogs = dialogs;
            _clock = clock;
            Log = log;
            Workspace = workspace;
            Catalog = catalog;
            PartsList = partsList;
            Filter = filter;
            Machining = machining;
            Preview = preview;
            SourcePart = sourcePart;
            Preparation = preparation;
            CleanOutside = cleanOutside;
            SortTools = sortTools;
            GrooveMill = grooveMill;
            BoreMill = boreMill;
            MillOffset = millOffset;
            MillOrder = millOrder;

            Workspace.PropertyChanged += OnWorkspacePropertyChanged;
        }

        #region Properties
        public string WindowTitle =>
            $"{_application.Name} - {(Workspace.IsOpen ? Path.GetFileName(Workspace.FullPath) : "No file selected")}";

        public ActivityLog Log { get; }

        public ProjectWorkspace Workspace { get; }

        public ProjectCatalogVM Catalog { get; }

        public PartsListVM PartsList { get; }

        public PartsFilterVM Filter { get; }

        public SelectedPartMachiningVM Machining { get; }

        public PartPreviewVM Preview { get; }

        public SourcePartVM SourcePart { get; }

        public PreparationPanelVM Preparation { get; }

        public CleanOutsidePanelVM CleanOutside { get; }

        public SortToolsPanelVM SortTools { get; }

        public GrooveMillPanelVM GrooveMill { get; }

        public BoreMillPanelVM BoreMill { get; }

        public MillOffsetPanelVM MillOffset { get; }

        public MillOrderPanelVM MillOrder { get; }
        #endregion

        #region Commands
        [RelayCommand]
        private void OpenFile()
        {
            var fullPath = _dialogs.ShowOpenProjectDialog();
            var opened = false;

            // Flush any pending auto-save against the file that's still open before
            // the workspace switches the session to the newly chosen document; otherwise
            // ReadItems()'s own SelectedPart = null autosaves the stale selection
            // against the wrong (just-opened) file.
            PartsList.SelectedPart = null;

            if (fullPath != null)
            {
                try
                {
                    Workspace.Open(fullPath);
                    opened = true;
                }
                catch (Exception e)
                {
                    Log.Text = e.Message;
                }
            }

            Workspace.ReadItems();

            if (opened)
            {
                WarnOnXncTurnDiscordance();
            }
        }

        [RelayCommand]
        private void SaveFile()
        {
            if (!Workspace.IsOpen)
            {
                Log.Append("No file selected!\n");
                return;
            }

            if (PartsList.SelectedPart != null)
            {
                Log.Append(_project.Editor.UpdatePart(PartsList.SelectedPart.Part));
            }

            _project.Session.Save();

            Log.Append($"Saved file: {Workspace.FullPath} at {_clock.GetLocalNow().DateTime}\n");
        }

        [RelayCommand]
        private void CloseFile()
        {
            PartsList.SelectedPart = null;
            SourcePart.SourcePart = null;
            Preparation.Labels.NewLabelToProcess = string.Empty;

            Workspace.Close();
        }

        [RelayCommand]
        private void ExportPartsList()
        {
            if (PartsList.Parts.Count == 0)
            {
                Log.Append("No parts to export!\n");
                return;
            }

            var partsCsv = PartsListText.Build(PartsList.Parts, Catalog, ';');
            var savePath = _dialogs.ShowSaveCsvDialog(Path.GetFileNameWithoutExtension(Workspace.FullPath));

            if (savePath != null)
            {
                _dialogs.SaveTextFile(savePath, partsCsv);
                _dialogs.ShowInfo("File saved successfully!");
            }
        }

        [RelayCommand]
        private void CopyPartsList()
        {
            if (PartsList.Parts.Count == 0)
            {
                Log.Append("No parts to copy!\n");
                return;
            }

            _dialogs.SetClipboardText(PartsListText.Build(PartsList.Parts, Catalog, '\t'));
            _dialogs.ShowInfo("PartsList was copied to clipboard!");
        }
        #endregion

        #region Methods
        private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectWorkspace.FullPath))
            {
                OnPropertyChanged(nameof(WindowTitle));
            }
        }

        /// <summary>
        /// After a project is opened, flag every part whose several XNC programs disagree on the
        /// <c>turn</c> corner (part orientation vs. the machine coordinate origin): pop a warning
        /// and log each offending part name.
        /// </summary>
        private void WarnOnXncTurnDiscordance()
        {
            var parts = _project.Reader.GetPartsWithXncTurnDiscordance();

            if (parts.Count == 0)
            {
                return;
            }

            var list = string.Join(", ", parts);

            Log.Append($"Discordance in xnc programs turn corner for parts: {list}\n");
            Log.Append(string.Concat(parts.Select(name => $"  turn discordance: {name}\n")));

            _dialogs.ShowWarning($"Discordance in xnc programs turn corner for parts: {list}");
        }
        #endregion
    }
}
