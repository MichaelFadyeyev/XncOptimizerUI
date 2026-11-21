using System.Globalization;
using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>
    /// In-place edits of the open project: parts, part rotation, bore / groove / mill / variable attributes
    /// of an XNC program, and deleting bores, grooves and mills.
    /// </summary>
    public sealed class ProjectEditor(ProjectDocument document, ProjectReader reader) : IProjectEditor
    {
        private readonly PartUpdater _partUpdater = new(document, reader);

        public OperationResult UpdatePart(Part part) => _partUpdater.Update(part);

        /// <summary>All programs are rotated first and only then written back, so a failing program leaves the document untouched.</summary>
        public OperationResult RotatePart(int partId, int targetTurn, bool flipEdgeGrooveTcl)
        {
            var log = new OperationLog();

            if (!document.IsOpen)
            {
                return log.Fail("No project is opened.");
            }

            if (targetTurn is < 0 or > 3)
            {
                return log.Fail($"Turn must be 0..3 (was {targetTurn}).");
            }

            var operations = document.XncOperationsOf(partId);

            if (operations.Count == 0)
            {
                return log.Fail($"Part id={partId} has no XNC program to rotate.");
            }

            try
            {
                operations
                    .Select(operation => (Operation: operation, Program: RotatedProgram(operation, targetTurn, flipEdgeGrooveTcl)))
                    .ToList()
                    .ForEach(r => ApplyRotation(r.Operation, r.Program, targetTurn));
            }
            catch (Exception e)
            {
                return log.Fail($"Part id={partId} rotation failed: {e.Message}");
            }

            return log.Changed();
        }

        public OperationResult UpdateBore(int operationId, int boreIndex, MachiningAttribute attribute, string expression) =>
            EditProgram(operationId, $"Bore #{boreIndex + 1}", program => XncProgramEdits.SetBoreAttribute(program, boreIndex, attribute, expression));

        public OperationResult UpdateMachiningElement(int operationId, XncElementKind kind, int index, MachiningAttribute attribute, string expression) =>
            EditProgram(operationId, $"{kind.DisplayName()} #{index + 1}", program => XncProgramEdits.SetElementAttribute(program, kind, index, attribute, expression));

        public OperationResult UpdateVariable(int operationId, int variableIndex, XncVariableAttribute attribute, string value) =>
            EditProgram(operationId, $"Variable #{variableIndex + 1}", program => XncProgramEdits.SetVariableAttribute(program, variableIndex, attribute, value));

        /// <summary>All programs are edited first and only then written back, so a failing one leaves the document untouched.</summary>
        public OperationResult DeleteMachiningElements(IReadOnlyList<XncElementRef> elements)
        {
            var log = new OperationLog();

            if (!document.IsOpen)
            {
                return log.Fail("No project is opened.");
            }

            if (elements.Count == 0)
            {
                return log.Fail("No elements to delete.");
            }

            List<ProgramDeletion> deletions;

            try
            {
                deletions = [.. elements.GroupBy(element => element.OperationId).Select(DeleteFromProgram)];
            }
            catch (Exception e)
            {
                return log.Fail($"Deleting failed: {e.Message}");
            }

            deletions.ForEach(deletion => XncProgramText.Write(deletion.Operation, deletion.Program));
            deletions
                .GroupBy(deletion => deletion.Part)
                .ToList()
                .ForEach(part => log.Info(DescribeDeletion(part.Key, [.. part])));

            return log.Changed();
        }

        #region Methods

        /// <summary>
        /// One XNC operation's program with elements deleted (not yet written back): the part it machines
        /// (<c>"name" (id=…)</c>), the deleted element count per kind and the tools removed.
        /// </summary>
        private sealed record ProgramDeletion(XElement Operation, string Program, string Part, IReadOnlyList<(XncElementKind Kind, int Count)> Deleted, int Tools);

        /// <summary>The program of one XNC operation with its <paramref name="elements"/> deleted (not yet written back).</summary>
        private ProgramDeletion DeleteFromProgram(IGrouping<int, XncElementRef> elements)
        {
            var operation = FindXncOperation(elements.Key);
            var programDocument = XncProgramText.Load(operation);
            var program = XncProgramText.Root(programDocument);

            var deleted = elements
                .GroupBy(element => element.Kind)
                .Select(kind => (kind.Key, Result: XncProgramEdits.DeleteElements(program, kind.Key, kind.Select(e => e.Index))))
                .ToList();

            return new ProgramDeletion(
                operation,
                XncProgramText.Serialize(programDocument),
                DescribePart(operation),
                [.. deleted.Select(d => (d.Key, d.Result.Elements))],
                deleted.Sum(d => d.Result.Tools));
        }

        /// <summary>
        /// One log line for the deletions in the programs of one part, e.g.
        /// <c>Deleted 2 bore(s) of part "Shelf" (id=1) from XNC id=4, 1 unused tool(s) removed</c>.
        /// </summary>
        private static string DescribeDeletion(string part, IReadOnlyList<ProgramDeletion> deletions)
        {
            var counts = deletions
                .SelectMany(deletion => deletion.Deleted)
                .GroupBy(deleted => deleted.Kind)
                .OrderBy(kind => kind.Key)
                .Select(kind => $"{kind.Sum(deleted => deleted.Count)} {kind.Key.DisplayName().ToLowerInvariant()}(s)");
            var operationIds = deletions.Select(deletion => deletion.Operation.GetIdValue());

            return $"Deleted {string.Join(", ", counts)} of part {part} from XNC id={string.Join(", id=", operationIds)}, "
                + $"{deletions.Sum(deletion => deletion.Tools)} unused tool(s) removed";
        }

        /// <summary>The part machined by <paramref name="operation"/>, as <c>"name" (id=…)</c> like the batch logs.</summary>
        private string DescribePart(XElement operation)
        {
            var partId = operation.GetPart()?.GetIdIntValue();
            var name = document.ProductParts().FirstOrDefault(part => part.GetIdIntValue() == partId)?.GetNameValue();

            return $"\"{name}\" (id={partId})";
        }

        private XElement FindXncOperation(int operationId) =>
            document.XncOperations().FirstOrDefault(o => o.GetIdValue() == operationId.ToString(CultureInfo.InvariantCulture))
                ?? throw new XncProgramFormatException($"XNC operation id={operationId} not found.");

        /// <summary>
        /// Applies <paramref name="edit"/> to the program of XNC operation <paramref name="operationId"/>
        /// and writes it back. An exception thrown by the edit is logged (prefixed with
        /// <paramref name="subject"/>) and leaves the document unchanged.
        /// </summary>
        private OperationResult EditProgram(int operationId, string subject, Action<XElement> edit)
        {
            var log = new OperationLog();

            if (!document.IsOpen)
            {
                return log.Fail("No project is opened.");
            }

            try
            {
                var operation = document.XncOperations().FirstOrDefault(o => o.GetIdValue() == operationId.ToString(CultureInfo.InvariantCulture))
                    ?? throw new XncProgramFormatException("XNC operation not found.");
                var programDocument = XncProgramText.Load(operation);

                edit(XncProgramText.Root(programDocument));
                XncProgramText.Write(operation, XncProgramText.Serialize(programDocument));
            }
            catch (Exception e)
            {
                return log.Fail($"{subject} of operation id={operationId}: {e.Message}");
            }

            return log.Changed();
        }

        /// <summary>Rotated program text of one operation, or <c>null</c> when it already has the target turn.</summary>
        private static string? RotatedProgram(XElement operation, int targetTurn, bool flipEdgeGrooveTcl)
        {
            var quarterTurns = XncProgramRotator.NormalizeQuarterTurns(targetTurn - operation.GetTurnIntValue());

            if (quarterTurns == 0)
            {
                return null;
            }

            var programDocument = XncProgramText.Load(operation);
            XncProgramRotator.RotateProgram(XncProgramText.Root(programDocument), quarterTurns, flipEdgeGrooveTcl);

            return XncProgramText.Serialize(programDocument);
        }

        private static void ApplyRotation(XElement operation, string? program, int targetTurn)
        {
            if (program != null)
            {
                XncProgramText.Write(operation, program);
            }

            operation.SetTurnValue(targetTurn);
        }

        #endregion
    }
}
