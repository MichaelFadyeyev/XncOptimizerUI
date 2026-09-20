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

- **`XncOptimizerUI.Contracts.IProjectService`** — abstraction for all XML manipulation and file I/O (`OpenProject`, `CloseProject`, `SaveProject`, `GroupIdenticalElements`, `PrepForSplitAlongX`, `UpdatePart`, `ReadParts`/`ReadBands`/`ReadSheets`, `ReadXncPrograms`, `GetXncProgramsCount`, `GetPartsWithXncTurnDiscordance`, `ReplaceXncPrograms`, `ConvertGroovesAndMills`, `ConvertBoresAndMills`, `OptimizeMillTraversal`, `FullPath`), implemented by `GibLabProjectService`.
- **`Services/GibLabProjectService.cs`** — contains the XML/document logic and XNC program reading (`ReadXncPrograms`), XNC program copying (`ReplaceXncPrograms`), XNC operation counting (`GetXncProgramsCount`), groove ⇄ mill conversion (`ConvertGroovesAndMills`), and mill traversal optimization (`OptimizeMillTraversal`). Uses `Services/Xnc/XncProgramReader.cs` + `XncExpressionEvaluator.cs` + `XncSymbolTable.cs` for program parsing (see [[project-file-processing-skill]]).
- **`IConfigService`**, **`IDialogService`** — injected abstractions (formerly static/modal classes), replacing the old `ConfigService` static methods and direct `MessageBox`/`SaveFileDialog` calls with testable dependencies.
- **`Extensions/XContainersExtensions.cs`** — null-safe getters/setters for XML attributes: typed accessors (`GetLengthDecimalValue`, `GetIdIntValue`, `GetElbIdIntValue` for `@operation#<id>` parsing, `GetProgramValue`, `GetSideValue`, etc.), and mutation methods (`SetLengthValue`, `SetWidthValue`, ...) to support in-place editing.

## Domain models & presentation

- **Models** (`MVVM/Models/Part.cs`, `Band.cs`, `Sheet.cs`, `Product.cs`) — plain data classes (`Id`, `Name`, `Length`, `Width`, `Count`, sheet/banding relationships). `Product` (`Id`, `Name`, `Code`, `Count`) wraps a `<good typeId="product">` good.
- **`MVVM/Models/Xnc/`** — read-only classes representing parsed XNC programs: `XncProgram` (top-level; `Dx`/`Dy`/`Dz`, `Side`, `Turn` (0..3 orientation code) + `TurnDegrees` (×90 clockwise)), `XncTool`, `XncBore`+`BoreSurface`, `XncGrooving`, `XncMillingContour`+`XncMillingSegment` (`XncLineSegment`, `XncArcSegment`), `XncMillingRectangle`, `ToolPosition`, `XncPoint`. Format documented in [[project-file-processing-skill]].
- **ViewModels** (`PartVM`, `BandVM`, `SheetVM`, `ProductVM`) — wrap domain models for data-binding; `AppViewModel` orchestrates the UI. `ProductVM` (`Id`, `Name`, `Code`, `Count`) wraps `Product` and is purely a read-only display projection — no command reads or writes it.
- **`AppViewModel.cs`** — file lifecycle (Open/Save/Close), filterable `Parts`/`Bands`/`Sheets`/`Products` grids, part selection + edit-on-selection-change that auto-saves, XNC program display (`SelectedPartPrograms`), CSV/clipboard export, label management.

## Configuration & testing

