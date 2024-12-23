
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Markup;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Core;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.Services;
using AutoMapper;
using XncOptimizerUI.Configuration;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public class AppViewModel : ObservableObject
    {
        private string _log = string.Empty;
        private string _fullPath = string.Empty;
        private ObservableCollection<PartVM> _parts = [];
        private RelayCommand? _openFile;
        private RelayCommand? _readParts;
        private RelayCommand? _executeOptimize;
        private RelayCommand? _executePrepForSplitAlongX;
        private IProjectService _projectService = new GibLabProjectService();
        private IMapper? _mapper;

        public AppViewModel()
        {
            _mapper = AutoMapperConfiguration.InitializeAutoMapper();
        }

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
                    var parts = _projectService.ReadParts().Select(p => new PartVM(p));
                    Parts = new ObservableCollection<PartVM>(parts);
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
    }
}
