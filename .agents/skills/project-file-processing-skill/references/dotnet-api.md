# `.project` handling — .NET API map

All paths relative to `XncOptimizerUI/`. The app is WPF/.NET 9, MVVM (CommunityToolkit.Mvvm).
All `.project` I/O goes through the **project services** (`Contracts/`, implemented in
`Services/Project/` and `Services/Xnc/Batch/`), using **LINQ-to-XML** (`XDocument`/`XElement`) -
never `XmlSerializer`. No CLI; the only entrypoint is `App.xaml.cs`, which registers one shared
`ProjectDocument` and every role over it (`App.AddProjectServices`); the view model receives them
bundled as `ProjectServices(Session, Reader, Editor, Restructuring, Batch)`.

Every operation returns **`OperationResult(bool Changed, IReadOnlyList<LogEntry> Log)`**: `Changed`
says whether the document was modified; `LogText` renders the log for the log box (highlighted
entries after a `***` line), `MessageText` without separators (warning dialogs). A refused or failed
operation leaves the document unchanged and explains why in the log.

## `IProjectSession` (`Contracts/IProjectSession.cs`, implemented by `Services/Project/ProjectDocument.cs`)

| member | notes |
|---|---|
| `void Open(string path)` | `XDocument.Load(path)`, root must be `<project>` |
| `void Close()` | forgets the document and its paths |
| `void Save()` | in-place overwrite of `FullPath` |
| `string FullPath { get; }` | current file path (moves when an operation saves a new file) |

`ProjectDocument` also offers the typed queries every service uses - `Operations(typeId)`,
`Goods(typeId)` (type ids in `Services/Project/GibLabTypes.cs`), `XncOperations()`,
`XncOperationsOf(partId)`, `ProductParts()` - plus `AppendDescription(text)` (-> `project/@description`)
and `SaveAsSuffixed(suffix)` (output names in `Services/Project/ProjectFileNames.cs`: `_opt`, `_spl`,
`_ren`, `_gm`, `_bm`, `_mo`, `_to`, `_off`, `_cln`; re-running on a suffixed file gives `(1)`, `(2)`, ...).

## `IProjectReader` (`Services/Project/ProjectReader.cs`) - every read is self-contained

| member | notes |
|---|---|
| `List<Band> ReadBands()` | one band per EL operation, named after its band good; external symbols A-Z by thickness |
| `List<Sheet> ReadSheets()` | one sheet per CS operation |
| `List<Product> ReadProducts()` | `<good typeId="product">` goods |
| `List<Part> ReadParts()` | resolves band names and the sheet itself; throws `InvalidDataException` for a part on no sheet |
| `int GetXncProgramsCount(int partId)` | XNC operations of a part |
| `IReadOnlyList<XncProgram> ReadXncPrograms(int partId)` | parsed, read-only programs of a part |
| `IReadOnlyList<string> GetPartsWithXncTurnDiscordance()` | parts whose XNC operations disagree on `turn` |

## `IProjectEditor` (`Services/Project/ProjectEditor.cs`) - in place, **saves nothing** (the caller `Session.Save()`s)

| member | notes |
|---|---|
| `UpdatePart(Part part)` | name/size/txt/bands back into `<part>` (`PartUpdater`): renames the part's XNC operations, syncs EL part lists, recalculates `jl`/`jw`; unchanged when nothing differs |
| `RotatePart(int partId, int targetTurn, bool flipEdgeGrooveTcl)` | every XNC op of the part to absolute `turn` 0..3, program rotated from its own turn (`Services/Xnc/XncProgramRotator.cs`); `flipEdgeGrooveTcl=false` keeps edge-groove `c` as GibLab does |
| `UpdateBore(int operationId, int boreIndex, BoreAttribute attribute, string expression)` | `Services/Xnc/XncProgramEdits.cs` |
| `UpdateVariable(int operationId, int variableIndex, XncVariableAttribute attribute, string value)` | `Services/Xnc/XncProgramEdits.cs` |

## `IProjectRestructuring` (`Services/Project/ProjectRestructuring.cs`) - saves a new file

| member | notes |
|---|---|
| `GroupIdenticalElements()` | `IdenticalPartsGrouper`: group XNC ops with the same program on parts with the same band materials, renumber ids, collapse products; `*_opt.project` |
| `PrepForSplitAlongX(IReadOnlyCollection<string> partIds)` | `SplitAlongXPreparer`: `width = w*2 + config.SawWidth`, count halved, bores mirrored; `*_spl.project` |
| `ReplaceXncPrograms(Part source, IList<Part> targets)` | `XncProgramReplacer`: copy `program`+`countBore` onto identical parts (face key `"{side}|{turn}"`); `*_ren.project` |

