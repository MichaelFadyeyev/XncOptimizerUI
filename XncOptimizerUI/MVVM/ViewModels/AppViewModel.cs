
using Microsoft.Win32;
using System.Diagnostics;
using XmlOperator;
using XncOptimizerUI.Core;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public class AppViewModel : ObservableObject
    {
        private Context _context;
        private RelayCommand? _openFile;
        private RelayCommand? _executeOptimize;
        private RelayCommand? _executePrepForSplitAlongX;

        private XncOperator _operator = new();


        public string Log
        {
            get { return _context.Log; }
            set { _context.Log = value; OnPropertyChanged(); }
        }
        public string FullPath
        {
            get { return _context.FullPath; }
            set { _context.FullPath = value; OnPropertyChanged(); }
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
                            _operator.OpenProject(openDialog.FileName);
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

        public RelayCommand ExecuteOptimize
        {
            get
            {

                return _executeOptimize ??= new RelayCommand(obj =>
                {
                    if (_context.FullPath == string.Empty)
                    {
                        Log += "No file selected!\n";
                        return;
                    }

                    var log = Log;

                    _operator.Optimize(ref log);

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
                    if (_context.FullPath == string.Empty)
                    {
                        Log += "No file selected!\n";
                        return;
                    }

                    var log = Log;

                    _operator.PrepForSplitAlongX(ref log, "_поріз.2х40мм");

                    Log = log;
                });
            }
        }



        public AppViewModel(Context context)
        {
            _context = context;
        }
    }
}
