# `.project` handling — .NET API map

All paths relative to `XncOptimizerUI/`. The app is WPF/.NET 9, MVVM (CommunityToolkit.Mvvm).
All `.project` I/O goes through **`GibLabProjectService`** (behind **`IProjectService`**), using
**LINQ-to-XML** (`XDocument`/`XElement`) — never `XmlSerializer`. No CLI; the only entrypoint is
`App.xaml.cs`.

## `IProjectService` (`Contracts/IProjectService.cs`)

| member | notes |
|---|---|
| `void OpenProject(string path)` | `_doc = XDocument.Load(path)`, `_project = doc.Element("project")` |
| `void CloseProject()` | nulls doc/project, clears caches |
| `List<Band> ReadBands()` | **call before `ReadParts`** — fills the band cache |
| `List<Sheet> ReadSheets()` | **call before `ReadParts`** — sheet-id resolution |
| `List<Part> ReadParts()` | resolves banding names + sheet ids from the two caches above |
| `bool UpdatePart(ref string log, Part part)` | writes the change and `SaveProject()` — **overwrites the input file** |
| `void SaveProject()` | `_doc.Save(FullPath)` — in-place overwrite |
| `bool GroupIdenticalElements(ref string log)` | de-dupe XNC ops within one file, renumber ids, collapse products; saves `*_opt.project` |
| `void PrepForSplitAlongX(ref string log, string[] selectedPartsIds)` | uses `config.SawWidth` (`width = w*2 + SawWidth`) |
| `bool ReplaceXncPrograms(ref string log, Part source, IList<Part> targets)` | copy `program`+`countBore` onto identical parts (face key `"{side}|{turn}"`); saves `*_ren.project` |
| `bool ConvertGroovesAndMills(ref string log, IList<Part> parts, GrooveMillDirection direction, bool processPockets)` | see §Conversions; saves `*_gm.project` |
| `bool OptimizeMillTraversal(ref string log, IList<Part> parts)` | greedy nearest-neighbour re-sequence of straight axis-parallel passes; saves `*_mo.project` |
| `int GetXncProgramsCount(int partId)` | count XNC ops for a part |
| `IReadOnlyList<XncProgram> ReadXncPrograms(int partId)` | parse every XNC program for a part (read-only model) |
| `string FullPath { get; }` | current file path (moves after a transform saves a new file) |

Batch transforms return `false` and save nothing when they changed nothing. Output filename
helpers (regex suffix, collision -> `(1)`, `(2)`): `_opt`, `_ren`, `_gm`, `_mo`. Audit trail via
`AppendDescription(text)` -> `project/@description`.

## Accessor layer — `Extensions/XContainersExtensions.cs`

Static `XElement`/`XContainer` extension methods — **use these, not raw `Attribute("x")?.Value`**.

- structure: `GetProject`, `GetOperations` (`Elements("operation")`), `GetGoods`, `GetPart`/`GetParts`
- ids/names: `GetIdValue`/`GetIdIntValue`/`SetIdValue`, `GetTypeIdValue`, `GetNameValue`/`SetNameValue`,
  `GetTypeNameValue`/`SetTypeNameValue`, `GetCodeValue`, `GetGroupCodeValue`
- sizes: `GetLengthDecimalValue`/`SetLengthValue`/`SetDLengthValue` (`l`/`dl`),
  `GetWidthDecimalValue`/`SetWidthValue`/`SetDWidthValue` (`w`/`dw`), `GetThicknessDecimalValue` (`t`)
- banding: `GetElbId..GetEltId` + `...IntValue` (decode `value.Split('#')[1]`), `GetElbMat..GetEltMat`
- material: `GetOperationMaterialIdValue` (`element.Element("material").Attribute("id")`), `GetMat`
- XNC sub-document (`#region XNC program sub-document`): `GetProgram`/`GetProgramValue`, and
  **string** readers `GetDxValue/GetDyValue/GetDzValue/GetDValue/GetExprValue/GetXValue/GetYValue/
  GetZValue/GetX1Value/GetY1Value/GetX2Value/GetY2Value/GetCxValue/GetCyValue/GetDpValue/GetTValue/
  GetCValue/GetPValue/GetInValue/GetOutValue/GetSxyValue/GetDirValue/GetAvValue/GetAValue/GetRValue/
  GetSideValue/GetCommentValue`. These stay **strings** because the values are often expressions.

## XNC parsing services — `Services/Xnc/`

