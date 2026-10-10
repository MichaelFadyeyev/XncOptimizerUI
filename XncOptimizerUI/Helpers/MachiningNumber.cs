using System.Globalization;

namespace XncOptimizerUI.Helpers
{
    /// <summary>Display format of machining values (coordinates, depths, diameters) in the program tables.</summary>
    public static class MachiningNumber
    {
        public static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
