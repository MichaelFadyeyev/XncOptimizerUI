namespace XncOptimizerUI.Configuration
{
    public class AppOptions
    {
        public decimal SawWidth { get; set; } = 4.0m;
        public List<string> LabelsToProcess { get; set; } = ["поріз.2х40"];
        public int LastLabelToProcessSelectedIndex { get; set; } = 0;
    }
}
