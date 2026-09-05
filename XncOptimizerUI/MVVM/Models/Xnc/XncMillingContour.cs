namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// A milling contour: an entry point (<c>&lt;ms&gt;</c>) followed by its line and
    /// arc segments (<c>&lt;ml&gt;</c>/<c>&lt;mac&gt;</c>).
    /// </summary>
    public class XncMillingContour
    {
        /// <summary>Name of the tool used for the whole contour (the <c>name</c> attribute).</summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>Contour entry point (the <c>x</c>/<c>y</c> attributes).</summary>
        public XncPoint Entry { get; init; }

        /// <summary>Cutting depth at the entry point, in millimetres (the <c>dp</c> attribute).</summary>
        public double EntryDepth { get; init; }

        /// <summary>Tool position relative to the contour centre line (the <c>c</c> attribute).</summary>
        public ToolPosition Position { get; init; }

        /// <summary>Lead-in code (the <c>in</c> attribute); 0 means none.</summary>
        public int LeadIn { get; init; }

        /// <summary>Lead-out code (the <c>out</c> attribute); 0 means none.</summary>
        public int LeadOut { get; init; }

        /// <summary>Start offset in the XY plane (the optional <c>sxy</c> attribute), or null when absent.</summary>
        public double? StartOffsetXY { get; init; }

        /// <summary>Line and arc moves that make up the contour, in order.</summary>
        public IReadOnlyList<XncMillingSegment> Segments { get; init; } = [];
    }
}