- **`Services/ConfigService.cs`** + **`Configuration/AppOptions.cs`** — persists user settings as JSON at `%AppData%\XncOptimizerUI\configuration.json`: `SawWidth` (kerf width, default 4.0), `LabelsToProcess` (text labels for filtering, default `["поріз.2х40"]`), `MillingToolDiams` (default `[6, 10, 20]`), and the remembered last-selected label. `GroovingToolWidths` is present in `AppOptions` but currently unused by conversion.
- **`App.xaml.cs`** — DI composition root using `Microsoft.Extensions.DependencyInjection`; configures `IProjectService` → `GibLabProjectService`, `IConfigService` → `ConfigService`, `IDialogService` → `DialogService`, injects `TimeProvider`.
- **`XncOptimizerUI.Test`** — NUnit test project with `AppViewModelTests`, `GibLabProjectServiceTests`, `ConfigServiceTests`, `XncProgramReaderTests`, `XncExpressionEvaluatorTests`, `Fakes/FakeProjectService.cs`. Headless (no `MessageBox`, `SaveFileDialog`, or static config singletons).
- **`MainWindow.xaml`** — left column: command buttons + label management. The rest of the window is a `TabControl` with two tabs, plus a log box (bottom row) spanning both.
  - **"Parts" tab**: filterable Parts `DataGrid` (row 1) over a resizable part-preview row (row 2: a scrollable `PartCanvas` viewport with a fixed axis/turn overlay, plus a "Programs" column on its right holding read-only Bores/Grooves `DataGrid`s over a `SelectedPartPrograms` `TextBox`). The Parts grid and preview row are separated by a horizontal `GridSplitter`; the preview and the Programs column are separated by a vertical `GridSplitter`. Both preview scrollbars are always visible. The part preview can be zoomed with the mouse wheel from the fit-to-view scale upward, preserving the point under the cursor, and a pressed mouse wheel pans magnified previews; splitter resizing recalculates the fit scale while preserving the relative part position, and selecting another part resets zoom and scroll position to fit-to-view. The coordinate axis and turn angle remain anchored to the preview border viewport regardless of zoom or scroll. Part-preview colours / line widths / metrics are `Window.Resources` keys (`PartFaceBrush`, `PartOutlineBrush`, `PartOutlineThickness`, `MinEdgeProjectionThickness`, `ProjectionGapMm` — the 10 mm face-to-side-view gap, scaled with the part, `PartPreviewMargin`, `AxisXBrush`, `AxisYBrush`, plus `BoreSideTrueBrush`/`BoreSideFalseBrush`/`BoreSelectedBrush`/`BoreOutlineThickness`/`BoreCenterLineThickness`/`BoreCenterLineOvershootMm` for the bore overlay). Canvas drawing lives in `MainWindow.xaml.cs` (`RenderPart`), which delegates bore drawing to `MVVM/Views/PartPreview/BorePreviewRenderer.cs`; re-run on `SelectedPart` change and canvas resize. See `.agents/todos.md` for deferred hooks (click hit-testing via shape `Tag` on face/side rectangles, grooving/milling/pocket overlay, banding visualisation).
  - **"Products & Materials" tab**: three stacked read-only `DataGrid`s — Products (Name/Code/Count), Sheets (Code/Name), Bands (Code/Name/Int. Sign/Ext. Sign, with a `SelectedBand` binding that has no further effect wired to it). No add/edit/delete; this tab is a reference view of the goods already parsed for the "Parts" tab.
  - Icon `xnc_logo.ico`.

# User-facing operations

**File lifecycle** — Open/Save/Close project XML file. Opening populates Bands, Sheets, Products, Parts grids from the parsed file and pre-selects the first part. On open only, `AppViewModel.WarnOnXncTurnDiscordance` calls `IProjectService.GetPartsWithXncTurnDiscordance()` — any part machined by several XNC operations that disagree on `turn` pops a warning ("Discordance in xnc programs turn corner for parts: …") and each offending part name is logged. Closing clears all state. Window title shows assembly name + current filename.

**Part preview** — the half-height row under the Parts grid draws the selected part in a `Canvas` viewport: a plain rectangle plus the four side views of the part. The rectangle is sized from the **first applied XNC program's `dx`/`dy`** (already in the turned machine frame) when the part has any program, otherwise from the part's own `Length x Width`; `AppViewModel` exposes these as `SelectedPartDisplayLength`/`Width`. Each side view is a band whose thickness is the owning sheet's `t` (looked up from `AppViewModel.Sheets` by `PartVM.SheetId`) and whose length is the part's size in that direction, offset from the face by a 10 mm gap; band thickness, gap and face are all drawn at one uniform scale that fits face + gap + band per side into the viewport (rescaled per part). A small X/Y axis glyph sits top-left and is **not** rotated by `turn` (screen axes stay X-horizontal / Y-vertical); the first program's turn is always shown as degrees (`0°`/`90°`/`180°`/`270°`, from `SelectedPartTurnText`; `0°` when the part has no XNC program) in the bottom-right corner at 2.5x the base font size. Redrawn whenever `SelectedPart` changes or the canvas resizes.

