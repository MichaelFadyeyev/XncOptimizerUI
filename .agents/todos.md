---
name: todos
description: Deferred work items / future hooks for XncOptimizerUI, captured during feature work.
---

# TODO / future hooks

## Part preview (MVVM/Views/MainWindow.xaml `PartCanvas`)

Iteration 1 draws only the part face rectangle + four edge-side projections, scaled
per selected part. Deferred:

- **Click sensitivity** — each face/band shape carries `.Tag`
  (`"Face" | "Top" | "Bottom" | "Left" | "Right"`). Add `MouseLeftButtonDown` on the
  shapes (or a single handler on `PartCanvas` using `e.OriginalSource`), read `.Tag`,
  and raise an event / bind a command on `AppViewModel` so a face/side can be selected
  from the drawing. (Bore shapes already do this themselves - see below.)
- **Extract to control** — rendering currently lives in `MainWindow.xaml.cs`
  (`RenderPart`). If it grows, move to a `PartView` `UserControl`, or go full MVVM:
  build an observable shape collection in the view model and bind it to an
  `ItemsControl` with a `Canvas` `ItemsPanel`.
- **Machining overlay** — all bores are drawn by `BorePreviewRenderer`
  (`MVVM/Views/PartPreview/BorePreviewRenderer.cs`). A face-drilled bore
  (`XncBore.Surface == Face`) gets a circle + crossed center lines on the Face
  rectangle, plus a rectangle + center line on each of the four edge bands, colored by
  the owning `XncProgram.Side` (all five projections toggle red together on click). An
  edge-drilled bore (`Top`/`Bottom`/`Left`/`Right`) is the mirror image: a circle +
  crossed center lines on the one band it was drilled from (perpendicular position from
  `bore.Z`, anchored to the band edge nearest the Face when `program.Side` is true, else
  the far/outer edge), plus a rectangle + center line on the Face rectangle, flush to
  that same edge and always growing inward toward the panel center regardless of
  `Side`; both shapes always use `BoreBrushes.SideTrue` regardless of the owning
  program's actual `Side`, and neither uses `ThroughFill` (both projections stay
  outline-only, unlike a face bore's front circle). Selection state is local to the
  current render pass, not yet wired into `AppViewModel`. Still deferred: groovings,
  milling contours/rectangles, and pockets, all sourced from the same
  `IProjectService.ReadXncPrograms(partId)` result (`AppViewModel.SelectedXncPrograms`).
  When they land, note the overlay coordinates must be rotated into the display frame
  by `XncProgram.Turn` (the face rectangle is already sized from the turned `dx`/`dy`,
  but per-feature `x`/`y` are still in each program's own frame) - bores are unaffected
  only because the sample data used so far has `turn="0"`.
- **Banding visualisation** — inner cream outline seen in `UiExamples/td-displaying-simple.png`
  represents edge-band material; render from `PartVM.TopBandingId` / `BottomBandingId` /
  `LeftBandingId` / `RightBandingId` once base drawing is stable.
