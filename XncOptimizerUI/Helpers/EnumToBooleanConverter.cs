using System.Globalization;
using System.Windows.Data;

namespace XncOptimizerUI.Helpers
{
    /// <summary>
    /// Two-way converter for binding a group of <see cref="System.Windows.Controls.RadioButton"/>
    /// to a single enum property: <c>IsChecked</c> is true when the bound value equals the
    /// <c>ConverterParameter</c>, and checking a radio writes that parameter back.
    /// </summary>
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null && value.Equals(parameter);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true && parameter != null ? parameter : Binding.DoNothing;
        }
    }
}