Bores from every applied `XncProgram` (`AppViewModel.SelectedXncPrograms`) are drawn on top of the geometry by `BorePreviewRenderer.DrawBores`: a face bore (`XncBore.Surface == Face`) gets a circle + crossed center lines on the Face rectangle plus a rectangle + center line on each of the four side bands, colored by the owning program's `Side`; an edge bore (`Top`/`Bottom`/`Left`/`Right`) gets a circle + crossed center lines on the one band it was drilled from (positioned from `bore.Z`) plus a rectangle + center line on the Face rectangle. All five projections of one bore toggle red together on click (selection is local to the current render pass, not yet wired into `AppViewModel`). Milling contours, groovings, and pockets are **not** rendered on the canvas — grooves are shown only in the "Grooves" `DataGrid` (below) — nor is banding; see `.agents/todos.md` for what's still deferred.

**Parts grid + filter + edit** — lists every part (checkbox, number, length/width/count, edge-banding materials resolved to human-readable names via Bands lookup, name). A "Checked no:" / "Filtered no:" pair above the grid shows how many parts are checked across the whole project (the set batch commands like Convert grooves/mills act on) versus how many pass the current filter; "Check all" / "Uncheck all" buttons act on the currently filtered set. Filtering is a **range** filter: `LengthMin`/`LengthMax`/`WidthMin`/`WidthMax` (each validated as a decimal by an active `DecimalValidationRule`, inclusive bounds, an empty bound is unbounded) plus `FilterName` (substring match on the part name, no validation). Typing a min above the current max (or vice versa) snaps the *other* edge to match, debounced so it reacts to the finished value rather than each keystroke (`AppViewModel.NormalizeRangeBounds`). "Filter"/"Clear" buttons apply/reset explicitly, but every field also re-filters live as it changes. On-grid editing of Name/Length/Width persists via `UpdatePart` (which sync's the part name with its XNC operation's group-code prefix `[01]` if present) and auto-saves.

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

The selected-part panel uses three equal-width tabs: "Operations" exposes read-only
`DataGrid`s for bores, grooves, milling contours, elliptical mills, and rectangular
mills/pockets; "Tools" exposes the tools table; and "XNC list" exposes the text summary.
`SelectedPartGrooves` is flattened and numbered in XNC program/document order through
`GrooveRowVM`. Groove rows display start/end coordinates, depth, width, and the
`ToolPosition` center-line relation. Their side is derived from groove `p`: `0` maps to
Front/Back from the XNC operation `side`, while `1`/`2`/`3`/`4` map to Right/Left/Top/Bottom.
The groove grid uses extended full-row selection. `XncGrooving.SideCode` preserves the parsed
`p` value (missing `p` defaults to `0`).

**Replace XNC programs** — source part's drill programs (one per face: front/back) are validated, then copied to selected target parts. Before copying, all targets are checked for: (1) identical dimensions & banding, (2) same number of XNC faces, and (3) matching face/turn orientations. If validation passes, the `program` attribute and `countBore` metadata are overwritten, and the file is saved with the `_ren.project` suffix (or a numbered collision suffix) with an audit description. Implemented in `GibLabProjectService.ReplaceXncPrograms(ref string log, Part sourcePart, IList<Part> targetParts)` with detailed validation and error logging.

**Convert grooves ⇄ mills** — for the parts **checked** in the Parts grid (the
`PartVM.IsSelected` column), rewrites every XNC program in the chosen direction. The service
also accepts a `processPockets` flag. *Grooves → Mills*: an axis-parallel primary-pass `<gr>`
(`p="0"` or absent) with an exact configured milling-tool diameter becomes a single-segment
`<ms>` + `<ml>` contour. An off-size groove becomes a rectangular pocket using the smallest
configured cutter when that cutter fits; otherwise it is ignored. Edge-reaching contour
endpoints overshoot by half the relevant cutter diameter. Secondary-pass and diagonal grooves
remain untouched. Generated milling tools use the conversion-specific `Mill<diameter>` name.
An existing tool with the same diameter but an unrelated name (for example `Bore8`) is not
reused; a separate `Mill8` declaration is created. Existing matching `Mill...` declarations
are reused. *Mills → Grooves*: a shallow, axis-parallel, single-straight-segment
non-pocket contour is converted and its outside endpoints are clamped to the outline. With
`processPockets`, supported axis-parallel rectangular `<mr>` pockets and rectangular contour
pockets are converted too. Generated grooving tools use `Cut<diameter>` (currently `Cut2.8`);
unrelated same-diameter tools are preserved and not reused. The grooving cutter is hard-coded to 2.8 mm;
`AppOptions.GroovingToolWidths` is not wired into this operation. Non-compliant elements are
left untouched and tallied as ignored; logs include converted/ignored and tools
added/removed counts. Nothing converted ⇒ returns `false`, saves nothing. Output is
`_gm.project` with collision numbering and an audit description.
`GibLabProjectService.ConvertGroovesAndMills(ref string log, IList<Part> parts, GrooveMillDirection direction, bool processPockets)`.

