namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>
    /// One choice of the Parts grid edge-band selects: an EL operation (by id, shown by its
    /// internal symbol and band name) or <see cref="None"/>, which leaves the edge unbanded.
    /// </summary>
    public sealed record BandOptionVM(int? Id, string InternalSymbol, string Name)
    {
        public static BandOptionVM None { get; } = new(null, string.Empty, string.Empty);

        public static BandOptionVM From(BandVM band) => new(band.Id, band.InternalSymbol, band.Name);

        public string Caption => Id == null ? string.Empty : $"{InternalSymbol} {Name}";
    }
}
