---
name: code-analysis
description: Analysis of the XncOptimizerUI repo — what the app does, its architecture, and open questions/gaps found while reading the code. Keep this document aligned with the current source and the project-file-processing skill.
---

# What XncOptimizerUI is

A Windows desktop app (WPF, .NET 9, MVVM) for opening, inspecting, editing, and batch-transforming `.project` XML files produced by **GibLab** CAM/nesting software (furniture/panel-cutting production). It provides a parts/bands/sheets browser with live editing, filtering, CSV/clipboard export, persisted user configuration, XNC machining-program reading and copying, groove/mill conversion, mill-traversal optimization, and dependency injection (DI) for testability.

# Domain model of the .project file (unchanged)

Root `<project>` containing `<good typeId="product">` (finished product, holding `<part>` panels: name, id, dimensions `l`/`w` plus detail/cut/trimmed size fields, `count`, edge-band refs `elt`/`elb`/`ell`/`elr` encoded as `@operation#<EL id>` and matching `*Mat` names), `<good typeId="sheet">` (raw sheet stock), `<good typeId="band">` (edge-banding material), and tool goods. Operations are flat `<operation typeId="...">` children: `XNC` (CNC drill/rout with an escaped XML `program` sub-document), `CS` (saw cut, references parts plus a trailing sheet part and `<material>`), and `EL` (edge-banding, references a `<material>` = band good).

# Architecture

## Core abstractions & services

- **`XncOptimizerUI.Contracts.IProjectService`** — abstraction for all XML manipulation and file I/O (`OpenProject`, `CloseProject`, `SaveProject`, `GroupIdenticalElements`, `PrepForSplitAlongX`, `UpdatePart`, `ReadParts`/`ReadBands`/`ReadSheets`, `ReadXncPrograms`, `GetXncProgramsCount`, `ReplaceXncPrograms`, `ConvertGroovesAndMills`, `OptimizeMillTraversal`, `FullPath`), implemented by `GibLabProjectService`.
- **`Services/GibLabProjectService.cs`** — contains the XML/document logic and XNC program reading (`ReadXncPrograms`), XNC program copying (`ReplaceXncPrograms`), XNC operation counting (`GetXncProgramsCount`), groove ⇄ mill conversion (`ConvertGroovesAndMills`), and mill traversal optimization (`OptimizeMillTraversal`). Uses `Services/Xnc/XncProgramReader.cs` + `XncExpressionEvaluator.cs` + `XncSymbolTable.cs` for program parsing (see [[project-file-processing-skill]]).
- **`IConfigService`**, **`IDialogService`** — injected abstractions (formerly static/modal classes), replacing the old `ConfigService` static methods and direct `MessageBox`/`SaveFileDialog` calls with testable dependencies.
- **`Extensions/XContainersExtensions.cs`** — null-safe getters/setters for XML attributes: typed accessors (`GetLengthDecimalValue`, `GetIdIntValue`, `GetElbIdIntValue` for `@operation#<id>` parsing, `GetProgramValue`, `GetSideValue`, etc.), and mutation methods (`SetLengthValue`, `SetWidthValue`, ...) to support in-place editing.

## Domain models & presentation

- **Models** (`MVVM/Models/Part.cs`, `Band.cs`, `Sheet.cs`) — plain data classes (`Id`, `Name`, `Length`, `Width`, `Count`, sheet/banding relationships).
- **`MVVM/Models/Xnc/`** — read-only classes representing parsed XNC programs: `XncProgram` (top-level), `XncTool`, `XncBore`+`BoreSurface`, `XncGrooving`, `XncMillingContour`+`XncMillingSegment` (`XncLineSegment`, `XncArcSegment`), `XncMillingRectangle`, `ToolPosition`, `XncPoint`. Format documented in [[project-file-processing-skill]].
- **ViewModels** (`PartVM`, `BandVM`, `SheetVM`) — wrap domain models for data-binding; `AppViewModel` orchestrates the UI.
- **`AppViewModel.cs`** — file lifecycle (Open/Save/Close), filterable `Parts`/`Bands`/`Sheets` grids, part selection + edit-on-selection-change that auto-saves, XNC program display (`SelectedPartPrograms`), CSV/clipboard export, label management.

## Configuration & testing

