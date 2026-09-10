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
        public int LeadIn { get; init; }
        public int LeadOut { get; init; }
        public double? StartOffsetXY { get; init; }
    }
}
