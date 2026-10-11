using System.Globalization;
using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;
using static XncOptimizerUI.Services.Xnc.XncProgramMath;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>In-place edits of the open project: parts, part rotation, and bore / variable attributes of an XNC program.</summary>
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

        public OperationResult UpdateBore(int operationId, int boreIndex, BoreAttribute attribute, string expression) =>
            EditProgram(operationId, $"Bore #{boreIndex + 1}", program => XncProgramEdits.SetBoreAttribute(program, boreIndex, attribute, expression));

        public OperationResult UpdateVariable(int operationId, int variableIndex, XncVariableAttribute attribute, string value) =>
            EditProgram(operationId, $"Variable #{variableIndex + 1}", program => XncProgramEdits.SetVariableAttribute(program, variableIndex, attribute, value));

        #region Methods

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
            var currentTurn = int.TryParse(operation.GetTurnValue(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn) ? turn : 0;
            var quarterTurns = XncProgramRotator.NormalizeQuarterTurns(targetTurn - currentTurn);

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
