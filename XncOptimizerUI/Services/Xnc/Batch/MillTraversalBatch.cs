using System.Xml.Linq;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.Services.Project;

namespace XncOptimizerUI.Services.Xnc.Batch
{
    /// <summary>"Optimize mill order" (<see cref="MillTraversalOptimizer"/>), saved as a <see cref="ProjectFileNames.MillOrderSuffix"/> file.</summary>
    internal sealed class MillTraversalBatch : IXncBatch<TraversalStats>
    {
        public string NoPartsMessage => "No parts selected for mill traversal optimization.";

        public string Description => "optimized mill traversal order";

        public string Suffix => ProjectFileNames.MillOrderSuffix;

        public ProgramRewrite<TraversalStats> Rewrite(XElement program, XElement operation)
        {
            var stats = MillTraversalOptimizer.Optimize(program);

            return new(stats.Reordered > 0, stats);
        }

        public IEnumerable<string> DescribePart(Part part, TraversalStats stats)
        {
            if (IsTouched(stats))
            {
                yield return $"Mill order: \"{part.Name}\" (id={part.Id}): reordered {stats.Reordered} pass(es) in {stats.ChangedGroups} group(s), {stats.Ignored} ignored";
            }
        }

        public bool IsTouched(TraversalStats stats) => stats.Reordered > 0 || stats.Ignored > 0;

        public bool HasChanges(TraversalStats total) => total.Reordered > 0;

        public string NothingDoneMessage(TraversalStats total) => $"No mill passes reordered (ignored {total.Ignored}).";

        public string Summary(TraversalStats total, int touchedParts, string path) =>
            $"Mill traversal optimization complete: reordered {total.Reordered} pass(es) across {touchedParts} part(s). Stored to: {path}";
    }
}
