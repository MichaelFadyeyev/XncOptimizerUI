using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels.Parts
{
    /// <summary>One sheet material of the project, for the Sheets grid.</summary>
    public class SheetVM(Sheet sheet) : ObservableObject
    {
        private readonly Sheet _sheet = sheet;

        public int Id
        {
            get => _sheet.Id;
            set => SetProperty(_sheet.Id, value, _sheet, (s, v) => s.Id = v);
        }

        public string Name
        {
            get => _sheet.Name;
            set => SetProperty(_sheet.Name, value, _sheet, (s, v) => s.Name = v);
        }

        public string Code
        {
            get => _sheet.Code;
            set => SetProperty(_sheet.Code, value, _sheet, (s, v) => s.Code = v);
        }

        public decimal Thickness
        {
            get => _sheet.Thickness;
            set => SetProperty(_sheet.Thickness, value, _sheet, (s, v) => s.Thickness = v);
        }
    }
}
