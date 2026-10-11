using System.Globalization;
using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>
    /// "Group identical elements": XNC operations with the same program on parts with the same
    /// band materials share a group code. Each operation gets <c>[code]</c> prepended to its name and
    /// <c>code--</c> to its code; parts are renamed <c>[code]name</c> (<c>[a+b]</c> for several
    /// operations), every part moves into one new product good numbered in group order (parts
    /// without XNC last), sheets are renumbered after them, and every operation's references follow.
    /// Saved as a new <see cref="ProjectFileNames.GroupedSuffix"/> file.
    /// </summary>
    internal sealed class IdenticalPartsGrouper(ProjectDocument document)
    {
        private const string OptimizedAttribute = "optimized";

        /// <summary>Old → new id of every part or sheet that is renumbered.</summary>
        private sealed class IdMap : Dictionary<string, string>;

        public OperationResult Group()
        {
            var log = new OperationLog();
            var xncOperations = document.XncOperations();
            var codeWidth = xncOperations.Count.ToString(CultureInfo.InvariantCulture).Length;

            try
            {
                var groupCodesByPart = AssignGroupCodes(xncOperations, codeWidth);

                if (groupCodesByPart.Count == 0)
                {
                    return log.Fail("File seems to be already optimized or contains no XNC.");
                }

                var orderedOperations = ReinsertXncOperationsInGroupOrder(xncOperations);
                var sortedParts = SortParts(orderedOperations, groupCodesByPart);
                var sheetIds = RenumberSheets(sortedParts.Count);
                var partIds = MoveIntoSingleProductGood(sortedParts, log, codeWidth);

                RenumberOperationParts(xncOperations, partIds);
                RenumberCutOperations(partIds, sheetIds);
                RenumberOperationParts(document.Operations(GibLabTypes.BandOperation), partIds);
                RenumberSheetGoods(sheetIds);

                document.AppendDescription("grouped by XNC");

                return log.Highlight($"Stored to: {document.SaveAsSuffixed(ProjectFileNames.GroupedSuffix)}").Changed();
            }
            catch (Exception e)
            {
                return log.Fail(e.Message);
            }
        }

        #region Methods

        /// <summary>
        /// Gives every not yet optimized operation, in document order, the next code and the same
        /// code to each later operation identical to it. Returns the code(s) of every part.
        /// </summary>
        private Dictionary<string, string> AssignGroupCodes(List<XElement> operations, int codeWidth)
        {
            var codesByPart = new Dictionary<string, string>();
            var productParts = document.ProductParts();

            void Assign(string groupCode, XElement operation)
            {
                var partId = operation.GetPart()!.GetIdValue()!;
                codesByPart[partId] = codesByPart.TryGetValue(partId, out var codes) ? $"{codes}+{groupCode}" : groupCode;
                MarkGrouped(groupCode, operation);
            }

            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];

                if (IsGrouped(operation)) continue;

                var groupCode = (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(codeWidth, '0');
                Assign(groupCode, operation);

                operations
                    .Skip(i + 1)
                    .Where(other => !IsGrouped(other) && IsIdentical(operation, other, productParts))
                    .ToList()
                    .ForEach(other => Assign(groupCode, other));
            }

            return codesByPart;
        }

        private static bool IsGrouped(XElement operation) => operation.Attribute(OptimizedAttribute) != null;

        /// <summary>Same program, and the two parts have the same band material on every edge (an unbanded edge only matches an unbanded edge).</summary>
        private static bool IsIdentical(XElement operation, XElement other, List<XElement> productParts)
        {
            if (operation.GetProgramValue() != other.GetProgramValue()) return false;

            XElement PartOf(XElement op) => productParts.First(p => p.GetIdValue() == op.GetPart()!.GetIdValue());

            var part1 = PartOf(operation);
            var part2 = PartOf(other);

            return part1.GetElbMatValue() == part2.GetElbMatValue()
                && part1.GetEllMatValue() == part2.GetEllMatValue()
                && part1.GetElrMatValue() == part2.GetElrMatValue()
                && part1.GetEltMatValue() == part2.GetEltMatValue();
        }

        private static void MarkGrouped(string groupCode, XElement operation)
        {
            operation.Add(new XAttribute(OptimizedAttribute, "true"));
            operation.Add(new XAttribute("groupCode", groupCode));
            operation.SetCodeValue($"{groupCode}--{operation.GetCodeValue()}");
            operation.SetTypeNameValue($"[{groupCode}]{operation.GetTypeNameValue()}");
        }

        /// <summary>Moves the XNC operations to the end of the project in group-code order, numbered after the other operations.</summary>
        private List<XElement> ReinsertXncOperationsInGroupOrder(List<XElement> xncOperations)
        {
            List<XElement> ordered = [.. xncOperations.OrderBy(o => o.GetGroupCodeValue())];

            document.Project.GetOperations().Where(o => o.GetTypeIdValue() == GibLabTypes.XncOperation).Remove();

            var firstId = document.Project.GetOperations()
                .Select(o => int.Parse(o.GetIdValue()!, CultureInfo.InvariantCulture))
                .DefaultIfEmpty(0)
                .Max() + 1;

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].SetIdValue((firstId + i).ToString(CultureInfo.InvariantCulture));
                document.Project.Add(ordered[i]);
            }

            return ordered;
        }

        /// <summary>Renames grouped parts <c>[code]name</c>; returns them in group order (each once), then the parts without XNC.</summary>
        private List<XElement> SortParts(List<XElement> orderedOperations, Dictionary<string, string> groupCodesByPart)
        {
            var grouped = new List<XElement>();
            var ungrouped = new List<XElement>();

            foreach (var part in document.ProductParts())
            {
                if (groupCodesByPart.TryGetValue(part.GetIdValue()!, out var groupCode))
                {
                    part.SetNameValue($"[{groupCode}]{part.GetNameValue()}");
                    grouped.Add(part);
                }
                else
                {
                    ungrouped.Add(part);
                }
            }

            var sorted = orderedOperations
                .Select(o => o.GetPart()!.GetIdValue())
                .Distinct()
                .Select(partId => new XElement(grouped.First(p => p.GetIdValue() == partId)))
                .ToList();

            return [.. sorted, .. ungrouped];
        }

        /// <summary>Sheets (each CS operation's last part) are numbered right after the <paramref name="partsCount"/> parts.</summary>
        private IdMap RenumberSheets(int partsCount)
        {
            var map = new IdMap();
            var cutOperations = document.Operations(GibLabTypes.CutOperation);

            for (var i = 1; i <= cutOperations.Count; i++)
            {
                map.Add(cutOperations[i - 1].GetParts().Last().GetIdValue()!, (partsCount + i).ToString(CultureInfo.InvariantCulture));
            }

            return map;
        }

        /// <summary>Replaces every product good by one good holding <paramref name="sortedParts"/>, numbered from 1; logs each part.</summary>
        private IdMap MoveIntoSingleProductGood(List<XElement> sortedParts, OperationLog log, int codeWidth)
        {
            var productGoods = document.Goods(GibLabTypes.ProductGood);
            var good = CreateProductGood(productGoods[0].GetIdValue()!);
            var map = new IdMap();

            document.Project.GetGoods().Where(g => g.GetTypeIdValue() == GibLabTypes.ProductGood).Remove();

            log.Info($"Parts count: {sortedParts.Count}");

            for (var i = 1; i <= sortedParts.Count; i++)
            {
                var part = sortedParts[i - 1];
                var newId = i.ToString(CultureInfo.InvariantCulture);

                map.Add(part.GetIdValue()!, newId);
                part.SetIdValue(newId);
                good.Add(part);
                log.Info($"{newId.PadLeft(codeWidth, '0')} -> {part.GetNameValue()}");
            }

            document.Project.Add(good);

            return map;
        }

        private static XElement CreateProductGood(string id) =>
            new("good",
                new XAttribute("typeId", GibLabTypes.ProductGood),
                new XAttribute("code", "000"),
                new XAttribute("cost", "0"),
                new XAttribute("costMaterial", "0"),
                new XAttribute("costOperation", "0"),
                new XAttribute("count", "1"),
                new XAttribute("id", id),
                new XAttribute("xncTypeName", "000"),
                new XAttribute("product.import", "bm.1.84"));

        private static void RenumberOperationParts(IEnumerable<XElement> operations, IdMap partIds)
        {
            foreach (var reference in operations.SelectMany(o => o.GetParts()))
            {
                reference.SetIdValue(partIds[reference.GetIdValue()!]);
            }
        }

        /// <summary>Renumbers each CS operation's parts and sheet, then lists the parts by id with the sheet last.</summary>
        private void RenumberCutOperations(IdMap partIds, IdMap sheetIds)
        {
            foreach (var operation in document.Operations(GibLabTypes.CutOperation))
            {
                var references = operation.GetParts().ToList();
                var sheet = references[^1];
                var parts = references.SkipLast(1).ToList();

                parts.ForEach(p => p.SetIdValue(partIds[p.GetIdValue()!]));
                sheet.SetIdValue(sheetIds[sheet.GetIdValue()!]);

                operation.GetParts().Remove();
                operation.Add(parts.OrderBy(p => int.Parse(p.GetIdValue()!, CultureInfo.InvariantCulture)).Append(sheet));
            }
        }

        private void RenumberSheetGoods(IdMap sheetIds)
        {
            foreach (var sheetPart in document.Goods(GibLabTypes.SheetGood).Select(g => g.GetPart()!))
            {
                sheetPart.SetIdValue(sheetIds[sheetPart.GetIdValue()!]);
            }
        }

        #endregion
    }
}
