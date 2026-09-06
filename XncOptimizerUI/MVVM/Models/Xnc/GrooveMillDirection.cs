namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// Direction of a batch groove &#8596; mill conversion (see
    /// <c>IProjectService.ConvertGroovesAndMills</c>).
    /// </summary>
    public enum GrooveMillDirection
    {
        /// <summary>Replace every axis-parallel <c>&lt;gr&gt;</c> grooving with a single-segment milling contour.</summary>
        GroovesToMills = 0,

        /// <summary>Replace every compliant single-segment milling contour with a <c>&lt;gr&gt;</c> grooving.</summary>
        MillsToGrooves = 1
    }
}
