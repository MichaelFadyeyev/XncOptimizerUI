namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// Which mills a mill path offset processes (see <c>IProjectService.OffsetMillPaths</c>).
    /// </summary>
    [Flags]
    public enum MillPathKinds
    {
        None = 0,

        /// <summary><c>&lt;ms&gt;</c> contours whose last segment does not return to the entry point.</summary>
        Open = 1,

        /// <summary>Closed <c>&lt;ms&gt;</c> contours, <c>&lt;mr&gt;</c> rectangles and <c>&lt;me&gt;</c> ellipses.</summary>
        Closed = 2,

        All = Open | Closed
    }
}
