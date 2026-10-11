using System.Xml.Linq;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Project;
using XncOptimizerUI.Services.Xnc.Conversions;

namespace XncOptimizerUI.Services.Xnc.Batch
{
    /// <summary>"Convert grooves / mills" (<see cref="GrooveMillConverter"/>), saved as a <see cref="ProjectFileNames.GrooveMillSuffix"/> file.</summary>
    /// <param name="millingToolDiameters">Configured milling cutters; used grooves → mills.</param>
    /// <param name="groovingToolWidths">Configured grooving cutters; the first one is used mills → grooves.</param>
    internal sealed class GrooveMillBatch(
        GrooveMillDirection direction,
        bool processPockets,
        IReadOnlyList<decimal> millingToolDiameters,
        IReadOnlyList<decimal> groovingToolWidths) : IXncBatch<ConversionStats>
    {
        private readonly double[] _millingToolDiameters = [.. millingToolDiameters.Select(d => (double)d).Order()];

        private bool ToMills => direction == GrooveMillDirection.GroovesToMills;

        public string NoPartsMessage => "No parts selected for groove/mill conversion.";

        public string Description => ToMills ? "converted grooves to mills" : "converted mills to grooves";

        public string Suffix => ProjectFileNames.GrooveMillSuffix;

        public string? Validate() => (ToMills, _millingToolDiameters.Length, groovingToolWidths.Count) switch
        {
            (true, 0, _) => "No milling tools configured (AppOptions.MillingToolDiams is empty). Grooves->Mills conversion cancelled.",
            (false, _, 0) => "No grooving tools configured (AppOptions.GroovingToolWidths is empty). Mills->Grooves conversion cancelled.",
            _ => null,
        };

        public ProgramRewrite<ConversionStats> Rewrite(XElement program, XElement operation)
        {
            var stats = ToMills
                ? GrooveMillConverter.GroovesToMills(program, _millingToolDiameters)
                : GrooveMillConverter.MillsToGrooves(program, processPockets, (double)groovingToolWidths[0]);

            return new(stats.Converted > 0, stats);
        }

        public IEnumerable<string> DescribePart(Part part, ConversionStats stats)
        {
            if (!IsTouched(stats))
            {
                yield break;
            }

            yield return ToMills
                ? $"Grooves->Mills: \"{part.Name}\" (id={part.Id}): {stats.Converted} groove(s) -> mill(s), {stats.ToolsAdded} tool(s) added, {stats.ToolsRemoved} tool(s) removed, {stats.Ignored} ignored"
                : $"Mills->Grooves: \"{part.Name}\" (id={part.Id}): {stats.Converted} mill(s) -> groove(s), {stats.ToolsRemoved} tool(s) removed, {stats.Ignored} ignored";
        }

        public bool IsTouched(ConversionStats stats) => stats.Converted > 0 || stats.Ignored > 0;

        public bool HasChanges(ConversionStats total) => total.Converted > 0;

        public string NothingDoneMessage(ConversionStats total) =>
            $"No {(ToMills ? "grooves" : "mills")} converted (ignored {total.Ignored}).";

        public string Summary(ConversionStats total, int touchedParts, string path) =>
            $"Groove/Mill conversion complete: converted {total.Converted}, ignored {total.Ignored}, {total.ToolsAdded} tool(s) added, {total.ToolsRemoved} tool(s) removed across {touchedParts} part(s). Stored to: {path}";
    }
}
