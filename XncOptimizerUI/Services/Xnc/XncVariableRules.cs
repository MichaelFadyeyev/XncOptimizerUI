using System.Text.RegularExpressions;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Rules for a <c>&lt;var&gt;</c> edited in the Variables table. Shared by the table (live
    /// input check) and <see cref="Contracts.IProjectService.UpdateVariable"/> (authoritative check
    /// before writing).
    /// <list type="bullet">
    /// <item><c>name</c>: a letter, then letters, digits, <c>_</c> or <c>.</c>; unique in the program
    /// (any case); not one of the built-in <c>dx</c>/<c>dy</c>/<c>dz</c>/<c>tool.dia</c>.</item>
    /// <item><c>expr</c> by <c>type</c>: <c>double</c> - a <see cref="NumericExpression"/> over
    /// <c>dx</c>/<c>dy</c>/<c>dz</c> and the numeric vars declared before it; <c>int</c> - the same,
    /// evaluating to a whole number; <c>bool</c> - <c>true</c> or <c>false</c>; <c>string</c> - any text.</item>
    /// </list>
    /// </summary>
    public static class XncVariableRules
    {
        private static readonly string[] BuiltInSymbols = ["dx", "dy", "dz", "tool.dia"];

        private static readonly Regex NamePattern = new(@"^\p{L}[\p{L}\p{Nd}_.]*$", RegexOptions.CultureInvariant);

        private const double WholeTolerance = 1e-9;

        /// <summary>Problem with <paramref name="name"/> among the program's <paramref name="otherNames"/>, or <c>null</c>.</summary>
        public static string? CheckName(string? name, IEnumerable<string> otherNames)
        {
            var trimmed = name?.Trim() ?? string.Empty;

            return trimmed switch
            {
                "" => "Name is required",
                _ when !NamePattern.IsMatch(trimmed) => "Name must start with a letter, followed by letters, digits, '_' or '.'",
                _ when BuiltInSymbols.Contains(trimmed, StringComparer.OrdinalIgnoreCase) => $"'{trimmed}' is a built-in name",
                _ when otherNames.Contains(trimmed, StringComparer.OrdinalIgnoreCase) => $"Variable '{trimmed}' already exists",
                _ => null
            };
        }

        /// <summary>
        /// Checks <paramref name="expr"/> for a variable of <paramref name="type"/>; a numeric
        /// <paramref name="value"/> is returned for <c>int</c>/<c>double</c>.
        /// </summary>
        public static bool TryCheckExpr(
            string? expr, XncVariableType type, XncSymbolTable symbols, out double? value, out string? error)
        {
            value = null;
            error = type switch
            {
                XncVariableType.Bool => IsBoolLiteral(expr) ? null : "A bool value must be true or false",
                XncVariableType.String => null,
                _ => CheckNumber(expr, type, symbols, out value)
            };

            return error is null;
        }

        /// <summary>The <c>expr</c> text to write: numeric and bool values trimmed, a string kept as typed.</summary>
        public static string NormalizeExpr(string? expr, XncVariableType type) =>
            type == XncVariableType.String ? expr ?? string.Empty : expr?.Trim() ?? string.Empty;

        private static string? CheckNumber(string? expr, XncVariableType type, XncSymbolTable symbols, out double? value)
        {
            value = null;

            if (!NumericExpression.TryEvaluate(expr, symbols, out var number, out var error))
            {
                return error;
            }

            value = number;

            return type == XncVariableType.Int && Math.Abs(number - Math.Round(number)) > WholeTolerance
                ? $"An int value must be whole (got {number.ToString(System.Globalization.CultureInfo.InvariantCulture)})"
                : null;
        }

        private static bool IsBoolLiteral(string? expr) =>
            expr?.Trim() is { } text
            && (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("false", StringComparison.OrdinalIgnoreCase));
    }
}
