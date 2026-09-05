namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>A straight groove cut by a <c>&lt;gr&gt;</c> element.</summary>
    public class XncGrooving
    {
        /// <summary>Name of the tool used (the <c>name</c> attribute).</summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>Groove start point (the <c>x1</c>/<c>y1</c> attributes).</summary>
        public XncPoint Start { get; init; }

        /// <summary>Groove end point (the <c>x2</c>/<c>y2</c> attributes).</summary>
        public XncPoint End { get; init; }

        /// <summary>Groove depth in millimetres (the <c>dp</c> attribute).</summary>
        public double Depth { get; init; }

        /// <summary>Groove width in millimetres (the <c>t</c> attribute); may exceed the tool diameter.</summary>
        public double Width { get; init; }

        /// <summary>Tool position relative to the groove centre line (the <c>c</c> attribute).</summary>
        public ToolPosition Position { get; init; }

        /// <summary>Free-text label from the <c>comment</c> attribute.</summary>
        public string Comment { get; init; } = string.Empty;
    }
}
