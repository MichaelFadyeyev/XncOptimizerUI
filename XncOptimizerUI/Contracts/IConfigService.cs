namespace XncOptimizerUI.Contracts
{
    public interface IConfigService
    {
        decimal SawWidth { get; }

        IReadOnlyList<decimal> MillingToolDiams { get; }

        /// <summary>Grooving cutter widths; the first one cuts the grooves made by mills -> grooves conversion.</summary>
        IReadOnlyList<decimal> GroovingToolWidths { get; }

        IReadOnlyList<string> LabelsToProcess { get; }

        /// <summary>Part rotation never flips an edge groove's TCL (<c>c</c>) - GibLab's behavior.</summary>
        bool NeverFlipTclOnTurn { get; }

        void UpdateNeverFlipTclOnTurn(bool value);

        string GetLastLabelToProcessSelected();

        void AddLabelToProcess(string newLabel);

        void DeleteLabelToProcess(string labelToRemove);

        void UpdateSawWidth(decimal newWidth);

        void UpdateLastLabelToProcessSelectedIndex(string label);
    }
}
