using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.Helpers.Enums;

namespace XncOptimizerUI.Services
{
    public class GibLabProjectService : IProjectService
    {
        const string Optimized = "optimized";

        private string _fullPath = string.Empty;
        XDocument? _doc;
        XElement? _project;
        string _path = string.Empty;
        string _source = string.Empty;
        List<XElement> _xncOperations = [];
        List<XElement> _csOperations = [];
        List<XElement> _elOperations = [];
        List<XElement> _sheetGoods = [];
        List<XElement> _bandGoods = [];
        List<XElement> _productGoods = [];

        List<Band> _bands = [];
        List<Sheet> _sheets = [];

        public string FullPath
        {
            get => _fullPath;
        }

        public GibLabProjectService() { }

        public void GroupIdenticalElements(ref string log)
        {

            Dictionary<string, string> partOperations = [];
            Dictionary<string, string> partsOldNewIds = [];
            Dictionary<string, string> sheetOldNewIds = [];

            _xncOperations = GetXncOperations();
            _csOperations = GetCsOperations();
            _elOperations = GetElOperations();
            _sheetGoods = GetSheetGoods();

            var format = _xncOperations.Count.ToString().Length;

            try
            {
                _productGoods = GetProductGoods();

                for (int i = 0; i < _xncOperations.Count; i++)
                {
                    var operation = _xncOperations[i];

                    if (operation.Attribute(Optimized) != null) continue;

                    var groupCode = (i + 1).ToString().PadLeft(format, '0');
                    var partId = operation.GetPart()!.GetIdValue()!;

                    AddToPartOperations(groupCode, partId);
                    AppendGroupCode(groupCode, operation);

                    if (i == _xncOperations.Count - 1) continue;

                    for (int j = i + 1; j < _xncOperations.Count; j++)
                    {
                        var comparedOperation = _xncOperations[j];

                        if (comparedOperation.Attribute(Optimized) != null) continue;
                        if (operation.GetProgramValue() != comparedOperation.GetProgramValue()) continue;

                        var part1 = _productGoods.GetParts().FirstOrDefault(e => e.GetIdValue()
                            == operation.GetPart()!.GetIdValue());

                        var part2 = _productGoods.GetParts().FirstOrDefault(e => e.GetIdValue()
                            == comparedOperation.GetPart()!.GetIdValue());

                        if (!CheckBendsAreIdentical(part1!, part2!)) continue;

                        partId = comparedOperation.GetPart()!.GetIdValue()!;

                        AddToPartOperations(groupCode, partId);
                        AppendGroupCode(groupCode, comparedOperation);
                    }
                }

                if (partOperations.Count == 0)
                {
                    var message = "File seems to be already optimized or contains no XNC.";
                    MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    log += $"***\n{message}";
                    return;
                }

                List<XElement> orderedXncOperations = [.. _xncOperations.OrderBy(o => o.GetGroupCodeValue())];
                _project!.GetOperations().Where(e => e.GetTypeIdValue() == "XNC").Remove();

                var operations = _project!.GetOperations().ToList();
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
                    _project!.Add(orderedXncOperations[i]);
                }

                var xncFreeParts = new List<XElement>(); // new list for all parts without xnc
                var xncParts = new List<XElement>(); // new list for all parts with xnc

                foreach (var good in _productGoods)
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

                for (var i = 1; i <= _csOperations.Count; i++) // assign new ids to _csOperations
                {
                    var part = _csOperations[i - 1].GetParts().Last();
                    var oldId = part.GetIdValue()!;
                    var newId = (partsCount + i).ToString();

                    sheetOldNewIds.Add(oldId, newId);
                }

                var id = _productGoods.First().GetIdValue()!; // store id of first existed good

                _project!.GetGoods().Where(e => e.GetTypeIdValue() == "product")
                        .Remove(); // remove all _productGoods (is "products") with parts

                // create new good is one for all parts; assign stored id
                var newGood =
                    new XElement("good",
                            new XAttribute("typeId", "product"),
                            new XAttribute("code", "000"),
                            new XAttribute("cost", "0"),
                            new XAttribute("costMaterial", "0"),
                            new XAttribute("costOperation", "0"),
                            new XAttribute("count", "1"),
                            new XAttribute("id", id),
                            new XAttribute("xncTypeName", "000"),
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

                foreach (var operation in _xncOperations)
                {
                    var parts = operation.GetParts().ToList();

                    for (var i = 0; i < parts.Count; i++)
                    {
                        var oldId = parts[i].GetIdValue()!;
                        var newId = partsOldNewIds[oldId];

                        parts[i].SetIdValue(newId);
                    }
                }

                foreach (var operation in _csOperations)
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

                foreach (var operation in _elOperations)
                {
                    var parts = operation.GetParts().ToList();

                    for (var i = 0; i < parts.Count; i++)
                    {
                        var oldId = parts[i].GetIdValue()!;
                        var newId = partsOldNewIds[oldId];

                        parts[i].SetIdValue(newId);
                    }
                }

                foreach (var good in _sheetGoods)
                {
                    var oldId = good.GetPart()!.GetIdValue()!;
                    var newId = sheetOldNewIds[oldId];

                    good.GetPart()!.SetIdValue(newId);
                }

                _project!.Add(newGood);

                AppendDescription("grouped by XNC");

                var result = GetNewFileName();


                _fullPath = Path.Combine(_path, result);
                _doc!.Save(_fullPath);

                log += $"***\nStored to: {_fullPath}";
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                log += $"***\n{e.Message}";
            }

            #region LOCAL_FUNCTIONS

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

            #endregion

        }