**Convert bores ⇄ mills** — for the parts **checked** in the Parts grid, rewrites every XNC
program in the chosen direction. *Bores → Mills*: each face bore (`<bf>`) whose tool diameter
exceeds 35 mm is milled out with a fixed 6 mm cutter (`Mill6`, created if absent) — by default
a closed two-arc contour (`<ms>` entry at `(cx+r, cy)` + two `<mac>` half circles about the
bore centre), or a single elliptical mill (`<me>` with `l = w = radius`) when the "as ellipses"
checkbox is set. Traversal is clockwise (`<ms>`/`<me>` `fwd="true"`, `<mac> dir="true"`). A through
bore keeps the right-of-centre-line position (`c="1"`); a blind bore is milled as a pocket
(`c="3"`) — in both the contour and the ellipse form. The mill depth equals the bore depth.
Smaller bores and edge bores are left untouched. *Mills → Bores*: a round mill — an `<ms>` entry plus ≥ 2 arc
segments (`<mac>` or radius-defined `<ma>`) that share one centre and radius and close onto the
entry point, or an `l == w` `<me>` ellipse — with diameter > 35 mm becomes a face bore, cut
with a `Bore<diameter>` tool (created if absent, e.g. `Bore40`). Orphaned tools are dropped.
Nothing converted ⇒ returns `false`, saves nothing. Output is `_bm.project` with collision
numbering and an audit description.
`GibLabProjectService.ConvertBoresAndMills(ref string log, IList<Part> parts, BoreMillDirection direction, bool useEllipse)`.

