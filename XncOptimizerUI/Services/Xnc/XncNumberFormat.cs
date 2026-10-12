using System.Xml;
using System.Xml.Linq;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>How the XNC program rewriters write recalculated values back.</summary>
    internal static class XncNumberFormat
    {
        /// <summary>Decimal places a rewriter writes recalculated values with.</summary>
        public const int OutputDecimals = 4;

        /// <summary>Formats a recalculated value: rounded to <see cref="OutputDecimals"/>, invariant, no <c>-0</c>.</summary>
        public static string FormatNumber(double value)
        {
            var rounded = Math.Round(value, OutputDecimals);

            return XmlConvert.ToString(rounded == 0d ? 0d : rounded);
        }

        /// <summary>Writes a changed value; an unchanged one keeps its authored text (e.g. an expression such as <c>dx+10</c>).</summary>
        public static void SetNumber(XElement element, string attribute, double value, double original)
        {
            if (FormatNumber(value) != FormatNumber(original))
            {
                element.SetAttributeValue(attribute, FormatNumber(value));
            }
        }
    }
}