        public void PrepForSplitAlongX(ref string log, string searchText)
        {
            var _productGoods = _project!.GetGoods()
                .Where(e => e.GetTypeIdValue() == "product")
                .ToList();

            var _xncOperations = _project!.GetOperations() // XNC operations
                .Where(e => e.GetTypeIdValue() == "XNC")
                .ToList();

            foreach (var good in _productGoods)
            {
                var parts = good.GetParts().Where(p => p.GetPartNameValue()!.Contains(searchText));

                foreach (var part in parts)
                {
                    var name = part.GetPartNameValue();

                    var dw = part.Attribute("dw");
                    var cw = part.Attribute("cw");
                    var w = part.Attribute("w");

                    var width = XmlConvert.ToDecimal(dw?.Value ?? throw new ArgumentException()) * 2 + ConfigService.SawWidth;
                    var storedWidth = XmlConvert.ToString(width);

                    dw.SetValue(storedWidth);
                    cw!.SetValue(storedWidth);
                    w!.SetValue(storedWidth);

                    var count = Int32.Parse(part.Attribute("count")!.Value);
                    var newCount = count / 2 + count % 2;

                    part.Attribute("count")!.SetValue(newCount);

                    var partXncOperations = _xncOperations.Where(x => x.Attribute("typeName")!.Value == name).ToList();

                    foreach (var xncOperation in partXncOperations)
                    {
                        var programAttribute = xncOperation.GetProgram();
                        var programInnerXml = XDocument.Parse(WebUtility.HtmlDecode(programAttribute!.Value!));
                        var program = programInnerXml.Element("program");
                        var dy = program!.Attribute("dy")!.Value;

                        program!.Attribute("dy")!.SetValue(width);

                        var bores = program!.Elements().Where(e => ElementIsBore(e.Name.ToString())).ToList();
                        var boreCount = bores.Count;

                        foreach (var bore in bores)
                        {
                            var boreType = bore.Name.ToString();
                            switch (boreType)
                            {
                                case "bf":
                                case "bl":
                                case "br":
                                    {
                                        var newBore = new XElement(bore);
                                        var y = bore.Attribute("y");
                                        var yValue = XmlConvert.ToDecimal(y?.Value ?? throw new ArgumentException());

                                        y.SetValue(XmlConvert.ToString(width - yValue));
                                        program.Add(newBore);
                                        boreCount++;
                                    }
                                    break;
                                case "bt":
                                case "bb":
                                    {
                                        var attributes = bore.Attributes();
                                        var newBore = new XElement(boreType == "bt" ? "bb" : "bt", attributes);

                                        program.Add(newBore);
                                        boreCount++;
                                    }
                                    break;
                            }
                        }

                        xncOperation.Attribute("count")!.SetValue($"{newCount}");
                        xncOperation.Attribute("countBore")!.SetValue($"{boreCount}");

                        programAttribute.Value = program.ToString();
                    }

                    log += $"{name} resized to {storedWidth}; count changed: {count} -> {newCount}\n";
                }
            }

            AppendDescription("specified parts prep for split along X");

            var result = GetNewFileName();

            _fullPath = Path.Combine(_path, result);
            _doc!.Save(_fullPath);

            log += $"***\nStored to: {_fullPath}";
        }

