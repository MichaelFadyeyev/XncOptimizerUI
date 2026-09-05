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

        /// <summary>True when the hole goes all the way through (the <c>av</c> flag on face bores).</summary>
        public bool Through { get; init; }
    }
}
