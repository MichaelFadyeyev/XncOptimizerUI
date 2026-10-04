using System.Globalization;
using System.Windows.Data;
using XncOptimizerUI.MVVM.ViewModels;

namespace XncOptimizerUI.Helpers
{
    /// <summary>
    /// Resolves an edge-banding EL operation id to its <see cref="BandOptionVM.Caption"/>.
    /// Values: <c>[0]</c> the <c>int?</c> banding id, <c>[1]</c> the band options to look it up in.
    /// Empty text when the edge is unbanded or the id is unknown.
    /// </summary>
    public class BandCaptionConverter : IMultiValueConverter
    {
        public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            return values is [int id, IEnumerable<BandOptionVM> options]
                ? options.FirstOrDefault(o => o.Id == id)?.Caption ?? string.Empty
                : string.Empty;
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
