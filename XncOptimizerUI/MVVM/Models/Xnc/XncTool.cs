namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>A cutting tool declared by a <c>&lt;tool&gt;</c> element.</summary>
    public class XncTool
    {
        /// <summary>Tool key, referenced by the <c>name</c> attribute of bores, grooves and millings.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Tool diameter in millimetres (the <c>d</c> attribute).</summary>
        public double Diameter { get; init; }
    }
}
