using System.Xml.Linq;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.Services.Xnc.Batch
{
    /// <summary>Counters of a batch operation, summed per part and over the whole run.</summary>
    internal interface IBatchStats<TSelf> where TSelf : IBatchStats<TSelf>
    {
        static abstract TSelf Zero { get; }

        TSelf Add(TSelf other);
    }

    /// <summary>What rewriting one program did: whether its text must be written back, and its counters.</summary>
    internal readonly record struct ProgramRewrite<TStats>(bool Changed, TStats Stats);

    /// <summary>
    /// One batch operation over the XNC programs of the selected parts, run by
    /// <see cref="XncBatchRunner"/>: the per-program rewrite plus the texts it reports.
    /// </summary>
    internal interface IXncBatch<TStats> where TStats : struct, IBatchStats<TStats>
    {
        /// <summary>Reported when no part is selected.</summary>
        string NoPartsMessage { get; }

        /// <summary>A problem with the operation's options that cancels the run, or <c>null</c>.</summary>
        string? Validate() => null;

        /// <summary>Rewrites the decoded <paramref name="program"/> of <paramref name="operation"/> in place.</summary>
        ProgramRewrite<TStats> Rewrite(XElement program, XElement operation);

        /// <summary>Log lines for one part; none when it had nothing to report.</summary>
        IEnumerable<string> DescribePart(Part part, TStats stats);

        /// <summary>Whether the part counts towards the summary's "across N part(s)".</summary>
        bool IsTouched(TStats stats);

        /// <summary>Whether anything was changed at all; otherwise nothing is saved.</summary>
        bool HasChanges(TStats total);

        /// <summary>Reported when nothing was changed.</summary>
        string NothingDoneMessage(TStats total);

        /// <summary>Appended to the project description.</summary>
        string Description { get; }

        /// <summary>Output file suffix (see <see cref="Project.ProjectFileNames"/>).</summary>
        string Suffix { get; }

        /// <summary>Final message once the result is saved to <paramref name="path"/>.</summary>
        string Summary(TStats total, int touchedParts, string path);

        /// <summary>Reported when a program cannot be rewritten; nothing is changed then.</summary>
        string FailureMessage(Exception exception) => exception.Message;
    }
}
