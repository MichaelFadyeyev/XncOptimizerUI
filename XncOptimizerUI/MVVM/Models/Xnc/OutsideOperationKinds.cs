namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// Which machining elements "Clean operations" removes when they lie outside the part (see
    /// <c>IProjectService.CleanOutsideOperations</c>).
    /// </summary>
    [Flags]
    public enum OutsideOperationKinds
    {
        None = 0,

        /// <summary>Face and edge bores (<c>bf</c>/<c>bt</c>/<c>bb</c>/<c>bl</c>/<c>br</c>).</summary>
        Bores = 1,

        /// <summary>Groovings (<c>&lt;gr&gt;</c>).</summary>
        Grooves = 2,

        /// <summary><c>&lt;ms&gt;</c> contours with their segments, <c>&lt;mr&gt;</c> rectangles and <c>&lt;me&gt;</c> ellipses.</summary>
        Mills = 4,

        All = Bores | Grooves | Mills
    }
}
