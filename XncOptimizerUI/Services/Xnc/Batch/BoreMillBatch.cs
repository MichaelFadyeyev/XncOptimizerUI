using System.Xml.Linq;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Project;
using XncOptimizerUI.Services.Xnc.Conversions;

namespace XncOptimizerUI.Services.Xnc.Batch
{
    /// <summary>"Convert bores / mills" (<see cref="BoreMillConverter"/>), saved as a <see cref="ProjectFileNames.BoreMillSuffix"/> file.</summary>
    internal sealed class BoreMillBatch(BoreMillDirection direction, bool useEllipse) : IXncBatch<ConversionStats>
    {
        private bool ToMills => direction == BoreMillDirection.BoresToMills;

        public string NoPartsMessage => "No parts selected for bore/mill conversion.";

        public string Description => ToMills ? "converted bores to mills" : "converted mills to bores";

        public string Suffix => ProjectFileNames.BoreMillSuffix;

        public ProgramRewrite<ConversionStats> Rewrite(XElement program, XElement operation)
        {
            var stats = ToMills
                ? BoreMillConverter.BoresToMills(program, useEllipse)
                : BoreMillConverter.MillsToBores(program);

            return new(stats.Converted > 0, stats);
        }

        public IEnumerable<string> DescribePart(Part part, ConversionStats stats)
        {
            if (!IsTouched(stats))
            {
                yield break;
            }

            yield return ToMills
                ? $"Bores->Mills: \"{part.Name}\" (id={part.Id}): {stats.Converted} bore(s) -> mill(s), {stats.ToolsAdded} tool(s) added, {stats.ToolsRemoved} tool(s) removed, {stats.Ignored} ignored"
                : $"Mills->Bores: \"{part.Name}\" (id={part.Id}): {stats.Converted} mill(s) -> bore(s), {stats.ToolsAdded} tool(s) added, {stats.ToolsRemoved} tool(s) removed, {stats.Ignored} ignored";
        }

        public bool IsTouched(ConversionStats stats) => stats.Converted > 0 || stats.Ignored > 0;

        public bool HasChanges(ConversionStats total) => total.Converted > 0;

        public string NothingDoneMessage(ConversionStats total) =>
            $"No {(ToMills ? "bores" : "mills")} converted (ignored {total.Ignored}).";

        public string Summary(ConversionStats total, int touchedParts, string path) =>
            $"Bore/Mill conversion complete: converted {total.Converted}, ignored {total.Ignored}, {total.ToolsAdded} tool(s) added, {total.ToolsRemoved} tool(s) removed across {touchedParts} part(s). Stored to: {path}";
    }
}
