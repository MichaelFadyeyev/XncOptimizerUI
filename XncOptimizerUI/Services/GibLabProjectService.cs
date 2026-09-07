using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Helpers.Enums;
using XncOptimizerUI.Services.Xnc;

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

        private readonly IConfigService _config;
        private readonly TimeProvider _timeProvider;

        public GibLabProjectService(IConfigService config, TimeProvider timeProvider)
        {
            _config = config;
            _timeProvider = timeProvider;
        }

        public bool GroupIdenticalElements(ref string log)
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
                    log += $"***\n{message}";
                    return false;
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
                log += $"***\n{e.Message}";
                return false;
            }

            return true;

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

                    var w = part.Attribute("w");
                    var dw = part.Attribute("dw");
                    var cw = part.Attribute("cw");
                    var jw = part.Attribute("jw");

                    var wValue = XmlConvert.ToDecimal(w?.Value 
                        ?? throw new ArgumentException("""XAttribute "w" not found or has no value"""));

                    var width = wValue * 2 + _config.SawWidth;
                    var widthStringValue = XmlConvert.ToString(width);

                    var jwValue = XmlConvert.ToDecimal(jw?.Value
                        ?? throw new ArgumentException("""XAttribute "jw" not found or has no value"""));
                    var jwStringValue = XmlConvert.ToString(wValue - (wValue - jwValue));

                    var dwValue = XmlConvert.ToDecimal(dw?.Value
                        ?? throw new ArgumentException("""XAttribute "dw" not found or has no value"""));
                    var dwStringValue = XmlConvert.ToString(dwValue - (wValue - jwValue));

                    var cwValue = XmlConvert.ToDecimal(cw?.Value
                        ?? throw new ArgumentException("""XAttribute "cw" not found or has no value"""));
                    var cwStringValue = XmlConvert.ToString(cwValue - (wValue - jwValue));

                    w!.SetValue(widthStringValue);
                    cw!.SetValue(cwStringValue);
                    dw.SetValue(dwStringValue);
                    jw!.SetValue(jwStringValue);

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

                    log += $"{name} resized to {widthStringValue}; count changed: {count} -> {newCount}\n";
                }

            }

            AppendDescription("specified parts prep for split along X");

            var result = GetNewFileName();

            _fullPath = Path.Combine(_path, result);
            _doc!.Save(_fullPath);

            log += $"***\nStored to: {_fullPath}";
        }

        public bool ReplaceXncPrograms(ref string log, Part sourcePart, IList<Part> targetParts)
        {
            if (targetParts == null || targetParts.Count == 0)
            {
                var message = "No target parts selected for XNC replacement.";
                log += $"***\n{message}";
                return false;
            }

            var xncOperations = GetXncOperations();

            // A part can carry more than one XNC operation (one per face), distinguished by (side, turn).
            var sourceOps = xncOperations.Where(o => o.GetPart()?.GetIdIntValue() == sourcePart.Id).ToList();

            if (sourceOps.Count == 0)
            {
                var message = $"""Source part "{sourcePart.Name}" (id={sourcePart.Id}) has no XNC operation.""";
                log += $"***\n{message}";
                return false;
            }

            if (sourceOps.GroupBy(GetXncFaceKey).Any(g => g.Count() > 1))
            {
                var message = $"""Source part "{sourcePart.Name}" (id={sourcePart.Id}) has ambiguous XNC operations (duplicate face).""";
                log += $"***\n{message}";
                return false;
            }

            var sourceOpsByFace = sourceOps.ToDictionary(GetXncFaceKey);

            // Validation phase: every target must be identical to the source, own an XNC operation,
            // and expose exactly the same set of faces so each program has a matching source.
            var problems = new List<string>();

            foreach (var target in targetParts)
            {
                var targetOps = xncOperations.Where(o => o.GetPart()?.GetIdIntValue() == target.Id).ToList();

                if (targetOps.Count == 0)
                {
                    problems.Add($"""Part "{target.Name}" (id={target.Id}) has no XNC operation.""");
                    continue;
                }

                if (!PartsAreIdentical(sourcePart, target, out var reason))
                {
                    problems.Add($"""Part "{target.Name}" (id={target.Id}) differs from source: {reason}.""");
                    continue;
                }

                var targetFaces = targetOps.Select(GetXncFaceKey).OrderBy(k => k).ToList();
                var sourceFaces = sourceOpsByFace.Keys.OrderBy(k => k).ToList();

                if (!targetFaces.SequenceEqual(sourceFaces))
                {
                    problems.Add($"""Part "{target.Name}" (id={target.Id}) has different XNC faces/turn than the source — source [{DescribeXncFaces(sourceOps)}] vs target [{DescribeXncFaces(targetOps)}].""");
                }
            }

            if (problems.Count > 0)
            {
                var message = "Copy aborted. The following parts are not identical to the source part:\n\n"
                    + string.Join("\n", problems);
                log += $"***\n{message}";
                return false;
            }

            // Apply phase: replace each target program (and its bore count) with the source's matching face.
            foreach (var target in targetParts)
            {
                var targetOps = xncOperations.Where(o => o.GetPart()?.GetIdIntValue() == target.Id).ToList();

                foreach (var targetOp in targetOps)
                {
                    var sourceOp = sourceOpsByFace[GetXncFaceKey(targetOp)];

                    targetOp.GetProgram()!.Value = sourceOp.GetProgramValue()!;

                    var sourceCountBore = sourceOp.Attribute("countBore")?.Value;
                    if (sourceCountBore != null)
                    {
                        targetOp.Attribute("countBore")?.SetValue(sourceCountBore);
                    }
                }

                log += $"XNC replaced: \"{target.Name}\" <- \"{sourcePart.Name}\" ({targetOps.Count} face(s))\n";
            }

            AppendDescription("replaced XNC programs with source part");

            var result = GetRenamedFileName();

            _fullPath = Path.Combine(_path, result);
            _doc!.Save(_fullPath);

            log += $"***\nStored to: {_fullPath}";

            return true;
        }

        public bool ConvertGroovesAndMills(ref string log, IList<Part> parts, GrooveMillDirection direction, bool processPockets)
        {
            if (parts == null || parts.Count == 0)
            {
                log += "***\nNo parts selected for groove/mill conversion.";
                return false;
            }

            var millingToolDiameters = (_config.MillingToolDiams ?? [])
                .Select(d => (double)d)
                .OrderBy(d => d)
                .ToArray();

            if (direction == GrooveMillDirection.GroovesToMills && millingToolDiameters.Length == 0)
            {
                log += "***\nNo milling tools configured (AppOptions.MillingToolDiams is empty). Grooves->Mills conversion cancelled.";
                return false;
            }

            var xncOperations = GetXncOperations();

            var totalConverted = 0;
            var totalIgnored = 0;
            var totalToolsAdded = 0;
            var totalToolsRemoved = 0;
            var touchedParts = 0;

            try
            {
                foreach (var part in parts)
                {
                    var partOps = xncOperations.Where(o => o.GetPart()?.GetIdIntValue() == part.Id).ToList();

                    if (partOps.Count == 0)
                    {
                        continue;
                    }

                    var partConverted = 0;
                    var partIgnored = 0;
                    var partToolsAdded = 0;
                    var partToolsRemoved = 0;

                    foreach (var op in partOps)
                    {
                        var programAttribute = op.GetProgram();

                        if (programAttribute == null)
                        {
                            continue;
                        }

                        var programXml = XDocument.Parse(WebUtility.HtmlDecode(programAttribute.Value));
                        var program = programXml.Element("program")
                            ?? throw new Exception($"""Part "{part.Name}" (id={part.Id}): XNC program has no <program> root element.""");

                        var (converted, ignored, toolsAdded, toolsRemoved) = direction == GrooveMillDirection.GroovesToMills
                            ? ConvertGroovesToMills(program, millingToolDiameters)
                            : ConvertMillsToGrooves(program, processPockets);

                        if (converted > 0)
                        {
                            programAttribute.Value = program.ToString();
                        }

                        partConverted += converted;
                        partIgnored += ignored;
                        partToolsAdded += toolsAdded;
                        partToolsRemoved += toolsRemoved;
                    }

                    if (partConverted > 0 || partIgnored > 0)
                    {
                        touchedParts++;

                        log += direction == GrooveMillDirection.GroovesToMills
                            ? $"Grooves->Mills: \"{part.Name}\" (id={part.Id}): {partConverted} groove(s) -> mill(s), {partToolsAdded} tool(s) added, {partToolsRemoved} tool(s) removed, {partIgnored} ignored\n"
                            : $"Mills->Grooves: \"{part.Name}\" (id={part.Id}): {partConverted} mill(s) -> groove(s), {partToolsRemoved} tool(s) removed, {partIgnored} ignored\n";
                    }

                    totalConverted += partConverted;
                    totalIgnored += partIgnored;
                    totalToolsAdded += partToolsAdded;
                    totalToolsRemoved += partToolsRemoved;
                }
            }
            catch (Exception e)
            {
                log += $"***\n{e.Message}";
                return false;
            }

            if (totalConverted == 0)
            {
                var what = direction == GrooveMillDirection.GroovesToMills ? "grooves" : "mills";
                log += $"***\nNo {what} converted (ignored {totalIgnored}).";
                return false;
            }

            AppendDescription(direction == GrooveMillDirection.GroovesToMills
                ? "converted grooves to mills"
                : "converted mills to grooves");

            var result = GetGrooveMillFileName();

            _fullPath = Path.Combine(_path, result);
            _doc!.Save(_fullPath);

            log += $"***\nGroove/Mill conversion complete: converted {totalConverted}, ignored {totalIgnored}, {totalToolsAdded} tool(s) added, {totalToolsRemoved} tool(s) removed across {touchedParts} part(s). Stored to: {_fullPath}";

            return true;
        }

        /// <summary>
        /// Re-sequences the straight axis-parallel milling passes in every XNC program of the
        /// supplied parts so the entry of each pass is next to the exit of the previous one.
        /// Passes are handled per XNC operation (one part face, one <c>side</c>) and grouped by
        /// tool; a greedy nearest-neighbour walk picks the order and the direction of each pass.
        /// Non-eligible elements (arcs, multi-segment contours, pockets, <c>&lt;mr&gt;</c>) keep
        /// their slot and are counted as ignored. Saves nothing and returns <c>false</c> when
        /// nothing was reordered.
        /// </summary>
        public bool OptimizeMillTraversal(ref string log, IList<Part> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                log += "***\nNo parts selected for mill traversal optimization.";
                return false;
            }

            var xncOperations = GetXncOperations();

            var totalReordered = 0;
            var totalIgnored = 0;
            var touchedParts = 0;

            try
            {
                foreach (var part in parts)
                {
                    var partOps = xncOperations.Where(o => o.GetPart()?.GetIdIntValue() == part.Id).ToList();

                    if (partOps.Count == 0)
                    {
                        continue;
                    }

                    var partReordered = 0;
                    var partIgnored = 0;
                    var partGroups = 0;

                    foreach (var op in partOps)
                    {
                        var programAttribute = op.GetProgram();

                        if (programAttribute == null)
                        {
                            continue;
                        }

                        var programXml = XDocument.Parse(WebUtility.HtmlDecode(programAttribute.Value));
                        var program = programXml.Element("program")
                            ?? throw new Exception($"""Part "{part.Name}" (id={part.Id}): XNC program has no <program> root element.""");

                        var (reordered, ignored, changedGroups) = OptimizeMillTraversalInProgram(program);

                        if (reordered > 0)
                        {
                            // Match ConvertGroovesAndMills' re-serialization, but keep the XML
                            // declaration when the source had one so the output diff stays minimal.
                            programAttribute.Value = programXml.Declaration is { } declaration
                                ? declaration + program.ToString()
                                : program.ToString();
                        }

                        partReordered += reordered;
                        partIgnored += ignored;
                        partGroups += changedGroups;
                    }

                    if (partReordered > 0 || partIgnored > 0)
                    {
                        touchedParts++;
                        log += $"Mill order: \"{part.Name}\" (id={part.Id}): reordered {partReordered} pass(es) in {partGroups} group(s), {partIgnored} ignored\n";
                    }

                    totalReordered += partReordered;
                    totalIgnored += partIgnored;
                }
            }
            catch (Exception e)
            {
                log += $"***\n{e.Message}";
                return false;
            }

            if (totalReordered == 0)
            {
                log += $"***\nNo mill passes reordered (ignored {totalIgnored}).";
                return false;
            }

            AppendDescription("optimized mill traversal order");

            var result = GetMillOrderFileName();

            _fullPath = Path.Combine(_path, result);
            _doc!.Save(_fullPath);

            log += $"***\nMill traversal optimization complete: reordered {totalReordered} pass(es) across {touchedParts} part(s). Stored to: {_fullPath}";

            return true;
        }

        // --- Parallel-mill traversal ordering ------------------------------------------------
        // A milling "pass" here is an <ms> entry followed by exactly one straight <ml> segment
        // that runs parallel to X or Y and is not a pocket (c="3"). Passes that share a tool
        // (the <ms> @name) are re-sequenced together by a greedy nearest-neighbour walk; the
        // first pass in document order keeps its authored direction and seeds the walk, then
        // each remaining pass is appended in whichever direction puts its entry closest to the
        // current tool position. Everything else is left exactly where it is.

        private readonly record struct MillPass(
            string ToolName,
            string MsX, string MsY, string? MsDp,
            string MlX, string MlY, string? MlDp,
            string? In, string? Out, string? Sxy, string? Fwd, string? C, string Name, string? Comment,
            double EntryX, double EntryY, double ExitX, double ExitY);

        private static (int reordered, int ignored, int changedGroups) OptimizeMillTraversalInProgram(XElement program)
        {
            var symbols = SeedProgramSymbols(program);

            var slots = new List<(XElement Ms, XElement Ml)>();
            var passes = new List<MillPass>();
            var ignored = 0;

            var elements = program.Elements().ToList();

            for (var i = 0; i < elements.Count; i++)
            {
                var tag = elements[i].Name.LocalName;

                if (tag == "mr")
                {
                    ignored++;
                    continue;
                }

                if (tag != "ms")
                {
                    continue;
                }

                var ms = elements[i];

                var segments = new List<XElement>();

                for (var j = i + 1; j < elements.Count; j++)
                {
                    var segTag = elements[j].Name.LocalName;

                    if (segTag != "ml" && segTag != "mac")
                    {
                        break;
                    }

                    segments.Add(elements[j]);
                }

                if (segments.Count != 1 || segments[0].Name.LocalName != "ml")
                {
                    ignored++;
                    continue;
                }

                var ml = segments[0];

                if (ParsePositionCode(ms.GetCValue()) == ToolPosition.Pocket)
                {
                    ignored++;
                    continue;
                }

                var toolName = ms.GetNameValue();

                if (toolName == null)
                {
                    ignored++;
                    continue;
                }

                var msX = ms.GetXValue();
                var msY = ms.GetYValue();
                var mlX = ml.GetXValue();
                var mlY = ml.GetYValue();

                double ax, ay, bx, by;

                try
                {
                    ax = EvalXnc(msX, symbols);
                    ay = EvalXnc(msY, symbols);
                    bx = EvalXnc(mlX, symbols);
                    by = EvalXnc(mlY, symbols);
                }
                catch (Exception)
                {
                    // A pass whose endpoints don't resolve to plain numbers isn't something we
                    // can reason about geometrically: leave it untouched.
                    ignored++;
                    continue;
                }

                var horizontal = Math.Abs(ay - by) <= AxisEpsilon;
                var vertical = Math.Abs(ax - bx) <= AxisEpsilon;

                if (horizontal == vertical) // diagonal (neither) or degenerate (both)
                {
                    ignored++;
                    continue;
                }

                slots.Add((ms, ml));
                passes.Add(new MillPass(
                    toolName,
                    msX!, msY!, ms.GetDpValue(),
                    mlX!, mlY!, ml.GetDpValue(),
                    ms.GetInValue(), ms.GetOutValue(), ms.GetSxyValue(),
                    ms.Attribute("fwd")?.Value, ms.GetCValue(), toolName, ms.GetCommentValue(),
                    ax, ay, bx, by));
            }

            if (slots.Count < 2)
            {
                return (0, ignored, 0);
            }

            var groups = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            for (var k = 0; k < passes.Count; k++)
            {
                if (!groups.TryGetValue(passes[k].ToolName, out var members))
                {
                    members = [];
                    groups[passes[k].ToolName] = members;
                }

                members.Add(k);
            }

            var reordered = 0;
            var changedGroups = 0;

            foreach (var members in groups.Values)
            {
                if (members.Count < 2)
                {
                    continue;
                }

                var plan = NearestNeighbourOrder(passes, members);

                var groupReordered = 0;

                for (var m = 0; m < members.Count; m++)
                {
                    var (sourceIndex, reversed) = plan[m];

                    if (sourceIndex != members[m] || reversed)
                    {
                        groupReordered++;
                    }
                }

                if (groupReordered == 0)
                {
                    continue;
                }

                reordered += groupReordered;
                changedGroups++;

                // `passes` holds attribute strings, not element references, so overwriting a slot
                // from any source pass is safe even when the two overlap.
                for (var m = 0; m < members.Count; m++)
                {
                    var (sourceIndex, reversed) = plan[m];
                    var (ms, ml) = slots[members[m]];

                    ApplyPass(ms, ml, passes[sourceIndex], reversed);
                }
            }

            return (reordered, ignored, changedGroups);
        }

        private static List<(int SourceIndex, bool Reversed)> NearestNeighbourOrder(List<MillPass> passes, List<int> members)
        {
            var order = new List<(int, bool)>(members.Count);
            var remaining = new List<int>(members);

            // The first pass in document order is left as authored and seeds the walk.
            var seed = remaining[0];
            remaining.RemoveAt(0);
            order.Add((seed, false));

            var currentX = passes[seed].ExitX;
            var currentY = passes[seed].ExitY;

            while (remaining.Count > 0)
            {
                var bestPos = 0;
                var bestReversed = false;
                var bestDistance = double.MaxValue;

                for (var r = 0; r < remaining.Count; r++)
                {
                    var pass = passes[remaining[r]];

                    var forward = SquaredDistance(currentX, currentY, pass.EntryX, pass.EntryY);

                    if (forward < bestDistance)
                    {
                        bestDistance = forward;
                        bestPos = r;
                        bestReversed = false;
                    }

                    var backward = SquaredDistance(currentX, currentY, pass.ExitX, pass.ExitY);

                    if (backward < bestDistance)
                    {
                        bestDistance = backward;
                        bestPos = r;
                        bestReversed = true;
                    }
                }

                var chosen = remaining[bestPos];
                remaining.RemoveAt(bestPos);
                order.Add((chosen, bestReversed));

                var chosenPass = passes[chosen];
                currentX = bestReversed ? chosenPass.EntryX : chosenPass.ExitX;
                currentY = bestReversed ? chosenPass.EntryY : chosenPass.ExitY;
            }

            return order;
        }

        private static void ApplyPass(XElement ms, XElement ml, in MillPass source, bool reversed)
        {
            // Reversing a pass swaps which authored endpoint the tool enters from; the constant
            // (non-running) axis is identical at both ends, so a plain string swap is exact.
            var entryX = reversed ? source.MlX : source.MsX;
            var entryY = reversed ? source.MlY : source.MsY;
            var entryDp = reversed ? source.MlDp ?? source.MsDp : source.MsDp;
            var exitX = reversed ? source.MsX : source.MlX;
            var exitY = reversed ? source.MsY : source.MlY;
            var exitDp = reversed ? source.MsDp : source.MlDp;

            ms.SetAttributeValue("x", entryX);
            ms.SetAttributeValue("y", entryY);
            ms.SetAttributeValue("dp", entryDp);
            ms.SetAttributeValue("in", source.In);
            ms.SetAttributeValue("out", source.Out);
            ms.SetAttributeValue("sxy", source.Sxy);
            ms.SetAttributeValue("fwd", source.Fwd);
            ms.SetAttributeValue("c", source.C);
            ms.SetAttributeValue("name", source.Name);
            ms.SetAttributeValue("comment", source.Comment);

            ml.SetAttributeValue("x", exitX);
            ml.SetAttributeValue("y", exitY);
            ml.SetAttributeValue("dp", exitDp);
        }

        private static double SquaredDistance(double x1, double y1, double x2, double y2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;

            return dx * dx + dy * dy;
        }

        // --- Groove <-> mill program rewriting -------------------------------------------------
        // Both directions decode the escaped <program> sub-document (as PrepForSplitAlongX does),
        // mutate the element tree in place, and let the caller re-serialize with program.ToString().
        // Only axis-parallel single-segment paths are converted; everything else is counted as
        // "ignored" and left untouched.

        private const double AxisEpsilon = 1e-6;
        private const double DiameterEpsilon = 1e-6;

        /// <summary>Diameter of the grooving cutter a mill is turned back into a groove with.</summary>
        private const double GroovingToolDiameter = 2.8;

        /// <summary>
        /// Rewrites every axis-parallel primary-pass <c>&lt;gr&gt;</c> groove as a milling
        /// operation, taking the shop's available cutters (<paramref name="millingToolDiameters"/>,
        /// from <c>AppOptions.MillingToolDiams</c>) into account:
        /// <list type="bullet">
        /// <item>groove width equals an available diameter (within <see cref="DiameterEpsilon"/>)
        /// =&gt; a linear mill (<c>&lt;ms&gt;</c> + <c>&lt;ml&gt;</c>), as before;</item>
        /// <item>otherwise, if the smallest available cutter fits the width =&gt; a rectangular
        /// pocket (<c>&lt;mr&gt;</c> with <c>c="3"</c>) that runs along the groove, its short side
        /// the groove width, cut with that smallest cutter;</item>
        /// <item>otherwise (smallest cutter wider than the groove) =&gt; the groove is left
        /// untouched and counted as ignored.</item>
        /// </list>
        /// The caller guarantees <paramref name="millingToolDiameters"/> is non-empty.
        /// </summary>
        private static (int converted, int ignored, int toolsAdded, int toolsRemoved) ConvertGroovesToMills(
            XElement program, IReadOnlyList<double> millingToolDiameters)
        {
            var symbols = SeedProgramSymbols(program);
            var toolsByName = ReadToolDiameters(program);
            symbols.TryGet("dx", out var dx);
            symbols.TryGet("dy", out var dy);

            var converted = 0;
            var ignored = 0;
            var addedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var originalToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var gr in program.Elements("gr").ToList())
            {
                // Only a primary-pass groove (p="0", or absent) may become a mill; a secondary
                // pass (p != "0") is left as-is.
                var p = gr.GetPValue();

                if (p != null && p.Trim() != "0")
                {
                    ignored++;
                    continue;
                }

                var x1 = EvalXnc(gr.GetX1Value(), symbols);
                var y1 = EvalXnc(gr.GetY1Value(), symbols);
                var x2 = EvalXnc(gr.GetX2Value(), symbols);
                var y2 = EvalXnc(gr.GetY2Value(), symbols);

                var horizontal = Math.Abs(y1 - y2) <= AxisEpsilon;
                var vertical = Math.Abs(x1 - x2) <= AxisEpsilon;

                if (horizontal == vertical) // diagonal (neither) or degenerate (both)
                {
                    ignored++;
                    continue;
                }

                var width = EvalXnc(gr.GetTValue(), symbols);
                var dp = gr.GetDpValue() ?? "0";

                if (gr.GetNameValue() is { } originalTool)
                {
                    originalToolNames.Add(originalTool); // considered for cleanup once conversion is done
                }

                var matchesAvailableDiameter = false;

                foreach (var d in millingToolDiameters)
                {
                    if (Math.Abs(d - width) <= DiameterEpsilon)
                    {
                        matchesAvailableDiameter = true;
                        break;
                    }
                }

                if (!matchesAvailableDiameter)
                {
                    // No cutter of the groove's exact width: mill the groove out as a rectangular
                    // pocket with the smallest available cutter. If even that one is wider than the
                    // groove it cannot be milled cleanly, so the groove is left as-is.
                    var toolDiam = double.PositiveInfinity;

                    foreach (var d in millingToolDiameters)
                    {
                        if (d < toolDiam)
                        {
                            toolDiam = d;
                        }
                    }

                    if (double.IsInfinity(toolDiam) || toolDiam > width)
                    {
                        ignored++;
                        continue;
                    }

                    EmitGrooveRectangle(gr, x1, y1, x2, y2, width, dp, horizontal, dx, dy, toolDiam,
                        toolsByName, addedToolNames);
                    gr.Remove();
                    converted++;
                    continue;
                }

                var toolName = FindToolByDiameter(toolsByName, width);

                if (toolName == null)
                {
                    toolName = MakeToolName(width, toolsByName, "Bore");
                    gr.AddBeforeSelf(new XElement("tool",
                        new XAttribute("name", toolName),
                        new XAttribute("d", XmlConvert.ToString(width))));
                    toolsByName[toolName] = width;
                    addedToolNames.Add(toolName); // counted (distinct) as toolsAdded at the end
                }

                // Overshoot the part outline by half a tool diameter (the tool centre must clear
                // the edge), but only along the axis the groove actually runs (the constant axis
                // keeps its authored value).
                var overshoot = width / 2d;

                if (horizontal)
                {
                    x1 = OvershootAlongAxis(x1, dx, overshoot);
                    x2 = OvershootAlongAxis(x2, dx, overshoot);
                }
                else
                {
                    y1 = OvershootAlongAxis(y1, dy, overshoot);
                    y2 = OvershootAlongAxis(y2, dy, overshoot);
                }

                var ms = new XElement("ms",
                    new XAttribute("x", XmlConvert.ToString(x1)),
                    new XAttribute("y", XmlConvert.ToString(y1)),
                    new XAttribute("dp", dp),
                    new XAttribute("in", "0"),
                    new XAttribute("out", "0"),
                    new XAttribute("sxy", "tool.dia/2"),
                    new XAttribute("fwd", "true"),
                    new XAttribute("c", gr.GetCValue() ?? "0"),
                    new XAttribute("name", toolName));

                var comment = gr.GetCommentValue();

                if (comment != null)
                {
                    ms.SetAttributeValue("comment", comment);
                }

                var ml = new XElement("ml",
                    new XAttribute("x", XmlConvert.ToString(x2)),
                    new XAttribute("y", XmlConvert.ToString(y2)),
                    new XAttribute("dp", dp));

                gr.AddBeforeSelf(ms);
                gr.AddBeforeSelf(ml);
                gr.Remove();
                converted++;
            }

            var toolsRemoved = RemoveUnreferencedTools(program, originalToolNames);

            return (converted, ignored, addedToolNames.Count, toolsRemoved);
        }

        private static (int converted, int ignored, int toolsAdded, int toolsRemoved) ConvertMillsToGrooves(XElement program, bool processPockets)
        {
            var symbols = SeedProgramSymbols(program);
            var toolsByName = ReadToolDiameters(program);
            symbols.TryGet("dx", out var dx);
            symbols.TryGet("dy", out var dy);
            symbols.TryGet("dz", out var dz);

            var converted = 0;
            var ignored = 0;
            var addedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var originalToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var elements = program.Elements().ToList();

            for (var i = 0; i < elements.Count; i++)
            {
                var tag = elements[i].Name.LocalName;

                if (tag == "mr")
                {
                    if (TryConvertRectanglePocketToGroove(
                            elements[i], processPockets, symbols, dx, dy, dz,
                            toolsByName, addedToolNames, originalToolNames))
                    {
                        converted++;
                    }
                    else
                    {
                        ignored++;
                    }

                    continue;
                }

                if (tag != "ms")
                {
                    continue;
                }

                var ms = elements[i];

                var segments = new List<XElement>();

                for (var j = i + 1; j < elements.Count; j++)
                {
                    var segTag = elements[j].Name.LocalName;

                    if (segTag != "ml" && segTag != "mac")
                    {
                        break;
                    }

                    segments.Add(elements[j]);
                }

                // A closed axis-parallel rectangular pocket contour (<ms c="3"> + several
                // straight <ml>) becomes a groove down its long axis, same rule as an <mr> pocket.
                if (processPockets
                    && ParsePositionCode(ms.GetCValue()) == ToolPosition.Pocket
                    && segments.Count >= 3
                    && segments.All(s => s.Name.LocalName == "ml")
                    && TryConvertContourPocketToGroove(ms, segments, symbols, dx, dy, dz,
                           toolsByName, addedToolNames, originalToolNames))
                {
                    converted++;
                    continue;
                }

                if (segments.Count != 1 || segments[0].Name.LocalName != "ml")
                {
                    ignored++;
                    continue;
                }

                var ml = segments[0];

                var entryDepth = EvalXnc(ms.GetDpValue(), symbols);
                var segmentDepth = ml.GetDpValue() is { } rawDp ? EvalXnc(rawDp, symbols) : entryDepth;

                if (Math.Max(entryDepth, segmentDepth) >= dz)
                {
                    ignored++;
                    continue;
                }

                if (ParsePositionCode(ms.GetCValue()) == ToolPosition.Pocket)
                {
                    ignored++;
                    continue;
                }

                var x1 = EvalXnc(ms.GetXValue(), symbols);
                var y1 = EvalXnc(ms.GetYValue(), symbols);
                var x2 = EvalXnc(ml.GetXValue(), symbols);
                var y2 = EvalXnc(ml.GetYValue(), symbols);

                var horizontal = Math.Abs(y1 - y2) <= AxisEpsilon;
                var vertical = Math.Abs(x1 - x2) <= AxisEpsilon;

                if (horizontal == vertical)
                {
                    ignored++;
                    continue;
                }

                var millToolName = ms.GetNameValue();

                if (millToolName == null || !toolsByName.TryGetValue(millToolName, out var millToolDiameter))
                {
                    ignored++;
                    continue;
                }

                // The groove is cut by a fixed-diameter grooving cutter (multiple passes when the
                // slot is wider); the mill's own tool is only used for the slot width `t`.
                var grooveToolName = FindToolByDiameter(toolsByName, GroovingToolDiameter);

                if (grooveToolName == null)
                {
                    grooveToolName = MakeToolName(GroovingToolDiameter, toolsByName, "Cut");
                    ms.AddBeforeSelf(new XElement("tool",
                        new XAttribute("name", grooveToolName),
                        new XAttribute("d", XmlConvert.ToString(GroovingToolDiameter))));
                    toolsByName[grooveToolName] = GroovingToolDiameter;
                    addedToolNames.Add(grooveToolName);
                }

                // Any endpoint that lies outside the part is pulled onto the boundary line;
                // on-edge / interior endpoints keep their value. Only the axis the mill runs
                // along can be out of range.
                if (horizontal)
                {
                    x1 = Math.Clamp(x1, 0d, dx);
                    x2 = Math.Clamp(x2, 0d, dx);
                }
                else
                {
                    y1 = Math.Clamp(y1, 0d, dy);
                    y2 = Math.Clamp(y2, 0d, dy);
                }

                var dpOut = ml.GetDpValue() ?? ms.GetDpValue() ?? "0";

                var gr = new XElement("gr",
                    new XAttribute("x1", XmlConvert.ToString(x1)),
                    new XAttribute("y1", XmlConvert.ToString(y1)),
                    new XAttribute("dp", dpOut),
                    new XAttribute("x2", XmlConvert.ToString(x2)),
                    new XAttribute("y2", XmlConvert.ToString(y2)),
                    new XAttribute("t", XmlConvert.ToString(millToolDiameter)),
                    new XAttribute("c", ms.GetCValue() ?? "0"),
                    new XAttribute("p", "0"),
                    new XAttribute("name", grooveToolName));

                var comment = ms.GetCommentValue();

                if (comment != null)
                {
                    gr.SetAttributeValue("comment", comment);
                }

                originalToolNames.Add(millToolName);

                ms.AddBeforeSelf(gr);
                ml.Remove();
                ms.Remove();
                converted++;
            }

            var toolsRemoved = RemoveUnreferencedTools(program, originalToolNames);

            return (converted, ignored, addedToolNames.Count, toolsRemoved);
        }

        /// <summary>
        /// Turns an axis-parallel rectangular pocket primitive (<c>&lt;mr&gt;</c> with
        /// <c>c="3"</c>) into a <c>&lt;gr&gt;</c> groove. Returns <c>false</c> (and leaves the
        /// <c>&lt;mr&gt;</c> untouched) when pocket processing is off or the rectangle is rotated,
        /// through-depth, not a pocket, or degenerate.
        /// </summary>
        private static bool TryConvertRectanglePocketToGroove(
            XElement mr,
            bool processPockets,
            XncSymbolTable symbols,
            double dx,
            double dy,
            double dz,
            Dictionary<string, double> toolsByName,
            HashSet<string> addedToolNames,
            HashSet<string> originalToolNames)
        {
            if (!processPockets)
            {
                return false;
            }

            if (ParsePositionCode(mr.GetCValue()) != ToolPosition.Pocket)
            {
                return false;
            }

            // A rotated rectangle has no clean groove representation. A missing angle is 0.
            var angle = mr.GetAValue() is { } rawAngle ? EvalXnc(rawAngle, symbols) : 0d;

            if (Math.Abs(angle) > AxisEpsilon)
            {
                return false;
            }

            if (EvalXnc(mr.GetDpValue(), symbols) >= dz)
            {
                return false;
            }

            var length = EvalXnc(mr.GetLengthValue(), symbols); // along X while a == 0
            var breadth = EvalXnc(mr.GetWidthValue(), symbols);  // along Y while a == 0

            if (length <= 0d || breadth <= 0d)
            {
                return false;
            }

            var cx = EvalXnc(mr.GetXValue(), symbols);
            var cy = EvalXnc(mr.GetYValue(), symbols);

            // <mr> x/y is the rectangle centre.
            EmitPocketGroove(mr,
                cx - length / 2d, cx + length / 2d, cy - breadth / 2d, cy + breadth / 2d,
                mr.GetDpValue() ?? "0", mr.GetCommentValue(), mr.GetNameValue(),
                dx, dy, toolsByName, addedToolNames, originalToolNames);

            mr.Remove();

            return true;
        }

        /// <summary>
        /// Turns a milling contour that traces an axis-parallel rectangle (an <c>&lt;ms c="3"&gt;</c>
        /// entry plus straight <c>&lt;ml&gt;</c> segments) into a <c>&lt;gr&gt;</c> groove. Returns
        /// <c>false</c> (leaving the contour untouched) when the vertices are not a rectangle, the
        /// pocket is through-depth, or it is degenerate.
        /// </summary>
        private static bool TryConvertContourPocketToGroove(
            XElement ms,
            List<XElement> segments,
            XncSymbolTable symbols,
            double dx,
            double dy,
            double dz,
            Dictionary<string, double> toolsByName,
            HashSet<string> addedToolNames,
            HashSet<string> originalToolNames)
        {
            var xs = new List<double> { EvalXnc(ms.GetXValue(), symbols) };
            var ys = new List<double> { EvalXnc(ms.GetYValue(), symbols) };

            foreach (var seg in segments)
            {
                xs.Add(EvalXnc(seg.GetXValue(), symbols));
                ys.Add(EvalXnc(seg.GetYValue(), symbols));
            }

            var minX = xs.Min();
            var maxX = xs.Max();
            var minY = ys.Min();
            var maxY = ys.Max();

            if (maxX - minX <= AxisEpsilon || maxY - minY <= AxisEpsilon)
            {
                return false; // degenerate: a line, not a rectangle
            }

            for (var k = 0; k < xs.Count; k++)
            {
                var onCorner = (Math.Abs(xs[k] - minX) <= AxisEpsilon || Math.Abs(xs[k] - maxX) <= AxisEpsilon)
                    && (Math.Abs(ys[k] - minY) <= AxisEpsilon || Math.Abs(ys[k] - maxY) <= AxisEpsilon);

                if (!onCorner)
                {
                    return false; // a vertex off the bounding-box corners => not a rectangle
                }

                if (k > 0
                    && Math.Abs(xs[k] - xs[k - 1]) > AxisEpsilon
                    && Math.Abs(ys[k] - ys[k - 1]) > AxisEpsilon)
                {
                    return false; // a diagonal step => not axis-parallel sides
                }
            }

            var depth = EvalXnc(ms.GetDpValue(), symbols);

            foreach (var seg in segments)
            {
                if (seg.GetDpValue() is { } rawDp)
                {
                    depth = Math.Max(depth, EvalXnc(rawDp, symbols));
                }
            }

            if (depth >= dz)
            {
                return false;
            }

            EmitPocketGroove(ms, minX, maxX, minY, maxY,
                ms.GetDpValue() ?? "0", ms.GetCommentValue(), ms.GetNameValue(),
                dx, dy, toolsByName, addedToolNames, originalToolNames);

            foreach (var seg in segments)
            {
                seg.Remove();
            }

            ms.Remove();

            return true;
        }

        /// <summary>
        /// Inserts a <c>&lt;gr&gt;</c> before <paramref name="anchor"/> for an axis-parallel
        /// rectangular pocket bounded by <c>[minX,maxX] x [minY,maxY]</c>: it runs along the
        /// longer side, its width is the shorter side, and endpoints beyond the part edge are
        /// pulled onto it. Creates the fixed grooving tool if the program has none. The caller
        /// removes the source element(s) and bumps the counters.
        /// </summary>
        private static void EmitPocketGroove(
            XElement anchor,
            double minX,
            double maxX,
            double minY,
            double maxY,
            string dpRaw,
            string? comment,
            string? pocketToolName,
            double dx,
            double dy,
            Dictionary<string, double> toolsByName,
            HashSet<string> addedToolNames,
            HashSet<string> originalToolNames)
        {
            var spanX = maxX - minX;
            var spanY = maxY - minY;

            double x1, y1, x2, y2, slot;

            if (spanX >= spanY)
            {
                x1 = Math.Clamp(minX, 0d, dx);
                x2 = Math.Clamp(maxX, 0d, dx);
                y1 = y2 = (minY + maxY) / 2d;
                slot = spanY;
            }
            else
            {
                x1 = x2 = (minX + maxX) / 2d;
                y1 = Math.Clamp(minY, 0d, dy);
                y2 = Math.Clamp(maxY, 0d, dy);
                slot = spanX;
            }

            var grooveToolName = FindToolByDiameter(toolsByName, GroovingToolDiameter);

            if (grooveToolName == null)
            {
                grooveToolName = MakeToolName(GroovingToolDiameter, toolsByName, "Cut");
                anchor.AddBeforeSelf(new XElement("tool",
                    new XAttribute("name", grooveToolName),
                    new XAttribute("d", XmlConvert.ToString(GroovingToolDiameter))));
                toolsByName[grooveToolName] = GroovingToolDiameter;
                addedToolNames.Add(grooveToolName);
            }

            var gr = new XElement("gr",
                new XAttribute("x1", XmlConvert.ToString(x1)),
                new XAttribute("y1", XmlConvert.ToString(y1)),
                new XAttribute("dp", dpRaw),
                new XAttribute("x2", XmlConvert.ToString(x2)),
                new XAttribute("y2", XmlConvert.ToString(y2)),
                new XAttribute("t", XmlConvert.ToString(slot)),
                new XAttribute("c", "0"),
                new XAttribute("p", "0"),
                new XAttribute("name", grooveToolName));

            if (comment != null)
            {
                gr.SetAttributeValue("comment", comment);
            }

            if (pocketToolName != null)
            {
                originalToolNames.Add(pocketToolName);
            }

            anchor.AddBeforeSelf(gr);
        }

        /// <summary>
        /// Inserts an <c>&lt;mr&gt;</c> pocket before <paramref name="gr"/> for an axis-parallel
        /// groove that has no matching milling-cutter diameter. The rectangle runs along the
        /// groove (<c>a=0</c>, so its <c>l</c>/<c>w</c> map straight onto world X/Y): the running
        /// side carries the groove length, the other side the groove <paramref name="width"/>. A
        /// running-axis end that lies outside or on the part border is pushed a further
        /// <paramref name="toolDiam"/><c> / 2</c> past it (via <see cref="OvershootAlongAxis"/>);
        /// the constant axis keeps the groove's centre-line. Fabricates the cutter
        /// (<c>Mill&lt;d&gt;</c>) when the program declares none. The caller removes the source
        /// <c>&lt;gr&gt;</c> and bumps the counters.
        /// </summary>
        private static void EmitGrooveRectangle(
            XElement gr,
            double x1,
            double y1,
            double x2,
            double y2,
            double width,
            string dpRaw,
            bool horizontal,
            double dx,
            double dy,
            double toolDiam,
            Dictionary<string, double> toolsByName,
            HashSet<string> addedToolNames)
        {
            var overshoot = toolDiam / 2d;

            double runStart, runEnd, runSize, constCoord;

            if (horizontal)
            {
                runStart = x1;
                runEnd = x2;
                runSize = dx;
                constCoord = (y1 + y2) / 2d;
            }
            else
            {
                runStart = y1;
                runEnd = y2;
                runSize = dy;
                constCoord = (x1 + x2) / 2d;
            }

            runStart = OvershootAlongAxis(runStart, runSize, overshoot);
            runEnd = OvershootAlongAxis(runEnd, runSize, overshoot);

            var length = Math.Abs(runEnd - runStart);
            var runCentre = (runStart + runEnd) / 2d;

            double cx, cy, l, w;

            if (horizontal)
            {
                cx = runCentre;
                cy = constCoord;
                l = length;
                w = width;
            }
            else
            {
                cx = constCoord;
                cy = runCentre;
                l = width;
                w = length;
            }

            var toolName = FindToolByDiameter(toolsByName, toolDiam);

            if (toolName == null)
            {
                toolName = MakeToolName(toolDiam, toolsByName, "Mill");
                gr.AddBeforeSelf(new XElement("tool",
                    new XAttribute("name", toolName),
                    new XAttribute("d", XmlConvert.ToString(toolDiam))));
                toolsByName[toolName] = toolDiam;
                addedToolNames.Add(toolName);
            }

            var mr = new XElement("mr",
                new XAttribute("x", XmlConvert.ToString(cx)),
                new XAttribute("y", XmlConvert.ToString(cy)),
                new XAttribute("dp", dpRaw),
                new XAttribute("in", "0"),
                new XAttribute("out", "0"),
                new XAttribute("sxy", "tool.dia/2"),
                new XAttribute("fwd", "true"),
                new XAttribute("l", XmlConvert.ToString(l)),
                new XAttribute("w", XmlConvert.ToString(w)),
                new XAttribute("a", "0"),
                new XAttribute("r", "0"),
                new XAttribute("c", "3"),
                new XAttribute("name", toolName));

            var comment = gr.GetCommentValue();

            if (comment != null)
            {
                mr.SetAttributeValue("comment", comment);
            }

            gr.AddBeforeSelf(mr);
        }

        private static double OvershootAlongAxis(double value, double size, double offset)
        {
            if (value <= 0d)
            {
                return -offset;
            }

            if (value >= size)
            {
                return size + offset;
            }

            return value;
        }

        /// <summary>
        /// Removes <c>&lt;tool&gt;</c> declarations whose name is in <paramref name="candidateNames"/>
        /// (the tools the converted grooves/mills referenced) and is no longer referenced by any
        /// remaining tool-using element in the program. Returns the number removed.
        /// </summary>
        private static int RemoveUnreferencedTools(XElement program, HashSet<string> candidateNames)
        {
            if (candidateNames.Count == 0)
            {
                return 0;
            }

            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in program.Elements())
            {
                var tag = element.Name.LocalName;

                // <tool> is the declaration; <var> @name is a variable, not a tool; <ml>/<mac> carry no name.
                if (tag is "tool" or "var" or "ml" or "mac")
                {
                    continue;
                }

                if (element.GetNameValue() is { } name)
                {
                    referenced.Add(name);
                }
            }

            var removed = 0;

            foreach (var tool in program.Elements("tool").ToList())
            {
                if (tool.GetNameValue() is { } name
                    && candidateNames.Contains(name)
                    && !referenced.Contains(name))
                {
                    tool.Remove();
                    removed++;
                }
            }

            return removed;
        }

        private static XncSymbolTable SeedProgramSymbols(XElement program)
        {
            var symbols = new XncSymbolTable();
            symbols.Set("dx", RequireProgramDouble(program.GetDxValue(), "dx"));
            symbols.Set("dy", RequireProgramDouble(program.GetDyValue(), "dy"));
            symbols.Set("dz", RequireProgramDouble(program.GetDzValue(), "dz"));

            return symbols;
        }

        private static double RequireProgramDouble(string? raw, string name)
        {
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            throw new Exception($"<program> @{name} is missing or not a number (was '{raw}').");
        }

        private static double EvalXnc(string? expression, XncSymbolTable symbols)
        {
            return XncExpressionEvaluator.Evaluate(
                expression ?? throw new Exception("Missing XNC coordinate/value."),
                symbols);
        }

        private static Dictionary<string, double> ReadToolDiameters(XElement program)
        {
            var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var tool in program.Elements("tool"))
            {
                var name = tool.GetNameValue();

                if (name != null
                    && double.TryParse(tool.GetDValue(), NumberStyles.Float, CultureInfo.InvariantCulture, out var diameter))
                {
                    map[name] = diameter; // last declaration wins, matching XncProgramReader
                }
            }

            return map;
        }

        private static string? FindToolByDiameter(Dictionary<string, double> toolsByName, double diameter)
        {
            foreach (var tool in toolsByName)
            {
                if (Math.Abs(tool.Value - diameter) <= DiameterEpsilon)
                {
                    return tool.Key;
                }
            }

            return null;
        }

        private static string MakeToolName(double diameter, Dictionary<string, double> toolsByName, string prefix)
        {
            var baseName = prefix + XmlConvert.ToString(diameter);

            if (!toolsByName.ContainsKey(baseName))
            {
                return baseName;
            }

            for (var n = 1; ; n++)
            {
                var candidate = $"{baseName}_{n}";

                if (!toolsByName.ContainsKey(candidate))
                {
                    return candidate;
                }
            }
        }

        private static ToolPosition ParsePositionCode(string? raw)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
                && Enum.IsDefined(typeof(ToolPosition), code)
                    ? (ToolPosition)code
                    : ToolPosition.Center;
        }

        public int GetXncProgramsCount(int partId)
        {
            if (_project == null) return 0;

            return GetXncOperations().Count(o => o.GetPart()?.GetIdIntValue() == partId);
        }

        public IReadOnlyList<XncProgram> ReadXncPrograms(int partId)
        {
            if (_project == null) return [];

            return GetXncOperations()
                .Where(o => o.GetPart()?.GetIdIntValue() == partId)
                .Select(XncProgramReader.Read)
                .ToList();
        }

        // A part's XNC operations are matched by both the drilled face ("side", front/back) and the
        // part's rotation in the layout ("turn"): the program's bore coordinates are only valid when
        // the target is oriented the same way, so a differing turn must block the copy.
        private static string GetXncFaceKey(XElement xncOperation)
        {
            return $"{xncOperation.Attribute("side")?.Value ?? string.Empty}|{xncOperation.Attribute("turn")?.Value ?? string.Empty}";
        }

        private static string DescribeXncFaces(IEnumerable<XElement> ops)
        {
            return string.Join("; ", ops
                .OrderBy(GetXncFaceKey)
                .Select(o => $"side={o.Attribute("side")?.Value ?? "?"},turn={o.Attribute("turn")?.Value ?? "?"}"));
        }

        private static bool PartsAreIdentical(Part source, Part target, out string reason)
        {
            if (source.Length != target.Length)
            {
                reason = $"length {target.Length} != {source.Length}";
                return false;
            }

            if (source.Width != target.Width)
            {
                reason = $"width {target.Width} != {source.Width}";
                return false;
            }

            if (source.TopBandingId != target.TopBandingId)
            {
                reason = "top band differs";
                return false;
            }

            if (source.BottomBandingId != target.BottomBandingId)
            {
                reason = "bottom band differs";
                return false;
            }

            if (source.LeftBandingId != target.LeftBandingId)
            {
                reason = "left band differs";
                return false;
            }

            if (source.RightBandingId != target.RightBandingId)
            {
                reason = "right band differs";
                return false;
            }

            reason = string.Empty;
            return true;
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

        private string GetRenamedFileName()
        {
            var regex1 = new Regex(@"_ren\.project$");
            var regex2 = new Regex(@"_ren\((\d*)\)\.project$");

            if (!regex1.IsMatch(_source) && !regex2.IsMatch(_source))
            {
                return _source.Replace(".project", "_ren.project");
            }

            if (regex1.IsMatch(_source))
            {
                return regex1.Replace(_source, "_ren(1).project");
            }

            var collection = regex2.Matches(_source);
            var version = int.Parse(collection[0].Groups[1].Value);

            return regex2.Replace(_source, $"_ren({version + 1}).project");
        }

        private string GetGrooveMillFileName()
        {
            var regex1 = new Regex(@"_gm\.project$");
            var regex2 = new Regex(@"_gm\((\d*)\)\.project$");

            if (!regex1.IsMatch(_source) && !regex2.IsMatch(_source))
            {
                return _source.Replace(".project", "_gm.project");
            }

            if (regex1.IsMatch(_source))
            {
                return regex1.Replace(_source, "_gm(1).project");
            }

            var collection = regex2.Matches(_source);
            var version = int.Parse(collection[0].Groups[1].Value);

            return regex2.Replace(_source, $"_gm({version + 1}).project");
        }

        private string GetMillOrderFileName()
        {
            var regex1 = new Regex(@"_mo\.project$");
            var regex2 = new Regex(@"_mo\((\d*)\)\.project$");

            if (!regex1.IsMatch(_source) && !regex2.IsMatch(_source))
            {
                return _source.Replace(".project", "_mo.project");
            }

            if (regex1.IsMatch(_source))
            {
                return regex1.Replace(_source, "_mo(1).project");
            }

            var collection = regex2.Matches(_source);
            var version = int.Parse(collection[0].Groups[1].Value);

            return regex2.Replace(_source, $"_mo({version + 1}).project");
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

        public bool UpdatePart(ref string log, Part part)
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

                log += $"""Cannot set new XNC typeName for part "{part.Name}" with old XNC typeName "{xnc.GetTypeNameValue()}".{'\n'}""";
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
                sheet.Thickness = sheetGood.GetThicknessDecimalValue()!;

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
            // Format string kept byte-identical: this timestamp is written into the
            // saved project XML, so changing it would change output files.
            var description = $"{_timeProvider.GetLocalNow().DateTime:yyyy-MM-dd hh:mm:ss} -> {message}\n";

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

