namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// One element of a milling contour. The start point is implicit: it is the end
    /// point of the previous segment, or the contour entry for the first segment.
    /// </summary>
    public abstract class XncMillingSegment
    {
        /// <summary>Segment end point (the <c>x</c>/<c>y</c> attributes).</summary>
        public XncPoint End { get; init; }
    }
}
