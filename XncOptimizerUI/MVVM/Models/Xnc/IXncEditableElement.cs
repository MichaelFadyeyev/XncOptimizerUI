namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>A groove or mill head, edited by its table through its <see cref="Source"/>.</summary>
    public interface IXncEditableElement : IXncMachiningElement
    {
        /// <summary>Position among the program's elements of the same tag and the authored attribute texts.</summary>
        XncElementSource Source { get; }
    }
}
