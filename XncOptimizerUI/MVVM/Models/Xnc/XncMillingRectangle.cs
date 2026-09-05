namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// A rectangular milling primitive (<c>&lt;mr&gt;</c>) &#8212; a self-contained pocket or
    /// frame, not a contour of segments. Attribute meanings are inferred from the fixture
    /// (see <c>.claude/xnc-program-read.md</c> &#167;6.6).
    /// </summary>
    public class XncMillingRectangle
    {
        /// <summary>Name of the tool used (the <c>name</c> attribute).</summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>Rectangle reference point (the <c>x</c>/<c>y</c> attributes); corner or centre is unconfirmed.</summary>
        public XncPoint Origin { get; init; }

        /// <summary>Rectangle length along its local X, in millimetres (the <c>l</c> attribute).</summary>
        public double Length { get; init; }

        /// <summary>Rectangle width along its local Y, in millimetres (the <c>w</c> attribute).</summary>
        public double Width { get; init; }

        /// <summary>Rotation angle in degrees (the <c>a</c> attribute).</summary>
        public double Angle { get; init; }

        /// <summary>Corner radius in millimetres (the <c>r</c> attribute); 0 for sharp corners.</summary>
        public double CornerRadius { get; init; }

        /// <summary>Cutting depth in millimetres (the <c>dp</c> attribute).</summary>
        public double Depth { get; init; }

        /// <summary>Tool position relative to the rectangle outline (the <c>c</c> attribute); <see cref="ToolPosition.Pocket"/> clears the interior.</summary>
        public ToolPosition Position { get; init; }

        /// <summary>Lead-in code (the <c>in</c> attribute); 0 means none.</summary>
        public int LeadIn { get; init; }

        /// <summary>Lead-out code (the <c>out</c> attribute); 0 means none.</summary>
        public int LeadOut { get; init; }

        /// <summary>Start offset in the XY plane (the optional <c>sxy</c> attribute), or null when absent.</summary>
        public double? StartOffsetXY { get; init; }
    }
}
