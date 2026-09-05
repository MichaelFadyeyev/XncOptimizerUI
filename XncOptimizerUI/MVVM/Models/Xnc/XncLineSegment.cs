namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>A straight milling move (<c>&lt;ml&gt;</c>).</summary>
    public class XncLineSegment : XncMillingSegment
    {
        /// <summary>Cutting depth at the end of the segment, in millimetres (the <c>dp</c> attribute).</summary>
        public double Depth { get; init; }
    }
}