- **`Services/ConfigService.cs`** + **`Configuration/AppOptions.cs`** — persists user settings as JSON at `%AppData%\XncOptimizerUI\configuration.json`: `SawWidth` (kerf width, default 4.0), `LabelsToProcess` (text labels for filtering, default `["поріз.2х40"]`), `MillingToolDiams` (default `[6, 10, 20]`), and the remembered last-selected label. `GroovingToolWidths` is present in `AppOptions` but currently unused by conversion.
- **`App.xaml.cs`** — DI composition root using `Microsoft.Extensions.DependencyInjection`; configures `IProjectService` → `GibLabProjectService`, `IConfigService` → `ConfigService`, `IDialogService` → `DialogService`, injects `TimeProvider`.
- **`XncOptimizerUI.Test`** — NUnit test project with `AppViewModelTests`, `GibLabProjectServiceTests`, `ConfigServiceTests`, `XncProgramReaderTests`, `XncExpressionEvaluatorTests`, `Fakes/FakeProjectService.cs`. Headless (no `MessageBox`, `SaveFileDialog`, or static config singletons).
- **`MainWindow.xaml`** — 3-column layout: command buttons + label management (left), Bands `DataGrid` (top-right), filterable Parts `DataGrid` + log box (bottom). Icon `xnc_logo.ico`.

# User-facing operations

**File lifecycle** — Open/Save/Close project XML file. Opening populates Bands, Sheets, Parts grids from the parsed file. Closing clears all state. Window title shows assembly name + current filename.

**Parts grid + filter + edit** — lists every part (name, length/width/count, edge-banding materials resolved to human-readable names via Bands lookup, sheet id). Filter by name substring / exact length / exact width. On-grid editing of Name/Length/Width persists via `UpdatePart` (which sync's the part name with its XNC operation's group-code prefix `[01]` if present) and auto-saves.

**XNC program display** — when a part is selected, its XNC programs (one per machined face) render in the "Programs" box as multi-line human-readable summaries:
```
Programs: 1
  /xnc/front/dx1380 dy600 dz19
  /tool/front/Bore8 Ø8
  /bore/front/Left Bore8 (0,65,10) dp34
  /groove/front/Cut3.2 (-10,565)-(1390,565) dp4 w10 Center
  /mill/front/Mill6 (250,382.5) dp21 Left 2 arc
```
Implemented via `ReadXncPrograms(int partId)` → `XncProgramReader.Read()` which parses the escaped XML `program` sub-document. Format documented in [[project-file-processing-skill]].

**Replace XNC programs** — source part's drill programs (one per face: front/back) are validated, then copied to selected target parts. Before copying, all targets are checked for: (1) identical dimensions & banding, (2) same number of XNC faces, and (3) matching face/turn orientations. If validation passes, the `program` attribute and `countBore` metadata are overwritten, and the file is saved with the `_ren.project` suffix (or a numbered collision suffix) with an audit description. Implemented in `GibLabProjectService.ReplaceXncPrograms(ref string log, Part sourcePart, IList<Part> targetParts)` with detailed validation and error logging.

**Convert grooves ⇄ mills** — for the parts **checked** in the Parts grid (the
`PartVM.IsSelected` column), rewrites every XNC program in the chosen direction. The service
also accepts a `processPockets` flag. *Grooves → Mills*: an axis-parallel primary-pass `<gr>`
(`p="0"` or absent) with an exact configured milling-tool diameter becomes a single-segment
`<ms>` + `<ml>` contour. An off-size groove becomes a rectangular pocket using the smallest
configured cutter when that cutter fits; otherwise it is ignored. Edge-reaching contour
endpoints overshoot by half the relevant cutter diameter. Secondary-pass and diagonal grooves
remain untouched. *Mills → Grooves*: a shallow, axis-parallel, single-straight-segment
non-pocket contour is converted and its outside endpoints are clamped to the outline. With
`processPockets`, supported axis-parallel rectangular `<mr>` pockets and rectangular contour
pockets are converted too. The grooving cutter is hard-coded to 2.8 mm;
`AppOptions.GroovingToolWidths` is not wired into this operation. Non-compliant elements are
left untouched and tallied as ignored; logs include converted/ignored and tools
added/removed counts. Nothing converted ⇒ returns `false`, saves nothing. Output is
`_gm.project` with collision numbering and an audit description.
`GibLabProjectService.ConvertGroovesAndMills(ref string log, IList<Part> parts, GrooveMillDirection direction, bool processPockets)`.

