using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.MVVM.ViewModels.Parts
{
    /// <summary>One edge band (EL operation) of the project, for the Bands grid and the band selects.</summary>
    public class BandVM(Band band) : ObservableObject
    {
        private readonly Band _band = band;

        public int Id
        {
            get => _band.Id;
            set => SetProperty(_band.Id, value, _band, (b, v) => b.Id = v);
        }

        public string Name
        {
            get => _band.Name;
            set => SetProperty(_band.Name, value, _band, (b, v) => b.Name = v);
        }

        public string Code
        {
            get => _band.Code;
            set => SetProperty(_band.Code, value, _band, (b, v) => b.Code = v);
        }

        public decimal Width
        {
            get => _band.Width;
            set => SetProperty(_band.Width, value, _band, (b, v) => b.Width = v);
        }

        public decimal Thickness
        {
            get => _band.Thickness;
            set => SetProperty(_band.Thickness, value, _band, (b, v) => b.Thickness = v);
        }

        public string InternalSymbol
        {
            get => _band.InternalSymbol;
            set => SetProperty(_band.InternalSymbol, value, _band, (b, v) => b.InternalSymbol = v);
        }

        public string ExternalSymbol
        {
            get => _band.ExternalSymbol;
            set => SetProperty(_band.ExternalSymbol, value, _band, (b, v) => b.ExternalSymbol = v);
        }
    }
}
