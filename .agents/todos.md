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
  current render pass, not yet wired into `AppViewModel`.
  Groovings are drawn the same way by `GroovePreviewRenderer`
  (`MVVM/Views/PartPreview/GroovePreviewRenderer.cs`), sharing the band/Face rectangle
  placement math with `BorePreviewRenderer` via `PartPreviewOverlayGeometry`. Unlike a
  bore (which anchors near/far by the owning program's `Side`), every groove
  perpendicular offset always measures from the target band's inner (Face-adjacent)
  edge, regardless of `Side` - confirmed with the user, so every
  `AddSideRectangleRange`/`Z`-anchor call for a groove passes a literal `true`, not
  `program.Side`. A front-plane groove (`XncGrooving.SideCode == 0`) gets a Face
  rectangle (length = distance between its start/end points, width = `t`, offset from
  the start-end line per `c` - `Center` straddles it, `Right`/`Left` put the full width
  to the physical right/left of the start-to-end travel direction), colored by
  `program.Side` like a face bore. Its four edge-band projections are orientation-aware:
  for a horizontal (X-oriented) groove, Top/Bottom get a plain `dp`-deep extent
  projection (its own X span) with **no** center line, while Left/Right get a small
  `t`-wide/`dp`-deep rectangle anchored at the groove's constant Y **with** a center
  line; a vertical (Y-oriented) groove is the mirror image. A diagonal groove (untested,
  no fixture exercises it, matching how the groove/mill conversion pipeline also only
  really supports axis-parallel grooves) falls back to the extent projection + center
  line on all four bands. An edge-plane groove (`SideCode` `1`/`2`/`3`/`4` =
  Left/Right/Top/Bottom) gets three things: (1) its true rectangle on the one band it
  belongs to, offset from the band's inner edge by its own `Z` attribute (the
  `<gr z="...">` through-thickness position - *not* the constant `Start`/`End`
  coordinate on the non-length axis, which is just the panel-edge value, e.g. `x1=x2=dx`
  for a right-edge groove); (2) a `dp` rectangle (no center line) on the Face rectangle,
  flush to that edge and growing inward; and (3) a small `t`-wide/`dp`-long marker (with
  a center line) on **each** of the two bands perpendicular to its own plane (Top+Bottom
  for a Left/Right-plane groove, Left+Right for a Top/Bottom-plane groove) - flush at the
  groove's own fixed edge coordinate and growing inward along its intrinsic depth axis
  (X for Left/Right-plane, Y for Top/Bottom-plane), and, perpendicular to that, displaced
  from *that target band's own* inner edge by the groove's own `Z` (the same
  `nearEdge + direction * Z` formula as (1), just evaluated per target band via
  `GetBandEdges`, and drawn with the same `AddOffsetRectangle` helper as (1) so `c`
  positions its `t` width the same way) - all three always use `BoreBrushes.SideTrue`
  like an edge bore. All four of one edge-plane groove's shapes
  (or five of a front-plane groove's) toggle red together on click, same as a bore.
  Verified against `TestData/td-grooving-preview.project` (front plane at both
  `side="true"`/`"false"`, plus one part per Right/Left/Top/Bottom plane). Still
  deferred: milling
  contours/rectangles and pockets, sourced from the same
  `IProjectService.ReadXncPrograms(partId)` result (`AppViewModel.SelectedXncPrograms`).
  When they land, note the overlay coordinates must be rotated into the display frame
  by `XncProgram.Turn` (the face rectangle is already sized from the turned `dx`/`dy`,
  but per-feature `x`/`y` are still in each program's own frame) - bores and groovings
  are unaffected only because the sample data used so far has `turn="0"`.
- **Banding visualisation** — inner cream outline seen in `UiExamples/td-displaying-simple.png`
  represents edge-band material; render from `PartVM.TopBandingId` / `BottomBandingId` /
  `LeftBandingId` / `RightBandingId` once base drawing is stable.
