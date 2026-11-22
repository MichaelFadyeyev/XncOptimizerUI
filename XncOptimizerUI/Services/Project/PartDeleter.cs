using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>
    /// Deletes product parts from the open project together with everything that refers to them:
    /// their XNC operations and their <c>&lt;part id&gt;</c> entries in the CS and EL operations
    /// (the operations themselves are kept). Does not save.
    /// </summary>
    internal sealed class PartDeleter(ProjectDocument document)
    {
        /// <summary>
        /// Deletes the parts with <paramref name="partIds"/>. Every id is resolved first, so an unknown
        /// one leaves the document unchanged. Logs one entry listing the deleted parts.
        /// </summary>
        public OperationResult Delete(IReadOnlyList<int> partIds)
        {
            var log = new OperationLog();

            if (!document.IsOpen)
            {
                return log.Fail("No project is opened.");
            }

            if (partIds.Count == 0)
            {
                return log.Fail("No parts to delete.");
            }

            if (MissingIds(partIds) is { Count: > 0 } missing)
            {
                return log.Fail($"Deleting failed: part id={string.Join(", id=", missing)} not found.");
            }

            var ids = partIds.ToHashSet();
            var parts = PartsWithIds(ids);
            var described = parts.Select(DescribePart).ToList();
            var xncOperations = RemoveXncOperations(ids);

            RemovePartReferences(ids);
            parts.ForEach(part => part.Remove());

            return log.Info(DescribeDeletion(described, xncOperations)).Changed();
        }

        #region Methods

        /// <summary>The ids of <paramref name="partIds"/> no product part has, in the given order.</summary>
        private List<int> MissingIds(IReadOnlyList<int> partIds)
        {
            var known = document.ProductParts().Select(part => part.GetIdIntValue()).ToHashSet();

            return [.. partIds.Where(id => !known.Contains(id)).Distinct()];
        }

        /// <summary>The product parts with <paramref name="ids"/>, in document order.</summary>
        private List<XElement> PartsWithIds(HashSet<int> ids) =>
            [.. document.ProductParts().Where(part => ids.Contains(part.GetIdIntValue()))];

        /// <summary>Removes the XNC operations machining one of the parts; returns how many.</summary>
        private int RemoveXncOperations(HashSet<int> ids)
        {
            var operations = document.XncOperations()
                .Where(operation => operation.GetPart() is { } part && ids.Contains(part.GetIdIntValue()))
                .ToList();

            operations.ForEach(operation => operation.Remove());

            return operations.Count;
        }

        /// <summary>Removes every <c>&lt;part id&gt;</c> of the parts listed by a CS or EL operation.</summary>
        private void RemovePartReferences(HashSet<int> ids) =>
            document.Operations(GibLabTypes.CutOperation)
                .Concat(document.Operations(GibLabTypes.BandOperation))
                .SelectMany(operation => operation.GetParts())
                .Where(reference => ids.Contains(reference.GetIdIntValue()))
                .ToList()
                .ForEach(reference => reference.Remove());

        /// <summary>A part as the batch logs name it: <c>"name" (id=…)</c>.</summary>
        private static string DescribePart(XElement part) => $"\"{part.GetNameValue()}\" (id={part.GetIdIntValue()})";

        /// <summary>
        /// One log entry, a line per part:
        /// <c>Deleted 2 part(s):\n"A" (id=1),\n"B" (id=2),\n3 XNC operation(s) removed</c>.
        /// </summary>
        private static string DescribeDeletion(IReadOnlyList<string> parts, int xncOperations) =>
            string.Join("\n", [$"Deleted {parts.Count} part(s):", .. parts.Select(part => $"{part},"), $"{xncOperations} XNC operation(s) removed"]);

        #endregion
    }
}