        public void PrepForSplitAlongX(ref string log, string[] selectedPartsIds)
        {
            var _productGoods = _project!.GetGoods()
                .Where(e => e.GetTypeIdValue() == "product");

            var _xncOperations = _project!.GetOperations() // XNC operations
                .Where(e => e.GetTypeIdValue() == "XNC");

            foreach (var good in _productGoods)
            {
                var parts = good.GetParts().Where(p => selectedPartsIds.Contains(p.GetIdValue()!));
                foreach (var part in parts)
                {
                    var name = part.GetPartNameValue();

                    var dw = part.Attribute("dw");
                    var cw = part.Attribute("cw");
                    var w = part.Attribute("w");

                    var width = XmlConvert.ToDecimal(dw?.Value
                        ?? throw new ArgumentException("""XAttribute "dw" not found or has no value""")) * 2 + ConfigService.SawWidth;
                    var storedWidth = XmlConvert.ToString(width);

                    dw.SetValue(storedWidth);
                    cw!.SetValue(storedWidth);
                    w!.SetValue(storedWidth);

                    var count = Int32.Parse(part.Attribute("count")!.Value);
                    var newCount = count / 2 + count % 2;

                    part.Attribute("count")!.SetValue(newCount);

                    var partXncOperations = _xncOperations.Where(x => x.Attribute("typeName")!.Value == name).ToList();

                    foreach (var xncOperation in partXncOperations)
                    {
                        var programAttribute = xncOperation.GetProgram();
                        var programInnerXml = XDocument.Parse(WebUtility.HtmlDecode(programAttribute!.Value!));
                        var program = programInnerXml.Element("program");
                        var dy = program!.Attribute("dy")!.Value;

                        program!.Attribute("dy")!.SetValue(width);

                        var bores = program!.Elements().Where(e => ElementIsBore(e.Name.ToString())).ToList();
                        var boreCount = bores.Count;

                        foreach (var bore in bores)
                        {
                            var boreType = bore.Name.ToString();
                            switch (boreType)
                            {
                                case "bf":
                                case "bl":
                                case "br":
                                    {
                                        var newBore = new XElement(bore);
                                        var y = bore.Attribute("y");
                                        var yValue = XmlConvert.ToDecimal(y?.Value ?? throw new ArgumentException());

                                        y.SetValue(XmlConvert.ToString(width - yValue));
                                        program.Add(newBore);
                                        boreCount++;
                                    }
                                    break;
                                case "bt":
                                case "bb":
                                    {
                                        var attributes = bore.Attributes();
                                        var newBore = new XElement(boreType == "bt" ? "bb" : "bt", attributes);

                                        program.Add(newBore);
                                        boreCount++;
                                    }
                                    break;
                            }
                        }

                        xncOperation.Attribute("count")!.SetValue($"{newCount}");
                        xncOperation.Attribute("countBore")!.SetValue($"{boreCount}");

                        programAttribute.Value = program.ToString();
                    }

                    log += $"{name} resized to {storedWidth}; count changed: {count} -> {newCount}\n";
                }

            }

            AppendDescription("specified parts prep for split along X");

            var result = GetNewFileName();

            _fullPath = Path.Combine(_path, result);
            _doc!.Save(_fullPath);

            log += $"***\nStored to: {_fullPath}";
        }


        #region GLOBAL_METHODES

        public void OpenProject(string fullPath)
        {
            _fullPath = fullPath;
            _path = Path.GetDirectoryName(_fullPath) ?? string.Empty;
            _source = Path.GetFileName(_fullPath);
            _doc = XDocument.Load(_fullPath);
            _project = _doc.GetProject() ?? throw new Exception($"""File "{_fullPath}" contains wrong data""");
        }


        public void CloseProject()
        {
            _fullPath = string.Empty;
            _doc = null;
            _project = null;
            _xncOperations = [];
            _csOperations = [];
            _elOperations = [];
            _sheetGoods = [];
            _bandGoods = [];
            _productGoods = [];
            _bands = [];
            _sheets = [];
        }

        public void SaveProject()
        {
            if (string.IsNullOrEmpty(_fullPath) || _doc == null)
            {
                throw new Exception("No project is opened");
            }
            _doc.Save(_fullPath);
        }

