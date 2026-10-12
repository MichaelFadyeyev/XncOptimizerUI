using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>One row of the selected part's grooves table.</summary>
    public sealed class GrooveRowVM(int number, string side, XncGrooving groove, Action<XncGrooving, bool> onSelectionChanged)
        : MachiningRowVM<XncGrooving>(number, side, groove, onSelectionChanged)
    {
        public string StartX => MachiningNumber.Format(Model.Start.X);

        public string StartY => MachiningNumber.Format(Model.Start.Y);

        public string EndX => MachiningNumber.Format(Model.End.X);

        public string EndY => MachiningNumber.Format(Model.End.Y);

        public string Depth => MachiningNumber.Format(Model.Depth);

        public string Width => MachiningNumber.Format(Model.Width);

        public string ToolToCenterLine => MachiningDisplay.ToolPositionCode(Model.Position);
    }
}
