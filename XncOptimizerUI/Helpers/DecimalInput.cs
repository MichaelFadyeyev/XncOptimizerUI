using System.Globalization;

namespace XncOptimizerUI.Helpers
{
    /// <summary>
    /// Single source of truth for parsing and formatting the decimal values typed into
    /// the filter inputs. Culture-independent on purpose: the user may type either
    /// <c>.</c> or <c>,</c> as the decimal separator regardless of the OS regional
    /// format, and both are accepted. Thousands separators are not — part dimensions
    /// never use them, and treating <c>,</c> as a group separator is exactly the bug
    /// this replaces.
    /// </summary>
    public static class DecimalInput
    {
        private const NumberStyles Styles =
            NumberStyles.AllowLeadingSign
            | NumberStyles.AllowDecimalPoint
            | NumberStyles.AllowLeadingWhite
            | NumberStyles.AllowTrailingWhite;

        public static bool TryParse(string? text, out decimal result)
        {
            result = 0m;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalized = text.Trim().Replace(',', '.');

            return decimal.TryParse(normalized, Styles, CultureInfo.InvariantCulture, out result);
        }

        public static string Format(decimal value) =>
            value.ToString(CultureInfo.InvariantCulture);
    }
}
