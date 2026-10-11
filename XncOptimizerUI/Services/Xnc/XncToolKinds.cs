namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Tool name prefixes that tell a tool's kind: the conversions name the tools they declare
    /// <c>{prefix}{diameter}</c> (<c>Bore40</c>, <c>Cut2.8</c>, <c>Mill6</c>), and tool sorting
    /// orders declarations by them.
    /// </summary>
    internal static class XncToolKinds
    {
        public const string Bore = "Bore";
        public const string Groove = "Cut";
        public const string Mill = "Mill";

        /// <summary>The kinds in tool-declaration order.</summary>
        public static readonly string[] Ordered = [Bore, Groove, Mill];
    }
}
