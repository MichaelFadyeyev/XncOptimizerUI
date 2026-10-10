namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// A single drilling operation (<c>bf</c>/<c>bt</c>/<c>bb</c>/<c>bl</c>/<c>br</c>).
    /// Coordinates are resolved to millimetres in the part frame; for an edge bore the
    /// coordinate of the drilled edge is pinned to 0 or the panel size.
    /// </summary>
    public class XncBore
    {
        /// <summary>Which surface the hole is drilled into (from the element name).</summary>
        public BoreSurface Surface { get; init; }

        /// <summary>Name of the tool used (the <c>name</c> attribute).</summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>Hole centre X in the part frame.</summary>
        public double X { get; init; }

        /// <summary>Hole centre Y in the part frame.</summary>
        public double Y { get; init; }

        /// <summary>Through-thickness position for an edge bore (the <c>z</c> attribute); 0 for a face bore.</summary>
        public double Z { get; init; }

        /// <summary>Drill depth into the surface, in millimetres (the <c>dp</c> attribute).</summary>
        public double Depth { get; init; }

        /// <summary>
        /// True when the hole goes all the way through: <see cref="Depth"/> reaches the panel
        /// dimension it drills into (not carried by any XNC attribute - <c>av</c>/<c>ac</c>/<c>as</c>
        /// describe a repeated-bore array, unrelated to depth).
        /// </summary>
        public bool Through { get; init; }

        /// <summary>Position among the bore elements of its program, in document order.</summary>
        public int Index { get; init; }

        /// <summary>Authored <c>x</c> attribute text (may be an expression such as <c>dx-32</c>); <c>null</c> when absent.</summary>
        public string? XText { get; init; }

        /// <summary>Authored <c>y</c> attribute text; <c>null</c> when absent.</summary>
        public string? YText { get; init; }

        /// <summary>Authored <c>z</c> attribute text; <c>null</c> when absent or pinned to the middle by <c>m="true"</c>.</summary>
        public string? ZText { get; init; }

        /// <summary>Authored <c>dp</c> attribute text.</summary>
        public string? DepthText { get; init; }
    }
}
