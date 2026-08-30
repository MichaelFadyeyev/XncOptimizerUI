namespace XncOptimizerUI.Contracts
{
    public interface IConfigService
    {
        decimal SawWidth { get; }

        IReadOnlyList<string> LabelsToProcess { get; }

        string GetLastLabelToProcessSelected();

        void AddLabelToProcess(string newLabel);

        void DeleteLabelToProcess(string labelToRemove);

        void UpdateSawWidth(decimal newWidth);

        void UpdateLastLabelToProcessSelectedIndex(string label);
    }
}
