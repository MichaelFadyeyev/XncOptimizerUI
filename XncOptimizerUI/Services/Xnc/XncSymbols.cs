using System.Text.RegularExpressions;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;
using static XncOptimizerUI.Services.Xnc.XncAttributeParsing;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// The symbols XNC expressions resolve against (<c>dx</c>/<c>dy</c>/<c>dz</c>, numeric
    /// <c>&lt;var&gt;</c>s, <c>tool.dia</c>), the one expression evaluation entry point, and
    /// whole-identifier references inside expressions.
    /// </summary>
    internal static class XncSymbols
    {
        /// <summary>
        /// Attributes of program elements that may carry an expression (and so reference
        /// <c>dx</c>/<c>dy</c>/<c>dz</c> or a <c>&lt;var&gt;</c> by name).
        /// </summary>
        public static readonly IReadOnlySet<string> ExpressionAttributes = new HashSet<string>
        {
            "x", "y", "z", "x1", "y1", "x2", "y2", "cx", "cy", "dp", "t", "l", "w", "r", "a", "sxy", "expr", "as"
        };

        /// <summary>A table holding only the program size <c>dx</c>/<c>dy</c>/<c>dz</c>.</summary>
        /// <exception cref="XncProgramFormatException">A size is missing or not a plain number.</exception>
        public static XncSymbolTable DimensionSymbols(XElement program) =>
            DimensionSymbols(RequireDimension(program, "dx"), RequireDimension(program, "dy"), RequireDimension(program, "dz"));

        /// <summary>
        /// Symbols of a program document: <c>dx</c>/<c>dy</c>/<c>dz</c> plus its numeric
        /// (<c>int</c>/<c>double</c>) <c>&lt;var&gt;</c>s in document order (a var may reference an
        /// earlier one), so elements referencing a custom variable resolve the same way
        /// XncProgramReader resolves them for display. With <paramref name="before"/> only the vars
        /// declared before that element are registered - the symbols its own expression may use.
        /// </summary>
        public static XncSymbolTable SeedProgramSymbols(XElement program, XElement? before = null)
        {
            var symbols = DimensionSymbols(program);

            var vars = program.Elements(XncTags.Variable)
                .TakeWhile(varElement => !ReferenceEquals(varElement, before))
                .Where(varElement => XncVariableTypes.Parse(varElement.GetTypeValue()).IsNumeric());

            foreach (var varElement in vars)
            {
                var name = varElement.GetNameValue()
                    ?? throw new XncProgramFormatException("<var> has no name.");
                symbols.Set(name, Eval(varElement.GetExprValue(), symbols));
            }

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

        /// <summary>
        /// Evaluates an attribute's expression; <paramref name="where"/> (e.g. <c>&lt;ms&gt; @x</c>)
        /// names a missing value in the error.
        /// </summary>
        /// <exception cref="XncProgramFormatException">The value is missing, malformed or references an unknown symbol.</exception>
        public static double Eval(string? expression, XncSymbolTable symbols, string? where = null) =>
            XncExpressionEvaluator.Evaluate(
                expression ?? throw new XncProgramFormatException(where is null ? "Missing XNC coordinate/value." : $"Missing value for {where}."),
                symbols);

        /// <summary>The element's <c>x</c>/<c>y</c> point, evaluated.</summary>
        /// <exception cref="XncProgramFormatException">A coordinate is missing or does not resolve.</exception>
        public static Vec2 EvalPoint(XElement element, XncSymbolTable symbols) =>
            new(Eval(element.GetXValue(), symbols), Eval(element.GetYValue(), symbols));

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

        private static XncSymbolTable DimensionSymbols(XncProgram program) => DimensionSymbols(program.Dx, program.Dy, program.Dz);

        private static XncSymbolTable DimensionSymbols(double dx, double dy, double dz)
        {
            var symbols = new XncSymbolTable();
            symbols.Set("dx", dx);
            symbols.Set("dy", dy);
            symbols.Set("dz", dz);

            return symbols;
        }
    }
}
