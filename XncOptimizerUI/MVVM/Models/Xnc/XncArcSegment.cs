namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>A circular milling move (<c>&lt;mac&gt;</c>).</summary>
    public class XncArcSegment : XncMillingSegment
    {
        /// <summary>Arc centre point (the <c>cx</c>/<c>cy</c> attributes).</summary>
        public XncPoint Center { get; init; }

        /// <summary>
        /// True when the arc is swept clockwise in the Y-up part frame. Derived from
        /// <c>dir="false"</c> (see <c>.claude/xnc-program-read.md</c> &#167;6.5 &#8212; still to be
        /// confirmed against <c>td-2.project</c>).
        /// </summary>
        public bool Clockwise { get; init; }

        /// <summary>Arc radius in millimetres, derived as the distance from centre to end point.</summary>
        public double Radius { get; init; }
    }
}
