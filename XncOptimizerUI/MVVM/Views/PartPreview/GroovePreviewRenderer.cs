using System.Windows;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.Views.PartPreview
{
    /// <summary>
    /// Draws every groove on top of the part preview built by <see cref="PartPreviewRenderer.DrawPart"/>,
    /// like <see cref="BorePreviewRenderer"/>: a front-plane groove by
    /// <see cref="FrontPlaneGrooveRenderer"/>, an edge-plane one by <see cref="EdgePlaneGrooveRenderer"/>.
    /// </summary>
    internal static class GroovePreviewRenderer
    {
        /// <summary>
        /// The shapes of every groove in <paramref name="programs"/>. A front-plane groove's five
        /// projections (its Face rectangle + its four edge-band rectangles), or an edge-plane
        /// groove's five (its band rectangle + opposite-band mirror + Face rectangle + two
        /// cross-band markers), toggle red together on click, since they represent the same
        /// physical groove.
        /// </summary>
        public static IReadOnlyList<UIElement> DrawGrooves(IEnumerable<XncProgram> programs, FaceLayout layout, BoreBrushes brushes)
        {
            var shapes = new List<UIElement>();

            foreach (var program in programs)
            {
                foreach (var groove in program.Groovings)
                {
                    if (groove.SideCode == 0)
                    {
                        FrontPlaneGrooveRenderer.Draw(shapes, program, groove, layout, brushes);
                    }
                    else
                    {
                        EdgePlaneGrooveRenderer.Draw(shapes, program, groove, layout, brushes);
                    }
                }
            }

            return shapes;
        }
    }
}
