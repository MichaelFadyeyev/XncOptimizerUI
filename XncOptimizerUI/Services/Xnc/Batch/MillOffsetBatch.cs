using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Project;

namespace XncOptimizerUI.Services.Xnc.Batch
{
    /// <summary>Counters of a mill path offset.</summary>
    internal readonly record struct OffsetStats(int Offset, int Ignored, int Skipped) : IBatchStats<OffsetStats>
    {
        public static OffsetStats Zero => default;

        public OffsetStats Add(OffsetStats other) => new(Offset + other.Offset, Ignored + other.Ignored, Skipped + other.Skipped);
    }

    /// <summary>"Offset mill path" (<see cref="MillPathOffsetter"/>), saved as a <see cref="ProjectFileNames.MillOffsetSuffix"/> file.</summary>
    internal sealed class MillOffsetBatch(double offset, MillOffsetSide side, MillPathKinds kinds) : IXncBatch<OffsetStats>
    {
        private string SideName => side == MillOffsetSide.Right ? "R" : "L";

        private string OffsetText => XmlConvert.ToString(offset);

        private string KindsName => (kinds & MillPathKinds.All) switch
        {
            MillPathKinds.Open => "open",
            MillPathKinds.Closed => "closed",
            _ => "open and closed",
        };

        public string NoPartsMessage => "No parts selected for mill path offset.";

        public string Description => $"offset {KindsName} mill paths {OffsetText} mm {SideName}";

        public string Suffix => ProjectFileNames.MillOffsetSuffix;

        public string? Validate() => true switch
        {
            _ when !(offset > 0d) => $"Mill path offset must be greater than 0 (was {OffsetText}).",
            _ when (kinds & MillPathKinds.All) == MillPathKinds.None => "No mill path types selected for offset.",
            _ => null,
        };

        public ProgramRewrite<OffsetStats> Rewrite(XElement program, XElement operation)
        {
            var (offsetCount, ignored, skipped) = MillPathOffsetter.OffsetInProgram(program, offset, side, kinds);

            return new(offsetCount > 0, new OffsetStats(offsetCount, ignored, skipped));
        }

        public IEnumerable<string> DescribePart(Part part, OffsetStats stats)
        {
            if (IsTouched(stats))
            {
                yield return $"Mill offset: \"{part.Name}\" (id={part.Id}): {stats.Offset} mill(s) offset, {stats.Ignored} ignored, {stats.Skipped} skipped";
            }
        }

        public bool IsTouched(OffsetStats stats) => stats.Offset > 0 || stats.Ignored > 0 || stats.Skipped > 0;

        public bool HasChanges(OffsetStats total) => total.Offset > 0;

        public string NothingDoneMessage(OffsetStats total) => $"No mill paths offset (ignored {total.Ignored}, skipped {total.Skipped}).";

        public string Summary(OffsetStats total, int touchedParts, string path) =>
            $"Mill path offset complete: offset {total.Offset} {KindsName} mill(s) by {OffsetText} mm {SideName}, ignored {total.Ignored}, skipped {total.Skipped}, across {touchedParts} part(s). Stored to: {path}";
    }
}
