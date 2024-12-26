
using XncOptimizerUI.Core;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels
{
    public class BandVM(Band band) : ObservableObject
    {
        Band _band = band;

        public int Id
        {
            get { return _band.Id; }
            set { _band.Id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get { return _band.Name; }
            set { _band.Name = value; OnPropertyChanged(); }
        }

        public decimal Width
        {
            get { return _band.Width; }
            set { _band.Width = value; OnPropertyChanged(); }
        }

        public decimal Thickness
        {
            get { return _band.Thickness; }
            set { _band.Thickness = value; OnPropertyChanged(); }
        }

        public string InternalSymbol
        {
            get { return _band.InternalSymbol; }
            set { _band.InternalSymbol = value; OnPropertyChanged(); }
        }

        public string ExternalSymbol
        {
            get { return _band.ExternalSymbol; }
            set { _band.ExternalSymbol = value; OnPropertyChanged(); }
        }
    }
}