        private string GetNewFileName()
        {
            // TODO Implement check if file exists
            var regex1 = new Regex(@"_opt\.project$");
            var regex2 = new Regex(@"_opt\((\d*)\)\.project$");

            if (!regex1.IsMatch(_source) && !regex2.IsMatch(_source))
            {
                return _source.Replace(".project", "_opt.project");
            }

            if (regex1.IsMatch(_source))
            {
                return regex1.Replace(_source, "_opt(1).project");
            }

            var collection = regex2.Matches(_source);
            var version = int.Parse(collection[0].Groups[1].Value);

            return regex2.Replace(_source, $"_opt({version + 1}).project");
        }

        private static Part CreatePart(XElement element)
        {
            return new Part()
            {
                Id = element.GetIdIntValue(),
                Name = element.GetNameValue()!,
                Count = int.Parse(element.Attribute("count")!.Value),
                Length = element.GetLengthDecimalValue(),
                Width = element.GetWidthDecimalValue(),
                TopBandingId = element.GetEltIdIntValue(),
                BottomBandingId = element.GetElbIdIntValue(),
                LeftBandingId = element.GetEllIdIntValue(),
                RightBandingId = element.GetElrIdIntValue()
            };
        }

        public bool UpdatePart(Part part)
        {
            var partToUpdate = GetProductGoods()
                .SelectMany(g => g.GetParts())
                .FirstOrDefault(p => p.GetIdIntValue() == part.Id);

            if (partToUpdate == null) return false;
            if (partToUpdate.GetNameValue() == part.Name
                && partToUpdate.GetLengthDecimalValue() == part.Length
                && partToUpdate.GetWidthDecimalValue() == part.Width
                ) return false;

            var xncsToUPdate = GetXncOperations()
                 .Where(o => o.GetPart()?.GetIdIntValue() == part.Id);

            foreach (var xnc in xncsToUPdate)
            {
                var xncTypeName = xnc.GetTypeNameValue()!;
                if (TrySetNewXncTypeName(ref xncTypeName, part.Name))

                {
                    xnc.SetTypeNameValue(xncTypeName);
                    continue;
                }

                MessageBox.Show($"""Cannot set new XNC typeName for part "{part.Name}" with old XNC typeName "{xnc.GetTypeNameValue()}".""",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            partToUpdate.SetNameValue(part.Name);
            partToUpdate.SetLengthValue(part.Length);
            partToUpdate.SetDLengthValue(part.Length);
            partToUpdate.SetWidthValue(part.Width);
            partToUpdate.SetDWidthValue(part.Width);

            return true;
        }

        private static Band CreateBand(XElement element)
        {
            return new Band()
            {
                Id = element.GetIdIntValue(),
                Thickness = element.GetThicknessDecimalValue(),
                Width = element.GetWidthDecimalValue(),
                InternalSymbol = element.Attribute("elSymbol")!.Value,
            };
        }

        private static Sheet CreateSheet(XElement element)
        {
            return new Sheet()
            {
                Id = element.GetMat()!.GetIdIntValue()
            };
        }

        public List<Band> ReadBands()
        {
            _bands = [];

            if (_project == null)
            {
                return _bands;
            }

            _bandGoods = GetBandGoods();
            _elOperations = GetElOperations();

            foreach (var operation in _elOperations)
            {
                var band = CreateBand(operation);
                var operationMatId = operation.GetOperationMaterialIdValue();
                var good = _bandGoods.Single(b => b.GetIdValue() == operationMatId);

                band.Name = good.GetNameValue()!;
                band.Code = good.GetCodeValue()!;
                //* for compatibility with original converter
                band.Thickness = good.GetThicknessDecimalValue()!;
                band.Width = good.GetWidthDecimalValue()!;
                //->
                _bands.Add(band);
            }

            var uniqueBandCodes = _bands.OrderBy(b => b.Thickness)
                .GroupBy(b => b.Code)
                .Select(g => g.First().Code)
                .ToList();

            var orderedBands = _bands.OrderBy(b => b.Thickness).ToList();

            foreach (var band in orderedBands)
            {
                var index = uniqueBandCodes.IndexOf(band.Code);
                var externalSymbol = Enum.GetNames<BandSymbols>()[index];

                band.ExternalSymbol = externalSymbol;
            }

            return _bands;
        }

        public List<Sheet> ReadSheets()
        {
            _sheets = [];

            if (_project == null)
            {
                return _sheets;
            }

            _sheetGoods = GetSheetGoods();
            _csOperations = GetCsOperations();

            foreach (var operation in _csOperations)
            {
                var sheet = CreateSheet(operation);
                var operationMatId = operation.GetOperationMaterialIdValue();
                var sheetGood = _sheetGoods.Single(b => b.GetIdValue() == operationMatId)!;

                sheet.Name = sheetGood.GetNameValue()!;
                sheet.Code = sheetGood!.GetCodeValue()!;
                sheet.Thickness = decimal.Parse(sheetGood.Attribute("t")!.Value);

                _sheets.Add(sheet);
            }

            return _sheets;
        }

        public List<Part> ReadParts()
        {
            var parts = new List<Part>();

            if (_project == null)
            {
                return parts;
            }

            var _productGoods = _project!.GetGoods().Where(g => g.GetTypeIdValue() == "product");

            foreach (var currentGood in _productGoods)
            {
                var currentParts = currentGood.GetParts()
                    .Select(x => CreatePart(x))
                    .ToList();

                var goodId = int.Parse(currentGood.GetIdValue()!);

                currentParts.ForEach(part => part.GoodId = goodId);
                parts.AddRange(currentParts);
            }

            foreach (var part in parts)
            {
                if (part.TopBandingId != null)
                {
                    part.TopBandingMat = _bands.Find(b => b.Id == part.TopBandingId)!.Name;
                }

                if (part.BottomBandingId != null)
                {
                    part.BottomBandingMat = _bands.Find(b => b.Id == part.BottomBandingId)!.Name;
                }

                if (part.LeftBandingId != null)
                {
                    part.LeftBandingMat = _bands.Find(b => b.Id == part.LeftBandingId)!.Name;
                }

                if (part.RightBandingId != null)
                {
                    part.RightBandingMat = _bands.Find(b => b.Id == part.RightBandingId)!.Name;
                }

                part.SheetId = GetPartSheetId(part);
            }

            return parts;
        }

        private int GetPartSheetId(Part part)
        {
            int? sheetId = default;

            foreach (var operation in _csOperations)
            {
                var parts = operation.GetParts().SkipLast(1).ToList();

                if (parts.Any(o => o.GetIdIntValue()! == part.Id))
                {
                    sheetId = operation.GetMat()!.GetIdIntValue();
                    break;
                }
            }

            return sheetId ?? throw new Exception($"No Sheet found for part with Id={part.Id}");
        }

        private List<XElement> GetXncOperations() // XNC operations
        {
            return _project!.GetOperations()
                .Where(o => o.GetTypeIdValue() == "XNC")
                .ToList();
        }

        private List<XElement> GetCsOperations() // cut operations
        {
            return _project!.GetOperations()
                .Where(o => o.GetTypeIdValue() == "CS")
                .ToList();
        }

        private List<XElement> GetElOperations() // band operations
        {
            return _project!.GetOperations()
                .Where(o => o.GetTypeIdValue() == "EL")
                .ToList();
        }

        private List<XElement> GetSheetGoods() // sheet materials
        {
            return _project!.GetGoods()
                .Where(e => e.GetTypeIdValue() == "sheet")
                .ToList();
        }

        private List<XElement> GetBandGoods() // band materials
        {
            return _project!.GetGoods()
                .Where(e => e.GetTypeIdValue() == "band")
                .ToList();
        }

        private List<XElement> GetProductGoods() // product _productGoods
        {
            return _project!.GetGoods()
                .Where(e => e.GetTypeIdValue() == "product")
                .ToList();
        }

        void AppendDescription(string message)
        {
            var description = $"{DateTime.Now:yyyy-MM-dd hh:mm:ss} -> {message}\n";

            if (_project!.Attribute("description") == null)
            {
                _project!.Add(new XAttribute("description", description));
                return;
            }

            _project!.Attribute("description")!.Value += description;
        }

        private static bool ElementIsBore(string name)
        {
            return name is "bf" || name is "bt" || name is "bb" || name is "bl" || name is "br";
        }

        private static readonly Regex BracketRegex = new(@"^(\[.*?\])(.*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static bool TrySetNewXncTypeName(ref string xncName, string partName)
        {
            var partMatch = BracketRegex.Match(partName);
            var xncMatch = BracketRegex.Match(xncName);

            //TODO consider case when both have brackets but different

            if (partMatch.Success && xncMatch.Success)
            {
                xncName = xncMatch.Groups[1].Value + partMatch.Groups[2].Value;
                return true;
            }

            if (!partMatch.Success && !xncMatch.Success)
            {
                xncName = partName;
                return true;
            }

            return false;

        }

        #endregion
    }
}

