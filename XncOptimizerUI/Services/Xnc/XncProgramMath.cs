using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Numeric helpers shared by the XNC program rewriters (groove/mill and bore/mill conversion,
    /// mill traversal ordering, mill path offset): symbol seeding, expression evaluation, attribute
    /// code parsing and the small amount of plane geometry they have in common.
    /// </summary>
    internal static class XncProgramMath
    {
        /// <summary>Tolerance for comparing reconstructed geometry (radii, chords), mm.</summary>
        public const double GeomTolerance = 1e-3;

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

        /// <summary>
        /// Attributes of program elements that may carry an expression (and so reference
        /// <c>dx</c>/<c>dy</c>/<c>dz</c> or a <c>&lt;var&gt;</c> by name).
        /// </summary>
        public static readonly IReadOnlySet<string> ExpressionAttributes = new HashSet<string>
        {
            "x", "y", "z", "x1", "y1", "x2", "y2", "cx", "cy", "dp", "t", "l", "w", "r", "a", "sxy", "expr", "as"
        };

        /// <summary>
        /// Symbols of a program document: <c>dx</c>/<c>dy</c>/<c>dz</c> plus its numeric
        /// (<c>int</c>/<c>double</c>) <c>&lt;var&gt;</c>s in document order (a var may reference an
        /// earlier one), so elements referencing a custom variable resolve the same way
        /// XncProgramReader resolves them for display. With <paramref name="before"/> only the vars
        /// declared before that element are registered - the symbols its own expression may use.
        /// </summary>
        public static XncSymbolTable SeedProgramSymbols(XElement program, XElement? before = null)
        {
            var symbols = new XncSymbolTable();
            symbols.Set("dx", RequireProgramDouble(program.GetDxValue(), "dx"));
            symbols.Set("dy", RequireProgramDouble(program.GetDyValue(), "dy"));
            symbols.Set("dz", RequireProgramDouble(program.GetDzValue(), "dz"));

            var vars = program.Elements("var")
                .TakeWhile(varElement => !ReferenceEquals(varElement, before))
                .Where(varElement => XncVariableTypes.Parse(varElement.GetTypeValue()).IsNumeric());

            foreach (var varElement in vars)
            {
                var name = varElement.GetNameValue()
                    ?? throw new Exception("<var> has no name.");
                symbols.Set(name, EvalXnc(varElement.GetExprValue(), symbols));
            }

            return symbols;
        }

        /// <summary>
        /// Symbols a variable's expression may use: <c>dx</c>/<c>dy</c>/<c>dz</c> plus the numeric
        /// variables declared before <paramref name="variable"/> - the model-side counterpart of
        /// <see cref="SeedProgramSymbols(XElement, XElement?)"/> with a stop element.
        /// </summary>
        public static XncSymbolTable SymbolsBefore(XncProgram program, XncVariable variable)
        {
            var symbols = DimensionSymbols(program);

            foreach (var earlier in program.DeclaredVariables.Where(v => v.Index < variable.Index && v.Value is not null))
            {
                symbols.Set(earlier.Name, earlier.Value!.Value);
            }

            return symbols;
        }

        /// <summary>Replaces every whole-identifier occurrence of <paramref name="oldName"/> (any case) with <paramref name="newName"/>.</summary>
        public static string RenameIdentifier(string expression, string oldName, string newName) =>
            IdentifierRegex(oldName).Replace(expression, newName.Replace("$", "$$"));

        /// <summary>Whether <paramref name="expression"/> references <paramref name="name"/> (any case) as a whole identifier.</summary>
        public static bool ReferencesIdentifier(string expression, string name) =>
            IdentifierRegex(name).IsMatch(expression);

        // Identifier characters are letters, digits, '_' and '.' (tool.dia), so a name must not be
        // part of a longer identifier on either side.
        private static Regex IdentifierRegex(string name) => new(
            $@"(?<![\w.]){Regex.Escape(name)}(?![\w.])",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static XncSymbolTable DimensionSymbols(XncProgram program)
        {
            var symbols = new XncSymbolTable();
            symbols.Set("dx", program.Dx);
            symbols.Set("dy", program.Dy);
            symbols.Set("dz", program.Dz);

            return symbols;
        }

        /// <summary>
        /// Symbols of an already read <paramref name="program"/>: <c>dx</c>/<c>dy</c>/<c>dz</c> plus
        /// its resolved <c>&lt;var&gt;</c>s - the model-side counterpart of <see cref="SeedProgramSymbols"/>.
        /// </summary>
        public static XncSymbolTable ProgramSymbols(XncProgram program)
        {
            var symbols = DimensionSymbols(program);

            foreach (var (name, value) in program.Variables)
            {
                symbols.Set(name, value);
            }

            return symbols;
        }

        public static double RequireProgramDouble(string? raw, string name)
        {
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            throw new Exception($"<program> @{name} is missing or not a number (was '{raw}').");
        }

        public static double EvalXnc(string? expression, XncSymbolTable symbols)
        {
            return XncExpressionEvaluator.Evaluate(
                expression ?? throw new Exception("Missing XNC coordinate/value."),
                symbols);
        }

        public static ToolPosition ParsePositionCode(string? raw)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
                && Enum.IsDefined(typeof(ToolPosition), code)
                    ? (ToolPosition)code
                    : ToolPosition.Center;
        }

        /// <summary><c>dir="true"</c> (clockwise sweep) maps to <c>+1</c>, anything else to <c>-1</c>.</summary>
        public static int ParseDirSign(string? raw) => bool.TryParse(raw, out var value) && value ? 1 : -1;

        /// <summary>
        /// Reconstructs the centre of a radius-defined arc (<c>&lt;ma&gt;</c>) from its chord.
        /// For the closed-circle-in-two-half-arcs case the chord equals the diameter and the
        /// centre is the chord midpoint regardless of <paramref name="dirSign"/>.
        /// </summary>
        public static bool TryReconstructArcCentre(
            double startX, double startY, double endX, double endY, double radius, int dirSign,
            out double centreX, out double centreY)
        {
            centreX = 0d;
            centreY = 0d;

            var chord = Hypot(startX, startY, endX, endY);

            if (chord <= GeomTolerance || chord > (2d * radius) + GeomTolerance)
            {
                return false;
            }

            var midX = (startX + endX) / 2d;
            var midY = (startY + endY) / 2d;
            var halfChord = chord / 2d;
            var offset = Math.Sqrt(Math.Max(0d, (radius * radius) - (halfChord * halfChord)));

            // Unit vector perpendicular to the chord.
            var perpX = -(endY - startY) / chord;
            var perpY = (endX - startX) / chord;

            centreX = midX + (dirSign * offset * perpX);
            centreY = midY + (dirSign * offset * perpY);

            return true;
        }

        public static double Hypot(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(SquaredDistance(x1, y1, x2, y2));
        }

        public static double SquaredDistance(double x1, double y1, double x2, double y2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;

            return dx * dx + dy * dy;
        }
    }
}
