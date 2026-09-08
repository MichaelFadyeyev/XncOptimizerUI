using System.Globalization;

namespace XncOptimizerUI.Helpers
{
    /// <summary>
    /// Single source of truth for parsing and formatting the decimal values typed into
    /// the filter inputs. Culture-independent on purpose: <c>.</c> is the only accepted
    /// decimal separator regardless of the OS regional format. A <c>,</c> is a validation
    /// error, not a separator; thousands separators are not accepted either.
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

            return decimal.TryParse(text.Trim(), Styles, CultureInfo.InvariantCulture, out result);
        }

        public static string Format(decimal value) =>
            value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Tidies the raw text of a filter bound for display: trims surrounding whitespace,
        /// strips trailing fractional zeros and a lone trailing separator ("0.500" -> "0.5",
        /// "1.00" -> "1", "18." -> "18"), prepends a "0" to a leading bare separator
        /// (".5" -> "0.5", "-.5" -> "-0.5"), and strips redundant leading zeros ("05" -> "5",
        /// "007" -> "7", "00.5" -> "0.5", "000" -> "0"). Parsing already tolerates all of
        /// these; this only changes what the TextBox shows. A "," is left untouched — it is
        /// an invalid character, not a separator.
        /// </summary>
        public static string Canonicalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var s = text.Trim();

            if (s.Contains('.'))
            {
                s = s.TrimEnd('0');

                if (s.EndsWith('.'))
                {
                    s = s[..^1];
                }
            }

            if (s.StartsWith('.'))
            {
                s = "0" + s;
            }
            else if (s.Length > 1 && (s[0] is '-' or '+') && s[1] == '.')
            {
                s = s[..1] + "0" + s[1..];
            }

            var sign = string.Empty;

            if (s.Length > 0 && (s[0] is '-' or '+'))
            {
                sign = s[..1];
                s = s[1..];
            }

            if (s.Length > 1 && s[0] == '0')
            {
                var withoutZeros = s.TrimStart('0');

                s = withoutZeros.Length == 0 ? "0"
                    : withoutZeros[0] == '.' ? "0" + withoutZeros
                    : withoutZeros;
            }

            return sign + s;
        }
    }
}