## `IXncBatchOperations` (`Services/Xnc/Batch/XncBatchOperations.cs`) - saves a new file

Each method is an `IXncBatch<TStats>` spec (`Services/Xnc/Batch/*Batch.cs`: per-program rewrite + log
texts) run by `XncBatchRunner`, which rewrites every program before writing any back (a failing program
leaves the document untouched), logs each part, and saves nothing when nothing changed.

| member | notes |
|---|---|
| `ConvertGroovesAndMills(IList<Part> parts, GrooveMillDirection direction, bool processPockets)` | `Conversions/GrooveMillConverter.cs`, see §Conversions; `*_gm.project` |
| `ConvertBoresAndMills(IList<Part> parts, BoreMillDirection direction, bool useEllipse)` | `Conversions/BoreMillConverter.cs`: face bore (Ø>34) <-> round mill (fixed 6 mm `Mill6`); `*_bm.project` |
| `OptimizeMillTraversal(IList<Part> parts)` | `MillTraversalOptimizer.cs`: greedy nearest-neighbour re-sequence of straight axis-parallel passes; `*_mo.project` |
| `SortToolDeclarations(IList<Part> parts)` | `XncToolDeclarationSorter.cs`; `*_to.project` |
| `OffsetMillPaths(IList<Part> parts, double offset, MillOffsetSide side, MillPathKinds kinds)` | `MillPathOffsetter.cs`; `*_off.project` |
| `CleanOutsideOperations(IList<Part> parts, OutsideOperationKinds kinds)` | `Outside/OutsideOperationDetector.cs` (`BoreOutsideRule`, `GrooveOutsideRule`, `MillOutsideRule`); `*_cln.project` |

Program text is always read and written through `Services/Xnc/XncProgramText.cs` (keeps the
`<?xml ...?>` declaration). Element names: `Services/Xnc/XncTags.cs`; tool name prefixes
(`Bore`/`Cut`/`Mill`): `Services/Xnc/XncToolKinds.cs`.

## Accessor layer — `Extensions/XContainersExtensions.cs`

Static `XElement`/`XContainer` extension methods — **use these, not raw `Attribute("x")?.Value`**.

- structure: `GetProject`, `GetOperations` (`Elements("operation")`), `GetGoods`, `GetPart`/`GetParts`
- ids/names: `GetIdValue`/`GetIdIntValue`/`SetIdValue`, `GetTypeIdValue`, `GetNameValue`/`SetNameValue`,
  `GetTypeNameValue`/`SetTypeNameValue`, `GetCodeValue`, `GetGroupCodeValue`
- sizes: `GetLengthDecimalValue`/`SetLengthValue`/`SetDLengthValue` (`l`/`dl`),
  `GetWidthDecimalValue`/`SetWidthValue`/`SetDWidthValue` (`w`/`dw`), `GetThicknessDecimalValue` (`t`)
- banding: `GetElbIdIntValue..GetEltIdIntValue` (`GetBandingIdIntValue` decodes `value.Split('#')[1]`), `SetBandingValue`, `GetElbMatValue..GetEltMatValue`
- material: `GetOperationMaterialIdValue` (`element.Element("material").Attribute("id")`), `GetMat`
- XNC sub-document (`#region XNC program sub-document`): `GetProgram`/`GetProgramValue`, and
  **string** readers `GetDxValue/GetDyValue/GetDzValue/GetDValue/GetExprValue/GetXValue/GetYValue/
  GetZValue/GetX1Value/GetY1Value/GetX2Value/GetY2Value/GetCxValue/GetCyValue/GetDpValue/GetTValue/
  GetCValue/GetPValue/GetInValue/GetOutValue/GetSxyValue/GetDirValue/GetAvValue/GetAValue/GetRValue/
  GetSideValue/GetCommentValue`. These stay **strings** because the values are often expressions.

## XNC parsing services — `Services/Xnc/`

