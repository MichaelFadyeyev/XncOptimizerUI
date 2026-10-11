using System.Collections.Immutable;
using System.Xml.Linq;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.Services.Project;

namespace XncOptimizerUI.Services.Xnc.Batch
{
    /// <summary>Counters of a tool declaration sort, with the reasons programs were skipped.</summary>
    internal readonly record struct ToolSortStats(int Sorted, int Skipped, ImmutableList<string> Problems) : IBatchStats<ToolSortStats>
    {
        public static ToolSortStats Zero => new(0, 0, []);

        public ToolSortStats Add(ToolSortStats other) =>
            new(Sorted + other.Sorted, Skipped + other.Skipped, (Problems ?? []).AddRange(other.Problems ?? []));
    }

    /// <summary>"Sort tools" (<see cref="XncToolDeclarationSorter"/>), saved as a <see cref="ProjectFileNames.ToolOrderSuffix"/> file.</summary>
    internal sealed class ToolSortBatch : IXncBatch<ToolSortStats>
    {
        public string NoPartsMessage => "No parts selected for tool declaration sorting.";

        public string Description => "sorted tool declarations";

        public string Suffix => ProjectFileNames.ToolOrderSuffix;

        public ProgramRewrite<ToolSortStats> Rewrite(XElement program, XElement operation)
        {
            var result = XncToolDeclarationSorter.SortInProgram(program);
            var stats = new ToolSortStats(
                result.Sorted ? 1 : 0,
                result.Skipped ? 1 : 0,
                result.Skipped ? [.. result.Problems] : []);

            return new(result.Sorted, stats);
        }

        public IEnumerable<string> DescribePart(Part part, ToolSortStats stats)
        {
            if (stats.Sorted > 0 || stats.Skipped > 0)
            {
                yield return $"Tool order: \"{part.Name}\" (id={part.Id}): sorted {stats.Sorted} program(s), {stats.Skipped} skipped";
            }

            foreach (var problem in stats.Problems ?? [])
            {
                yield return $"  skipped: {problem}";
            }
        }

        public bool IsTouched(ToolSortStats stats) => stats.Sorted > 0 || stats.Skipped > 0;

        public bool HasChanges(ToolSortStats total) => total.Sorted > 0;

        public string NothingDoneMessage(ToolSortStats total) => $"No tool declarations reordered (skipped {total.Skipped} program(s)).";

        public string Summary(ToolSortStats total, int touchedParts, string path) =>
            $"Tool declaration sorting complete: sorted {total.Sorted} program(s), skipped {total.Skipped}. Stored to: {path}";
    }
}
