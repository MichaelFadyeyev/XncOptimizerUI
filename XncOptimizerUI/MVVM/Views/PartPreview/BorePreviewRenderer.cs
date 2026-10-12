using System.Windows;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws every bore on top of the part preview built by <see cref="PartPreviewRenderer.DrawPart"/>.
    /// A face-drilled bore (<see cref="BoreSurface.Face"/>) gets a circle with crossed
    /// center lines on the Face rectangle, plus a rectangle with one center line projected
    /// onto each of the four Top/Bottom/Left/Right edge bands. An edge-drilled bore
    /// (<see cref="BoreSurface.Top"/>/<see cref="BoreSurface.Bottom"/>/
    /// <see cref="BoreSurface.Left"/>/<see cref="BoreSurface.Right"/>) is the mirror image:
    /// its true cross-section (circle) sits on the one band it was drilled from, and its
    /// depth reading (rectangle) sits on the Face rectangle, flush to that same edge and
    /// growing inward toward the panel center.
    /// </summary>
    internal static class BorePreviewRenderer
    {
        /// <summary>The four side bands as (horizontal, near): Top, Bottom, Left, Right.</summary>
        private static readonly (bool Horizontal, bool Near)[] Bands = [(true, true), (true, false), (false, true), (false, false)];

        /// <summary>
        /// The shapes of every bore in <paramref name="programs"/>. A bore whose tool can't be
        /// resolved (or has a non-positive diameter) is skipped. A face bore's five projections
        /// (the circle + its four edge-band rectangles), or an edge bore's two (its band circle +
        /// Face rectangle), toggle red together on click, since they represent the same physical
        /// hole.
        /// </summary>
        public static IReadOnlyList<UIElement> DrawBores(IEnumerable<XncProgram> programs, FaceLayout layout, BoreBrushes brushes)
        {
            var shapes = new List<UIElement>();

            foreach (var program in programs)
            {
                foreach (var bore in program.Bores)
                {
                    var diameter = program.Tools
                        .FirstOrDefault(t => t.Name == bore.ToolName)?.Diameter ?? 0;

                    if (diameter <= 0)
                    {
                        continue;
                    }

                    if (bore.Surface == BoreSurface.Face)
                    {
                        DrawFaceBore(shapes, program, bore, diameter, layout, brushes);
                    }
                    else
                    {
                        DrawEdgeBore(shapes, program, bore, diameter, layout, brushes);
                    }
                }
            }

            return shapes;
        }

        /// <summary>
        /// A face bore: a circle with crossed center lines on the Face rectangle - filled with
        /// <see cref="BoreBrushes.ThroughFill"/> when it goes through - plus a depth rectangle with
        /// one center line on each side band, measured from the band edge of the program's side.
        /// Colored by the owning program's <c>Side</c>.
        /// </summary>
        private static void DrawFaceBore(
            List<UIElement> shapes,
            XncProgram program,
            XncBore bore,
            double diameter,
            FaceLayout layout,
            BoreBrushes brushes)
        {
            var sink = new OverlayShapeSink(new BoreTag(program, bore), program.Side ? brushes.SideTrue : brushes.SideFalse);
            var diameterPx = layout.MmToPx(diameter);
            var radiusPx = diameterPx / 2;
            var depthPx = layout.MmToPx(bore.Depth);
            var overshootPx = layout.MmToPx(brushes.CenterLineOvershootMm);
            var centre = new Vec2(layout.XPx(bore.X), layout.YPx(bore.Y));

            // Front (circle) projection. Only it shows the through-bore background; the side
            // (rectangle) projections stay transparent regardless of Through.
            var circle = sink.AddCircle(shapes, centre, diameterPx, brushes.Bold);

            if (bore.Through)
            {
                circle.Fill = brushes.ThroughFill;
            }

            sink.AddCrossArms(shapes, centre, radiusPx + overshootPx, brushes.Thin(brushes.CrossLineDashPx(diameter, layout)));

            // Edge-band (side) projections: one rectangle + one center line per band. The
            // circle's crossed center lines are keyed on the bore's diameter, a rectangle's
            // (depth-axis) center line on its depth - independent thresholds, per the drawing spec.
            var depthLineStyle = brushes.Thin(brushes.DepthLineDashPx(bore.Depth, layout));

            foreach (var (horizontal, near) in Bands)
            {
                var along = horizontal ? centre.X : centre.Y;
                var (rect, centreLine) = PartPreviewOverlayGeometry.SideRectangleRange(
                    along - radiusPx, along + radiusPx, depthPx, overshootPx, layout, program.Side, horizontal, near);

                sink.AddRectangle(shapes, rect, brushes.Thin());
                sink.AddCentreLine(shapes, centreLine, depthLineStyle);
            }

            sink.AttachClickToggle(brushes.Selected);
        }

        /// <summary>
        /// An edge-drilled bore: a circle with crossed center lines on the one band it was drilled
        /// from, plus a rectangle with one center line on the Face rectangle, flush to that same
        /// edge and growing inward. Mirrors <see cref="DrawFaceBore"/>'s circle/rectangle roles but
        /// swaps which projection carries which shape, and always colors with
        /// <see cref="BoreBrushes.SideTrue"/> regardless of the program's own <c>Side</c> (which
        /// still decides the band edge the circle's <c>z</c> is measured from). Neither shape uses
        /// <see cref="BoreBrushes.ThroughFill"/> - both stay outline-only.
        /// </summary>
        private static void DrawEdgeBore(
            List<UIElement> shapes,
            XncProgram program,
            XncBore bore,
            double diameter,
            FaceLayout layout,
            BoreBrushes brushes)
        {
            var sink = new OverlayShapeSink(new BoreTag(program, bore), brushes.SideTrue);
            var diameterPx = layout.MmToPx(diameter);
            var radiusPx = diameterPx / 2;
            var depthPx = layout.MmToPx(bore.Depth);
            var overshootPx = layout.MmToPx(brushes.CenterLineOvershootMm);
            var horizontal = bore.Surface is BoreSurface.Top or BoreSurface.Bottom;
            var near = bore.Surface is BoreSurface.Top or BoreSurface.Left;
            var alongPx = layout.AlongPx(horizontal, horizontal ? bore.X : bore.Y);

            // Side-band (circle) projection: the bore's true cross-section, z into the band from
            // its inner edge when the program machines the front, else from its outer edge.
            var acrossPx = layout.Band(horizontal, near).At(layout.MmToPx(bore.Z), program.Side);
            var centre = horizontal ? new Vec2(alongPx, acrossPx) : new Vec2(acrossPx, alongPx);

            sink.AddCircle(shapes, centre, diameterPx, brushes.Bold);
            sink.AddCrossArms(shapes, centre, radiusPx + overshootPx, brushes.Thin(brushes.CrossLineDashPx(diameter, layout)));

            // Front (rectangle) projection: the depth reading, always flush to the bore's own
            // physical Face edge and growing inward toward the panel center.
            var (rect, centreLine) = PartPreviewOverlayGeometry.FaceRectangleRange(
                alongPx - radiusPx, alongPx + radiusPx, depthPx, overshootPx, layout, bore.Surface, horizontal);

            sink.AddRectangle(shapes, rect, brushes.Thin());
            sink.AddCentreLine(shapes, centreLine, brushes.Thin(brushes.DepthLineDashPx(bore.Depth, layout)));

            sink.AttachClickToggle(brushes.Selected);
        }
    }
}
