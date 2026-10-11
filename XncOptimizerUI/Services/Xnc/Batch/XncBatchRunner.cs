using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.Services.Project;

namespace XncOptimizerUI.Services.Xnc.Batch
{
    /// <summary>
    /// Runs an <see cref="IXncBatch{TStats}"/> over the XNC programs of the given parts:
    /// every program is rewritten first, and only when all succeed are the changed ones written
    /// back - a failing program leaves the document untouched. Then each part is logged, and the
    /// result is saved as a new suffixed file unless nothing changed.
    /// </summary>
    internal sealed class XncBatchRunner(ProjectDocument document)
    {
        /// <summary>A rewritten program; <see cref="Text"/> is <c>null</c> when it is unchanged.</summary>
        private sealed record RewrittenProgram<TStats>(XElement Operation, string? Text, TStats Stats);

        private sealed record RewrittenPart<TStats>(Part Part, List<RewrittenProgram<TStats>> Programs, TStats Stats);

        public OperationResult Run<TStats>(IXncBatch<TStats> batch, IList<Part>? parts)
            where TStats : struct, IBatchStats<TStats>
        {
            var log = new OperationLog();

            if (parts == null || parts.Count == 0)
            {
                return log.Fail(batch.NoPartsMessage);
            }

            if (batch.Validate() is { } problem)
            {
                return log.Fail(problem);
            }

            List<RewrittenPart<TStats>> rewritten;

            try
            {
                rewritten = parts.Select(part => RewritePart(batch, part)).ToList();
            }
            catch (Exception e)
            {
                return log.Fail(batch.FailureMessage(e));
            }

            WriteChangedPrograms(rewritten);
            rewritten.ForEach(p => log.Info(batch.DescribePart(p.Part, p.Stats)));

            var total = rewritten.Aggregate(TStats.Zero, (sum, p) => sum.Add(p.Stats));

            if (!batch.HasChanges(total))
            {
                return log.Fail(batch.NothingDoneMessage(total));
            }

            document.AppendDescription(batch.Description);

            var path = document.SaveAsSuffixed(batch.Suffix);
            var touchedParts = rewritten.Count(p => batch.IsTouched(p.Stats));

            return log.Highlight(batch.Summary(total, touchedParts, path)).Changed();
        }

        #region Methods

        private RewrittenPart<TStats> RewritePart<TStats>(IXncBatch<TStats> batch, Part part)
            where TStats : struct, IBatchStats<TStats>
        {
            var programs = document.XncOperationsOf(part.Id)
                .Where(operation => operation.GetProgram() != null)
                .Select(operation => RewriteProgram(batch, operation))
                .ToList();

            return new RewrittenPart<TStats>(part, programs, programs.Aggregate(TStats.Zero, (sum, p) => sum.Add(p.Stats)));
        }

        private static RewrittenProgram<TStats> RewriteProgram<TStats>(IXncBatch<TStats> batch, XElement operation)
            where TStats : struct, IBatchStats<TStats>
        {
            var programDocument = XncProgramText.Load(operation);
            var rewrite = batch.Rewrite(XncProgramText.Root(programDocument), operation);

            return new RewrittenProgram<TStats>(
                operation,
                rewrite.Changed ? XncProgramText.Serialize(programDocument) : null,
                rewrite.Stats);
        }

        private static void WriteChangedPrograms<TStats>(List<RewrittenPart<TStats>> parts) =>
            parts
                .SelectMany(p => p.Programs)
                .Where(p => p.Text != null)
                .ToList()
                .ForEach(p => XncProgramText.Write(p.Operation, p.Text!));

        #endregion
    }
}
