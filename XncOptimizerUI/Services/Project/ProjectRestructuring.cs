using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>Operations that reorganize the open project and save it as a new file.</summary>
    public sealed class ProjectRestructuring(ProjectDocument document, IConfigService config) : IProjectRestructuring
    {
        public OperationResult GroupIdenticalElements() => new IdenticalPartsGrouper(document).Group();

        public OperationResult PrepForSplitAlongX(IReadOnlyCollection<string> partIds) => new SplitAlongXPreparer(document, config).Prepare(partIds);

        public OperationResult ReplaceXncPrograms(Part sourcePart, IList<Part> targetParts) => new XncProgramReplacer(document).Replace(sourcePart, targetParts);
    }
}
