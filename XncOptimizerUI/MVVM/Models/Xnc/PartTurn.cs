namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// Absolute part orientation on the XNC machine: the operation <c>turn</c> code, clockwise
    /// quarter turns in the operator (preview) frame (see <c>IProjectService.RotatePart</c>).
    /// </summary>
    public enum PartTurn
    {
        /// <summary>Not turned (<c>turn="0"</c>).</summary>
        Deg0 = 0,

        /// <summary>Turned 90° clockwise (<c>turn="1"</c>).</summary>
        Deg90 = 1,

        /// <summary>Turned 180° (<c>turn="2"</c>).</summary>
        Deg180 = 2,

        /// <summary>Turned 270° clockwise (<c>turn="3"</c>).</summary>
        Deg270 = 3
    }
}
