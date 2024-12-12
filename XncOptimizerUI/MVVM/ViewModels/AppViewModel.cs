
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
        private RelayCommand? _executeOperation;


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
                        FullPath = openDialog.FileName;
                        Log += $"Opened file: {FullPath}\n";
                    }
                });
            }
        }
        public RelayCommand ExecuteOperation
        {
            get
            {
                return _executeOperation ??= new RelayCommand(obj =>
                {
                    var @operator = new XncOperator(FullPath);
                    var log = Log;

                    @operator.Execute(ref log);

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
