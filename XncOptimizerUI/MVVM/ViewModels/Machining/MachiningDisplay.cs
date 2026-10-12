using System.Globalization;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>How the program tables name sides and tool positions.</summary>
    public static class MachiningDisplay
    {
        /// <summary>The face a program machines: its <c>side</c> attribute, true for the front.</summary>
        public static string SideName(bool front) => front ? "Front" : "Back";

        /// <summary>The surface a bore is drilled into; a face bore is on its program's side.</summary>
        public static string BoreSide(XncBore bore, XncProgram program) => bore.Surface switch
        {
            BoreSurface.Face => SideName(program.Side),
            _ => bore.Surface.ToString()
        };

        /// <summary>The surface a groove is cut into, from its <c>p</c> code; a face groove (0) is on its program's side.</summary>
        public static string GrooveSide(XncGrooving groove, XncProgram program) => groove.SideCode switch
        {
            0 => SideName(program.Side),
            1 => "Left",
            2 => "Right",
            3 => "Top",
            4 => "Bottom",
            _ => groove.SideCode.ToString(CultureInfo.InvariantCulture)
        };

        /// <summary>The TCL column: the tool position relative to the path (the <c>c</c> attribute) as one letter.</summary>
        public static string ToolPositionCode(ToolPosition position) => position switch
        {
            ToolPosition.Center => "C",
            ToolPosition.Left => "L",
            ToolPosition.Right => "R",
            ToolPosition.Pocket => "P",
            _ => position.ToString()
        };
    }
}
