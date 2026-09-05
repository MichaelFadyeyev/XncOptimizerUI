namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Case-insensitive name &#8594; value table for XNC expression evaluation. Seeded with
    /// <c>dx</c>/<c>dy</c>/<c>dz</c> from <c>&lt;program&gt;</c>, updated with <c>tool.dia</c>
    /// before each tool-bound element, and extended with every <c>&lt;var&gt;</c> as it is read.
    /// </summary>
    public sealed class XncSymbolTable
    {
        private readonly Dictionary<string, double> _values = new(StringComparer.OrdinalIgnoreCase);

        public void Set(string name, double value) => _values[name] = value;

        public bool TryGet(string name, out double value) => _values.TryGetValue(name, out value);
    }
}
