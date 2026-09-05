namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// Tool position relative to the tool-path centre line (the program's <c>c</c>
    /// attribute). <see cref="Pocket"/> only occurs on milling entries.
    /// </summary>
    public enum ToolPosition
    {
        Center = 0,
        Right = 1,
        Left = 2,
        Pocket = 3
    }
}
