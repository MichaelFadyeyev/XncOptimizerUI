
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Threading;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Machining;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public partial class AppViewModel : ObservableObject
    {
        private const int BoundsNormalizeDelayMs = 400;

        private readonly string _assembly;
        private readonly DispatcherTimer _boundsNormalizeTimer;
        private readonly ProjectServices _project;
        private readonly IConfigService _config;
        private readonly IDialogService _dialogs;

        /// <summary>
        /// Part properties edited through in-cell controls (Txt checkbox, band selects). Clicking
        /// the Txt checkbox doesn't select its row, and a band pick should not wait for the
        /// selection-change auto-save either; these are saved as soon as they change instead.
        /// </summary>
        private static readonly HashSet<string> ImmediatelySavedPartProperties =
        [
            nameof(PartVM.ConsiderTexture),
            nameof(PartVM.TopBandingId),
            nameof(PartVM.BottomBandingId),
            nameof(PartVM.LeftBandingId),
            nameof(PartVM.RightBandingId),
        ];

        private string _filterName = string.Empty;

        // Each range bound keeps the raw text the user typed AND its parsed value. The text
        // is what the TextBox shows, so a half-typed "18." or a "," separator is not
        // reformatted away mid-edit; the decimal is what the filter and the equalize use.
        private string _lengthMinText = string.Empty;
        private string _lengthMaxText = string.Empty;
        private string _widthMinText = string.Empty;
        private string _widthMaxText = string.Empty;
        private decimal? _lengthMin;
        private decimal? _lengthMax;
        private decimal? _widthMin;
        private decimal? _widthMax;
        private bool _applyPartsFilter = true;

        private enum RangeEdge { None, Min, Max }

        private RangeEdge _lengthPendingEdge = RangeEdge.None;
        private RangeEdge _widthPendingEdge = RangeEdge.None;

        private List<PartVM> _allParts = [];
        private int _sourceXncCount;

        // Set while SelectedPartRotation is updated from the model (part selection, rollback),
        // so the change is not mistaken for an operator request to rotate the part.
        private bool _isSyncingRotation;

        public AppViewModel(
            ProjectServices project,
            IConfigService config,
            IDialogService dialogs,
            string assembly,
            ObservableCollection<string> labelsToProcess,
            string selectedLabel)
        {
            _project = project;
            _config = config;
            _dialogs = dialogs;
            _assembly = assembly;
            _windowTitle = _assembly + " - No file selected";
            _labelsToProcess = labelsToProcess;
            _selectedLabel = selectedLabel;
            _neverFlipTclOnTurn = config.NeverFlipTclOnTurn;

            _boundsNormalizeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(BoundsNormalizeDelayMs)
            };
            _boundsNormalizeTimer.Tick += (_, _) =>
            {
                _boundsNormalizeTimer.Stop();
                NormalizeRangeBounds();
            };
        }

        #region Properties
        public string FilterName
        {
            get { return _filterName; }
            set
            {
                _filterName = value;
                OnPropertyChanged(nameof(FilterName));

                if (_applyPartsFilter)
                {
                    FilterParts();
                    return;
                }

                _applyPartsFilter = true;
            }
        }
        public string LengthMin
        {
            get => _lengthMinText;
            set => SetRangeBound(ref _lengthMinText, ref _lengthMin, value, ref _lengthPendingEdge, RangeEdge.Min);
        }
        public string LengthMax
        {
            get => _lengthMaxText;
            set => SetRangeBound(ref _lengthMaxText, ref _lengthMax, value, ref _lengthPendingEdge, RangeEdge.Max);
        }
        public string WidthMin
        {
            get => _widthMinText;
            set => SetRangeBound(ref _widthMinText, ref _widthMin, value, ref _widthPendingEdge, RangeEdge.Min);
        }
        public string WidthMax
        {
            get => _widthMaxText;
            set => SetRangeBound(ref _widthMaxText, ref _widthMax, value, ref _widthPendingEdge, RangeEdge.Max);
        }

        /// <summary>
        /// Number of parts currently checked ("Sel") across the whole project, including
        /// any hidden by the active filter — this is the set the batch commands act on.
        /// </summary>
        public int CheckedCount => _allParts.Count(p => p.IsSelected);

        public string SourcePartInfo
        {
            get
            {
                var p = SourcePart;

                string Band(int? id) => id == null ? "-" : GetBandingExternalSymbol(id);

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

        /// <summary>
        /// Bores checked via the bores <c>DataGrid</c>'s checkbox column. Reset whenever the
        /// selected part changes. Not consumed by anything yet - reserved for future features.
        /// </summary>
        public CheckedSet<XncBore> CheckedBores { get; } = [];

        /// <summary>Grooves checked via the grooves table. Reset when the selected part changes.</summary>
        public CheckedSet<XncGrooving> CheckedGrooves { get; } = [];

        /// <summary>Contours checked via the milling contours table.</summary>
        public CheckedSet<XncMillingContour> CheckedMillingContours { get; } = [];

        /// <summary>Ellipses checked via the milling ellipses table.</summary>
        public CheckedSet<XncMillingEllipse> CheckedMillingEllipses { get; } = [];

        /// <summary>Rectangles checked via the milling rectangles table.</summary>
        public CheckedSet<XncMillingRectangle> CheckedMillingRectangles { get; } = [];
        #endregion

        #region ObservableProperties
        [ObservableProperty]
        private string _log = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        partial void OnFullPathChanged(string value)
        {
            var fileName = string.IsNullOrEmpty(value) ? "No file selected" : Path.GetFileName(value);
            WindowTitle = $"{_assembly} - {fileName}";
        }

        [ObservableProperty]
        private string _newLabelToProcess = string.Empty;

        [ObservableProperty]
        private string _selectedLabel;

        partial void OnSelectedLabelChanged(string value)
        {
            _config.UpdateLastLabelToProcessSelectedIndex(value);
        }

        [ObservableProperty]
        private string _windowTitle = string.Empty;

        [ObservableProperty]
        private ObservableCollection<PartVM> _parts = [];

        [ObservableProperty]
        private ObservableCollection<BandVM> _bands = [];

        /// <summary>
        /// Choices of the Parts grid edge-band selects: <see cref="BandOptionVM.None"/> followed
        /// by every band of <see cref="Bands"/>. Rebuilt with <see cref="Bands"/>.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<BandOptionVM> _bandOptions = [];

        [ObservableProperty]
        private ObservableCollection<SheetVM> _sheets = [];

        [ObservableProperty]
        private ObservableCollection<ProductVM> _products = [];

        [ObservableProperty]
        private ObservableCollection<string> _labelsToProcess;

        [ObservableProperty]
        private PartVM? _selectedPart;

        partial void OnSelectedPartChanging(PartVM? value)
        {
            if (_selectedPart != null)
            {
                SavePartEdits(_selectedPart);
            }
        }

        partial void OnSelectedPartChanged(PartVM? value)
        {
            RefreshSelectedPartMachining(value);
        }

        /// <summary>
        /// The bores of <see cref="SelectedPart"/>'s XNC programs, flattened across all programs
        /// and numbered in order, for the bores <c>DataGrid</c>. Refreshed whenever the selection
        /// changes.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<BoreRowVM> _selectedPartBores = [];

        /// <summary>
        /// Whether the bores <c>DataGrid</c> should be shown at all for <see cref="SelectedPart"/>.
        /// </summary>
        [ObservableProperty]
        private bool _hasSelectedPartBores;

        /// <summary>
        /// The bore row selected in the bores <c>DataGrid</c>; its X / Y / Depth cells are edited
        /// in place (see <see cref="OnBoreRowEdited"/>).
        /// </summary>
        [ObservableProperty]
        private BoreRowVM? _selectedBore;

        [ObservableProperty]
        private ObservableCollection<GrooveRowVM> _selectedPartGrooves = [];

        [ObservableProperty]
        private bool _hasSelectedPartGrooves;

        [ObservableProperty]
        private ObservableCollection<ToolRowVM> _selectedPartTools = [];

        [ObservableProperty]
        private bool _hasSelectedPartTools;

        /// <summary>
        /// The <c>&lt;var&gt;</c> declarations of <see cref="SelectedPart"/>'s XNC programs, numbered
        /// in order, for the Variables <c>DataGrid</c> (name / type / expr / comment editable).
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<VariableRowVM> _selectedPartVariables = [];

        [ObservableProperty]
        private bool _hasSelectedPartVariables;

        [ObservableProperty]
        private ObservableCollection<MillingContourRowVM> _selectedPartMillingContours = [];

        [ObservableProperty]
        private bool _hasSelectedPartMillingContours;

        [ObservableProperty]
        private ObservableCollection<MillingEllipseRowVM> _selectedPartMillingEllipses = [];

        [ObservableProperty]
        private bool _hasSelectedPartMillingEllipses;

        [ObservableProperty]
        private ObservableCollection<MillingRectangleRowVM> _selectedPartMillingRectangles = [];

        [ObservableProperty]
        private bool _hasSelectedPartMillingRectangles;

        /// <summary>
        /// Brief, one-line-per-feature summary of the XNC programs attached to
        /// <see cref="SelectedPart"/>. Refreshed whenever the selection changes.
        /// </summary>
        [ObservableProperty]
        private string _selectedPartPrograms = ProgramsSummary.NoPart;

        /// <summary>
        /// The XNC programs attached to <see cref="SelectedPart"/>, for the preview's
        /// machining overlay (e.g. bores). Refreshed whenever the selection changes.
        /// </summary>
        [ObservableProperty]
        private IReadOnlyList<XncProgram> _selectedXncPrograms = [];

        /// <summary>
        /// Length / width the part preview draws the selected part at: the first applied XNC
        /// program's <c>dx</c> / <c>dy</c> when the part has any program, otherwise the part's
        /// own <c>Length</c> / <c>Width</c>.
        /// </summary>
        [ObservableProperty]
        private double _selectedPartDisplayLength;

        [ObservableProperty]
        private double _selectedPartDisplayWidth;

        /// <summary>
        /// First applied XNC program's <c>turn</c> code (0..3) for the selected part, or
        /// <c>null</c> when it has no program. <see cref="SelectedPartTurnText"/> is the same
        /// value formatted as degrees for the preview.
        /// </summary>
        [ObservableProperty]
        private int? _selectedPartTurn;

        [ObservableProperty]
        private string _selectedPartTurnText = string.Empty;

        /// <summary>
        /// Orientation the rotation buttons show. Follows the selected part's first program turn;
        /// an operator change rotates every XNC program of the part to it and saves the file.
        /// </summary>
        [ObservableProperty]
        private PartTurn _selectedPartRotation;

        partial void OnSelectedPartRotationChanged(PartTurn oldValue, PartTurn newValue)
        {
            if (_isSyncingRotation)
            {
                return;
            }

            if (!RotateSelectedPart(newValue))
            {
                SyncSelectedPartRotation(oldValue);
            }
        }

        /// <summary>Whether the rotation buttons are enabled: a part with at least one XNC program is selected.</summary>
        [ObservableProperty]
        private bool _canRotateSelectedPart;

        /// <summary>
        /// Compatibility switch, persisted in the application options: part rotation keeps every
        /// edge groove's TCL (<c>c</c>) as authored, like GibLab, instead of flipping Right/Left
        /// to keep the groove on its physical side. Off by default.
        /// </summary>
        [ObservableProperty]
        private bool _neverFlipTclOnTurn;

        partial void OnNeverFlipTclOnTurnChanged(bool value)
        {
            _config.UpdateNeverFlipTclOnTurn(value);
        }

        [ObservableProperty]
        private BandVM? _selectedBand;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SourcePartInfo))]
        private PartVM? _sourcePart;

        partial void OnSourcePartChanged(PartVM? value)
        {
            _sourceXncCount = value == null ? 0 : _project.Reader.GetXncProgramsCount(value.Id);
        }

        [ObservableProperty]
        private GrooveMillDirection _grooveMillDirection = GrooveMillDirection.GroovesToMills;

        /// <summary>
        /// When set, a Mills → Grooves conversion also turns axis-parallel rectangular pocket
        /// mills into grooves. Opt-in: off by default.
        /// </summary>
        [ObservableProperty]
        private bool _processPockets;

        [ObservableProperty]
        private BoreMillDirection _boreMillDirection = BoreMillDirection.BoresToMills;

        /// <summary>
        /// When set, a Bores → Mills conversion emits an elliptical mill (<c>&lt;me&gt;</c>) per
        /// bore instead of a closed two-arc contour. Opt-in: off by default.
        /// </summary>
        [ObservableProperty]
        private bool _convertBoresToEllipses;

        /// <summary>Raw text of the mill path offset distance (mm); parsed when the command runs.</summary>
        [ObservableProperty]
        private string _millOffset = string.Empty;

        [ObservableProperty]
        private MillOffsetSide _millOffsetSide = MillOffsetSide.Right;

        /// <summary>Mill path offset processes open <c>&lt;ms&gt;</c> contours.</summary>
        [ObservableProperty]
        private bool _offsetOpenMillPaths = true;

        /// <summary>Mill path offset processes closed contours, rectangles and ellipses.</summary>
        [ObservableProperty]
        private bool _offsetClosedMillPaths = true;

        /// <summary>
        /// Tool sorting processes every part passing the current filter (all parts when no filter
        /// is set), checked or not, instead of only the checked ones. Opt-in: off by default.
        /// </summary>
        [ObservableProperty]
        private bool _sortToolsGlobally;

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
        [RelayCommand]
        private void OpenFile()
        {
            var fullPath = _dialogs.ShowOpenProjectDialog();
            var opened = false;

            // Flush any pending auto-save against the file that's still open before
            // OpenProject switches the service to the newly chosen document; otherwise
            // ReadItems()'s own SelectedPart = null autosaves the stale selection
            // against the wrong (just-opened) file.
            SelectedPart = null;

            if (fullPath != null)
            {
                try
                {
                    LoadProject(fullPath, true);
                    opened = true;
                }
                catch (Exception e)
                {
                    Log = e.Message;
                }
            }

            ReadItems();

            if (opened)
            {
                WarnOnXncTurnDiscordance();
            }
        }

        [RelayCommand]
        private void SaveFile()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";

                return;
            }

            if (SelectedPart != null)
            {
                Log += _project.Editor.UpdatePart(SelectedPart.Part).LogText;
            }

            _project.Session.Save();

            Log += $"Saved file: {FullPath} at {DateTime.Now.ToLocalTime()}\n";
        }

        [RelayCommand]
        private void ExecuteOptimize()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";
                return;
            }

            SelectedPart = null;

            RunSavingOperation(_project.Restructuring.GroupIdenticalElements);
        }

        [RelayCommand]
        private void ExecutePrepForSplitAlongX()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";
                return;
            }

            var ids = Parts
                .Where(p => p.Name.Contains(SelectedLabel))
                .Select(p => p.Id.ToString(CultureInfo.InvariantCulture))
                .ToArray();

            RunSavingOperation(() => _project.Restructuring.PrepForSplitAlongX(ids));
        }

        [RelayCommand]
        private void ExportPartsList()
        {
            if (Parts.Count == 0)
            {
                Log += "No parts to export!\n";
                return;
            }

            var partsCSV = GetPartsList(';');

            var savePath = _dialogs.ShowSaveCsvDialog(Path.GetFileNameWithoutExtension(FullPath));

            if (savePath != null)
            {
                _dialogs.SaveTextFile(savePath, partsCSV);
                _dialogs.ShowInfo("File saved successfully!");
            }
        }

        [RelayCommand]
        private void CopyPartsList()
        {
            if (Parts.Count == 0)
            {
                Log += "No parts to copy!\n";
                return;
            }

            var partsList = GetPartsList('\t');

            _dialogs.SetClipboardText(partsList);
            _dialogs.ShowInfo("PartsList was copied to clipboard!");
        }

        [RelayCommand]
        private void SetSourcePart()
        {
            if (SelectedPart == null)
            {
                Log += "No part selected to set as source!\n";
                return;
            }

            SourcePart = SelectedPart;
            Log += $"Source part set: {SelectedPart.Name}\n";
        }

        [RelayCommand]
        private void ResetSourcePart()
        {
            SourcePart = null;
        }

        [RelayCommand]
        private void ReplaceXNCs()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";
                return;
            }

            if (SourcePart == null)
            {
                Log += "No source part set!\n";
                return;
            }

            var targets = _allParts
                .Where(p => p.IsSelected && p != SourcePart)
                .Select(p => p.Part)
                .ToList();

            if (targets.Count == 0)
            {
                Log += "No parts selected to replace XNCs for!\n";
                return;
            }

            if (RunSavingOperation(() => _project.Restructuring.ReplaceXncPrograms(SourcePart.Part, targets)))
            {
                SourcePart = null;
            }
        }

        [RelayCommand]
        private void ConvertGroovesAndMills()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";
                return;
            }

            var parts = _allParts
                .Where(p => p.IsSelected)
                .Select(p => p.Part)
                .ToList();

            if (parts.Count == 0)
            {
                Log += "No parts checked for groove/mill conversion!\n";
                return;
            }

            RunSavingOperation(() => _project.Batch.ConvertGroovesAndMills(parts, GrooveMillDirection, ProcessPockets));
        }

        [RelayCommand]
        private void ConvertBoresAndMills()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";
                return;
            }

            var parts = _allParts
                .Where(p => p.IsSelected)
                .Select(p => p.Part)
                .ToList();

            if (parts.Count == 0)
            {
                Log += "No parts checked for bore/mill conversion!\n";
                return;
            }

            RunSavingOperation(() => _project.Batch.ConvertBoresAndMills(parts, BoreMillDirection, ConvertBoresToEllipses));
        }

        [RelayCommand]
        private void OptimizeMillTraversal()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";
                return;
            }

            var parts = _allParts
                .Where(p => p.IsSelected)
                .Select(p => p.Part)
                .ToList();

            if (parts.Count == 0)
            {
                Log += "No parts checked for mill order optimization!\n";
                return;
            }

            RunSavingOperation(() => _project.Batch.OptimizeMillTraversal(parts));
        }

        [RelayCommand]
        private void SortToolDeclarations()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";
                return;
            }

            // Globally: every part the grid shows - the filtered set, or all parts when unfiltered.
            var parts = (SortToolsGlobally ? Parts : _allParts.Where(p => p.IsSelected))
                .Select(p => p.Part)
                .ToList();

            if (parts.Count == 0)
            {
                Log += SortToolsGlobally
                    ? "No parts pass the filter for tool sorting!\n"
                    : "No parts checked for tool sorting!\n";
                return;
            }

            RunSavingOperation(() => _project.Batch.SortToolDeclarations(parts));
        }

        [RelayCommand]
        private void OffsetMillPaths()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";
                return;
            }

            if (TryParseToDecimal(MillOffset) is not { } offset || offset <= 0m)
            {
                Log += "Enter a mill path offset greater than 0!\n";
                return;
            }

            var kinds = (OffsetOpenMillPaths ? MillPathKinds.Open : MillPathKinds.None)
                | (OffsetClosedMillPaths ? MillPathKinds.Closed : MillPathKinds.None);

            if (kinds == MillPathKinds.None)
            {
                Log += "Select open and/or closed mill paths to offset!\n";
                return;
            }

            var parts = _allParts
                .Where(p => p.IsSelected)
                .Select(p => p.Part)
                .ToList();

            if (parts.Count == 0)
            {
                Log += "No parts checked for mill path offset!\n";
                return;
            }

            RunSavingOperation(() => _project.Batch.OffsetMillPaths(parts, (double)offset, MillOffsetSide, kinds));
        }

        /// <summary>
        /// Removes the selected kinds of operations lying outside their part from the checked parts,
        /// or from every part of the project when none is checked.
        /// </summary>
        [RelayCommand]
        private void CleanOutsideOperations()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";
                return;
            }

            var kinds = (CleanOutsideBores ? OutsideOperationKinds.Bores : OutsideOperationKinds.None)
                | (CleanOutsideGrooves ? OutsideOperationKinds.Grooves : OutsideOperationKinds.None)
                | (CleanOutsideMills ? OutsideOperationKinds.Mills : OutsideOperationKinds.None);

            if (kinds == OutsideOperationKinds.None)
            {
                Log += "Select bores, grooves and/or mills to clean!\n";
                return;
            }

            var checkedParts = _allParts.Where(p => p.IsSelected).ToList();
            var parts = (checkedParts.Count > 0 ? checkedParts : _allParts)
                .Select(p => p.Part)
                .ToList();

            if (parts.Count == 0)
            {
                Log += "No parts to clean operations for!\n";
                return;
            }

            RunSavingOperation(() => _project.Batch.CleanOutsideOperations(parts, kinds));
        }

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

        [RelayCommand]
        private void CloseFile()
        {
            SelectedPart = null;
            SelectedBand = null;
            SourcePart = null;

            _project.Session.Close();

            Log = string.Empty;
            FullPath = string.Empty;
            _applyPartsFilter = false;
            FilterName = string.Empty;
            _applyPartsFilter = false;
            LengthMin = string.Empty;
            _applyPartsFilter = false;
            LengthMax = string.Empty;
            _applyPartsFilter = false;
            WidthMin = string.Empty;
            _applyPartsFilter = false;
            WidthMax = string.Empty;
            NewLabelToProcess = string.Empty;
            CancelBoundsNormalize();

            RebindCheckedCount(_allParts, []);
            _allParts = [];

            Parts = [];
            Bands = [];
            BandOptions = [];
            Sheets = [];
            Products = [];
        }

        [RelayCommand]
        private void ClearFilters()
        {
            _applyPartsFilter = false;
            FilterName = string.Empty;
            _applyPartsFilter = false;
            LengthMin = string.Empty;
            _applyPartsFilter = false;
            LengthMax = string.Empty;
            _applyPartsFilter = false;
            WidthMin = string.Empty;
            _applyPartsFilter = false;
            WidthMax = string.Empty;
            CancelBoundsNormalize();
            Parts = new ObservableCollection<PartVM>(_allParts);
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            NormalizeRangeBounds();

            if (_applyPartsFilter)
            {
                FilterParts();
                return;
            }

            _applyPartsFilter = true;
        }

        [RelayCommand]
        private void CheckAll() => SetChecked(true);

        [RelayCommand]
        private void UncheckAll() => SetChecked(false);
        #endregion

        #region Methods
        private void SetRangeBound(ref string text, ref decimal? parsed, string? value,
            ref RangeEdge pendingEdge, RangeEdge edge, [CallerMemberName] string? propertyName = null)
        {
            // Store the raw text verbatim. "." is the only decimal separator; "," is a
            // validation error (DecimalValidationRule rejects it, so it never reaches here
            // through the binding). Trimming and canonicalization happen only on commit
            // (NormalizeRangeBounds), so they cannot eat a digit still being typed.
            text = value ?? string.Empty;
            parsed = TryParseToDecimal(text);
            OnPropertyChanged(propertyName);

            pendingEdge = edge;
            RestartBoundsNormalizeTimer();

            if (_applyPartsFilter)
            {
                FilterParts();
                return;
            }

            _applyPartsFilter = true;
        }

        private void RestartBoundsNormalizeTimer()
        {
            _boundsNormalizeTimer.Stop();
            _boundsNormalizeTimer.Start();
        }

        private void CancelBoundsNormalize()
        {
            _boundsNormalizeTimer.Stop();
            _lengthPendingEdge = RangeEdge.None;
            _widthPendingEdge = RangeEdge.None;
        }

        /// <summary>
        /// Snaps an inverted [min, max] pair together, moving the edge the user did NOT
        /// just edit. Runs off the debounce timer so it acts on the fully typed value
        /// rather than each intermediate digit. Public so the Filter button and tests
        /// can force it without waiting for the timer.
        /// </summary>
        public void NormalizeRangeBounds()
        {
            CanonicalizeBoundText(ref _lengthMinText, nameof(LengthMin));
            CanonicalizeBoundText(ref _lengthMaxText, nameof(LengthMax));
            CanonicalizeBoundText(ref _widthMinText, nameof(WidthMin));
            CanonicalizeBoundText(ref _widthMaxText, nameof(WidthMax));

            var changed = NormalizePair(
                ref _lengthMin, ref _lengthMinText, nameof(LengthMin),
                ref _lengthMax, ref _lengthMaxText, nameof(LengthMax), _lengthPendingEdge);
            changed |= NormalizePair(
                ref _widthMin, ref _widthMinText, nameof(WidthMin),
                ref _widthMax, ref _widthMaxText, nameof(WidthMax), _widthPendingEdge);

            _lengthPendingEdge = RangeEdge.None;
            _widthPendingEdge = RangeEdge.None;

            if (changed)
            {
                FilterParts();
            }
        }

        // Canonicalization (trim, trailing-separator strip) never changes the parsed value,
        // so the decimal? fields stay correct and this does not need to re-run the filter.
        private void CanonicalizeBoundText(ref string text, string propertyName)
        {
            var canonical = DecimalInput.Canonicalize(text);

            if (canonical != text)
            {
                text = canonical;
                OnPropertyChanged(propertyName);
            }
        }

        private bool NormalizePair(
            ref decimal? min, ref string minText, string minProp,
            ref decimal? max, ref string maxText, string maxProp,
            RangeEdge edited)
        {
            if (edited == RangeEdge.None || !min.HasValue || !max.HasValue || min <= max)
            {
                return false;
            }

            if (edited == RangeEdge.Min)
            {
                max = min;
                maxText = DecimalInput.Format(max.Value);
                OnPropertyChanged(maxProp);
            }
            else
            {
                min = max;
                minText = DecimalInput.Format(min.Value);
                OnPropertyChanged(minProp);
            }

            return true;
        }

        // Acts on the currently displayed (filtered) parts only, mirroring the "Filtered no:"
        // label. Each IsSelected write raises PartVM.PropertyChanged, which refreshes CheckedCount.
        private void SetChecked(bool value)
        {
            foreach (var part in Parts)
            {
                part.IsSelected = value;
            }
        }

        private void LoadProject(string fullPath, bool firstTimeOpen = default)
        {
            _project.Session.Open(fullPath);

            if (firstTimeOpen)
            {
                Log = string.Empty;
            }
            else
            {
                Log += "\n***\n";
            }

            FullPath = fullPath;
            Log += $"Opened file: {FullPath}\n";
        }

        private void ReadItems()
        {
            var bands = _project.Reader.ReadBands().Select(b => new BandVM(b));
            Bands = new ObservableCollection<BandVM>(bands);
            BandOptions = [BandOptionVM.None, .. Bands.Select(BandOptionVM.From)];

            var sheets = _project.Reader.ReadSheets().Select(s => new SheetVM(s));
            Sheets = new ObservableCollection<SheetVM>(sheets);

            var products = _project.Reader.ReadProducts().Select(p => new ProductVM(p));
            Products = new ObservableCollection<ProductVM>(products);

            SelectedPart = null;
            _applyPartsFilter = false;

            var previousParts = _allParts;
            _allParts = [.. _project.Reader.ReadParts().Select(p => new PartVM(p))];

            for (var i = 0; i < _allParts.Count; i++)
            {
                _allParts[i].Number = i + 1;
            }

            RebindCheckedCount(previousParts, _allParts);

            // Each Filter*/*Min/*Max setter re-arms _applyPartsFilter, so disarm before every
            // assignment to keep the reset from triggering a mid-load FilterParts pass.
            _applyPartsFilter = false;
            LengthMin = string.Empty;
            _applyPartsFilter = false;
            LengthMax = string.Empty;
            _applyPartsFilter = false;
            WidthMin = string.Empty;
            _applyPartsFilter = false;
            WidthMax = string.Empty;
            _applyPartsFilter = false;
            CancelBoundsNormalize();

            Parts = new ObservableCollection<PartVM>(_allParts);
            FilterName = string.Empty;

            // Pre-select the first part so the preview viewport has something to draw
            // the moment a project opens. _selectedPart was just set to null above, so
            // OnSelectedPartChanging skips its auto-save on this assignment.
            SelectedPart = Parts.FirstOrDefault();
        }

        /// <summary>
        /// Keeps <see cref="CheckedCount"/> live: the label must react to every "Sel" checkbox
        /// toggle in the grid, not just to Check all / Uncheck all. Detaches the handler from the
        /// old part VMs, attaches it to the new ones, and refreshes the count.
        /// </summary>
        private void RebindCheckedCount(IEnumerable<PartVM> oldParts, IEnumerable<PartVM> newParts)
        {
            foreach (var part in oldParts)
            {
                part.PropertyChanged -= OnPartVmPropertyChanged;
            }

            foreach (var part in newParts)
            {
                part.PropertyChanged += OnPartVmPropertyChanged;
            }

            OnPropertyChanged(nameof(CheckedCount));
        }

        private void OnPartVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PartVM.IsSelected))
            {
                OnPropertyChanged(nameof(CheckedCount));
            }
            else if (sender is PartVM part && e.PropertyName is { } name && ImmediatelySavedPartProperties.Contains(name))
            {
                SavePartEdits(part);
            }
        }

        /// <summary>
        /// Writes <paramref name="part"/>'s pending grid edits (name, size, Txt, bands) into the
        /// document and saves the file in place when anything changed.
        /// </summary>
        private void SavePartEdits(PartVM part)
        {
            var result = _project.Editor.UpdatePart(part.Part);

            Log += result.LogText;

            if (result.Changed)
            {
                _project.Session.Save();
                Log += $"Updates saved: {DateTime.Now.ToLocalTime()}\n";
            }
        }

        /// <summary>
        /// The service layer reports failures by appending to the log rather than
        /// popping its own dialog, so the text it just appended is what the user
        /// needs to see.
        /// </summary>
        private void WarnAbout(OperationResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.MessageText))
            {
                _dialogs.ShowWarning(result.MessageText);
            }
        }

        /// <summary>
        /// Runs an operation that saves its result as a new file: logs it, then opens the saved file,
        /// or - when it changed nothing - shows its messages as a warning. Returns whether it changed the project.
        /// </summary>
        private bool RunSavingOperation(Func<OperationResult> operation)
        {
            var result = operation();

            Log += result.LogText;

            if (!result.Changed)
            {
                WarnAbout(result);
                return false;
            }

            LoadProject(_project.Session.FullPath);
            ReadItems();

            return true;
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

            Log += $"Discordance in xnc programs turn corner for parts: {list}\n";
            foreach (var name in parts)
            {
                Log += $"  turn discordance: {name}\n";
            }

            _dialogs.ShowWarning($"Discordance in xnc programs turn corner for parts: {list}");
        }

        private void FilterParts()
        {
            if (_allParts.Count == 0) return;

            Parts = new ObservableCollection<PartVM>(
                _allParts.Where(p => (string.IsNullOrEmpty(_filterName) || p.Name.Contains(_filterName))
                    && (_lengthMin == null || p.Length >= _lengthMin)
                    && (_lengthMax == null || p.Length <= _lengthMax)
                    && (_widthMin == null || p.Width >= _widthMin)
                    && (_widthMax == null || p.Width <= _widthMax))
                );
        }

        private string GetPartsList(char sep)
        {
            // todo: add cells with formulas
            // $"=IF(ISNUMBER(AN{row});AN{row}-AU{row};VALUE(LEFT(AN{row};SEARCH(" ";AN{row})-1))-AU{row})	=IF(ISNUMBER(AO{row});AO{row}-AV{row};VALUE(LEFT(AO{row};SEARCH(" ";AO{row})-1))-AV{row})	=L{row}-AW{row}	=IF(ISNUMBER(AN{row});AND(ISBLANK(AX{row});ISBLANK(AY{row}));EXACT(RIGHT(AN{row};LEN(AN{row})-SEARCH(" ";AN{row}));CONCAT(AX{row};AY{row};)))	=IF(ISNUMBER(AO{row});AND(ISBLANK(AZ{row});ISBLANK(BA{row}));EXACT(RIGHT(AO{row};LEN(AO{row})-SEARCH(" ";AO{row}));CONCAT(AZ{row};BA{row};)))"

            var partsList = new StringBuilder();
            var newLine = '\n';

            foreach (var part in Parts)
            {
                partsList.AppendFormat(
                    CultureInfo.CurrentCulture,
                    "{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}",
                    sep,
                    part.Length,
                    part.Width,
                    part.Count,
                    GetBandingExternalSymbol(part.TopBandingId),
                    GetBandingExternalSymbol(part.BottomBandingId),
                    GetBandingExternalSymbol(part.LeftBandingId),
                    GetBandingExternalSymbol(part.RightBandingId),
                    part.Name,
                    newLine
                    );

                var partSheet = Sheets.First(s => s.Id == part.SheetId);

                if (partSheet.Name.Contains("Сращ.(2)"))
                {
                    partsList.AppendFormat(CultureInfo.InvariantCulture, "{0}{0}{0}{0}{0}{0}{0}-{1}", sep, newLine);
                }

            }

            partsList.Remove(partsList.Length - 1, 1);

            return partsList.ToString();
        }

        private string GetBandingExternalSymbol(int? bandingId)
        {
            return bandingId == null ? string.Empty : Bands.First(b => b.Id == bandingId).ExternalSymbol;
        }

        /// <summary>The "XNC List" text of the selected part's programs (see <see cref="ProgramsSummary"/>).</summary>
        private string BuildSelectedPartPrograms(
            PartVM? part, IReadOnlyList<XncProgram> programs, string? readError) =>
            part is null || string.IsNullOrEmpty(FullPath)
                ? ProgramsSummary.NoPart
                : ProgramsSummary.Describe(programs, readError);

        /// <summary>
        /// One numbered row per element that <paramref name="elements"/> picks from each program,
        /// across all of the selected part's programs in order.
        /// </summary>
        private static ObservableCollection<TRow> BuildRows<TElement, TRow>(
            IReadOnlyList<XncProgram> programs,
            Func<XncProgram, IEnumerable<TElement>> elements,
            Func<int, XncProgram, TElement, TRow> createRow) =>
            new(programs
                .SelectMany(program => elements(program).Select(element => (Program: program, Element: element)))
                .Select((entry, i) => createRow(i + 1, entry.Program, entry.Element)));

        private ObservableCollection<BoreRowVM> BuildSelectedPartBoreRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.Bores, (number, program, bore) => new BoreRowVM(
                number,
                MachiningDisplay.BoreSide(bore, program),
                program.Tools.FirstOrDefault(t => t.Name == bore.ToolName)?.Diameter is { } diameter ? Num(diameter) : null,
                bore,
                program,
                CheckedBores.Set,
                OnBoreRowEdited));

        /// <summary>
        /// Writes one committed bore cell edit into its program, saves the file in place (together
        /// with any pending grid edit of the part) and refreshes the bores and the preview.
        /// Returns <c>false</c> when nothing was written, so the cell stays in edit mode.
        /// </summary>
        private bool OnBoreRowEdited(BoreRowVM row, BoreAttribute attribute, string expression) =>
            SaveProgramEdit(
                SelectedXncPrograms.FirstOrDefault(p => p.Bores.Contains(row.Bore))?.OperationId,
                $"Bore #{row.Number} {attribute.XmlName()} = \"{expression}\"",
                operationId => _project.Editor.UpdateBore(operationId, row.Bore.Index, attribute, expression),
                RefreshSelectedPartBoresInPlace);

        /// <summary>
        /// Writes one committed variable cell edit into its program, saves the file in place and
        /// refreshes everything derived from the programs (a variable may drive bores, mills, ...).
        /// Returns <c>false</c> when nothing was written, so the cell stays in edit mode.
        /// </summary>
        private bool OnVariableRowEdited(VariableRowVM row, XncVariableAttribute attribute, string value) =>
            SaveProgramEdit(
                SelectedXncPrograms.FirstOrDefault(p => p.DeclaredVariables.Contains(row.Variable))?.OperationId,
                $"Variable #{row.Number} {attribute.XmlName()} = \"{value}\"",
                operationId => _project.Editor.UpdateVariable(operationId, row.Variable.Index, attribute, value),
                RefreshSelectedPartAfterVariableEdit);

        /// <summary>
        /// Applies <paramref name="edit"/> to the program of XNC operation <paramref name="operationId"/>
        /// of the selected part, saves the file in place (see <see cref="SaveSelectedPartEdit"/>) and
        /// runs <paramref name="refresh"/>.
        /// </summary>
        private bool SaveProgramEdit(int? operationId, string description, Func<int, OperationResult> edit, Action<PartVM> refresh)
        {
            var part = SelectedPart;

            if (part is null || operationId is null || string.IsNullOrEmpty(FullPath))
            {
                return false;
            }

            if (!SaveSelectedPartEdit(part, () => edit(operationId.Value), description))
            {
                return false;
            }

            refresh(part);

            return true;
        }

        /// <summary>
        /// Applies an in-place <paramref name="edit"/> of the selected part, together with any pending grid
        /// edit of the part, and saves the file in place; logs <paramref name="description"/> with the save
        /// outcome. Returns <c>false</c> when the edit changed nothing.
        /// </summary>
        private bool SaveSelectedPartEdit(PartVM part, Func<OperationResult> edit, string description)
        {
            Log += _project.Editor.UpdatePart(part.Part).LogText;

            var result = edit();

            Log += result.LogText;

            if (!result.Changed)
            {
                return false;
            }

            try
            {
                _project.Session.Save();
                Log += $"{description}, saved: {DateTime.Now.ToLocalTime()}\n";
            }
            catch (Exception e)
            {
                Log += $"***\n{description}, but saving failed: {e.Message}\n";
            }

            return true;
        }

        /// <summary>
        /// Re-reads <paramref name="part"/>'s programs after a bore edit and updates the bore rows
        /// in place - the collection is not replaced, because this runs while the bores
        /// <c>DataGrid</c> is still ending its cell edit (replacing its items source then throws),
        /// and in-place rows keep the grid's selection and the checkboxes. Bores do not affect the
        /// other machining tables, so those are left alone.
        /// </summary>
        private void RefreshSelectedPartBoresInPlace(PartVM part)
        {
            var programs = _project.Reader.ReadXncPrograms(part.Id);
            var bores = programs.SelectMany(program => program.Bores.Select(bore => (Bore: bore, Program: program))).ToList();

            if (bores.Count != SelectedPartBores.Count)
            {
                RefreshSelectedPartMachining(part);
                return;
            }

            SelectedPartPrograms = BuildSelectedPartPrograms(part, programs, null);
            SelectedXncPrograms = programs;

            SelectedPartBores
                .Zip(bores)
                .ToList()
                .ForEach(pair => ApplyBoreRow(pair.First, pair.Second.Bore, pair.Second.Program));
        }

        /// <summary>Refreshes one row and keeps <see cref="CheckedBores"/> pointing at its current bore.</summary>
        private void ApplyBoreRow(BoreRowVM row, XncBore bore, XncProgram program)
        {
            CheckedBores.Replace(row.Bore, bore);
            row.Apply(bore, program);
        }

        private static ObservableCollection<ToolRowVM> BuildSelectedPartToolRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.Tools,
                (number, program, tool) => new ToolRowVM(number, MachiningDisplay.SideName(program.Side), tool));

        private ObservableCollection<VariableRowVM> BuildSelectedPartVariableRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.DeclaredVariables,
                (number, program, variable) => new VariableRowVM(
                    number, MachiningDisplay.SideName(program.Side), variable, program, OnVariableRowEdited));

        private ObservableCollection<GrooveRowVM> BuildSelectedPartGrooveRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.Groovings,
                (number, program, groove) => new GrooveRowVM(
                    number, MachiningDisplay.GrooveSide(groove, program), groove, CheckedGrooves.Set));

        private ObservableCollection<MillingContourRowVM> BuildSelectedPartMillingContourRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.MillingContours,
                (number, program, contour) => new MillingContourRowVM(
                    number, MachiningDisplay.SideName(program.Side), contour, CheckedMillingContours.Set));

        private ObservableCollection<MillingEllipseRowVM> BuildSelectedPartMillingEllipseRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.MillingEllipses,
                (number, program, ellipse) => new MillingEllipseRowVM(
                    number, MachiningDisplay.SideName(program.Side), ellipse, CheckedMillingEllipses.Set));

        private ObservableCollection<MillingRectangleRowVM> BuildSelectedPartMillingRectangleRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.MillingRectangles,
                (number, program, rectangle) => new MillingRectangleRowVM(
                    number, MachiningDisplay.SideName(program.Side), rectangle, CheckedMillingRectangles.Set));

        /// <summary>
        /// Sets the preview's display dimensions and turn readout from the selected part's XNC
        /// programs: the first applied program's <c>dx</c> / <c>dy</c> and <c>turn</c> when the
        /// part has any program, otherwise the part's own length / width and no turn.
        /// </summary>
        private void UpdateSelectedPartDisplay(PartVM? part, IReadOnlyList<XncProgram> programs)
        {
            if (part is null)
            {
                SelectedPartDisplayLength = 0;
                SelectedPartDisplayWidth = 0;
                SelectedPartTurn = null;
                SelectedPartTurnText = string.Empty;
                CanRotateSelectedPart = false;
                SyncSelectedPartRotation(PartTurn.Deg0);
                return;
            }

            var length = (double)part.Length;
            var width = (double)part.Width;
            int? turn = null;

            if (programs.Count > 0)
            {
                var first = programs[0];

                if (first.Dx > 0 && first.Dy > 0)
                {
                    length = first.Dx;
                    width = first.Dy;
                }

                turn = first.Turn;
                SelectedPartTurnText = $"{first.TurnDegrees}°";
            }
            else
            {
                // No XNC program -> no turn code, but the preview still reads 0° (untouched frame).
                SelectedPartTurnText = "0°";
            }

            SelectedPartDisplayLength = length;
            SelectedPartDisplayWidth = width;
            SelectedPartTurn = turn;
            CanRotateSelectedPart = programs.Count > 0;
            SyncSelectedPartRotation(ToPartTurn(turn ?? 0));
        }

        /// <summary>
        /// Re-reads the XNC programs of <paramref name="part"/> and rebuilds everything derived
        /// from them: programs summary, preview inputs, rotation state and the machining tables.
        /// </summary>
        private void RefreshSelectedPartMachining(PartVM? part)
        {
            var (programs, readError) = ReadSelectedPartPrograms(part);

            ShowSelectedPartMachining(part, programs, readError);
            SelectedPartVariables = BuildSelectedPartVariableRows(programs);
            HasSelectedPartVariables = SelectedPartVariables.Count > 0;
        }

        /// <summary>
        /// Re-reads <paramref name="part"/>'s programs after a variable edit and rebuilds every
        /// table derived from them except the variables, whose rows are updated in place (this
        /// runs while the Variables <c>DataGrid</c> is still ending its cell edit, when replacing
        /// its items source throws).
        /// </summary>
        private void RefreshSelectedPartAfterVariableEdit(PartVM part)
        {
            var (programs, readError) = ReadSelectedPartPrograms(part);
            var variables = programs.SelectMany(p => p.DeclaredVariables.Select(v => (Variable: v, Program: p))).ToList();

            if (readError is not null || variables.Count != SelectedPartVariables.Count)
            {
                RefreshSelectedPartMachining(part);
                return;
            }

            ShowSelectedPartMachining(part, programs, null);

            SelectedPartVariables
                .Zip(variables)
                .ToList()
                .ForEach(pair => pair.First.Apply(pair.Second.Variable, pair.Second.Program));
        }

        private (IReadOnlyList<XncProgram> Programs, string? ReadError) ReadSelectedPartPrograms(PartVM? part)
        {
            if (part is null || string.IsNullOrEmpty(FullPath))
            {
                return ([], null);
            }

            try
            {
                return (_project.Reader.ReadXncPrograms(part.Id), null);
            }
            catch (Exception e)
            {
                return ([], e.Message);
            }
        }

        /// <summary>Programs summary, preview inputs, rotation state and the machining tables (all but the variables).</summary>
        private void ShowSelectedPartMachining(PartVM? part, IReadOnlyList<XncProgram> programs, string? readError)
        {
            SelectedPartPrograms = BuildSelectedPartPrograms(part, programs, readError);
            SelectedXncPrograms = programs;
            UpdateSelectedPartDisplay(part, programs);

            CheckedBores.Clear();
            CheckedGrooves.Clear();
            CheckedMillingContours.Clear();
            CheckedMillingEllipses.Clear();
            CheckedMillingRectangles.Clear();
            SelectedPartBores = BuildSelectedPartBoreRows(programs);
            HasSelectedPartBores = SelectedPartBores.Count > 0;
            SelectedPartGrooves = BuildSelectedPartGrooveRows(programs);
            HasSelectedPartGrooves = SelectedPartGrooves.Count > 0;
            SelectedPartTools = BuildSelectedPartToolRows(programs);
            HasSelectedPartTools = SelectedPartTools.Count > 0;
            SelectedPartMillingContours = BuildSelectedPartMillingContourRows(programs);
            HasSelectedPartMillingContours = SelectedPartMillingContours.Count > 0;
            SelectedPartMillingEllipses = BuildSelectedPartMillingEllipseRows(programs);
            HasSelectedPartMillingEllipses = SelectedPartMillingEllipses.Count > 0;
            SelectedPartMillingRectangles = BuildSelectedPartMillingRectangleRows(programs);
            HasSelectedPartMillingRectangles = SelectedPartMillingRectangles.Count > 0;
        }

        /// <summary>
        /// Turns the selected part's XNC programs to <paramref name="target"/>, saves the file in
        /// place (together with any pending grid edit of the part) and refreshes the preview and
        /// tables. Returns <c>false</c> when nothing was rotated, so the caller restores the buttons.
        /// </summary>
        private bool RotateSelectedPart(PartTurn target)
        {
            var part = SelectedPart;

            if (part is null || string.IsNullOrEmpty(FullPath))
            {
                return false;
            }

            var rotated = SaveSelectedPartEdit(
                part,
                () => _project.Editor.RotatePart(part.Id, (int)target, flipEdgeGrooveTcl: !NeverFlipTclOnTurn),
                $"Part \"{part.Name}\" rotated to {(int)target * 90}°");

            if (rotated)
            {
                RefreshSelectedPartMachining(part);
            }

            return rotated;
        }

        private void SyncSelectedPartRotation(PartTurn value)
        {
            _isSyncingRotation = true;

            try
            {
                SelectedPartRotation = value;
            }
            finally
            {
                _isSyncingRotation = false;
            }
        }

        private static PartTurn ToPartTurn(int turn) => (PartTurn)(((turn % 4) + 4) % 4);

        private static string Num(double value) => MachiningNumber.Format(value);

        private static decimal? TryParseToDecimal(string value) =>
            DecimalInput.TryParse(value, out var result) ? result : null;

        #endregion
    }
}
