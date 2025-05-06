
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Core;
using XncOptimizerUI.Services;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public class AppViewModel : ObservableObject
    {
        private string _log = string.Empty;
        private string _fullPath = string.Empty;

        private ObservableCollection<PartVM> _parts = [];
        private ObservableCollection<BandVM> _bands = [];
        private ObservableCollection<SheetVM> _sheets = [];
        private PartVM? _selectedPart;
        private BandVM? _selectedBand;

        private RelayCommand? _openFile;
        private RelayCommand? _readParts;
        private RelayCommand? _executeOptimize;
        private RelayCommand? _executePrepForSplitAlongX;
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
            set { _fullPath = value; OnPropertyChanged(); }
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

        public PartVM? SelectedPart
        {
            get { return _selectedPart; }
            set { _selectedPart = value; OnPropertyChanged(); }
        }
        public BandVM? SelectedBand
        {
            get { return _selectedBand; }
            set { _selectedBand = value; OnPropertyChanged(); }
        }
        #endregion

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
                        try
                        {
                            _projectService.OpenProject(openDialog.FileName);
                            Log = string.Empty;
                            FullPath = openDialog.FileName;
                            Log += $"Opened file: {FullPath}\n";
                        }
                        catch (Exception e)
                        {
                            Log = e.Message;
                        }
                    }

                    ReadParts();
                });
            }
        }

        public RelayCommand ReadPartsCommand
        {
            get
            {
                return _readParts ??= new RelayCommand(obj =>
                {
                    ReadParts();
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

        private void ReadParts()
        {
            var bands = _projectService.ReadBands().Select(b => new BandVM(b));
            Bands = new ObservableCollection<BandVM>(bands);

            var sheets = _projectService.ReadSheets().Select(s => new SheetVM(s));
            Sheets = new ObservableCollection<SheetVM>(sheets);

            var parts = _projectService.ReadParts().Select(p => new PartVM(p));
            Parts = new ObservableCollection<PartVM>(parts);

            for (var i = 0; i < Parts.Count; i++)
            {
                Parts[i].Number = i + 1;
            }
        }

        private string GetPartsList(char sep)
        {
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
    }
}
