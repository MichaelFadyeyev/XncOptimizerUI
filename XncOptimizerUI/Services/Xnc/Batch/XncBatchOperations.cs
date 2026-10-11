using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Project;

namespace XncOptimizerUI.Services.Xnc.Batch
{
    /// <summary>The XNC program batch operations, each an <see cref="IXncBatch{TStats}"/> run by <see cref="XncBatchRunner"/>.</summary>
    public sealed class XncBatchOperations(ProjectDocument document, IConfigService config) : IXncBatchOperations
    {
        private readonly XncBatchRunner _runner = new(document);

        public OperationResult ConvertGroovesAndMills(IList<Part> parts, GrooveMillDirection direction, bool processPockets) =>
            _runner.Run(new GrooveMillBatch(direction, processPockets, config.MillingToolDiams ?? [], config.GroovingToolWidths ?? []), parts);

        public OperationResult ConvertBoresAndMills(IList<Part> parts, BoreMillDirection direction, bool useEllipse) =>
            _runner.Run(new BoreMillBatch(direction, useEllipse), parts);

        public OperationResult OptimizeMillTraversal(IList<Part> parts) =>
            _runner.Run(new MillTraversalBatch(), parts);

        public OperationResult SortToolDeclarations(IList<Part> parts) =>
            _runner.Run(new ToolSortBatch(), parts);

        public OperationResult OffsetMillPaths(IList<Part> parts, double offset, MillOffsetSide side, MillPathKinds kinds) =>
            _runner.Run(new MillOffsetBatch(offset, side, kinds), parts);

        public OperationResult CleanOutsideOperations(IList<Part> parts, OutsideOperationKinds kinds) =>
            _runner.Run(new CleanOutsideBatch(kinds), parts);
    }
}
