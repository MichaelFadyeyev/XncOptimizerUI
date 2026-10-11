namespace XncOptimizerUI.Contracts
{
    /// <summary>The project services, by role, as one dependency of the view model.</summary>
    public sealed record ProjectServices(
        IProjectSession Session,
        IProjectReader Reader,
        IProjectEditor Editor,
        IProjectRestructuring Restructuring,
        IXncBatchOperations Batch);
}
