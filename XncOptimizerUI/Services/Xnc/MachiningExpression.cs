using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Validates and evaluates a value typed for a numeric attribute in a program table (bore,
    /// groove or mill coordinates, depth, sizes, angle): a <see cref="NumericExpression"/> over the
    /// element's symbols (<c>dx</c>/<c>dy</c>/<c>dz</c>, every numeric <c>&lt;var&gt;</c> of the
    /// program and <c>tool.dia</c> of the element's tool), within the attribute's range
    /// (<see cref="MachiningAttributes.CheckRange"/>). Shared by the program tables (live input
    /// check) and <see cref="Contracts.IProjectEditor"/> (authoritative check before writing).
    /// </summary>
    public static class MachiningExpression
    {
        /// <summary>
        /// Evaluates <paramref name="text"/> for <paramref name="attribute"/> against the element's
        /// <paramref name="symbols"/>. Returns <c>false</c> with a user-facing
        /// <paramref name="error"/> when the text is not acceptable.
        /// </summary>
        public static bool TryEvaluate(
            string? text, MachiningAttribute attribute, XncSymbolTable symbols,
            out double value, out string? error)
        {
            if (!NumericExpression.TryEvaluate(text, symbols, out value, out error))
            {
                return false;
            }

            error = attribute.CheckRange(value);

            return error is null;
        }
    }
}
