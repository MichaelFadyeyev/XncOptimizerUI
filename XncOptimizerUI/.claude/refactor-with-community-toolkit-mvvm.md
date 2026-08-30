# Refactor `AppViewModel.cs` with CommunityToolkit.Mvvm

## Context

`MVVM/ViewModels/AppViewModel.cs` is currently built on hand-rolled MVVM primitives in
`Core/ObservableObject.cs` (manual `INotifyPropertyChanged`) and `Core/RelayCommand.cs`
(a custom `ICommand`). Every property is a full get/set with an explicit `OnPropertyChanged()`
call, and every command is a lazily-instantiated `RelayCommand` backing field with a
`??=` getter. This is verbose (~670 lines) and error-prone — e.g. `ExecutePrepForSplitAlongXCommand`
uses `??` instead of `??=` (allocating a new command on every access), and there are two dead
command fields (`_executePrepForSplitAlongX`, `_executePrepForSplitAlongX2`).

Migrating to **CommunityToolkit.Mvvm** replaces the boilerplate with source-generated
`[ObservableProperty]` properties and `[RelayCommand]` methods, cutting the file substantially
while keeping all existing XAML bindings working unchanged.

**Scope:** `AppViewModel.cs` only (per request). Child VMs (`PartVM`, `BandVM`, `SheetVM`)
keep using `Core.ObservableObject` and are out of scope.

## Constraints / things that must not break

- All XAML command bindings must keep their exact names: `OpenFileCommand`, `SaveFileCommand`,
  `ExecuteOptimizeCommand`, `ExecutePrepForSplitAlongXCommand`, `ExportPartsListCommand`,
  `CopyPartsListCommand`, `SetSourcePartCommand`, `ResetSourcePartCommand`, `ReplaceXNCsCommand`,
  `AddNewLabelCommand`, `DeleteLabelCommand`, `CloseFileCommand`, `ClearFiltersCommand`,
  `ApplyFilterCommand`. (`[RelayCommand]` on method `Foo` generates property `FooCommand`.)
- All bound property names must stay identical: `Log`, `FullPath`, `FilterName`, `FilterLength`,
  `FilterWidth`, `NewLabelToProcess`, `SelectedLabel`, `WindowTitle`, `Parts`, `Bands`, `Sheets`,
  `LabelsToProcess`, `SelectedPart`, `SelectedBand`, `SourcePart`, `SourcePartInfo`.
- The parameterless constructor must remain (used by `d:DataContext` design instance and
  `<viewmodels:AppViewModel />` in `MainWindow.xaml`).
- No command currently defines `CanExecute`, so buttons never disable — behavior must stay the same.

## Steps

### 1. Add the package
Add to `XncOptimizerUI.csproj`:
```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
```
(Latest stable 8.x; `dotnet add package CommunityToolkit.Mvvm` to pin the current version.)

### 2. Class declaration & usings
- Make the class `partial`: `public partial class AppViewModel : ObservableObject`.
- Add `using CommunityToolkit.Mvvm.ComponentModel;` and `using CommunityToolkit.Mvvm.Input;`.
- **Remove `using XncOptimizerUI.Core;`** — it pulls in `Core.ObservableObject` and
  `Core.RelayCommand`, which collide by name with the CommunityToolkit types. AppViewModel uses
  nothing else from `Core`. The base `ObservableObject` now resolves to the CommunityToolkit one.

### 3. Convert properties

