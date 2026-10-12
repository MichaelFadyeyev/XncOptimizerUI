using System.Windows;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws a front-plane groove (<see cref="XncGrooving.SideCode"/> <c>0</c>): its true rectangle
    /// (length = distance between its start/end points, width = <c>t</c>, offset from its center
    /// line per <c>c</c>) on the Face rectangle, plus a depth (<c>dp</c>) projection on each of the
    /// four side bands, starting flush at a band's inner edge when the owning program's
    /// <c>Side</c> is true, or its outer edge when false - like a bore. A band projection is solid
    /// where the groove's true rectangle reaches or crosses that band's edge, else a dashed
    /// reference.
    /// </summary>
    internal static class FrontPlaneGrooveRenderer
    {
        /// <summary>Orientation tolerance (mm) for telling an axis-parallel groove's constant coordinate from noise.</summary>
        private const double OrientationEpsilon = 1e-6;

        /// <summary>The four side bands as (horizontal, near): Top, Bottom, Left, Right.</summary>
        private static readonly (bool Horizontal, bool Near)[] Bands = [(true, true), (true, false), (false, true), (false, false)];

        /// <summary>One groove's drawing context.</summary>
        private sealed record Groove(
            GrooveShapes Shapes,
            XncGrooving Model,
            FaceLayout Layout,
            bool FromInner,
            double DepthPx,
            double OvershootPx,
            MmBounds TrueBounds)
        {
            /// <summary>The side-band rectangle spanning <paramref name="lowPx"/>..<paramref name="highPx"/> along the band, <see cref="DepthPx"/> deep.</summary>
            public PxRect BandRectangle(double lowPx, double highPx, bool horizontal, bool near) =>
                PartPreviewOverlayGeometry.SideRectangleRange(lowPx, highPx, DepthPx, OvershootPx, Layout, FromInner, horizontal, near).Rect;

            /// <summary>Whether the true rectangle reaches the band's face edge, so its projection there is solid.</summary>
            public bool Reaches(bool horizontal, bool near) =>
                GroovePreviewGeometry.ReachesBand(TrueBounds, Layout, horizontal, near);
        }

        public static void Draw(ICollection<UIElement> shapes, XncProgram program, XncGrooving groove, FaceLayout layout, BoreBrushes brushes)
        {
            var overshootPx = layout.MmToPx(brushes.CenterLineOvershootMm);
            var sink = new OverlayShapeSink(new GrooveTag(program, groove), program.Side ? brushes.SideTrue : brushes.SideFalse);
            var context = new Groove(
                new GrooveShapes(shapes, sink, brushes, overshootPx),
                groove,
                layout,
                FromInner: program.Side,
                layout.MmToPx(groove.Depth),
                overshootPx,
                GroovePreviewGeometry.TrueBounds(groove));

            // Face (true) rectangle: the groove's home plane - always solid.
            var startPx = layout.ToPx(groove.Start.ToVec2());
            var endPx = layout.ToPx(groove.End.ToVec2());
            context.Shapes.OffsetRectangle(startPx, endPx, layout.MmToPx(groove.Width), groove.Position, solid: true, withCentreLine: true);

            if (Math.Abs(groove.Start.Y - groove.End.Y) < OrientationEpsilon)
            {
                DrawAxisParallelProjections(context, startPx, endPx, alongX: true);
            }
            else if (Math.Abs(groove.Start.X - groove.End.X) < OrientationEpsilon)
            {
                DrawAxisParallelProjections(context, startPx, endPx, alongX: false);
            }
            else
            {
                DrawDiagonalProjections(context);
            }

            context.Shapes.AttachClickToggle();
        }

        /// <summary>
        /// Band projections of a groove running along X (<paramref name="alongX"/>) or Y. The two
        /// parallel bands get a plain extent projection of its run, without a center line - the
        /// groove's own center line is already on the Face. The two perpendicular bands get a
        /// rectangle spanning the groove's true extent across (width and <c>c</c> included), plus a
        /// center line at the groove's actual travel line - not recentered on the <c>c</c>-offset
        /// rectangle, since <c>c</c> only offsets the cut, not the programmed center line.
        /// </summary>
        private static void DrawAxisParallelProjections(Groove groove, Vec2 startPx, Vec2 endPx, bool alongX)
        {
            var (layout, bounds) = (groove.Layout, groove.TrueBounds);
            var (runLow, runHigh) = alongX
                ? (Math.Min(startPx.X, endPx.X), Math.Max(startPx.X, endPx.X))
                : (Math.Min(startPx.Y, endPx.Y), Math.Max(startPx.Y, endPx.Y));
            var (acrossLow, acrossHigh) = alongX
                ? (layout.YPx(bounds.YLo), layout.YPx(bounds.YHi))
                : (layout.XPx(bounds.XLo), layout.XPx(bounds.XHi));

            foreach (var near in new[] { true, false })
            {
                groove.Shapes.Rectangle(groove.BandRectangle(runLow, runHigh, alongX, near), groove.Reaches(alongX, near));
            }

            foreach (var near in new[] { true, false })
            {
                groove.Shapes.Rectangle(groove.BandRectangle(acrossLow, acrossHigh, !alongX, near), groove.Reaches(!alongX, near));
            }

            foreach (var near in new[] { true, false })
            {
                groove.Shapes.CentreLine(PerpendicularBandCentreLine(groove, alongX ? startPx.Y : startPx.X, horizontal: !alongX, near));
            }
        }

        /// <summary>
        /// A perpendicular band's center line at the groove's travel-line coordinate
        /// <paramref name="travelPx"/>, spanning the band projection's depth plus the overshoot.
        /// </summary>
        private static PxLine PerpendicularBandCentreLine(Groove groove, double travelPx, bool horizontal, bool near)
        {
            var (start, direction) = groove.Layout.Band(horizontal, near).Anchor(groove.FromInner);
            var end = start + (direction * groove.DepthPx);
            var low = Math.Min(start, end) - groove.OvershootPx;
            var high = Math.Max(start, end) + groove.OvershootPx;

            return horizontal
                ? new PxLine(travelPx, low, travelPx, high)
                : new PxLine(low, travelPx, high, travelPx);
        }

        /// <summary>
        /// Band projections of a diagonal (not axis-parallel) groove, treated as the true 3D box it
        /// is - its footprint extruded straight down by its depth. Every band gets the box's
        /// projected ribs (dashed, schematic reference), plus a solid cross-section rectangle only
        /// where the footprint actually reaches or crosses that band's edge.
        /// </summary>
        private static void DrawDiagonalProjections(Groove groove)
        {
            var model = groove.Model;
            var footprint = OffsetStrip.Along(model.Start.ToVec2(), model.End.ToVec2(), model.Width, model.Position)!.Value;

            foreach (var (horizontal, near) in Bands)
            {
                DrawRibs(groove, footprint, horizontal, near);
            }

            foreach (var (horizontal, near) in Bands)
            {
                if (groove.Reaches(horizontal, near))
                {
                    DrawCrossSection(groove, footprint, horizontal, near);
                }
            }
        }

        /// <summary>
        /// The box's ribs on one band: a depth-direction rib per footprint corner, and the
        /// footprint at the face and at full depth, each simplified to its first-to-third corner
        /// diagonal - a closed outline's shared corners would show dash seams, and the thin
        /// footprint's diagonal spans essentially the same extent.
        /// </summary>
        private static void DrawRibs(Groove groove, OffsetStrip footprint, bool horizontal, bool near)
        {
            var corners = footprint.Corners;

            foreach (var corner in corners)
            {
                groove.Shapes.Rib(Line(
                    ProjectOntoBand(groove, corner, horizontal, near, 0),
                    ProjectOntoBand(groove, corner, horizontal, near, groove.Model.Depth)));
            }

            foreach (var zMm in new[] { 0, groove.Model.Depth })
            {
                groove.Shapes.Rib(Line(
                    ProjectOntoBand(groove, corners[0], horizontal, near, zMm),
                    ProjectOntoBand(groove, corners[2], horizontal, near, zMm)));
            }
        }

        /// <summary>
        /// The solid depth rectangle where the footprint's two long sides cross the band's face
        /// edge.
        /// </summary>
        private static void DrawCrossSection(Groove groove, OffsetStrip footprint, bool horizontal, bool near)
        {
            var layout = groove.Layout;
            var edgeMm = near ? 0 : (horizontal ? layout.FaceHeightMm : layout.FaceWidthMm);
            var crossA = footprint.CrossingAt(horizontal, edgeMm, footprint.OffsetA);
            var crossB = footprint.CrossingAt(horizontal, edgeMm, footprint.OffsetB);
            var rect = groove.BandRectangle(
                layout.AlongPx(horizontal, Math.Min(crossA, crossB)),
                layout.AlongPx(horizontal, Math.Max(crossA, crossB)),
                horizontal,
                near);

            groove.Shapes.Rectangle(rect, solid: true);
        }

        /// <summary>
        /// Projects footprint point <paramref name="cornerMm"/> at depth <paramref name="zMm"/>
        /// onto a band: along the band it keeps its X (horizontal band) or Y; across, the depth
        /// is measured like a band projection's, from the inner or outer edge per the program side.
        /// </summary>
        private static Vec2 ProjectOntoBand(Groove groove, Vec2 cornerMm, bool horizontal, bool near, double zMm)
        {
            var layout = groove.Layout;
            var acrossPx = layout.Band(horizontal, near).At(layout.MmToPx(zMm), groove.FromInner);
            var alongPx = layout.AlongPx(horizontal, horizontal ? cornerMm.X : cornerMm.Y);

            return horizontal ? new Vec2(alongPx, acrossPx) : new Vec2(acrossPx, alongPx);
        }

        private static PxLine Line(Vec2 from, Vec2 to) => new(from.X, from.Y, to.X, to.Y);
    }
}
