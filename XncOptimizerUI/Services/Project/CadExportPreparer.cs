using System.IO;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.Helpers.Enums;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>
    /// Completes a <c>.project</c> exported by the CAD program (a windows-1251 file) with the data GibLab
    /// would have calculated, so it reads like any other project:
    /// <list type="number">
    /// <item>each edge-banding (<c>EL</c>) operation id gets a unique <c>elSymbol</c> letter A..Z;</item>
    /// <item>each product part gets <c>l</c>/<c>w</c> from its detail size <c>dl</c>/<c>dw</c>;</item>
    /// <item>each saw-cut (<c>CS</c>) operation gets its sheet part (the first part of the sheet good its
    /// <c>&lt;material id&gt;</c> names) appended as its last <c>&lt;part&gt;</c>.</item>
    /// </list>
    /// Values already present are kept, so preparing twice changes nothing.
    /// </summary>
    public static class CadExportPreparer
    {
        public static void Prepare(XElement project)
        {
            AssignBandSymbols(project);
            CopyDetailSizes(project);
            AppendSheetParts(project);
        }

        #region Methods

        /// <summary>Gives every EL operation id without an <c>elSymbol</c> the next letter not used yet.</summary>
        private static void AssignBandSymbols(XElement project)
        {
            var operations = OperationsOf(project, GibLabTypes.BandOperation);
            var symbolById = operations
                .Where(o => o.Attribute("elSymbol") != null)
                .GroupBy(o => o.GetIdValue())
                .ToDictionary(g => g.Key ?? string.Empty, g => g.First().Attribute("elSymbol")!.Value);
            var freeSymbols = new Queue<string>(Enum.GetNames<BandSymbols>().Except(symbolById.Values));

            foreach (var operation in operations.Where(o => o.Attribute("elSymbol") == null))
            {
                var id = operation.GetIdValue() ?? string.Empty;

                if (!symbolById.TryGetValue(id, out var symbol))
                {
                    symbol = freeSymbols.Count > 0
                        ? freeSymbols.Dequeue()
                        : throw new InvalidDataException("More than 26 edge-banding (EL) operations: no band symbol left (A..Z).");
                    symbolById[id] = symbol;
                }

                operation.SetAttributeValue("elSymbol", symbol);
            }
        }

        /// <summary>Sets each product part's missing <c>l</c>/<c>w</c> to its <c>dl</c>/<c>dw</c>.</summary>
        private static void CopyDetailSizes(XElement project)
        {
            var parts = GoodsOf(project, GibLabTypes.ProductGood).SelectMany(g => g.GetParts());

            foreach (var part in parts)
            {
                CopyAttributeIfMissing(part, from: "dl", to: "l");
                CopyAttributeIfMissing(part, from: "dw", to: "w");
            }
        }

        private static void CopyAttributeIfMissing(XElement element, string from, string to)
        {
            if (element.Attribute(to) == null && element.Attribute(from) is { } source)
            {
                element.SetAttributeValue(to, source.Value);
            }
        }

        /// <summary>Appends to each CS operation the first part of its material's sheet good, unless listed already.</summary>
        private static void AppendSheetParts(XElement project)
        {
            var sheets = GoodsOf(project, GibLabTypes.SheetGood);

            foreach (var operation in OperationsOf(project, GibLabTypes.CutOperation))
            {
                var sheetPartId = SheetPartId(operation, sheets);

                if (!operation.GetParts().Any(p => p.GetIdValue() == sheetPartId))
                {
                    operation.Add(new XElement("part", new XAttribute("id", sheetPartId)));
                }
            }
        }

        /// <summary>Id of the first part of the sheet good named by the operation's <c>&lt;material id&gt;</c>.</summary>
        private static string SheetPartId(XElement operation, List<XElement> sheets)
        {
            var operationId = operation.GetIdValue();
            var materialId = operation.GetMat()?.GetIdValue()
                ?? throw new InvalidDataException($"Cut operation id={operationId} has no <material id>.");
            var sheet = sheets.FirstOrDefault(g => g.GetIdValue() == materialId)
                ?? throw new InvalidDataException($"Cut operation id={operationId}: no sheet good with id={materialId}.");

            return sheet.GetParts().FirstOrDefault()?.GetIdValue()
                ?? throw new InvalidDataException($"Sheet good id={materialId} has no <part id>.");
        }

        private static List<XElement> OperationsOf(XElement project, string typeId) =>
            project.GetOperations().Where(o => o.GetTypeIdValue() == typeId).ToList();

        private static List<XElement> GoodsOf(XElement project, string typeId) =>
            project.GetGoods().Where(g => g.GetTypeIdValue() == typeId).ToList();

        #endregion
    }
}
