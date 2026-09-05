namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// One element of a milling contour. The start point is implicit: it is the end
    /// point of the previous element (the <c>&lt;ms&gt;</c> entry for the first segment,
    /// otherwise the previous segment's end).
    /// </summary>
    public abstract class XncMillingSegment
    {
        /// <summary>Segment end point (the <c>x</c>/<c>y</c> attributes).</summary>
        public XncPoint End { get; init; }

        /// <summary>
        /// Cutting depth in millimetres at the end of the segment. Comes from the
        /// segment's own <c>dp</c> attribute when present, otherwise carried forward
        /// from the previous segment (or the contour entry depth).
        /// </summary>
        public double Depth { get; init; }
    }
}
