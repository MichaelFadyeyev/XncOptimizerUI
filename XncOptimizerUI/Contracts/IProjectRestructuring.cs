using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.Contracts
{
    /// <summary>
    /// Operations that reorganize the open project and save the result as a new file next to it
    /// (which becomes <see cref="IProjectSession.FullPath"/>); the source file is never overwritten.
    /// </summary>
    public interface IProjectRestructuring
    {
        /// <summary>
        /// Groups XNC operations with the same program on parts with the same band materials under one
        /// group code, renames and renumbers the parts accordingly and moves them into one product good.
        /// Saved with the <c>_opt</c> suffix. Refused when the file is already grouped or has no XNC.
        /// </summary>
        OperationResult GroupIdenticalElements();

        /// <summary>
        /// Prepares the parts with the given ids to be cut as double-width blanks and split along X:
        /// width <c>2w + saw kerf</c>, count halved (rounded up), bores mirrored onto the second half.
        /// Saved with the <c>_spl</c> suffix.
        /// </summary>
        OperationResult PrepForSplitAlongX(IReadOnlyCollection<string> partIds);

        /// <summary>
        /// Copies <paramref name="sourcePart"/>'s XNC programs onto every target, face by face, after
        /// checking each target is identical (size, bands, faces/turn). Nothing is copied unless all pass.
        /// Saved with the <c>_ren</c> suffix.
        /// </summary>
        OperationResult ReplaceXncPrograms(Part sourcePart, IList<Part> targetParts);
    }
}
