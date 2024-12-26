
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
        private PartVM? _selectedPart;
        private BandVM? _selectedBand;

        private RelayCommand? _openFile;
        private RelayCommand? _readParts;
        private RelayCommand? _executeOptimize;
        private RelayCommand? _executePrepForSplitAlongX;
        private RelayCommand? _exportPartsList;

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

        public RelayCommand OpenFile
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
                });
            }
        }

        public RelayCommand ReadParts
        {
            get
            {
                return _readParts ??= new RelayCommand(obj =>
                {
                    var bands = _projectService.ReadBands().Select(b => new BandVM(b));
                    Bands = new ObservableCollection<BandVM>(bands);

                    var parts = _projectService.ReadParts().Select(p => new PartVM(p));
                    Parts = new ObservableCollection<PartVM>(parts);

                    for (var i = 0; i < Parts.Count; i++)
                    {
                        Parts[i].Number = i + 1;
                    }
                });
            }
        }

        public RelayCommand ExecuteOptimize
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

        public RelayCommand ExecutePrepForSplitAlongX
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

        public RelayCommand ExportPartsList
        {
            get
            {
                return _exportPartsList ??= new RelayCommand(obj =>
                {
                    var partsCSV = string.Empty;

                    foreach (var part in Parts)
                    {
                        partsCSV += $"{part.Length};";
                        partsCSV += $"{part.Width};";
                        partsCSV += $"{part.Count};";
                        partsCSV += $"{part.TopBandingMat};";
                        partsCSV += $"{part.BottomBandingMat};";
                        partsCSV += $"{part.LeftBandingMat};";
                        partsCSV += $"{part.RightBandingMat};";
                        partsCSV += $"{part.Name};\n";
                    }

                    var saveDialog = new SaveFileDialog() {
                        Filter = "CSV file (*.csv)|*.csv",
                        Title = "Save CSV File"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        File.WriteAllText(saveDialog.FileName, partsCSV, Encoding.UTF8);
                        MessageBox.Show("File saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            }
        }
    }
}
