namespace XncOptimizerUI.MVVM.Models.Xnc
{
    public class XncMillingEllipse
    {
        public string ToolName { get; init; } = string.Empty;
        public XncPoint Center { get; init; }
        public double Length { get; init; }
        public double Width { get; init; }
        public double Angle { get; init; }
        public double Depth { get; init; }
        public ToolPosition Position { get; init; }

        /// <summary>Traversal direction (the <c>fwd</c> attribute, absent = <c>true</c>): <c>true</c> = clockwise, <c>false</c> = counter-clockwise (operator's view; pockets included) - the opposite of a closed <c>&lt;ms&gt;</c> contour.</summary>
        public bool Forward { get; init; } = true;

        public int LeadIn { get; init; }
        public int LeadOut { get; init; }
        public double? StartOffsetXY { get; init; }
    }
}
