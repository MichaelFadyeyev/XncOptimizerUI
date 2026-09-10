---
name: todos
description: Deferred work items / future hooks for XncOptimizerUI, captured during feature work.
---

# TODO / future hooks

## Part preview (MVVM/Views/MainWindow.xaml `PartCanvas`)

Iteration 1 draws only the part face rectangle + four edge-side projections, scaled
per selected part. Deferred:

- **Click sensitivity** — each drawn shape carries `.Tag` (`"Face" | "Top" | "Bottom" |
  "Left" | "Right"`). Add `MouseLeftButtonDown` on the shapes (or a single handler on
  `PartCanvas` using `e.OriginalSource`), read `.Tag`, and raise an event / bind a
  command on `AppViewModel` so a face/side can be selected from the drawing.
- **Extract to control** — rendering currently lives in `MainWindow.xaml.cs`
  (`RenderPart`). If it grows, move to a `PartView` `UserControl`, or go full MVVM:
  build an observable shape collection in the view model and bind it to an
  `ItemsControl` with a `Canvas` `ItemsPanel`.
- **Machining overlay** — draw bores, mills, groovings, pockets as a separate shape
  layer on top of the face, sourced from `IProjectService.ReadXncPrograms(partId)`
  (`XncProgram` / `XncBore` / `XncGrooving` / `XncMillingContour` / `XncMillingRectangle`).
  Explicitly out of scope for iteration 1.
- **Banding visualisation** — inner cream outline seen in `UiExamples/td-displaying-simple.png`
  represents edge-band material; render from `PartVM.TopBandingId` / `BottomBandingId` /
  `LeftBandingId` / `RightBandingId` once base drawing is stable.