| file | role |
|---|---|
| `XncProgramReader.cs` | `static XncProgram Read(XElement xncOperation)`. Decodes via `XncProgramText.Load`, seeds `dx/dy/dz`, dispatches each child by tag to its `Parse*` method (one per element kind; state in a private `ReadState`), builds contours, resolves every coordinate. Unknown elements ignored. Throws `XncProgramFormatException`. |
| `XncAttributeParsing.cs` | plain attribute values: `RequireDouble`, `RequireDimension`, `ParseInt`/`ParseBool`, `ParsePositionCode`, `IsClockwise`, `ArrayCount`, `ToolDiameter`/`ReadToolDiameters` |
| `XncSymbols.cs` | `SeedProgramSymbols`, `DimensionSymbols`, `ProgramSymbols`, `SymbolsBefore`, `Eval` (the one evaluation wrapper), `EvalPoint`, `ExpressionAttributes`, `RenameIdentifier`/`ReferencesIdentifier` |
| `XncNumberFormat.cs` | `FormatNumber`/`SetNumber`: how rewriters write recalculated values |
| `Geometry/` | `Vec2`, `Primitive`, `PlanarGeometry`, `Box`, `ChordPath`, `ContourOffsetGeometry`, `MillShapeGeometry` |
| `XncExpressionEvaluator.cs` | `Evaluate(expr, XncSymbolTable)` — recursive-descent `+ - * /`, parens, unary sign, dotted ids |
| `XncSymbolTable.cs` | `Set` / `SetToolDiameter` / `TryGet`, **case-insensitive**; predefined `dx, dy, dz, tool.dia` |
| `XncProgramFormatException.cs` | parse-error type |

## Parsed XNC model — `MVVM/Models/Xnc/` (read-only — cannot write back to XML)

| type | key members |
|---|---|
| `XncProgram` | `Dx, Dy, Dz` (mm), `Side` (bool), `Tools`, `Bores`, `Groovings`, `MillingContours`, `MillingRectangles`, `Variables` (`IReadOnlyDictionary<string,double>`) |
| `XncTool` | `Name`, `Diameter` |
| `XncBore` | `Surface` (`BoreSurface`), `ToolName`, `X`, `Y`, `Z`, `Depth`, `Through` |
| `XncGrooving` | `ToolName`, `Start`, `End` (`XncPoint`), `Depth`, `Width`, `SideCode`, `Position` (`ToolPosition`), `Comment` |
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
| `Product` | `Id, Name, Code, Count` — wraps `<good typeId="product">` |

There is **no** `Program`/`Groove` domain class at the `.project` level — those stay
raw `XElement` inside the project services.

## Config — `Configuration/AppOptions.cs`, `Services/ConfigService.cs`

```csharp
public class AppOptions
{
    public decimal SawWidth { get; set; } = 4.0m;
    public List<string> LabelsToProcess { get; set; } = ["поріз.2х40"];
    public int LastLabelToProcessSelectedIndex { get; set; } = 0;
    public List<decimal> GroovingToolWidths { get; set; } = [2.8m, 3.2m];   // the first one cuts mills -> grooves
    public List<decimal> MillingToolDiams { get; set; } = [6.0m, 10.0m, 20.0m];
}
```

Persisted JSON at `%AppData%/XncOptimizerUI/configuration.json`. `IConfigService` exposes
`SawWidth`, `MillingToolDiams`, `GroovingToolWidths`, `LabelsToProcess`, `GetLastLabelToProcessSelected()`,
`AddLabelToProcess`, `DeleteLabelToProcess`, `UpdateSawWidth`,
`UpdateLastLabelToProcessSelectedIndex`. Consumers: `SawWidth` -> `PrepForSplitAlongX`;
`MillingToolDiams` / `GroovingToolWidths` -> `ConvertGroovesAndMills` (empty => that direction is cancelled).

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
- grooving cutter = the **first `AppOptions.GroovingToolWidths`** entry (default 2.8 mm -> `Cut2.8`).
- convert only: exactly one straight axis-parallel `<ml>`, `max(entryDepth, segDepth) < dz`,
  not a pocket (`c != 3`), `<ms>@name` resolves to a known tool diameter (that diameter becomes
  the groove `t`). Running-axis endpoints **clamped** onto `[0,dx]`/`[0,dy]` (no overshoot).
  Emit `<gr x1 y1 dp x2 y2 t=millDia c=@c??"0" p="0" name="Cut2.8" [comment]/>`.
- with `processPockets`: also `<mr c="3">` (`a` ~ 0, `dp<dz`) and rectangular contour pockets
  (`c="3"`, >=3 `<ml>`, axis-parallel rectangle) -> groove along the longer side.
- non-compliant / through / diagonal / `<mr>` (without `processPockets`) -> ignored, tallied.

Constants: `GrooveMillConverter.AxisEpsilon = 1e-6`, `ConversionTools.DiameterEpsilon = 1e-6`.

