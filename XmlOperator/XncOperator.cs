using System.IO;
using System.Text;
using System.Xml.Linq;
using XncOptimizer.Extensions;

namespace XmlOperator
{
    public class XncOperator
    {
        const string Optimized = "optimized";

        private string _fullPath = string.Empty;

        public XncOperator(string fullPath)
        {
            _fullPath = fullPath;
        }

        public void Execute(ref string log)
        {
            XDocument? doc;
            XElement? project;
            Dictionary<string, string> partOperations = [];
            Dictionary<string, string> partsOldNewIds = [];
            Dictionary<string, string> sheetOldNewIds = [];

            string path = string.Empty;
            string source = string.Empty;

            path = Path.GetDirectoryName(_fullPath) ?? string.Empty;
            source = Path.GetFileName(_fullPath);

            doc = XDocument.Load(_fullPath);
            project = doc.GetProject()!;

            var xncOperations = project.GetOperations() // XNC operations
                .Where(e => e.GetTypeIdValue() == "XNC")
                .ToList();

            var csOperations = project.GetOperations() // sheet operations
                .Where(e => e.GetTypeIdValue() == "CS")
                .ToList();

            var elOperations = project.GetOperations() // band operations
                .Where(e => e.GetTypeIdValue() == "EL")
                .ToList();

            var sheetGoods = project.GetGoods() // sheet materials
                .Where(e => e.GetTypeIdValue() == "sheet")
                .ToList();

            var format = xncOperations.Count.ToString().Length;
            var goods = project.GetGoods().Where(e => e.GetTypeIdValue() == "product").ToList();

            for (int i = 0; i < xncOperations.Count; i++)
            {
                var operation = xncOperations[i];

                if (operation.Attribute(Optimized) != null) continue;

                var groupCode = (i + 1).ToString().PadLeft(format, '0');
                var partId = operation.GetPart()!.GetIdValue()!;

                AddToPartOperations(groupCode, partId);
                AppendGroupCode(groupCode, operation);

                if (i == xncOperations.Count - 1) continue;

                for (int j = i + 1; j < xncOperations.Count; j++)
                {
                    var comparedOperation = xncOperations[j];

                    if (comparedOperation.Attribute(Optimized) != null) continue;
                    if (operation.GetProgramValue() != comparedOperation.GetProgramValue()) continue;

                    var part1 = goods.GetParts().FirstOrDefault(e => e.GetIdValue()
                        == operation.GetPart()!.GetIdValue());

                    var part2 = goods.GetParts().FirstOrDefault(e => e.GetIdValue()
                        == comparedOperation.GetPart()!.GetIdValue());

                    if (!CheckBendsAreIdentical(part1!, part2!)) continue;

                    partId = comparedOperation.GetPart()!.GetIdValue()!;

                    AddToPartOperations(groupCode, partId);
                    AppendGroupCode(groupCode, comparedOperation);
                }
            }

            List<XElement> orderedXncOperations = [.. xncOperations.OrderBy(o => o.GetGroupCodeValue())];
            project.GetOperations().Where(e => e.GetTypeIdValue() == "XNC").Remove();

            var operations = project.GetOperations().ToList();
            var maxOpIndex = 0;

            foreach (var operation in operations)
            {
                var intId = int.Parse(operation.GetIdValue()!);
                if (intId > maxOpIndex) maxOpIndex = intId;
            }

            maxOpIndex++;

            for (var i = 0; i < orderedXncOperations.Count; i++)
            {
                orderedXncOperations[i].SetIdValue((maxOpIndex + i).ToString());
                project.Add(orderedXncOperations[i]);
            }

            //*
            var xncFreeParts = new List<XElement>(); // new list for all parts without xnc
            var xncParts = new List<XElement>(); // new list for all parts with xnc

            foreach (var good in goods)
            {
                var parts = good.GetParts().ToList();

                foreach (var part in parts)
                {
                    var partId = part.GetIdValue()!;
                    partOperations.TryGetValue(partId, out string? groupCode);
                    var name = part.GetNameValue();

                    if (groupCode != null)
                    {
                        part.SetNameValue($"[{groupCode}]{name}");
                        xncParts.Add(part);
                    }
                    else
                    {
                        xncFreeParts.Add(part);
                    }
                }
            }

            var sortedParts = new List<XElement>();

            foreach (var operation in orderedXncOperations) // add ordered xncParts
            {
                var operationPartId = operation.GetPart()!.GetIdValue();
                var part = xncParts.First(p => p.GetIdValue() == operationPartId);
                if (!sortedParts.Exists(p => p.GetIdValue() == operationPartId))
                {
                    sortedParts.Add(new XElement(part));
                }
            }

            sortedParts.AddRange([.. xncFreeParts]); // add unordered parts without xnc

            var partsCount = sortedParts.Count;

            for (var i = 1; i <= csOperations.Count; i++) // assign new ids to csOperations
            {
                var part = csOperations[i - 1].GetParts().Last();
                var oldId = part.GetIdValue()!;
                var newId = (partsCount + i).ToString();

                sheetOldNewIds.Add(oldId, newId);
            }

            var id = goods.First().GetIdValue()!; // store id of first existed good

            project.GetGoods().Where(e => e.GetTypeIdValue() == "product")
                    .Remove(); // remove all goods (is "products") with parts

            // create new good is one for all parts; assign stored id
            var newGood =
                new XElement("good",
                        new XAttribute("typeId", "product"),
                        new XAttribute("code", "09"),
                        new XAttribute("cost", "0"),
                        new XAttribute("costMaterial", "0"),
                        new XAttribute("costOperation", "0"),
                        new XAttribute("count", "1"),
                        new XAttribute("id", id),
                        new XAttribute("name", "09"),
                        new XAttribute("product.import", "bm.1.84")
                    );

            log += $"Parts count: {sortedParts.Count}\n";

            for (var i = 1; i <= sortedParts.Count; i++)
            {
                var part = sortedParts[i - 1];
                var oldId = part.GetIdValue()!;
                var newId = (i).ToString();
                part.SetIdValue(newId);
                newGood.Add(part);
                partsOldNewIds.Add(oldId, newId);
                log += $"{part.GetIdValue()!.PadLeft(format, '0')} -> {part.GetNameValue()}\n";
            }

            foreach (var operation in xncOperations)
            {
                var parts = operation.GetParts().ToList();

                for (var i = 0; i < parts.Count; i++)
                {
                    var oldId = parts[i].GetIdValue()!;
                    var newId = partsOldNewIds[oldId];
                    parts[i].SetIdValue(newId);
                }
            }

            foreach (var operation in csOperations)
            {
                var parts = operation.GetParts().ToList();

                for (var i = 0; i < parts.Count - 1; i++)
                {
                    var oldId = parts[i].GetIdValue()!;
                    var newId = partsOldNewIds[oldId];
                    parts[i].SetIdValue(newId);
                }

                var sheetOldId = parts.Last().GetIdValue()!;
                var sheetNewId = sheetOldNewIds[sheetOldId];
                parts.Last().SetIdValue(sheetNewId);
                var sheetPart = new XElement(parts.Last());
                parts.Last().Remove();

                List<XElement> orederedParts = [.. parts.OrderBy(p => int.Parse(p.GetIdValue()!))];
                operation.GetParts().Remove();
                orederedParts.Add(sheetPart);
                operation.Add(orederedParts);
            }

            foreach (var operation in elOperations)
            {
                var parts = operation.GetParts().ToList();

                for (var i = 0; i < parts.Count; i++)
                {
                    var oldId = parts[i].GetIdValue()!;
                    var newId = partsOldNewIds[oldId];
                    parts[i].SetIdValue(newId);
                }
            }

            foreach (var good in sheetGoods)
            {
                var oldId = good.GetPart()!.GetIdValue()!;
                var newId = sheetOldNewIds[oldId];
                good.GetPart()!.SetIdValue(newId);
            }

            project.Add(newGood);

            AppendDescription();

            var result = source.Replace(".project", "_opt.project");
            _fullPath = Path.Combine(path, result);

            doc.Save(_fullPath);

            log += $"***\nStored to: {_fullPath}";

            #region METHODES

            static void AppendGroupCode(string groupCode, XElement operation)
            {
                operation.Add(new XAttribute("optimized", "true"));
                operation.Add(new XAttribute("groupCode", $"{groupCode}"));
                operation.SetCodeValue($"{groupCode}--{operation.GetCodeValue()}");
                operation.SetTypeNameValue($"[{groupCode}]{operation.GetTypeNameValue()}");
            }

            void AddToPartOperations(string groupCode, string partId)
            {
                if (partOperations.TryGetValue(partId, out string? value))
                {
                    partOperations[partId] = $"{value}+{groupCode}";
                    return;
                }

                partOperations.Add(partId, groupCode);
            }

            void AppendDescription()
            {
                var description = $"{DateTime.Now:yyyy-MM-dd hh:mm:ss} -> added XNC group codes";

                if (project.Attribute("description") == null)
                {
                    project.Add(new XAttribute("description", description));
                    return;
                }

                project.Attribute("description")!.Value = description;
            }

            bool CheckBendsAreIdentical(XElement part1, XElement part2)
            {
                if (part1.GetElbMat() == null && part2.GetElbMat() != null
                    || part1.GetElbMat() != null && part2.GetElbMat() == null) return false;

                if (part1.GetEllMat() == null && part2.GetEllMat() != null
                    || part1.GetEllMat() != null && part2.GetEllMat() == null) return false;

                if (part1.GetElrMat() == null && part2.GetElrMat() != null
                    || part1.GetElrMat() != null && part2.GetElrMat() == null) return false;

                if (part1.GetEltMat() == null && part2.GetEltMat() != null
                    || part1.GetEltMat() != null && part2.GetEltMat() == null) return false;

                return (part1.GetElbMat() != null && part1.GetElbMatValue() == part2.GetElbMatValue() || true)
                    && (part1.GetEllMat() != null && part1.GetEllMatValue() == part2.GetEllMatValue() || true)
                    && (part1.GetElrMat() != null && part1.GetElrMatValue() == part2.GetElrMatValue() || true)
                    && (part1.GetEltMat() != null && part1.GetEltMatValue() == part2.GetEltMatValue() || true);
            }

            bool WrongFileType(string fileName)
            {
                return Path.GetExtension(fileName) != "project";
            }

            bool FileNotExists(string fullPath)
            {
                return !Path.Exists(fullPath);
            }

            #endregion
        }
    }
}

