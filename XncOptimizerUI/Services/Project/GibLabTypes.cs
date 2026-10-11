namespace XncOptimizerUI.Services.Project
{
    /// <summary><c>typeId</c> values of the GibLab <c>&lt;operation&gt;</c> and <c>&lt;good&gt;</c> elements this app works with.</summary>
    internal static class GibLabTypes
    {
        /// <summary>CNC drilling/milling operation; its <c>program</c> attribute carries the XNC program.</summary>
        public const string XncOperation = "XNC";

        /// <summary>Saw-cut operation: lists the parts cut from one sheet, the sheet part last.</summary>
        public const string CutOperation = "CS";

        /// <summary>Edge-banding operation: one band material and the parts using it.</summary>
        public const string BandOperation = "EL";

        public const string ProductGood = "product";

        public const string SheetGood = "sheet";

        public const string BandGood = "band";
    }
}
