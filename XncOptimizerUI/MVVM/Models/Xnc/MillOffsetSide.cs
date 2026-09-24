namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// Side of the tool traversal direction a mill path is shifted to (see
    /// <c>IProjectService.OffsetMillPaths</c>).
    /// </summary>
    public enum MillOffsetSide
    {
        /// <summary>Shift the path to the right of the traversal direction.</summary>
        Right = 0,

        /// <summary>Shift the path to the left of the traversal direction.</summary>
        Left = 1
    }
}
