namespace XncOptimizerUI.Configuration
{
    public class AppOptions
    {
        public decimal SawWidth { get; set; } = 4.0m;
        public List<string> LabelsToProcess { get; set; } = ["поріз.2х40"];
        public int LastLabelToProcessSelectedIndex { get; set; } = 0;

        public List<decimal> GroovingToolWidths { get; set; } = [2.8m, 3.2m];

        public List<decimal> MillingToolDiams { get; set; } = [6.0m, 10.0m, 20.0m];

        /// <summary>
        /// Part rotation keeps an edge groove's <c>c</c> (tool-to-centre-line position) as is,
        /// like GibLab does, instead of flipping Right/Left to keep the groove on its side.
        /// </summary>
        public bool NeverFlipTclOnTurn { get; set; } = false;
    }
    
}
