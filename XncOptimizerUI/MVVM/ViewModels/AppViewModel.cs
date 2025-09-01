
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Core;
using XncOptimizerUI.Services;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public class AppViewModel : ObservableObject
    {
        private readonly static string _assembly = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
        private string _log = string.Empty;
        private string _fullPath = string.Empty;
        private string _filterName = string.Empty;
        private decimal? _filterLength;
        private decimal? _filterWidth;
        private string _selectedLabel = ConfigService.GetLastLabelToProcessSelected();
        private string _newLabelToProcess = string.Empty;
        private string _windowTitle = _assembly + " - No file selected";
        private bool _applyPartsFilter = true;

        private List<PartVM> _allParts = [];
        private ObservableCollection<string> _labelsToProcess = [.. ConfigService.LabelsToProcess];

        private ObservableCollection<PartVM> _parts = [];
        private ObservableCollection<BandVM> _bands = [];
        private ObservableCollection<SheetVM> _sheets = [];
        private PartVM? _selectedPart;
        private BandVM? _selectedBand;

        private RelayCommand? _openFile;
        private RelayCommand? _readParts;
        private RelayCommand? _executeOptimize;
        private RelayCommand? _executePrepForSplitAlongX;
        private RelayCommand? _executePrepForSplitAlongX2;
        private RelayCommand? _exportPartsList;
        private RelayCommand? _copyPartsList;

        private IProjectService _projectService = new GibLabProjectService();

        #region Props
        public string Log
        {
            get { return _log; }
            set { _log = value; OnPropertyChanged(); }
        }
        public string FullPath
        {
            get { return _fullPath; }
            set
            {
                _fullPath = value;
                SetWindowTitle();
                OnPropertyChanged();

                void SetWindowTitle()
                {
                    var fileName = string.IsNullOrEmpty(FullPath) ? "No file selected" : Path.GetFileName(FullPath);
                    WindowTile = $"{_assembly} - {fileName}";
                }
            }
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

        private static decimal? TryParseToDecimal(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var width))
            {
                return width;
            }
            else
            {
                return null;
            }
        }


        public string NewLabelToProcess
        {
            get { return _newLabelToProcess; }
            set { _newLabelToProcess = value; OnPropertyChanged(); }
        }
        public string SelectedLabel
        {
            get { return _selectedLabel; }
            set
            {
                _selectedLabel = value;
                ConfigService.UpdateLastLabelToProcessSelectedIndex(_selectedLabel);
                OnPropertyChanged();
            }
        }
        public string WindowTile
        {
            get
            {
                return _windowTitle;
            }
            set
            {
                _windowTitle = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<PartVM> Parts
        {
            get { return _parts; }
            set { _parts = value; OnPropertyChanged(); }
        }

        public ObservableCollection<BandVM> Bands
        {
            get { return _bands; }
            set { _bands = value; OnPropertyChanged(); }
        }

        public ObservableCollection<SheetVM> Sheets
        {
            get { return _sheets; }
            set { _sheets = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> LabelsToProcess
        {
            get { return _labelsToProcess; }
            set { _labelsToProcess = value; OnPropertyChanged(); }
        }

        public PartVM? SelectedPart
        {
            get { return _selectedPart; }

            set
            {
                if (_selectedPart != null)
                {
                    if (_projectService.UpdatePart(_selectedPart!.Part))
                    {
                        _projectService.SaveProject();
                        Log += $"Updates saved: {DateTime.Now.ToLocalTime()}\n";
                    }
                }

                _selectedPart = value; OnPropertyChanged();
            }
        }

        public BandVM? SelectedBand
        {
            get { return _selectedBand; }
            set { _selectedBand = value; OnPropertyChanged(); }
        }


        #endregion

        #region Commands
        public RelayCommand OpenFileCommand
        {
            get
            {
                return _openFile ??= new RelayCommand(obj =>
                {
                    var openDialog = new OpenFileDialog()
                    {
                        Filter = "GibLab project files (*.project)|*.project"
                    };
                    if (openDialog.ShowDialog() == true)
                    {
                        var fullPath = openDialog.FileName;
                        try
                        {
                            OpenFile(fullPath, true);
                        }
                        catch (Exception e)
                        {
                            Log = e.Message;
                        }
                    }

                    ReadItems();
                });
            }
        }

        public RelayCommand SaveFileCommand
        {
            get
            {
                return new RelayCommand(obj =>
                {
                    if (_fullPath == string.Empty)
                    {
                        Log += "No file selected!\n";

                        return;
                    }

                    _projectService.SaveProject();

                    Log += $"Saved file: {FullPath}\n";
                });
            }
        }

        public RelayCommand ReadPartsCommand
        {
            get
            {
                return _readParts ??= new RelayCommand(obj =>
                {
                    //FilterName = string.Empty;
                    ReadItems();
                });
            }
        }

        public RelayCommand ExecuteOptimizeCommand
        {
            get
            {
                return _executeOptimize ??= new RelayCommand(obj =>
                {
                    if (_fullPath == string.Empty)
                    {
                        Log += "No file selected!\n";
                        return;
                    }

                    var log = Log;

                    _projectService.GroupIdenticalElements(ref log);

                    Log = log;

                    OpenFile(_projectService.FullPath);
                    ReadItems();
                });
            }
        }

        public RelayCommand ExecutePrepForSplitAlongXCommand
        {
            get
            {
                return _executePrepForSplitAlongX ??= new RelayCommand(obj =>
                {
                    if (_fullPath == string.Empty)
                    {
                        Log += "No file selected!\n";
                        return;
                    }

                    var log = Log;

                    _projectService.PrepForSplitAlongX(ref log, "_поріз.2х40мм");

                    Log = log;

                    OpenFile(_projectService.FullPath);
                    ReadItems();
                });
            }
        }

        public RelayCommand ExecutePrepForSplitAlongXCommand2
        {
            get
            {

                return _executePrepForSplitAlongX2 ?? new RelayCommand(obj =>
                {
                    if (_fullPath == string.Empty)
                    {
                        Log += "No file selected!\n";
                        return;
                    }

                    var log = Log;

                    var ids = Parts
                        .Select(p => p.Id.ToString())
                        .ToArray() ?? [];

                    _projectService.PrepForSplitAlongX(ref log, ids);

                    Log = log;

                    OpenFile(_projectService.FullPath);
                    ReadItems();
                });
            }
        }

        public RelayCommand ExportPartsListCommand
        {
            get
            {
                return _exportPartsList ??= new RelayCommand(obj =>
                {
                    var partsCSV = GetPartsList(';');

                    var saveDialog = new SaveFileDialog()
                    {
                        Filter = "CSV file (*.csv)|*.csv",
                        Title = "Save CSV File",
                        FileName = Path.GetFileNameWithoutExtension(_fullPath)
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        File.WriteAllText(saveDialog.FileName, partsCSV, Encoding.UTF8);
                        MessageBox.Show("File saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            }
        }

        public RelayCommand CopyPartsListCommand
        {
            get
            {
                return _copyPartsList ??= new(obj =>
                {
                    var partsList = GetPartsList('\t');

                    Clipboard.SetText(partsList);
                    MessageBox.Show("PartsList was copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }

        public RelayCommand AddNewLabelCommand
        {
            get
            {
                return new RelayCommand(obj =>
                {
                    if (!string.IsNullOrEmpty(NewLabelToProcess))
                    {
                        ConfigService.AddLabelToProcess(NewLabelToProcess);
                        LabelsToProcess = [.. ConfigService.LabelsToProcess];
                        SelectedLabel = NewLabelToProcess;
                        NewLabelToProcess = string.Empty;
                    }
                });
            }
        }

        public RelayCommand DeleteLabelCommand
        {
            get
            {
                return new RelayCommand(obj =>
                {
                    if (LabelsToProcess.Count > 1)
                    {
                        ConfigService.DeleteLabelToProcess(SelectedLabel);
                        LabelsToProcess = [.. ConfigService.LabelsToProcess];
                        SelectedLabel = ConfigService.LabelsToProcess.First();
                    }
                });
            }
        }

        public RelayCommand CloseFileCommand
        {
            get
            {
                return new RelayCommand(obj =>
                {

                    _projectService.CloseProject();

                    Log = string.Empty;
                    FullPath = string.Empty;
                    FilterName = string.Empty;
                    FilterLength = null;
                    FilterWidth = null;
                    NewLabelToProcess = string.Empty;

                    Parts = [];
                    Bands = [];
                    Sheets = [];
                });
            }
        }

        public RelayCommand ClearFiltersCommand
        {
            get
            {
                return new RelayCommand(obj =>
                {
                    _applyPartsFilter = false;
                    FilterName = string.Empty;
                    FilterLength = string.Empty;
                    FilterWidth = string.Empty;
                    Parts = new ObservableCollection<PartVM>(_allParts);
                });
            }
        }

        public RelayCommand ApplyFilter
        {
            get
            {
                return new RelayCommand(obj =>
                {
                    if (_applyPartsFilter)
                    {
                        FilterParts();
                        return;
                    }

                    _applyPartsFilter = true;
                });
            }
        }

        #endregion

        #region Methods
        private void OpenFile(string fullPath, bool firstTimeOpen = default)
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

            _selectedPart = null;
            _applyPartsFilter = false;
            _allParts = [.. _projectService.ReadParts().Select(p => new PartVM(p))];

            for (var i = 0; i < _allParts.Count; i++)
            {
                _allParts[i].Number = i + 1;
            }

            Parts = new ObservableCollection<PartVM>(_allParts);
            //_applyPartsFilter = false;
            FilterName = string.Empty;
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

        #endregion
    }
}
