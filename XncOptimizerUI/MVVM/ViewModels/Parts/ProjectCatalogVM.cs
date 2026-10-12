using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.MVVM.ViewModels.Parts
{
    /// <summary>The open project's edge bands, sheet materials and products.</summary>
    public sealed partial class ProjectCatalogVM : ObservableObject
    {
        #region ObservableProperties
        [ObservableProperty]
        private ObservableCollection<BandVM> _bands = [];

        /// <summary>
        /// Choices of the Parts grid edge-band selects: <see cref="BandOptionVM.None"/> followed
        /// by every band of <see cref="Bands"/>. Rebuilt with <see cref="Bands"/>.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<BandOptionVM> _bandOptions = [];

        [ObservableProperty]
        private ObservableCollection<SheetVM> _sheets = [];

        [ObservableProperty]
        private ObservableCollection<ProductVM> _products = [];

        [ObservableProperty]
        private BandVM? _selectedBand;
        #endregion

        #region Methods
        /// <summary>Reads the bands, sheets and products of the open project.</summary>
        public void Load(IProjectReader reader)
        {
            Bands = new ObservableCollection<BandVM>(reader.ReadBands().Select(b => new BandVM(b)));
            BandOptions = [BandOptionVM.None, .. Bands.Select(BandOptionVM.From)];
            Sheets = new ObservableCollection<SheetVM>(reader.ReadSheets().Select(s => new SheetVM(s)));
            Products = new ObservableCollection<ProductVM>(reader.ReadProducts().Select(p => new ProductVM(p)));
        }

        public void Clear()
        {
            SelectedBand = null;
            Bands = [];
            BandOptions = [];
            Sheets = [];
            Products = [];
        }

        /// <summary>The external symbol of band <paramref name="bandId"/>; empty for an unbanded edge.</summary>
        public string BandSymbol(int? bandId) =>
            bandId == null ? string.Empty : Bands.First(b => b.Id == bandId).ExternalSymbol;

        /// <summary>Sheet <paramref name="sheetId"/>, or <c>null</c> when the project has no such sheet.</summary>
        public SheetVM? FindSheet(int sheetId) => Sheets.FirstOrDefault(s => s.Id == sheetId);
        #endregion
    }
}