**Group identical elements** — (originally "Optimize") clusters XNC operations by: program content + edge-band materials. Groups are renumbered, consolidated into one product good, saved as `_opt.project`. Guards against re-running on already-optimized files or files with no XNC operations (logs warning instead of silently no-op'ing).

**Optimize mill traversal** — for checked parts, re-sequences eligible one-segment,
axis-parallel milling contours per XNC face and tool using a greedy nearest-neighbour walk,
reversing passes as needed to form a serpentine path. Arcs, multi-segment contours, pockets,
and `<mr>` rectangles remain in place and are counted as ignored. Nothing reordered ⇒ returns
`false`, saves nothing. Output is `_mo.project` with collision numbering and an audit description.

**Prep for split along X** — two variants (same core algorithm): (1) hardcoded text label `"_поріз.2х40мм"` (button 1), (2) user-selected label from config (button 2). Algorithm: double width by `2× + SawWidth`, halve count rounding up, double/mirror the drill program's bores. Saw-kerf width is now configurable via `ConfigService.SawWidth` (injected; can be changed at runtime, default 4.0).

**Export parts list** — builds tab- or semicolon-separated list (length, width, count, banding external symbols, name) with blank separator row after parts on sheets named `"Сращ.(2)"` (spliced/joined marker). Outputs to CSV file or clipboard.

**Bands external symbols** — for display/export, each distinct band material's XML `elSymbol` is mapped to A–Z "external symbol" (ordered by thickness) via `ReadBands()`.

# Known gaps, dead code, and risks

**Fixed since last update:**
- ✅ Orphaned `XmlOperator` project removed.
- ✅ `AutoMapper` and `InitializeAutoMapper()` removed (unused).
- ✅ Test scaffold placeholder replaced with real `AppViewModelTests`, `GibLabProjectServiceTests`, etc.

**Still present:**
- **`DecimalValidationRule`** is wired but inactive — the XAML binding for filter length/width is commented out (lines 248, 264 in MainWindow.xaml). The filters currently accept free text, validated only by lenient `TryParseToDecimal()` (unparsable input silently means "no filter"). If validation is desired, uncomment the bindings.
- **Unused extension + interface method**: `GetOperationMaterialIdIntValue` (extension) and `IConfigService.UpdateSawWidth()` (interface + implementation) are defined but have no callers. There is no UI control to change the saw-width at runtime; it can only be changed by hand-editing the JSON config file. These may be stubs for a future "settings" dialog.
- **Bug in `CheckBendsAreIdentical`** (GibLabProjectService.cs:329–332): each clause is shaped `(part1.GetXxxMat() != null && part1.GetXxxMatValue() == part2.GetXxxMatValue() || true)`. Due to `&&`/`||` precedence, the `|| true` term makes every clause always true, so banding materials are never actually compared. This makes `GroupIdenticalElements` less discriminating than intended and can group parts with different banding.
- **Incomplete test coverage**: real tests exist for AppViewModel, GibLabProjectService, ConfigService, and XncProgram reading/evaluation. However, UpdatePart edge cases, CSV export formatting, and some PrepForSplitAlongX branches have minimal or no coverage.

**New observations:**
- `IConfigService.UpdateSawWidth()` is now an interface method (was item 45 of previous list), but remains unreachable from the UI (no settings dialog wired to it).
- XNC program reading and replacement are now fully implemented and tested (`ReplaceXncPrograms`, `ReadXncPrograms`, `GetXncProgramsCount`).

# For developers / contributors

See [[project-file-processing-skill]] (`.agents/skills/project-file-processing-skill/`, especially `references/project-file-schema.md` and `references/xnc-program-format.md`) for a comprehensive reference on the GibLab `.project` XML format, especially:
- How XNC machining programs are encoded as escaped XML inside the `program` attribute of `<operation typeId="XNC">` elements
- The coordinate frame, symbol table, expression evaluation, and all element types (tools, bores, groovings, milling contours, rectangles)
- A worked example from the test fixture

**Adding features:**
- All business logic is in `GibLabProjectService`, which implements `IProjectService`. Add new methods to the interface first, then implement.
- Tests must not be modal or disk-dependent — use `FakeProjectService` and NSubstitute mocks; inject `TimeProvider` for deterministic timestamps.
- UI commands are methods in `AppViewModel` decorated with `[RelayCommand]` (from `CommunityToolkit.Mvvm`). Bind them in XAML with `Command="{Binding CommandName}"`.

**Refactoring / cleanup priorities:**
1. Fix the `CheckBendsAreIdentical` logic (and add a test to verify it actually distinguishes different banding).
2. Uncomment `DecimalValidationRule` XAML bindings or remove the class.
3. Wire `UpdateSawWidth()` to a UI control (settings panel), or delete the method and interface member.
4. Add CSV export and UpdatePart edge-case tests to improve coverage.
