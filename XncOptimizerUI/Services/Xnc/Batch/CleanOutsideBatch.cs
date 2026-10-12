using System.Collections.Immutable;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Project;
using XncOptimizerUI.Services.Xnc.Conversions;
using XncOptimizerUI.Services.Xnc.Outside;

namespace XncOptimizerUI.Services.Xnc.Batch
{
    /// <summary>An element removed by outside cleaning, with the id of the XNC operation it came from.</summary>
    internal readonly record struct RemovedElement(string? OperationId, OutsideElement Element);

    /// <summary>Counters of outside cleaning, with every removed element for the log.</summary>
    internal readonly record struct CleanStats(ImmutableList<RemovedElement> Removed, int ToolsRemoved, int Unresolved) : IBatchStats<CleanStats>
    {
        public static CleanStats Zero => new([], 0, 0);

        public CleanStats Add(CleanStats other) =>
            new((Removed ?? []).AddRange(other.Removed ?? []), ToolsRemoved + other.ToolsRemoved, Unresolved + other.Unresolved);
    }

    /// <summary>
    /// "Clean operations" (<see cref="OutsideOperationDetector"/>): removes the elements of the selected
    /// kinds lying outside the part - a removed <c>&lt;ms&gt;</c> takes its segments along - and the tools
    /// no longer used. Saved as a <see cref="ProjectFileNames.CleanedSuffix"/> file.
    /// </summary>
    internal sealed class CleanOutsideBatch(OutsideOperationKinds kinds) : IXncBatch<CleanStats>
    {
        private static readonly (OutsideOperationKinds Kind, string Name)[] KindNames =
        [
            (OutsideOperationKinds.Bores, "bores"),
            (OutsideOperationKinds.Grooves, "grooves"),
            (OutsideOperationKinds.Mills, "mills"),
        ];

        private string KindsName => string.Join(", ", KindNames.Where(k => kinds.HasFlag(k.Kind)).Select(k => k.Name));

        public string NoPartsMessage => "No parts selected for outside operations cleaning.";

        public string Description => $"cleaned outside {KindsName}";

        public string Suffix => ProjectFileNames.CleanedSuffix;

        public string? Validate() =>
            (kinds & OutsideOperationKinds.All) == OutsideOperationKinds.None ? "No operation types selected for outside cleaning." : null;

        public ProgramRewrite<CleanStats> Rewrite(XElement program, XElement operation)
        {
            var found = OutsideOperationDetector.Find(program, kinds);

            found.Outside
                .SelectMany(o => o.Segments.Prepend(o.Element))
                .ToList()
                .ForEach(e => e.Remove());

            var toolsRemoved = ConversionTools.RemoveUnreferenced(program, ConversionTools.NamesUsedBy(found.Outside.Select(o => o.Element)));
            var removed = found.Outside.Select(o => new RemovedElement(operation.GetIdValue(), o)).ToImmutableList();

            return new(found.Outside.Count > 0, new CleanStats(removed, toolsRemoved, found.Unresolved));
        }

        /// <summary>One line per removed element, then the part summary; nothing for a part without removed or unresolved elements.</summary>
        public IEnumerable<string> DescribePart(Part part, CleanStats stats)
        {
            if (stats.Removed.Count == 0 && stats.Unresolved == 0)
            {
                yield break;
            }

            foreach (var removed in stats.Removed)
            {
                yield return $"Clean: \"{part.Name}\" (id={part.Id}), XNC id={removed.OperationId}: removed {Describe(removed.Element)}";
            }

            yield return $"Clean: \"{part.Name}\" (id={part.Id}): {stats.Removed.Count} removed"
                + (stats.Removed.Count > 0 ? $" ({DescribeCounts(stats.Removed)})" : string.Empty)
                + $", {stats.ToolsRemoved} tool(s) removed, {stats.Unresolved} unresolved";
        }

        public bool IsTouched(CleanStats stats) => stats.Removed.Count > 0;

        public bool HasChanges(CleanStats total) => total.Removed.Count > 0;

        public string NothingDoneMessage(CleanStats total) => $"No outside {KindsName} found (unresolved {total.Unresolved}).";

        public string Summary(CleanStats total, int touchedParts, string path) =>
            $"Outside operations cleaning complete: removed {total.Removed.Count} ({DescribeCounts(total.Removed)}), "
            + $"{total.ToolsRemoved} tool(s) removed, unresolved {total.Unresolved}, across {touchedParts} part(s). Stored to: {path}";

        public string FailureMessage(Exception exception) => $"Outside operations cleaning failed: {exception.Message}";

        #region Methods

        /// <summary>The element as authored, e.g. <c>&lt;bf x="-20" y="30" dp="10" name="Bore8"/&gt;</c>; a contour adds its segment count.</summary>
        private static string Describe(OutsideElement removed)
        {
            var element = new XElement(removed.Element.Name, removed.Element.Attributes()).ToString(SaveOptions.DisableFormatting);

            return removed.Segments.Count > 0 ? $"{element} + {removed.Segments.Count} segment(s)" : element;
        }

        private static string DescribeCounts(IEnumerable<RemovedElement> removed)
        {
            var counts = removed.GroupBy(r => r.Element.Kind).ToDictionary(g => g.Key, g => g.Count());

            return string.Join(", ", KindNames.Select(k => $"{counts.GetValueOrDefault(k.Kind)} {k.Name[..^1]}(s)"));
        }

        #endregion
    }
}
