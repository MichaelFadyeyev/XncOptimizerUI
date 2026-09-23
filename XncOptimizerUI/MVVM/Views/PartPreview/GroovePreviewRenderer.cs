using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws every groove on top of the part preview built by <c>MainWindow.RenderPart</c>,
    /// mirroring <see cref="BorePreviewRenderer"/>. A front-plane groove (<see cref="XncGrooving.SideCode"/>
    /// <c>0</c>) gets its true rectangle (length = distance between its start/end points, width =
    /// <c>t</c>, offset from its center line per <c>c</c>) on the Face rectangle, plus a
    /// depth (<c>dp</c>) rectangle projected onto each of the four Top/Bottom/Left/Right edge
    /// bands. An edge-plane groove (<c>1</c>/<c>2</c>/<c>3</c>/<c>4</c> = Left/Right/Top/Bottom)
    /// is the mirror image: its true rectangle sits on the one band it belongs to (offset from
    /// the band's inner edge by its own <c>Z</c>), a <c>dp</c> rectangle sits on the Face
    /// rectangle flush to that same edge and growing inward, and a small <c>t</c>-wide/<c>dp</c>-
    /// long marker sits on each of the two bands perpendicular to its own plane, flush at its
    /// fixed edge coordinate, plus a dashed mirror of its band rectangle on the band directly
    /// opposite its own. A front-plane groove's band projections start flush at each band's
    /// inner (Face-adjacent) edge when the owning program's <c>Side</c> is true, or the outer
    /// edge when false - like a bore. An edge-plane groove's own-band rectangle and perpendicular
    /// markers always measure from a band's inner edge, regardless of <c>Side</c>.
    /// </summary>
    internal static class GroovePreviewRenderer
    {
        /// <summary>Identifies the groove (and its owning program, for the <c>Side</c> colour) a shape belongs to.</summary>
        internal readonly record struct GrooveTag(XncProgram Program, XncGrooving Groove);

        /// <summary>
        /// Draws every groove in <paramref name="programs"/> onto <paramref name="canvas"/>. A
        /// front-plane groove's five projections (its Face rectangle + its four edge-band
        /// rectangles), or an edge-plane groove's five (its band rectangle + opposite-band mirror
        /// + Face rectangle + two cross-band markers), toggle red together on click, since they
        /// represent the same physical groove.
        /// </summary>
        public static void DrawGrooves(
            Canvas canvas,
            IEnumerable<XncProgram> programs,
            BorePreviewRenderer.FaceLayout layout,
            BorePreviewRenderer.BoreBrushes brushes)
        {
            foreach (var program in programs)
            {
                foreach (var groove in program.Groovings)
                {
                    if (groove.SideCode == 0)
                    {
                        DrawFrontPlaneGroove(canvas, program, groove, layout, brushes);
                    }
                    else
                    {
                        DrawEdgePlaneGroove(canvas, program, groove, layout, brushes);
                    }
                }
            }
        }

        /// <summary>Orientation tolerance (mm) for telling an axis-parallel groove's constant coordinate from noise.</summary>
        private const double OrientationEpsilon = 1e-6;

        /// <summary>Tolerance (mm) for deciding whether a groove's own extent actually reaches a part edge.</summary>
        private const double EdgeTouchEpsilonMm = 1e-3;

        /// <summary>
        /// True when a groove's true-rectangle low bound (<paramref name="loMm"/>, on some axis)
        /// reaches or crosses the part's near edge (0) on that axis, meaning the groove
        /// physically reaches - or slightly overruns - that edge rather than stopping short of it.
        /// </summary>
        private static bool ReachesNearEdge(double loMm) => loMm <= EdgeTouchEpsilonMm;

        /// <summary>
        /// True when a groove's true-rectangle high bound (<paramref name="hiMm"/>, on some axis)
        /// reaches or crosses the part's far edge (<paramref name="edgeMm"/> - the face width or
        /// height on that axis).
        /// </summary>
        private static bool ReachesFarEdge(double hiMm, double edgeMm) => hiMm >= edgeMm - EdgeTouchEpsilonMm;

        private static void DrawFrontPlaneGroove(
            Canvas canvas,
            XncProgram program,
            XncGrooving groove,
            BorePreviewRenderer.FaceLayout layout,
            BorePreviewRenderer.BoreBrushes brushes)
        {
            var normalBrush = program.Side ? brushes.SideTrue : brushes.SideFalse;
            var side = program.Side;
            var tag = new GrooveTag(program, groove);
            var widthPx = groove.Width * layout.Scale;
            var depthPx = groove.Depth * layout.Scale;
            var overshootPx = brushes.CenterLineOvershootMm * layout.Scale;

            var startPx = (X: layout.OriginX + (groove.Start.X * layout.Scale), Y: layout.OriginY + (groove.Start.Y * layout.Scale));
            var endPx = (X: layout.OriginX + (groove.End.X * layout.Scale), Y: layout.OriginY + (groove.End.Y * layout.Scale));

            var (clickTargets, allStroked, addSolidShape, addDashedShape, addCenterLine) = CreateShapeSink(canvas, tag, normalBrush, brushes.Thickness2Px, brushes.Thickness1Px, brushes.DashLengthPx);
            void NoCenterLine(double x1, double y1, double x2, double y2)
            {
            }

            // Face (true) rectangle: this is the groove's home plane - always solid, regardless
            // of orientation or edge-touching.
            AddOffsetRectangle(addSolidShape, addCenterLine, startPx, endPx, widthPx, groove.Position, overshootPx);

            var xLo = Math.Min(startPx.X, endPx.X);
            var xHi = Math.Max(startPx.X, endPx.X);
            var yLo = Math.Min(startPx.Y, endPx.Y);
            var yHi = Math.Max(startPx.Y, endPx.Y);

            // The groove's true (home-plane) rectangle, in mm - accounts for its width and
            // tool-to-centre-line position (c), not just its start/end run. A band is solid
            // whenever this rectangle actually reaches or crosses that band's edge (whether
            // because the groove's own run extends there, or because its width does), else it's
            // just a reference projection and stays dashed - one rule for every one of the four
            // bands, regardless of which pair happens to run parallel vs. perpendicular to the
            // groove.
            var trueBounds = TrueRectangleBoundsMm(groove.Start, groove.End, groove.Width, groove.Position);
            var faceWidthMm = layout.FaceWidth / layout.Scale;
            var faceHeightMm = layout.FaceHeight / layout.Scale;
            var topTouches = ReachesNearEdge(trueBounds.YLo);
            var bottomTouches = ReachesFarEdge(trueBounds.YHi, faceHeightMm);
            var leftTouches = ReachesNearEdge(trueBounds.XLo);
            var rightTouches = ReachesFarEdge(trueBounds.XHi, faceWidthMm);
            var topShape = topTouches ? addSolidShape : addDashedShape;
            var bottomShape = bottomTouches ? addSolidShape : addDashedShape;
            var leftShape = leftTouches ? addSolidShape : addDashedShape;
            var rightShape = rightTouches ? addSolidShape : addDashedShape;

            var isXOriented = Math.Abs(groove.Start.Y - groove.End.Y) < OrientationEpsilon;
            var isYOriented = Math.Abs(groove.Start.X - groove.End.X) < OrientationEpsilon;

            // A front-plane groove's band projections start flush at each band's inner
            // (Face-adjacent) edge when program.Side is true, or the outer edge when false -
            // like a bore. AddSideRectangleRange's side parameter carries that through.
            if (isXOriented)
            {
                // Parallel bands (Top/Bottom, same run direction as the groove): a plain extent
                // projection, no center line - the groove's own center line is already on the Face.
                PartPreviewOverlayGeometry.AddSideRectangleRange(topShape, NoCenterLine, xLo, xHi, depthPx, overshootPx, layout, side, horizontal: true, near: true);
                PartPreviewOverlayGeometry.AddSideRectangleRange(bottomShape, NoCenterLine, xLo, xHi, depthPx, overshootPx, layout, side, horizontal: true, near: false);

                // Perpendicular bands (Left/Right): a rectangle (depth dp) spanning the groove's
                // true Y extent - trueBounds already accounts for width and c (tool-to-center-line
                // position), not just the center line. The center line itself stays at the
                // groove's actual travel-line Y (startPx.Y), same as on the Face - not recentered
                // on the c-offset rectangle - since c only offsets the tool's cut, not the
                // programmed center line.
                var yLoPx = layout.OriginY + (trueBounds.YLo * layout.Scale);
                var yHiPx = layout.OriginY + (trueBounds.YHi * layout.Scale);
                PartPreviewOverlayGeometry.AddSideRectangleRange(leftShape, NoCenterLine, yLoPx, yHiPx, depthPx, overshootPx, layout, side, horizontal: false, near: true);
                PartPreviewOverlayGeometry.AddSideRectangleRange(rightShape, NoCenterLine, yLoPx, yHiPx, depthPx, overshootPx, layout, side, horizontal: false, near: false);
                AddPerpendicularBandCenterLine(addCenterLine, startPx.Y, depthPx, overshootPx, layout, side, horizontal: false, near: true);
                AddPerpendicularBandCenterLine(addCenterLine, startPx.Y, depthPx, overshootPx, layout, side, horizontal: false, near: false);
            }
            else if (isYOriented)
            {
                PartPreviewOverlayGeometry.AddSideRectangleRange(leftShape, NoCenterLine, yLo, yHi, depthPx, overshootPx, layout, side, horizontal: false, near: true);
                PartPreviewOverlayGeometry.AddSideRectangleRange(rightShape, NoCenterLine, yLo, yHi, depthPx, overshootPx, layout, side, horizontal: false, near: false);

                var xLoPx = layout.OriginX + (trueBounds.XLo * layout.Scale);
                var xHiPx = layout.OriginX + (trueBounds.XHi * layout.Scale);
                PartPreviewOverlayGeometry.AddSideRectangleRange(topShape, NoCenterLine, xLoPx, xHiPx, depthPx, overshootPx, layout, side, horizontal: true, near: true);
                PartPreviewOverlayGeometry.AddSideRectangleRange(bottomShape, NoCenterLine, xLoPx, xHiPx, depthPx, overshootPx, layout, side, horizontal: true, near: false);
                AddPerpendicularBandCenterLine(addCenterLine, startPx.X, depthPx, overshootPx, layout, side, horizontal: true, near: true);
                AddPerpendicularBandCenterLine(addCenterLine, startPx.X, depthPx, overshootPx, layout, side, horizontal: true, near: false);
            }
            else
            {
                // Diagonal groove (not axis-parallel): treat it as the true 3D box it is - a
                // footprint (the true rectangle above) extruded straight down in Z from the Face
                // by its depth. Each edge band gets that box's projected ribs (dashed, schematic
                // reference) plus a solid crossing rectangle only where the box's footprint
                // actually reaches/crosses that band's own edge.
                var (p1, p2, p3, p4) = FootprintCornersMm(groove.Start, groove.End, groove.Width, groove.Position);
                var dxMm = groove.End.X - groove.Start.X;
                var dyMm = groove.End.Y - groove.Start.Y;
                var lengthMm = Math.Sqrt((dxMm * dxMm) + (dyMm * dyMm));
                var dirXMm = dxMm / lengthMm;
                var dirYMm = dyMm / lengthMm;
                var normalXMm = -dirYMm;
                var normalYMm = dirXMm;
                var (offsetA, offsetB) = groove.Position switch
                {
                    ToolPosition.Right => (0d, groove.Width),
                    ToolPosition.Left => (-groove.Width, 0d),
                    _ => (-groove.Width / 2, groove.Width / 2),
                };

                // Where the offset line at perpendicular distance offsetMm from the groove's own
                // center line crosses the edge (Y = edgeMm when horizontal, else X = edgeMm) -
                // solved from the center line's own parametric equation.
                double CrossAt(bool bandHorizontal, double edgeMm, double offsetMm)
                {
                    if (bandHorizontal)
                    {
                        var s = (edgeMm - groove.Start.Y - (normalYMm * offsetMm)) / dirYMm;
                        return groove.Start.X + (s * dirXMm) + (normalXMm * offsetMm);
                    }

                    var sV = (edgeMm - groove.Start.X - (normalXMm * offsetMm)) / dirXMm;
                    return groove.Start.Y + (sV * dirYMm) + (normalYMm * offsetMm);
                }

                // Projects one XY footprint corner, at Z level zMm, onto a given edge band's
                // screen space - the along-band coordinate maps directly (X for a horizontal
                // band, Y otherwise), the Z coordinate maps through GetBandEdges the same way
                // AddSideRectangleRange does: starting flush at the band's inner edge and
                // growing outward when side is true, or at the outer edge growing inward when false.
                (double X, double Y) ProjectCorner((double X, double Y) cornerMm, bool bandHorizontal, bool near, double zMm)
                {
                    var (nearEdge, farEdge, direction) = PartPreviewOverlayGeometry.GetBandEdges(layout, bandHorizontal, near);
                    var start = side ? nearEdge : farEdge;
                    var dir = side ? direction : -direction;
                    var perpPx = start + (dir * (zMm * layout.Scale));
                    var alongMm = bandHorizontal ? cornerMm.X : cornerMm.Y;
                    var alongPx = (bandHorizontal ? layout.OriginX : layout.OriginY) + (alongMm * layout.Scale);
                    return bandHorizontal ? (X: alongPx, Y: perpPx) : (X: perpPx, Y: alongPx);
                }

                void DrawRibs(bool bandHorizontal, bool near)
                {
                    // 4 "vertical" (Z-direction) ribs, one per footprint corner.
                    foreach (var corner in new[] { p1, p2, p3, p4 })
                    {
                        var atFace = ProjectCorner(corner, bandHorizontal, near, 0);
                        var atDepth = ProjectCorner(corner, bandHorizontal, near, groove.Depth);
                        addDashedShape(new Line { X1 = atFace.X, Y1 = atFace.Y, X2 = atDepth.X, Y2 = atDepth.Y }, false);
                    }

                    // The 4 "horizontal" (Z-constant) footprint edges, once at Z=0 and once at
                    // Z=depth, are each simplified to one P1->P3 diagonal rather than the full
                    // P1->P2->P3->P4->P1 polyline - a polyline's shared corners still show
                    // dashed-pattern seams/overlap; a lone diagonal has no joints and (since the
                    // footprint is a thin rectangle) still spans essentially the same extent.
                    foreach (var zMm in new[] { 0, groove.Depth })
                    {
                        var from = ProjectCorner(p1, bandHorizontal, near, zMm);
                        var to = ProjectCorner(p3, bandHorizontal, near, zMm);
                        addDashedShape(new Line { X1 = from.X, Y1 = from.Y, X2 = to.X, Y2 = to.Y }, false);
                    }
                }

                void DrawCrossSection(bool bandHorizontal, bool near, double edgeMm, bool touches)
                {
                    if (!touches)
                    {
                        return;
                    }

                    var crossA = CrossAt(bandHorizontal, edgeMm, offsetA);
                    var crossB = CrossAt(bandHorizontal, edgeMm, offsetB);
                    var crossLoPx = (bandHorizontal ? layout.OriginX : layout.OriginY) + (Math.Min(crossA, crossB) * layout.Scale);
                    var crossHiPx = (bandHorizontal ? layout.OriginX : layout.OriginY) + (Math.Max(crossA, crossB) * layout.Scale);
                    PartPreviewOverlayGeometry.AddSideRectangleRange(addSolidShape, NoCenterLine, crossLoPx, crossHiPx, depthPx, overshootPx, layout, side, bandHorizontal, near);
                }

                DrawRibs(bandHorizontal: true, near: true);
                DrawRibs(bandHorizontal: true, near: false);
                DrawRibs(bandHorizontal: false, near: true);
                DrawRibs(bandHorizontal: false, near: false);

                DrawCrossSection(bandHorizontal: true, near: true, edgeMm: 0, topTouches);
                DrawCrossSection(bandHorizontal: true, near: false, edgeMm: faceHeightMm, bottomTouches);
                DrawCrossSection(bandHorizontal: false, near: true, edgeMm: 0, leftTouches);
                DrawCrossSection(bandHorizontal: false, near: false, edgeMm: faceWidthMm, rightTouches);
            }

            AttachClickToggle(clickTargets, allStroked, normalBrush, brushes.Selected);
        }

        private static void DrawEdgePlaneGroove(
            Canvas canvas,
            XncProgram program,
            XncGrooving groove,
            BorePreviewRenderer.FaceLayout layout,
            BorePreviewRenderer.BoreBrushes brushes)
        {
            // Edge-plane grooves always share one color, regardless of the owning program's Side
            // - unlike a front-plane groove's Side-based coloring (mirrors DrawEdgeBore).
            var normalBrush = brushes.SideTrue;
            var tag = new GrooveTag(program, groove);
            var widthPx = groove.Width * layout.Scale;
            var depthPx = groove.Depth * layout.Scale;
            var overshootPx = brushes.CenterLineOvershootMm * layout.Scale;

            var surface = groove.SideCode switch
            {
                1 => BoreSurface.Left,
                2 => BoreSurface.Right,
                3 => BoreSurface.Top,
                _ => BoreSurface.Bottom,
            };
            var horizontal = surface is BoreSurface.Top or BoreSurface.Bottom;
            var near = surface is BoreSurface.Top or BoreSurface.Left;

            // The axis not carrying the groove's along-edge extent stays constant (Start ~ End)
            // at the panel edge (e.g. x1=x2=dx for a right-edge groove) - it's not the
            // through-thickness position; that comes from the groove's own Z attribute.
            var axisLo = horizontal ? Math.Min(groove.Start.X, groove.End.X) : Math.Min(groove.Start.Y, groove.End.Y);
            var axisHi = horizontal ? Math.Max(groove.Start.X, groove.End.X) : Math.Max(groove.Start.Y, groove.End.Y);
            var zMm = groove.Z;

            var axisLoPx = (horizontal ? layout.OriginX : layout.OriginY) + (axisLo * layout.Scale);
            var axisHiPx = (horizontal ? layout.OriginX : layout.OriginY) + (axisHi * layout.Scale);
            var faceExtentMm = horizontal ? layout.FaceWidth / layout.Scale : layout.FaceHeight / layout.Scale;

            var (clickTargets, allStroked, addSolidShape, addDashedShape, addCenterLine) = CreateShapeSink(canvas, tag, normalBrush, brushes.Thickness2Px, brushes.Thickness1Px, brushes.DashLengthPx);
            void NoCenterLine(double x1, double y1, double x2, double y2)
            {
            }

            // c is defined relative to the tool's real traversal direction, which for an
            // edge-plane groove isn't always Start->End - it's max(y1,y2)->min(y1,y2) for a
            // Left/Right-plane groove (p=1|2), or min(x1,x2)->max(x1,x2) for a Top/Bottom-plane
            // groove (p=3|4). axisLo/axisHi below are already the order-normalized min/max, so
            // bandStart/bandEnd (axisLo->axisHi, i.e. increasing) match that real direction as-is
            // for p=3|4 but run exactly opposite to it for p=1|2 - ResolveTravelPosition mirrors
            // groove.Position once, up front, to compensate.
            var travelPosition = ResolveTravelPosition(groove);

            // Band (true) rectangle: this is the groove's home plane - always solid. Offset from
            // the band's inner (Face-adjacent) edge by zMm - always the inner edge, regardless of
            // program.Side, unlike an edge bore's circle (which flips near/far on Side).
            var (nearEdge, farEdge, direction) = PartPreviewOverlayGeometry.GetBandEdges(layout, horizontal, near);
            var zStart = nearEdge;
            var zDir = direction;
            var perpPx = zStart + (zDir * (zMm * layout.Scale));

            var bandStart = horizontal ? (X: axisLoPx, Y: perpPx) : (X: perpPx, Y: axisLoPx);
            var bandEnd = horizontal ? (X: axisHiPx, Y: perpPx) : (X: perpPx, Y: axisHiPx);
            AddOffsetRectangle(addSolidShape, addCenterLine, bandStart, bandEnd, widthPx, travelPosition, overshootPx);

            // Opposite-band mirror: the same band rectangle, dashed, on the band directly across
            // from this groove's own (Right for a Left-plane groove, Left for Right, Bottom for
            // Top, Top for Bottom) - same axis extent and Z offset, just evaluated against that
            // band's own inner edge via GetBandEdges(near: !near). Always dashed and without a
            // center line, since it's a reference mirror rather than the groove's true plane.
            // c's Right/Left offset is mirrored too: AddOffsetRectangle derives its offset
            // direction from this call's own (unchanged) start/end points, so it can't see that
            // GetBandEdges just flipped the target band's near/far sense - MirrorPosition
            // compensates so the offset lands on the physically correct side of the center line.
            var (oppNearEdge, _, oppDirection) = PartPreviewOverlayGeometry.GetBandEdges(layout, horizontal, near: !near);
            var oppPerpPx = oppNearEdge + (oppDirection * (zMm * layout.Scale));
            var oppStart = horizontal ? (X: axisLoPx, Y: oppPerpPx) : (X: oppPerpPx, Y: axisLoPx);
            var oppEnd = horizontal ? (X: axisHiPx, Y: oppPerpPx) : (X: oppPerpPx, Y: axisHiPx);
            AddOffsetRectangle(addDashedShape, NoCenterLine, oppStart, oppEnd, widthPx, MirrorPosition(travelPosition), overshootPx);

            // Face depth projection: flush to the groove's own physical Face edge, growing inward -
            // no center line, always dashed (the "front plane" symbol for a p=1|2|3|4 groove).
            PartPreviewOverlayGeometry.AddFaceRectangleRange(addDashedShape, NoCenterLine, axisLoPx, axisHiPx, depthPx, overshootPx, layout, surface, horizontal);

            // Cross-band depth markers: on the two bands perpendicular to this groove's own plane
            // (Top/Bottom for a Left/Right-plane groove, Left/Right for a Top/Bottom-plane groove),
            // a small t-wide/dp-long marker flush at the groove's own fixed edge coordinate, growing
            // inward along its intrinsic depth axis (X for Left/Right-plane, Y for Top/Bottom-plane).
            // Perpendicular to that, each marker is displaced by the groove's own Z from *that*
            // band's inner edge - the same nearEdge/direction/Z formula as the own-band rectangle
            // above, just evaluated per target band via GetBandEdges. Each marker is solid only
            // where the groove's own along-edge extent actually reaches that marker's edge (the
            // "near" marker checks axisLo against 0, the "far" marker checks axisHi against the
            // face's extent on this axis); otherwise it's just a reference projection.
            var otherHorizontal = !horizontal;
            double markerLoPx, markerHiPx;
            if (otherHorizontal)
            {
                (markerLoPx, markerHiPx) = surface == BoreSurface.Left
                    ? (layout.OriginX, layout.OriginX + depthPx)
                    : (layout.OriginX + layout.FaceWidth - depthPx, layout.OriginX + layout.FaceWidth);
            }
            else
            {
                (markerLoPx, markerHiPx) = surface == BoreSurface.Top
                    ? (layout.OriginY, layout.OriginY + depthPx)
                    : (layout.OriginY + layout.FaceHeight - depthPx, layout.OriginY + layout.FaceHeight);
            }

            // Unlike the own-band/opposite-band pair above (which are self-relative to this
            // groove's own near), marker A always targets a near:true band (Top/Left) and marker
            // B always targets a near:false band (Bottom/Right), regardless of which side this
            // groove's own plane is on. So which one gets travelPosition vs. its mirror depends
            // on whether this groove's own plane (near) matches marker A's near:true target: when
            // it does (own plane is Left/Top, near true), marker A is a same-near cross section
            // and needs the mirror instead, and marker B needs travelPosition instead - the
            // opposite pairing from when this groove's own plane is Right/Bottom (near false).
            var markerAPosition = near ? MirrorPosition(travelPosition) : travelPosition;
            var markerBPosition = near ? travelPosition : MirrorPosition(travelPosition);

            var (nearEdgeA, _, directionA) = PartPreviewOverlayGeometry.GetBandEdges(layout, otherHorizontal, near: true);
            var anchorA = nearEdgeA + (directionA * (zMm * layout.Scale));
            var markerStartA = otherHorizontal ? (X: markerLoPx, Y: anchorA) : (X: anchorA, Y: markerLoPx);
            var markerEndA = otherHorizontal ? (X: markerHiPx, Y: anchorA) : (X: anchorA, Y: markerHiPx);
            AddOffsetRectangle(ReachesNearEdge(axisLo) ? addSolidShape : addDashedShape, addCenterLine, markerStartA, markerEndA, widthPx, markerAPosition, overshootPx);

            var (nearEdgeB, _, directionB) = PartPreviewOverlayGeometry.GetBandEdges(layout, otherHorizontal, near: false);
            var anchorB = nearEdgeB + (directionB * (zMm * layout.Scale));
            var markerStartB = otherHorizontal ? (X: markerLoPx, Y: anchorB) : (X: anchorB, Y: markerLoPx);
            var markerEndB = otherHorizontal ? (X: markerHiPx, Y: anchorB) : (X: anchorB, Y: markerHiPx);
            AddOffsetRectangle(ReachesFarEdge(axisHi, faceExtentMm) ? addSolidShape : addDashedShape, addCenterLine, markerStartB, markerEndB, widthPx, markerBPosition, overshootPx);

            AttachClickToggle(clickTargets, allStroked, normalBrush, brushes.Selected);
        }

        /// <summary>
        /// Builds the shared <c>addSolidShape</c>/<c>addDashedShape</c>/<c>addCenterLine</c>
        /// closures + tracking lists used by both groove variants, mirroring
        /// <c>BorePreviewRenderer.DrawBore</c>'s local functions. A groove rectangle is solid
        /// 2px where it represents the groove's true (home-plane) shape or a real continuation
        /// onto an adjacent plane, else dashed 1px; a groove center line is always dashed 1px,
        /// regardless of which rectangle it belongs to.
        /// </summary>
        private static (List<Shape> ClickTargets, List<Shape> AllStroked, Action<Shape, bool> AddSolidShape, Action<Shape, bool> AddDashedShape, Action<double, double, double, double> AddCenterLine) CreateShapeSink(
            Canvas canvas, GrooveTag tag, Brush brush, double thickness2Px, double thickness1Px, double dashLengthPx)
        {
            var clickTargets = new List<Shape>(5);
            var allStroked = new List<Shape>(11);

            void AddShape(Shape shape, bool isClickTarget, double thickness, double? dashPx)
            {
                shape.Stroke = brush;
                shape.Fill = Brushes.Transparent;
                PartPreviewOverlayGeometry.SetStroke(shape, thickness, dashPx);
                shape.Tag = tag;
                canvas.Children.Add(shape);
                allStroked.Add(shape);

                if (isClickTarget)
                {
                    clickTargets.Add(shape);
                }
            }

            void AddSolidShape(Shape shape, bool isClickTarget) => AddShape(shape, isClickTarget, thickness2Px, null);

            void AddDashedShape(Shape shape, bool isClickTarget) => AddShape(shape, isClickTarget, thickness1Px, dashLengthPx);

            void AddCenterLine(double x1, double y1, double x2, double y2)
            {
                var line = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = brush,
                };
                PartPreviewOverlayGeometry.SetStroke(line, thickness1Px, dashLengthPx);
                canvas.Children.Add(line);
                allStroked.Add(line);
            }

            return (clickTargets, allStroked, AddSolidShape, AddDashedShape, AddCenterLine);
        }

        /// <summary>
        /// Returns the mm-space corners of a groove's true (home-plane) footprint rectangle - the
        /// same offset-from-travel-direction geometry as <see cref="AddOffsetRectangle"/>, but in
        /// model-space mm rather than device px. <c>P1</c>/<c>P2</c> sit at the <c>offsetA</c> side
        /// (<c>Start</c>/<c>End</c>), <c>P3</c>/<c>P4</c> at the <c>offsetB</c> side (<c>End</c>/
        /// <c>Start</c>) - the same corner order <see cref="AddOffsetRectangle"/> draws its polygon in.
        /// </summary>
        private static ((double X, double Y) P1, (double X, double Y) P2, (double X, double Y) P3, (double X, double Y) P4) FootprintCornersMm(
            XncPoint start, XncPoint end, double width, ToolPosition position)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));

            if (length <= 0)
            {
                return ((start.X, start.Y), (start.X, start.Y), (start.X, start.Y), (start.X, start.Y));
            }

            var normalX = -(dy / length);
            var normalY = dx / length;

            var (offsetA, offsetB) = position switch
            {
                ToolPosition.Right => (0d, width),
                ToolPosition.Left => (-width, 0d),
                _ => (-width / 2, width / 2),
            };

            var p1 = (X: start.X + (normalX * offsetA), Y: start.Y + (normalY * offsetA));
            var p2 = (X: end.X + (normalX * offsetA), Y: end.Y + (normalY * offsetA));
            var p3 = (X: end.X + (normalX * offsetB), Y: end.Y + (normalY * offsetB));
            var p4 = (X: start.X + (normalX * offsetB), Y: start.Y + (normalY * offsetB));

            return (p1, p2, p3, p4);
        }

        /// <summary>
        /// The mm-space bounding box of <see cref="FootprintCornersMm"/> - used to test whether
        /// the groove's true rectangle reaches a part edge, not to draw it.
        /// </summary>
        private static (double XLo, double XHi, double YLo, double YHi) TrueRectangleBoundsMm(
            XncPoint start, XncPoint end, double width, ToolPosition position)
        {
            var (p1, p2, p3, p4) = FootprintCornersMm(start, end, width, position);

            var xLo = Math.Min(Math.Min(p1.X, p2.X), Math.Min(p3.X, p4.X));
            var xHi = Math.Max(Math.Max(p1.X, p2.X), Math.Max(p3.X, p4.X));
            var yLo = Math.Min(Math.Min(p1.Y, p2.Y), Math.Min(p3.Y, p4.Y));
            var yHi = Math.Max(Math.Max(p1.Y, p2.Y), Math.Max(p3.Y, p4.Y));

            return (xLo, xHi, yLo, yHi);
        }

        /// <summary>
        /// Draws a rectangle of width <paramref name="widthPx"/> running the length of the line
        /// from <paramref name="startPx"/> to <paramref name="endPx"/>, offset perpendicular to
        /// that line per <paramref name="position"/> (<see cref="ToolPosition.Center"/> straddles
        /// it; <see cref="ToolPosition.Right"/>/<see cref="ToolPosition.Left"/> put the full width
        /// to the physical right/left of the start-to-end travel direction; an unexpected
        /// <see cref="ToolPosition.Pocket"/> falls back to <see cref="ToolPosition.Center"/>),
        /// plus the line itself as the center line, extended by <paramref name="overshootPx"/> at
        /// each end. Works for any orientation (axis-parallel or diagonal), since it never assumes
        /// a horizontal/vertical split - unlike the band/Face helpers in
        /// <see cref="PartPreviewOverlayGeometry"/>.
        /// </summary>
        private static void AddOffsetRectangle(
            Action<Shape, bool> addShape,
            Action<double, double, double, double> addCenterLine,
            (double X, double Y) startPx,
            (double X, double Y) endPx,
            double widthPx,
            ToolPosition position,
            double overshootPx)
        {
            var dx = endPx.X - startPx.X;
            var dy = endPx.Y - startPx.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));

            if (length <= 0)
            {
                return;
            }

            var dirX = dx / length;
            var dirY = dy / length;

            // Right of the start-to-end travel direction, in this Y-down canvas frame.
            var normalX = -dirY;
            var normalY = dirX;

            var (offsetA, offsetB) = position switch
            {
                ToolPosition.Right => (0d, widthPx),
                ToolPosition.Left => (-widthPx, 0d),
                _ => (-widthPx / 2, widthPx / 2),
            };

            var pA1 = (X: startPx.X + (normalX * offsetA), Y: startPx.Y + (normalY * offsetA));
            var pA2 = (X: endPx.X + (normalX * offsetA), Y: endPx.Y + (normalY * offsetA));
            var pB2 = (X: endPx.X + (normalX * offsetB), Y: endPx.Y + (normalY * offsetB));
            var pB1 = (X: startPx.X + (normalX * offsetB), Y: startPx.Y + (normalY * offsetB));

            var polygon = new Polygon
            {
                Points =
                [
                    new System.Windows.Point(pA1.X, pA1.Y),
                    new System.Windows.Point(pA2.X, pA2.Y),
                    new System.Windows.Point(pB2.X, pB2.Y),
                    new System.Windows.Point(pB1.X, pB1.Y),
                ],
            };
            addShape(polygon, true);

            addCenterLine(
                startPx.X - (dirX * overshootPx), startPx.Y - (dirY * overshootPx),
                endPx.X + (dirX * overshootPx), endPx.Y + (dirY * overshootPx));
        }

        /// <summary>
        /// Draws a perpendicular-band center line at the groove's true travel-line coordinate
        /// (<paramref name="fixedAxisPx"/> - e.g. <c>startPx.Y</c> for an X-oriented groove's
        /// Left/Right band), spanning that band's own depth (perpendicular) extent, the same way
        /// <see cref="PartPreviewOverlayGeometry.AddSideRectangleRange"/> computes it internally -
        /// but anchored at the fixed coordinate rather than the rectangle's own midpoint, since a
        /// non-<see cref="ToolPosition.Center"/> <c>c</c> offsets the band rectangle away from
        /// the true center line without moving the center line itself (mirroring how the Face
        /// rectangle's own center line always runs along the unoffset Start-End line).
        /// </summary>
        private static void AddPerpendicularBandCenterLine(
            Action<double, double, double, double> addCenterLine,
            double fixedAxisPx,
            double depthPx,
            double overshootPx,
            BorePreviewRenderer.FaceLayout layout,
            bool side,
            bool horizontal,
            bool near)
        {
            var (nearEdge, farEdge, direction) = PartPreviewOverlayGeometry.GetBandEdges(layout, horizontal, near);
            var start = side ? nearEdge : farEdge;
            var dir = side ? direction : -direction;
            var end = start + (dir * depthPx);
            var lo = Math.Min(start, end);
            var hi = Math.Max(start, end);

            if (horizontal)
            {
                addCenterLine(fixedAxisPx, lo - overshootPx, fixedAxisPx, hi + overshootPx);
            }
            else
            {
                addCenterLine(lo - overshootPx, fixedAxisPx, hi + overshootPx, fixedAxisPx);
            }
        }

        /// <summary>
        /// Swaps <see cref="ToolPosition.Right"/>/<see cref="ToolPosition.Left"/> (leaving
        /// <see cref="ToolPosition.Center"/>/<see cref="ToolPosition.Pocket"/> unchanged) - used
        /// wherever a groove's <c>c</c> offset is drawn against a band on the opposite side of
        /// <see cref="PartPreviewOverlayGeometry.GetBandEdges"/>'s near/far flip from the
        /// groove's own band, since
        /// <see cref="AddOffsetRectangle"/> otherwise has no way to know that flip happened.
        /// </summary>
        private static ToolPosition MirrorPosition(ToolPosition position) => position switch
        {
            ToolPosition.Right => ToolPosition.Left,
            ToolPosition.Left => ToolPosition.Right,
            _ => position,
        };

        /// <summary>
        /// Resolves an edge-plane groove's <c>c</c> to the <see cref="ToolPosition"/> that
        /// matches the drawing code's own min-to-max along-band convention (<c>axisLo</c> -&gt;
        /// <c>axisHi</c>), given the tool's real traversal direction: max(y1,y2)-&gt;min(y1,y2)
        /// for a Left/Right-plane groove (<c>p</c> 1|2), or min(x1,x2)-&gt;max(x1,x2) for a
        /// Top/Bottom-plane groove (<c>p</c> 3|4) - both expressed via already order-normalized
        /// min/max, so (unlike a front-plane groove's literal Start-&gt;End) this doesn't depend
        /// on which endpoint the file happens to label Start vs. End. That real direction runs
        /// opposite to the drawing convention for p=1|2 (needs <see cref="MirrorPosition"/>) and
        /// matches it for p=3|4 (needs no change).
        /// </summary>
        private static ToolPosition ResolveTravelPosition(XncGrooving groove) =>
            groove.SideCode is 1 or 2 ? MirrorPosition(groove.Position) : groove.Position;

        private static void AttachClickToggle(List<Shape> clickTargets, List<Shape> allStroked, Brush normalBrush, Brush selectedBrush)
        {
            var selected = false;

            void OnClick(object sender, MouseButtonEventArgs e)
            {
                selected = !selected;
                var brush = selected ? selectedBrush : normalBrush;

                foreach (var shape in allStroked)
                {
                    shape.Stroke = brush;
                }

                e.Handled = true;
            }

            foreach (var shape in clickTargets)
            {
                shape.MouseLeftButtonDown += OnClick;
            }
        }
    }
}
