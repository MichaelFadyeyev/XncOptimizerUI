
using XncOptimizerUI.Core;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public class SheetVM(Sheet sheet) : ObservableObject
    {
        Sheet _sheet = sheet;

        public int Id
        {
            get { return _sheet.Id; }
            set { _sheet.Id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get { return _sheet.Name; }
            set { _sheet.Name = value; OnPropertyChanged(); }
        }

        public string Code
        {
            get { return _sheet.Code; }
            set { _sheet.Code = value; OnPropertyChanged(); }
        }

        public decimal Thickness
        {
            get { return _sheet.Thickness; }
            set { _sheet.Thickness = value; OnPropertyChanged(); }
        }
    }
}
