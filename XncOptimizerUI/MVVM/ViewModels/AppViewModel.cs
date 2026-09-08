
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

namespace XncOptimizerUI.MVVM.ViewModels
{
    public partial class AppViewModel : ObservableObject
    {
        private const string NoProgramsInfo = "Programs: -";

        private const int BoundsNormalizeDelayMs = 400;

        private readonly string _assembly;
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

        private readonly DispatcherTimer _boundsNormalizeTimer;
        private RangeEdge _lengthPendingEdge = RangeEdge.None;
        private RangeEdge _widthPendingEdge = RangeEdge.None;

        private List<PartVM> _allParts = [];
        private int _sourceXncCount;

        private readonly IProjectService _projectService;
        private readonly IConfigService _config;
        private readonly IDialogService _dialogs;

        public AppViewModel(
            IProjectService projectService,
            IConfigService config,
            IDialogService dialogs,
            string assembly,
            ObservableCollection<string> labelsToProcess,
            string selectedLabel)
        {
            _projectService = projectService;
            _config = config;
            _dialogs = dialogs;
            _assembly = assembly;
            _windowTitle = _assembly + " - No file selected";
            _labelsToProcess = labelsToProcess;
            _selectedLabel = selectedLabel;

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

        #region Props
        [ObservableProperty]
        private string _log = string.Empty;

        [ObservableProperty]
        private string _fullPath = string.Empty;

        partial void OnFullPathChanged(string value)
        {
            var fileName = string.IsNullOrEmpty(value) ? "No file selected" : Path.GetFileName(value);
            WindowTitle = $"{_assembly} - {fileName}";
        }

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

        /// <summary>
        /// Number of parts currently checked ("Sel") across the whole project, including
        /// any hidden by the active filter — this is the set the batch commands act on.
        /// </summary>
        public int CheckedCount => _allParts.Count(p => p.IsSelected);

        [ObservableProperty]
        private ObservableCollection<BandVM> _bands = [];

        [ObservableProperty]
        private ObservableCollection<SheetVM> _sheets = [];

        [ObservableProperty]
        private ObservableCollection<string> _labelsToProcess;

        [ObservableProperty]
        private PartVM? _selectedPart;

        partial void OnSelectedPartChanging(PartVM? value)
        {
            if (_selectedPart != null)
            {
                var log = Log;

                if (_projectService.UpdatePart(ref log, _selectedPart.Part))
                {
                    _projectService.SaveProject();
                    log += $"Updates saved: {DateTime.Now.ToLocalTime()}\n";
                }

                Log = log;
            }
        }

        partial void OnSelectedPartChanged(PartVM? value)
        {
            SelectedPartPrograms = BuildSelectedPartPrograms(value);
        }

        /// <summary>
        /// Brief, one-line-per-feature summary of the XNC programs attached to
        /// <see cref="SelectedPart"/>. Refreshed whenever the selection changes.
        /// </summary>
        [ObservableProperty]
        private string _selectedPartPrograms = NoProgramsInfo;

        [ObservableProperty]
        private BandVM? _selectedBand;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SourcePartInfo))]
        private PartVM? _sourcePart;

        partial void OnSourcePartChanged(PartVM? value)
        {
            _sourceXncCount = value == null ? 0 : _projectService.GetXncProgramsCount(value.Id);
        }

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


        #endregion

        #region Commands
        [RelayCommand]
        private void OpenFile()
        {
            var fullPath = _dialogs.ShowOpenProjectDialog();

            if (fullPath != null)
            {
                try
                {
                    LoadProject(fullPath, true);
                }
                catch (Exception e)
                {
                    Log = e.Message;
                }
            }

            ReadItems();
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
                var log = Log;
                _ = _projectService.UpdatePart(ref log, SelectedPart.Part);
                Log = log;
            }

            _projectService.SaveProject();

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

            var log = Log;
            var logStart = log.Length;

            var success = _projectService.GroupIdenticalElements(ref log);

            Log = log;

            if (!success)
            {
                WarnFromLogDelta(logStart);
                return;
            }

            LoadProject(_projectService.FullPath);
            ReadItems();
        }

        [RelayCommand]
        private void ExecutePrepForSplitAlongX()
        {
            if (FullPath == string.Empty)
            {
                Log += "No file selected!\n";
                return;
            }

            var log = Log;

            var ids = Parts
                .Where(p => p.Name.Contains(SelectedLabel))
                .Select(p => p.Id.ToString())
                .ToArray() ?? [];

            _projectService.PrepForSplitAlongX(ref log, ids);

            Log = log;

            LoadProject(_projectService.FullPath);
            ReadItems();
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

            var log = Log;
            var logStart = log.Length;

            var success = _projectService.ReplaceXncPrograms(ref log, SourcePart.Part, targets);

            Log = log;

            if (!success)
            {
                WarnFromLogDelta(logStart);
                return;
            }

            SourcePart = null;
            LoadProject(_projectService.FullPath);
            ReadItems();
        }

        [ObservableProperty]
        private GrooveMillDirection _grooveMillDirection = GrooveMillDirection.GroovesToMills;

        /// <summary>
        /// When set, a Mills → Grooves conversion also turns axis-parallel rectangular pocket
        /// mills into grooves. Opt-in: off by default.
        /// </summary>
        [ObservableProperty]
        private bool _processPockets;

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

            var log = Log;
            var logStart = log.Length;

            var success = _projectService.ConvertGroovesAndMills(ref log, parts, GrooveMillDirection, ProcessPockets);

            Log = log;

            if (!success)
            {
                WarnFromLogDelta(logStart);
                return;
            }

            LoadProject(_projectService.FullPath);
            ReadItems();
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

            var log = Log;
            var logStart = log.Length;

            var success = _projectService.OptimizeMillTraversal(ref log, parts);

            Log = log;

            if (!success)
            {
                WarnFromLogDelta(logStart);
                return;
            }

            LoadProject(_projectService.FullPath);
            ReadItems();
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
                SelectedLabel = _config.LabelsToProcess.First();
            }
        }

        [RelayCommand]
        private void CloseFile()
        {
            _projectService.CloseProject();

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

            SelectedPart = null;
            SelectedBand = null;
            SourcePart = null;

            RebindCheckedCount(_allParts, []);
            _allParts = [];

            Parts = [];
            Bands = [];
            Sheets = [];
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

        // Acts on the currently displayed (filtered) parts only, mirroring the "Filtered no:"
        // label. Each IsSelected write raises PartVM.PropertyChanged, which refreshes CheckedCount.
        private void SetChecked(bool value)
        {
            foreach (var part in Parts)
            {
                part.IsSelected = value;
            }
        }

        #endregion

        #region Methods
        private void LoadProject(string fullPath, bool firstTimeOpen = default)
        {
            _projectService.OpenProject(fullPath);

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
            var bands = _projectService.ReadBands().Select(b => new BandVM(b));
            Bands = new ObservableCollection<BandVM>(bands);

            var sheets = _projectService.ReadSheets().Select(s => new SheetVM(s));
            Sheets = new ObservableCollection<SheetVM>(sheets);

            SelectedPart = null;
            _applyPartsFilter = false;

            var previousParts = _allParts;
            _allParts = [.. _projectService.ReadParts().Select(p => new PartVM(p))];

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
        }

        /// <summary>
        /// The service layer reports failures by appending to the log rather than
        /// popping its own dialog, so the text it just appended is what the user
        /// needs to see.
        /// </summary>
        private void WarnFromLogDelta(int logStart)
        {
            var message = Log[logStart..].Replace("***\n", string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(message))
            {
                _dialogs.ShowWarning(message);
            }
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
                    partsList.AppendFormat("{0}{0}{0}{0}{0}{0}{0}-{1}", sep, newLine);
                }

            }

            partsList.Remove(partsList.Length - 1, 1);

            return partsList.ToString();
        }

        private string GetBandingExternalSymbol(int? bandingId)
        {
            return bandingId == null ? string.Empty : Bands.First(b => b.Id == bandingId).ExternalSymbol;
        }

        /// <summary>
        /// Reads the selected part's XNC programs and renders them as brief lines of
        /// the shape <c>#/type/side/&lt;params&gt;</c> (one per tool, bore, groove,
        /// milling contour and rectangle), preceded by a <c>Programs: N</c> header.
        /// </summary>
        private string BuildSelectedPartPrograms(PartVM? part)
        {
            if (part is null || string.IsNullOrEmpty(FullPath))
            {
                return NoProgramsInfo;
            }

            IReadOnlyList<XncProgram> programs;

            try
            {
                programs = _projectService.ReadXncPrograms(part.Id);
            }
            catch (Exception e)
            {
                return $"Programs: <read error: {e.Message}>";
            }

            if (programs.Count == 0)
            {
                return "Programs: none";
            }

            var sb = new StringBuilder();
            sb.Append("Programs: ").Append(programs.Count).Append('\n');

            var n = 0;

            foreach (var program in programs)
            {
                var side = program.Side ? "front" : "back";

                void Line(string type, string parameters) => sb
                    .Append(++n).Append('/').Append(type).Append('/').Append(side).Append('/')
                    .Append(parameters).Append('\n');

                Line("xnc", $"dx{Num(program.Dx)} dy{Num(program.Dy)} dz{Num(program.Dz)}");

                foreach (var tool in program.Tools)
                {
                    Line("tool", $"{tool.Name} Ø{Num(tool.Diameter)}");
                }

                foreach (var bore in program.Bores)
                {
                    var through = bore.Through ? " through" : string.Empty;
                    Line("bore", $"{bore.Surface} {bore.ToolName} "
                        + $"({Num(bore.X)},{Num(bore.Y)},{Num(bore.Z)}) dp{Num(bore.Depth)}{through}");
                }

                foreach (var groove in program.Groovings)
                {
                    Line("groove", $"{groove.ToolName} "
                        + $"({Num(groove.Start.X)},{Num(groove.Start.Y)})-({Num(groove.End.X)},{Num(groove.End.Y)}) "
                        + $"dp{Num(groove.Depth)} w{Num(groove.Width)} {groove.Position}");
                }

                foreach (var contour in program.MillingContours)
                {
                    Line("mill", $"{contour.ToolName} "
                        + $"({Num(contour.Entry.X)},{Num(contour.Entry.Y)}) dp{Num(contour.EntryDepth)} "
                        + $"{contour.Position} {DescribeSegments(contour.Segments)}");
                }

                foreach (var rect in program.MillingRectangles)
                {
                    Line("rect", $"{rect.ToolName} "
                        + $"({Num(rect.Origin.X)},{Num(rect.Origin.Y)}) {Num(rect.Length)}x{Num(rect.Width)} "
                        + $"dp{Num(rect.Depth)} {rect.Position}");
                }
            }

            return sb.ToString().TrimEnd('\n');
        }

        private static string DescribeSegments(IReadOnlyList<XncMillingSegment> segments)
        {
            var lines = segments.Count(s => s is XncLineSegment);
            var arcs = segments.Count(s => s is XncArcSegment);

            var parts = new List<string>(2);
            if (lines > 0) parts.Add($"{lines} line");
            if (arcs > 0) parts.Add($"{arcs} arc");

            return parts.Count == 0 ? "0 seg" : string.Join("+", parts);
        }

        private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static decimal? TryParseToDecimal(string value) =>
            DecimalInput.TryParse(value, out var result) ? result : null;

        #endregion
    }
}