**Decision:** use **partial properties** (C# 13 / .NET 9 style) rather than field-based
`[ObservableProperty]`. Each becomes `[ObservableProperty] public partial T Name { get; set; }`
and the corresponding private backing field is removed. Requires the class to be `partial`
(step 2) and `LangVersion` 13 (default for `net9.0`).

**Simple → `[ObservableProperty] public partial …`** (remove the old backing field, keep initializers
by moving them into the constructor or a field initializer where needed):
- `Log` (init `string.Empty`), `NewLabelToProcess` (`string.Empty`),
  `WindowTitle` (init `_assembly + " - No file selected"`),
  `Parts`, `Bands`, `Sheets` (init `[]`),
  `LabelsToProcess` (init `[.. ConfigService.LabelsToProcess]`),
  `SelectedBand`.
  > Partial properties can't have inline `= …` initializers, so any non-default initial value
  > (e.g. `WindowTitle`, `LabelsToProcess`) must be assigned in the constructor.

**With side-effects → `[ObservableProperty] public partial …` + generated partial hook methods:**
- `FullPath`: use `partial void OnFullPathChanged(string value)` to recompute `WindowTitle`
  (replaces the nested `SetWindowTitle` local function).
- `SelectedLabel`: `partial void OnSelectedLabelChanged(string value)` calls
  `ConfigService.UpdateLastLabelToProcessSelectedIndex(value)`.
- `SelectedPart`: `partial void OnSelectedPartChanging(PartVM? value)` (or `OnSelectedPartChanged`)
  runs the "save previous part" logic currently in the setter (`_projectService.UpdatePart` +
  `SaveProject` + log). Note the current setter runs this on the *old* value before assigning —
  use the `Changing` hook or capture the old field value to preserve exact semantics.
- `SourcePart`: annotate with `[NotifyPropertyChangedFor(nameof(SourcePartInfo))]` and add
  `partial void OnSourcePartChanged(PartVM? value)` to set
  `_sourceXncCount = value == null ? 0 : _projectService.GetXncProgramsCount(value.Id);`.

**Keep as manual properties (backing type differs / guard logic):**
- `FilterName`, `FilterLength`, `FilterWidth` — these expose `string` but back onto `_filterName`
  (string) and `_filterLength`/`_filterWidth` (`decimal?` via `TryParseToDecimal`), and carry the
  `_applyPartsFilter` guard that calls `FilterParts()`. `[ObservableProperty]` can't model the
  type conversion cleanly, so leave these as hand-written properties calling `OnPropertyChanged()`
  (which is still provided by the CommunityToolkit base class — signature-compatible).

**Read-only computed:**
- `SourcePartInfo` stays a plain get-only property; change notifications come from the
  `[NotifyPropertyChangedFor]` on `SourcePart`.

**Plain fields (unchanged, not observable):** `_assembly`, `_allParts`, `_sourceXncCount`,
`_projectService`, `_applyPartsFilter`.

### 4. Convert commands → `[RelayCommand]` methods
Delete all `RelayCommand? _openFile;` etc. backing fields. Turn each command getter's lambda body
into a private/void method annotated `[RelayCommand]`, named so the generated `…Command` property
matches the binding:

| Generated property | Method name | Notes |
|---|---|---|
| `OpenFileCommand` | `OpenFile()` | **Collision:** a private helper `OpenFile(string, bool)` already exists. Rename the helper (e.g. `LoadProject(string, bool)`) and update its 3 call sites. |
| `SaveFileCommand` | `SaveFile()` | |
| `ExecuteOptimizeCommand` | `ExecuteOptimize()` | |
| `ExecutePrepForSplitAlongXCommand` | `ExecutePrepForSplitAlongX()` | fixes the `??` bug + drops 2 dead fields |
| `ExportPartsListCommand` | `ExportPartsList()` | |
| `CopyPartsListCommand` | `CopyPartsList()` | |
| `SetSourcePartCommand` | `SetSourcePart()` | |
| `ResetSourcePartCommand` | `ResetSourcePart()` | |
| `ReplaceXNCsCommand` | `ReplaceXNCs()` | |
| `AddNewLabelCommand` | `AddNewLabel()` | |
| `DeleteLabelCommand` | `DeleteLabel()` | |
| `CloseFileCommand` | `CloseFile()` | |
| `ClearFiltersCommand` | `ClearFilters()` | |
| `ApplyFilterCommand` | `ApplyFilter()` | |

Command methods take no parameter (drop the unused `obj` lambda arg). Method bodies are copied
verbatim from the current lambdas, except the internal `OpenFile(...)` helper calls become the
renamed helper.

> Behavioral note: the old `Core.RelayCommand` hooked `CommandManager.RequerySuggested` for
> `CanExecuteChanged`; CommunityToolkit's does not. Since no command uses `CanExecute`, there is
> no observable difference.

### 5. Helper methods (unchanged)
`LoadProject` (renamed from `OpenFile`), `ReadItems`, `FilterParts`, `GetPartsList`,
`GetBandingExternalSymbol`, `TryParseToDecimal` stay as-is.

### 6. Cleanup
Delete `Core/RelayCommand.cs` — after this refactor it has zero references (confirmed: only
`AppViewModel.cs` used it). `Core/ObservableObject.cs` stays — `PartVM`/`BandVM`/`SheetVM` still use it.

## Files touched
- `XncOptimizerUI.csproj` — add package reference.
- `MVVM/ViewModels/AppViewModel.cs` — the refactor.
- Delete `Core/RelayCommand.cs`.
- No XAML changes required.

## Verification
1. `dotnet build XncOptimizerUI.csproj` — must compile clean (source generators run at build;
   watch for CS0102/CS0111 duplicate-member errors that would signal a name collision with a
   generated member).
2. Run the app (`dotnet run` / F5). Smoke-test each button maps to its command:
   - Open file → parts/bands/sheets grids populate, window title shows the file name.
   - Filter Length/Width/Name + Filter/Clear buttons behave (filtered count updates).
   - Set/Reset source part → `Source:` text and tooltip update (`SourcePartInfo`).
   - Add/Delete label → combo box updates and persists via `ConfigService`.
   - Selecting a different part triggers the auto-save log line ("Updates saved: …").
   - Optimize / Prep For Split Along / Replace XNCs / Copy parts / Save / Close all run without error.
3. Confirm no XAML binding warnings in the Output window at runtime (would indicate a renamed
   property/command).
