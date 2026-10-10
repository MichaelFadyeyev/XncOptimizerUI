using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Validates and evaluates a value typed for a bore coordinate or depth in the bores table:
    /// a <see cref="NumericExpression"/> over the bore's program symbols (<c>dx</c>/<c>dy</c>/<c>dz</c>
    /// plus every numeric <c>&lt;var&gt;</c> it declares); a depth must also be positive.
    /// Shared by the bores table (live input check) and
    /// <see cref="Contracts.IProjectService.UpdateBore"/> (authoritative check before writing).
    /// </summary>
    public static class BoreExpression
    {
        /// <summary>
        /// Evaluates <paramref name="text"/> for <paramref name="attribute"/> against the bore's
        /// program <paramref name="symbols"/>. Returns <c>false</c> with a user-facing
        /// <paramref name="error"/> when the text is not acceptable.
        /// </summary>
        public static bool TryEvaluate(
            string? text, BoreAttribute attribute, XncSymbolTable symbols,
            out double value, out string? error)
        {
            if (!NumericExpression.TryEvaluate(text, symbols, out value, out error))
            {
                return false;
            }

            error = CheckRange(attribute, value);

            return error is null;
        }

        private static string? CheckRange(BoreAttribute attribute, double value) =>
            attribute == BoreAttribute.Depth && value <= 0d ? "Depth must be greater than 0" : null;
    }
}
