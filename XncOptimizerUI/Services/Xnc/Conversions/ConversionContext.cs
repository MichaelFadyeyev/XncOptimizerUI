using System.Xml.Linq;
using XncOptimizerUI.Services.Xnc.Batch;

namespace XncOptimizerUI.Services.Xnc.Conversions
{
    /// <summary>What a converter does with one element.</summary>
    internal enum ElementOutcome
    {
        /// <summary>Replaced by its converted form.</summary>
        Converted,

        /// <summary>A candidate that does not qualify; left untouched and counted.</summary>
        Ignored,

        /// <summary>Not a candidate for this conversion; left untouched, not counted.</summary>
        NotApplicable
    }

    /// <summary>Counters of a groove/mill or bore/mill conversion.</summary>
    internal readonly record struct ConversionStats(int Converted, int Ignored, int ToolsAdded, int ToolsRemoved)
        : IBatchStats<ConversionStats>
    {
        public static ConversionStats Zero => default;

        public ConversionStats Add(ConversionStats other) => new(
            Converted + other.Converted,
            Ignored + other.Ignored,
            ToolsAdded + other.ToolsAdded,
            ToolsRemoved + other.ToolsRemoved);
    }

    /// <summary>
    /// State of one program while it is converted: its symbols (<c>dx</c>/<c>dy</c>/<c>dz</c> +
    /// <c>&lt;var&gt;</c>s), tools, and the tools of replaced elements, which are dropped at the end
    /// when nothing uses them any more.
    /// </summary>
    internal sealed class ConversionContext
    {
        private ConversionContext(XElement program)
        {
            Program = program;
            Symbols = XncProgramMath.SeedProgramSymbols(program);
            Tools = ConversionTools.ToolSet.Of(program);
            Dx = Symbol("dx");
            Dy = Symbol("dy");
            Dz = Symbol("dz");
        }

        public XElement Program { get; }

        public XncSymbolTable Symbols { get; }

        public ConversionTools.ToolSet Tools { get; }

        public double Dx { get; }

        public double Dy { get; }

        public double Dz { get; }

        /// <summary>Tools of the elements replaced so far.</summary>
        public HashSet<string> ReplacedTools { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static ConversionContext Of(XElement program) => new(program);

        public double Eval(string? expression) => XncProgramMath.EvalXnc(expression, Symbols);

        /// <summary>Records the tool of a replaced element, if it names one.</summary>
        public void Replacing(string? toolName)
        {
            if (toolName != null)
            {
                ReplacedTools.Add(toolName);
            }
        }

        /// <summary>Drops the replaced elements' tools nothing uses any more and returns the counters.</summary>
        public ConversionStats Finish(IReadOnlyCollection<ElementOutcome> outcomes) => new(
            outcomes.Count(o => o == ElementOutcome.Converted),
            outcomes.Count(o => o == ElementOutcome.Ignored),
            Tools.Added.Count,
            ConversionTools.RemoveUnreferenced(Program, ReplacedTools));

        private double Symbol(string name) => Symbols.TryGet(name, out var value) ? value : 0d;
    }
}
