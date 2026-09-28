namespace XncOptimizerUI.Contracts
{
    public interface IConfigService
    {
        decimal SawWidth { get; }

        IReadOnlyList<decimal> MillingToolDiams { get; }

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
