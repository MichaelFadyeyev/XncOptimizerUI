namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// A circular milling move: centre-defined (<c>&lt;mac&gt;</c>) or radius-defined
    /// (<c>&lt;ma&gt;</c>, whose centre is reconstructed from its chord, radius and <c>dir</c>).
    /// </summary>
    public class XncArcSegment : XncMillingSegment
    {
        /// <summary>Arc centre point (the <c>&lt;mac&gt;</c> <c>cx</c>/<c>cy</c> attributes, or reconstructed for <c>&lt;ma&gt;</c>).</summary>
        public XncPoint Center { get; init; }

        /// <summary>
        /// True when the arc sweeps clockwise in the operator's view - the raw XNC frame mirrored
        /// in Y, i.e. the Y-down frame the part preview draws in. Read from <c>dir="true"</c>
        /// (see <c>xnc-program-format.md</c> &#167;6.4 "Traversal direction").
        /// </summary>
        public bool Clockwise { get; init; }

        /// <summary>Arc radius in millimetres, derived as the distance from centre to end point.</summary>
        public double Radius { get; init; }
    }
}
