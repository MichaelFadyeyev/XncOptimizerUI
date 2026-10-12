using System.Globalization;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Parsing of plain (non-expression) attribute values of XNC program elements, shared by the
    /// reader, the rewriters, the outside-operation detector and the tool sorter. Numbers are
    /// invariant-culture.
    /// </summary>
    internal static class XncAttributeParsing
    {
        public static double? TryParseDouble(string? raw) =>
            double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

        public static int? TryParseInt(string? raw) =>
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

        /// <summary>The number <paramref name="raw"/>; <paramref name="where"/> names it in the error when it is missing or not a number.</summary>
        /// <exception cref="XncProgramFormatException"><paramref name="raw"/> is missing or not a number.</exception>
        public static double RequireDouble(string? raw, string where) =>
            TryParseDouble(raw) ?? throw new XncProgramFormatException($"{where} is missing or not a number (was '{raw}').");

        /// <summary>A size attribute (<c>dx</c>/<c>dy</c>/<c>dz</c>) of <c>&lt;program&gt;</c>, which must be a plain number.</summary>
        /// <exception cref="XncProgramFormatException">The attribute is missing or not a number.</exception>
        public static double RequireDimension(XElement program, string attribute) =>
            RequireDouble(program.Attribute(attribute)?.Value, $"<program> @{attribute}");

        public static int ParseInt(string? raw, int fallback = 0) => TryParseInt(raw) ?? fallback;

        public static bool ParseBool(string? raw, bool fallback) => bool.TryParse(raw, out var value) ? value : fallback;

        /// <summary>The tool position <c>c</c>; absent or unknown codes read as <see cref="ToolPosition.Center"/>.</summary>
        public static ToolPosition ParsePositionCode(string? raw) =>
            TryParseInt(raw) is { } code && Enum.IsDefined(typeof(ToolPosition), code)
                ? (ToolPosition)code
                : ToolPosition.Center;

        /// <summary>An arc's <c>dir</c>: <c>true</c> is a clockwise sweep; absent or anything else is counter-clockwise.</summary>
        public static bool IsClockwise(XElement arc) => ParseBool(arc.GetDirValue(), false);

        /// <summary>The bore array count <c>ac</c> (absent = 1), or <c>null</c> when it is not a positive integer.</summary>
        public static int? ArrayCount(XElement bore) => bore.Attribute("ac")?.Value is not { } raw
            ? 1
            : TryParseInt(raw) is { } count && count > 0 ? count : null;

        /// <summary>A <c>&lt;tool&gt;</c>'s diameter <c>d</c>, or <c>null</c> when it is not a number.</summary>
        public static double? ToolDiameter(XElement tool) => TryParseDouble(tool.GetDValue());

        /// <summary>Diameter (<c>d</c>) of every <c>&lt;tool&gt;</c> of a program by name (any case); the last declaration wins, matching XncProgramReader.</summary>
        public static Dictionary<string, double> ReadToolDiameters(XElement program)
        {
            var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var tool in program.Elements(XncTags.Tool))
            {
                if (tool.GetNameValue() is { } name && ToolDiameter(tool) is { } diameter)
                {
                    map[name] = diameter;
                }
            }

            return map;
        }
    }
}
