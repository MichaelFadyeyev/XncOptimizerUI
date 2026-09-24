using System.Globalization;
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

        public static XncSymbolTable SeedProgramSymbols(XElement program)
        {
            var symbols = new XncSymbolTable();
            symbols.Set("dx", RequireProgramDouble(program.GetDxValue(), "dx"));
            symbols.Set("dy", RequireProgramDouble(program.GetDyValue(), "dy"));
            symbols.Set("dz", RequireProgramDouble(program.GetDzValue(), "dz"));

            // Register every declared <var> up front (document order, so a var may reference an
            // earlier one) so elements that reference a custom variable by name in dp/x/y/etc.
            // resolve the same way XncProgramReader already resolves them for display.
            foreach (var varElement in program.Elements("var"))
            {
                var name = varElement.GetNameValue()
                    ?? throw new Exception("<var> has no name.");
                symbols.Set(name, EvalXnc(varElement.GetExprValue(), symbols));
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