**Group identical elements** — (originally "Optimize") clusters XNC operations by: program content + edge-band materials. Groups are renumbered, consolidated into one product good, saved as `_opt.project`. Guards against re-running on already-optimized files or files with no XNC operations (logs warning instead of silently no-op'ing).

**Optimize mill traversal** — for checked parts, re-sequences eligible one-segment,
axis-parallel milling contours per XNC face and tool using a greedy nearest-neighbour walk,
reversing passes as needed to form a serpentine path. Arcs, multi-segment contours, pockets,
and `<mr>` rectangles remain in place and are counted as ignored. Nothing reordered ⇒ returns
`false`, saves nothing. Output is `_mo.project` with collision numbering and an audit description.

**Prep for split along X** — two variants (same core algorithm): (1) hardcoded text label `"_поріз.2х40мм"` (button 1), (2) user-selected label from config (button 2). Algorithm: double width by `2× + SawWidth`, halve count rounding up, double/mirror the drill program's bores. Saw-kerf width is now configurable via `ConfigService.SawWidth` (injected; can be changed at runtime, default 4.0).

**Export parts list** — builds tab- or semicolon-separated list (length, width, count, banding external symbols, name) with blank separator row after parts on sheets named `"Сращ.(2)"` (spliced/joined marker). Outputs to CSV file or clipboard.

**Bands external symbols** — for display/export, each distinct band material's XML `elSymbol` is mapped to A–Z "external symbol" (ordered by thickness) via `ReadBands()`.

**Products & Materials tab** — a second top-level tab, separate from the Parts/preview tab, showing three read-only reference grids populated on open by `AppViewModel.ReadItems()`: Products (`<good typeId="product">` goods, via `ReadProducts()`/`ProductVM`: Name/Code/Count), Sheets (Code/Name), and Bands (Code/Name/internal+external symbol). Nothing here is editable or exported from directly — it exists to let the user inspect what goods a file declares alongside the Parts grid.

# Known gaps, dead code, and risks

## Selected-part machining tabs

The selected-part Programs panel uses three equal-width tabs: "Operations" contains
read-only tables for bores, grooves, milling contours, elliptical mills, and rectangular
mills/pockets; "Tools" contains the tools table; and "XNC list" contains the human-readable
`SelectedPartPrograms` summary. `XncProgram` and
`XncProgramReader` expose elliptical milling (`<me>`) alongside the other parsed machining
primitives, and `AppViewModel` rebuilds all table rows whenever the selected part changes.

**Fixed since last update:**
- ✅ Orphaned `XmlOperator` project removed.
- ✅ `AutoMapper` and `InitializeAutoMapper()` removed (unused).
- ✅ Test scaffold placeholder replaced with real `AppViewModelTests`, `GibLabProjectServiceTests`, etc.
- ✅ `DecimalValidationRule` is now active, wired to the Parts grid's `LengthMin`/`LengthMax`/`WidthMin`/`WidthMax` range-filter bindings (see the Parts grid bullet above); the filter model itself changed from exact-length/width to an inclusive min/max range.
- ✅ `GibLabProjectService.SeedProgramSymbols` (shared by `ConvertBoresAndMills`, `ConvertGroovesAndMills`, `OptimizeMillTraversal`) only seeded `dx`/`dy`/`dz`; any element whose `dp`/`x`/`y` referenced a custom `<var>` (e.g. `dp="throughBoreDepth"`) crashed conversion with an "unknown identifier" error, even though `XncProgramReader` already resolved the same `<var>` correctly for read-only display. Now registers every `<var>` up front (document order, so a var may reference an earlier one), matching the reader's behavior. Covered by `GibLabProjectServiceTests.ConvertBoresAndMills_BoresToMills_CustomVariableAsBoreDepth` / `TestData/td-bore-depth-variable.project`.
- ✅ **Re-opening a file after `ExecuteOptimizeCommand` silently renamed its first part.** `AppViewModel.OnSelectedPartChanging` auto-saves the *previously* selected part whenever `SelectedPart` changes, by looking it up in whatever document `IProjectService` currently has open. `OpenFile()` called `LoadProject(fullPath, true)` — which switches `IProjectService` to the newly chosen file via `OpenProject` — *before* releasing the old selection; the eventual `SelectedPart = null` inside `ReadItems()` then fired the auto-save against the *new* document using the *stale* `PartVM` from the previous one. After running "Execute Optimize" (which renames grouped parts with a `[groupCode]` prefix in the new `_opt.project` result and re-selects that renamed part), re-opening the original source file replayed that stale renamed name into the freshly-opened document and saved it — corrupting the source on disk, even though `GroupIdenticalElements` itself never writes to the original path. Fixed by releasing `SelectedPart` in `OpenFile()` *before* `LoadProject` switches documents (mirroring the pattern `CloseFile()` already used). Covered by `AppViewModelTests.OpenFile_AfterExecuteOptimize_ReopeningSourceLeavesFirstPartNameUnchanged` and `GibLabProjectServiceTests.GroupIdenticalElements_DoesNotTouchSourceFileOnDisk` / `TestData/td-execute-optimize.project`.

**Still present:**
- **Unused extension + interface method**: `GetOperationMaterialIdIntValue` (extension) and `IConfigService.UpdateSawWidth()` (interface + implementation) are defined but have no callers. There is no UI control to change the saw-width at runtime; it can only be changed by hand-editing the JSON config file. These may be stubs for a future "settings" dialog.
- **Bug in `CheckBendsAreIdentical`** (GibLabProjectService.cs:330–333): each clause is shaped `(part1.GetXxxMat() != null && part1.GetXxxMatValue() == part2.GetXxxMatValue() || true)`. Due to `&&`/`||` precedence, the `|| true` term makes every clause always true, so banding materials are never actually compared. This makes `GroupIdenticalElements` less discriminating than intended and can group parts with different banding.
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
2. Wire `UpdateSawWidth()` to a UI control (settings panel), or delete the method and interface member.
3. Add CSV export and UpdatePart edge-case tests to improve coverage.
