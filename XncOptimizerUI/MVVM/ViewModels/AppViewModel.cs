
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public partial class AppViewModel : ObservableObject
    {
        private const string NoProgramsInfo = "Programs: -";

        private readonly string _assembly;
        private string _filterName = string.Empty;
        private decimal? _filterLength;
        private decimal? _filterWidth;
        private bool _applyPartsFilter = true;

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
        public string FilterLength
        {
            get { return _filterLength == null ? string.Empty : _filterLength.ToString()!; }
            set
            {
                _filterLength = TryParseToDecimal(value);
                OnPropertyChanged();

                if (_applyPartsFilter)
                {
                    FilterParts();
                    return;
                }

                _applyPartsFilter = true;
            }
        }
        public string FilterWidth
        {
            get
            {
                return _filterWidth == null ? string.Empty : _filterWidth.ToString()!;
            }
            set
            {
                _filterWidth = TryParseToDecimal(value);
                OnPropertyChanged();

                if (_applyPartsFilter)
                {
                    FilterParts();
                    return;
                }

                _applyPartsFilter = true;
            }
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
            FilterName = string.Empty;
            FilterLength = string.Empty;
            FilterWidth = string.Empty;
            NewLabelToProcess = string.Empty;

            SelectedPart = null;
            SelectedBand = null;
            SourcePart = null;

            Parts = [];
            Bands = [];
            Sheets = [];
        }

        [RelayCommand]
        private void ClearFilters()
        {
            _applyPartsFilter = false;
            FilterName = string.Empty;
            FilterLength = string.Empty;
            FilterWidth = string.Empty;
            Parts = new ObservableCollection<PartVM>(_allParts);
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            if (_applyPartsFilter)
            {
                FilterParts();
                return;
            }

            _applyPartsFilter = true;
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
            _allParts = [.. _projectService.ReadParts().Select(p => new PartVM(p))];

            for (var i = 0; i < _allParts.Count; i++)
            {
                _allParts[i].Number = i + 1;
            }

            Parts = new ObservableCollection<PartVM>(_allParts);
            FilterName = string.Empty;
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
                    && (_filterLength == null || p.Length == _filterLength)
                    && (_filterWidth == null || p.Width == _filterWidth))
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

        private static decimal? TryParseToDecimal(string value)
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var width))
            {
                return width;
            }
            else
            {
                return null;
            }
        }

        #endregion
    }
}