| file | role |
|---|---|
| `XncProgramReader.cs` | `static XncProgram Read(XElement xncOperation)` / `Parse(XElement program, bool side)`. Decodes `XDocument.Parse(WebUtility.HtmlDecode(raw))`, seeds `dx/dy/dz`, walks children, builds contours, resolves every coordinate. Unknown elements ignored. Throws `XncProgramFormatException`. |
| `XncExpressionEvaluator.cs` | `Evaluate(expr, XncSymbolTable)` — recursive-descent `+ - * /`, parens, unary sign, dotted ids |
| `XncSymbolTable.cs` | `Set` / `TryGet`, **case-insensitive**; predefined `dx, dy, dz, tool.dia` |
| `XncProgramFormatException.cs` | parse-error type |

## Parsed XNC model — `MVVM/Models/Xnc/` (read-only — cannot write back to XML)

| type | key members |
|---|---|
| `XncProgram` | `Dx, Dy, Dz` (mm), `Side` (bool), `Tools`, `Bores`, `Groovings`, `MillingContours`, `MillingRectangles`, `Variables` (`IReadOnlyDictionary<string,double>`) |
| `XncTool` | `Name`, `Diameter` |
| `XncBore` | `Surface` (`BoreSurface`), `ToolName`, `X`, `Y`, `Z`, `Depth`, `Through` |
| `XncGrooving` | `ToolName`, `Start`, `End` (`XncPoint`), `Depth`, `Width`, `Position` (`ToolPosition`), `Comment` |
| `XncMillingContour` | `ToolName`, `Entry`, `EntryDepth`, `Position`, `LeadIn`, `LeadOut`, `StartOffsetXY`, `Segments` |
| `XncMillingSegment` (abstract) | `End` (`XncPoint`), `Depth` |
| `XncLineSegment : XncMillingSegment` | `<ml>` |
| `XncArcSegment : XncMillingSegment` | `Center`, `Clockwise`, `Radius` (`<mac>` — radius derived) |
| `XncMillingRectangle` | `ToolName`, `Origin` (centre), `Length`, `Width`, `Angle`, `CornerRadius`, `Depth`, `Position`, `LeadIn`, `LeadOut`, `StartOffsetXY` |
| `XncPoint` | `readonly record struct XncPoint(double X, double Y)` — mm, expressions resolved |
| `ToolPosition` (enum) | `Center=0, Right=1, Left=2, Pocket=3` (the `c` attribute) |
| `BoreSurface` (enum) | `Face, Top, Bottom, Left, Right` (`bf/bt/bb/bl/br`) |
| `GrooveMillDirection` (enum) | `GroovesToMills=0, MillsToGrooves=1` |

## Domain models — `MVVM/Models/`

| type | members |
|---|---|
| `Part` | `Id, GoodId, MaterialId, Name, Count, Length, Width, ConsiderTexture, TopBandingId/BottomBandingId/LeftBandingId/RightBandingId (nullable), TopBandingMat..RightBandingMat, SheetId` |
| `Band` | `Id, Name, Code, Width, Thickness, InternalSymbol (elSymbol), ExternalSymbol, Color` |
| `Sheet` | `Id, Name, Code, Thickness` |
| `Context` | `Log, FullPath, SearchText, ObservableCollection<Part> Parts` |

There is **no** `Program`/`Groove`/`Product` domain class at the `.project` level — those stay
raw `XElement` inside `GibLabProjectService`.

## Config — `Configuration/AppOptions.cs`, `Services/ConfigService.cs`

```csharp
public class AppOptions
{
    public decimal SawWidth { get; set; } = 4.0m;
    public List<string> LabelsToProcess { get; set; } = ["поріз.2х40"];
    public int LastLabelToProcessSelectedIndex { get; set; } = 0;
    public List<decimal> GroovingToolWidths { get; set; } = [2.8m, 3.2m];   // NOT wired to anything
    public List<decimal> MillingToolDiams { get; set; } = [6.0m, 10.0m, 20.0m];
}
```

Persisted JSON at `%AppData%/XncOptimizerUI/configuration.json`. `IConfigService` exposes
`SawWidth`, `MillingToolDiams`, `LabelsToProcess`, `GetLastLabelToProcessSelected()`,
`AddLabelToProcess`, `DeleteLabelToProcess`, `UpdateSawWidth`,
`UpdateLastLabelToProcessSelectedIndex`. Consumers: `SawWidth` -> `PrepForSplitAlongX`;
`MillingToolDiams` -> `ConvertGroovesAndMills` (empty => Grooves->Mills cancelled).

## §Conversions — `ConvertGroovesAndMills` algorithm