`ConvertBoresAndMills(parts, BoreMillDirection, useEllipse)` — face bores <-> round mills:

- **`BoresToMills`**, per `<bf>` whose `<tool>` diameter `> BoreMillConverter.MinDiameter` (34 mm): mill it
  out with a fixed 6 mm cutter, `ConversionTools.Ensure(bf, 6.0, XncToolKinds.Mill, ...)` -> `Mill6` (reused
  if already declared at 6 mm). `dp` is carried across verbatim (string). Default form emits a
  closed contour: `<ms x=cx y=cy-r dp in="0" out="1" sxy="tool.dia/2" fwd="true" c=? name="Mill6"/>`
  (top of the circle) then four `<mac>` quarter arcs about `(cx, cy)` with `dir="false"`, ending
  at `(cx-r, cy)`, `(cx, cy+r)`, `(cx+r, cy)` and back at `(cx, cy-r)` - declared
  counter-clockwise on screen. `useEllipse` emits one `<me x=cx y=cy dp ... l=r w=r a="0" c=? name="Mill6"/>`
  instead (**`<me>` `l`/`w` are semi-axes = radius, not diameter**). A blind bore (`av != "true"`
  and `dp < dz`) is a pocket, `c="3"`, in both forms. A through bore keeps the tool inside the
  hole: `c="2"` for the counter-clockwise contour, `c="1"` for the `<me>` (clockwise for
  `fwd="true"`, so right = inside) - see `xnc-program-format.md` §6.4. Edge
  bores and Ø<=34 bores are untouched (not tallied). Orphaned `Bore*` tools dropped via
  `ConversionTools.RemoveUnreferenced`.
- **`MillsToBores`**: an `<me>` with `l == w` (Ø = `2*l`), or an `<ms>` + a run of >= 2 arc
  segments — `<mac>` (centre = `cx`/`cy`) or `<ma>` (centre reconstructed from chord + `r` + `dir`;
  half-circle case is centre = chord midpoint) — that share one centre and radius and whose last
  end point returns to the entry. Ø must exceed 34 mm. Emits `<bf x=cx y=cy dp ac="1" av="false"
  name="Bore<Ø>"/>` (depth = max of entry + segment depths), `ConversionTools.Ensure(anchor, Ø,
  XncToolKinds.Bore, ...)`. Non-compliant `<ms>`/`<me>` are tallied as ignored. Orphaned `Mill*` tools dropped.
- Nothing converted ⇒ unchanged result, saves nothing. Constants: `BoreMillConverter.MinDiameter = 34.0`,
  `BoreMillConverter.CutterDiameter = 6.0`, geometry tolerance `1e-3`.

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
   Model the whole renumber-and-remap on `Services/Project/IdenticalPartsGrouper.cs`, which
   already does exactly this within one file.
3. Deduplicate infra goods by value: `tool.cutting`, `tool.edgeline`, `sheet`, `band`. Keep one
   instance, repoint merged operations/parts at its id.
4. Append the foreign `<good>` then `<operation>` nodes into the base `<project>` (**goods before
   operations** — preserve that order).
5. `AppendDescription($"merged {Path.GetFileName(src)}")` per source.
6. `doc.Save` to a new `*_merged.project` (reuse the regex filename-suffix helper pattern).

XNC `program` sub-documents need **no** id remapping — their coordinates are part-local.

## Headless usage pattern (from `XncOptimizerUI.Test/ProjectServicesTests.cs`)

```csharp
var config = Substitute.For<IConfigService>();
config.SawWidth.Returns(4.0m);
config.MillingToolDiams.Returns(new List<decimal> { 6.0m, 10.0m, 20.0m });
config.GroovingToolWidths.Returns(new List<decimal> { 2.8m });
var project = TestProject.Create(config, timeProvider);   // ProjectServices, wired like App.xaml.cs
project.Session.Open(path);
var parts = project.Reader.ReadParts();
var result = project.Batch.ConvertGroovesAndMills(parts, GrooveMillDirection.GroovesToMills, processPockets: false);
// result.Changed, result.LogText; output -> <name>_gm.project next to the input (project.Session.FullPath)
```

Fixtures: `XncOptimizerUI/TestData/*.project`. Conversion/optimization tests:
`ProjectServicesTests.ConvertGroovesAndMills_*`, `OptimizeMillTraversal_*`; byte-exact output of every
operation: `OperationGoldenTests` (`Golden/Operations/`). Reader tests: `XncProgramReaderTests`,
`XncExpressionEvaluatorTests`.
