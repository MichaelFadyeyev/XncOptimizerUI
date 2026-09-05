namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// A parsed XNC machining program: the decoded contents of the <c>program</c>
    /// attribute of one <c>&lt;operation typeId="XNC"&gt;</c>. See
    /// <c>.claude/xnc-program-read.md</c> for the format this follows.
    /// </summary>
    public class XncProgram
    {
        /// <summary>Part length in millimetres (<c>&lt;program dx&gt;</c>).</summary>
        public double Dx { get; init; }

        /// <summary>Part width in millimetres (<c>&lt;program dy&gt;</c>).</summary>
        public double Dy { get; init; }

        /// <summary>Part thickness in millimetres (<c>&lt;program dz&gt;</c>).</summary>
        public double Dz { get; init; }

        /// <summary>Which panel face this program machines (the operation's <c>side</c> attribute).</summary>
        public bool Side { get; init; }

        /// <summary>Tools declared by <c>&lt;tool&gt;</c>, in declaration order.</summary>
        public IReadOnlyList<XncTool> Tools { get; init; } = [];

        /// <summary>All bores (<c>bf</c>/<c>bt</c>/<c>bb</c>/<c>bl</c>/<c>br</c>), in document order.</summary>
        public IReadOnlyList<XncBore> Bores { get; init; } = [];

        /// <summary>All groovings (<c>&lt;gr&gt;</c>), in document order.</summary>
        public IReadOnlyList<XncGrooving> Groovings { get; init; } = [];

        /// <summary>All milling contours (<c>&lt;ms&gt;</c> + its segments), in document order.</summary>
        public IReadOnlyList<XncMillingContour> MillingContours { get; init; } = [];

        /// <summary>All rectangular milling primitives (<c>&lt;mr&gt;</c>), in document order.</summary>
        public IReadOnlyList<XncMillingRectangle> MillingRectangles { get; init; } = [];

        /// <summary>
        /// Custom variables declared by <c>&lt;var&gt;</c>, resolved to their numeric values.
        /// Keys are compared case-insensitively.
        /// </summary>
        public IReadOnlyDictionary<string, double> Variables { get; init; } =
            new Dictionary<string, double>();
    }
}