`GrooveMillDirection.GroovesToMills`, per `<gr>`:
1. **primary pass only** — `p` absent or `"0"`; else ignored.
2. resolve `x1,y1,x2,y2`; must be **axis-parallel** (exactly one of horizontal / vertical); else ignored.
3. `width = eval(t)`, `dp = @dp ?? "0"`.
4. **exact diameter match** in `MillingToolDiams` (+/-1e-6): emit `<ms x y dp in="0" out="0"
   sxy="tool.dia/2" fwd="true" c=@c??"0" name=tool/>` + `<ml x y dp/>`. Reuse a `<tool>` of that
   diameter, else synthesise `<tool name="Bore<d>" d=.../>`. Running-axis endpoints on/over the
   edge (`<=0` or `>= dx/dy`) pushed out by `width/2`. Replace the `<gr>`.
5. **no exact match** -> mill as a rectangular pocket: `toolDiam = min(MillingToolDiams)`; if
   `toolDiam > width` -> ignored; else emit `<mr ... l w a="0" r="0" c="3" name=tool/>` along the
   groove's long axis (running side = length, other = `width`), overshoot edge ends by
   `toolDiam/2`. Remove the `<gr>`.
6. drop `<tool>` declarations left unreferenced by the conversion (`toolsRemoved`).

`GrooveMillDirection.MillsToGrooves`, per `<ms>`+segment run:
- grooving cutter is **hard-coded 2.8 mm** (`Cut2.8`; `AppOptions.GroovingToolWidths` unused).
- convert only: exactly one straight axis-parallel `<ml>`, `max(entryDepth, segDepth) < dz`,
  not a pocket (`c != 3`), `<ms>@name` resolves to a known tool diameter (that diameter becomes
  the groove `t`). Running-axis endpoints **clamped** onto `[0,dx]`/`[0,dy]` (no overshoot).
  Emit `<gr x1 y1 dp x2 y2 t=millDia c=@c??"0" p="0" name="Cut2.8" [comment]/>`.
- with `processPockets`: also `<mr c="3">` (`a` ~ 0, `dp<dz`) and rectangular contour pockets
  (`c="3"`, >=3 `<ml>`, axis-parallel rectangle) -> groove along the longer side.
- non-compliant / through / diagonal / `<mr>` (without `processPockets`) -> ignored, tallied.

Constants: `AxisEpsilon = 1e-6`, `DiameterEpsilon = 1e-6`, `GroovingToolDiameter = 2.8`.

## §Merge recipe — merging N `.project` files (not implemented; build with this)

1. `XDocument.Load` each source. Pick a **base** (target `version` must match across sources; warn
   on differing sheet `t`).
2. Ids are **file-local integers** and will collide. For each non-base source compute an offset
   (`maxIdInMergedSoFar + 1`) or a fresh sequence, then rewrite the `id` **and every reference**:
   - `good/@id`, `operation/@id`, `part/@id`
   - `operation/part/@id` (-> product part), last `operation[CS]/part/@id` (-> sheet part)
   - `operation[CS]/material/@id` (-> sheet), `operation[EL]/material/@id` (-> band)
   - `operation/@tool1` (-> tool.cutting / tool.edgeline)
   - `part/@elt|@elb|@ell|@elr` — `"@operation#<id>"` token, rewrite the numeric tail
   Model the whole renumber-and-remap on `GibLabProjectService.GroupIdenticalElements`, which
   already does exactly this within one file.
3. Deduplicate infra goods by value: `tool.cutting`, `tool.edgeline`, `sheet`, `band`. Keep one
   instance, repoint merged operations/parts at its id.
4. Append the foreign `<good>` then `<operation>` nodes into the base `<project>` (**goods before
   operations** — preserve that order).
5. `AppendDescription($"merged {Path.GetFileName(src)}")` per source.
6. `doc.Save` to a new `*_merged.project` (reuse the regex filename-suffix helper pattern).

XNC `program` sub-documents need **no** id remapping — their coordinates are part-local.

## Headless usage pattern (from `XncOptimizerUI.Test/GibLabProjectServiceTests.cs`)

```csharp
var config = Substitute.For<IConfigService>();
config.SawWidth.Returns(4.0m);
config.MillingToolDiams.Returns(new List<decimal> { 6.0m, 10.0m, 20.0m });
var svc = new GibLabProjectService(config, timeProvider);
svc.OpenProject(path);
svc.ReadBands();          // before ReadParts
svc.ReadSheets();         // before ReadParts
var parts = svc.ReadParts();
string log = "";
svc.ConvertGroovesAndMills(ref log, parts, GrooveMillDirection.GroovesToMills, processPockets: false);
// output -> <name>_gm.project next to the input
```

Fixtures: `XncOptimizerUI/TestData/*.project`. Conversion/optimization tests:
`GibLabProjectServiceTests.ConvertGroovesAndMills_*`, `OptimizeMillTraversal_*`. Reader tests:
`XncProgramReaderTests`, `XncExpressionEvaluatorTests`.
