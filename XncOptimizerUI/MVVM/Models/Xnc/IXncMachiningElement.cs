namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>A program element cut with one declared tool: a bore, a groove or a mill.</summary>
    public interface IXncMachiningElement
    {
        /// <summary>Name of the tool used (the <c>name</c> attribute); its diameter is <c>tool.dia</c> in the element's expressions.</summary>
        string ToolName { get; }
    }
}
