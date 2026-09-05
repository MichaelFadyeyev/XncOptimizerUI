namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>
    /// The panel surface a bore is drilled into. Encoded in the program element name:
    /// <c>bf</c> &#8594; <see cref="Face"/>, <c>bt</c> &#8594; <see cref="Top"/>,
    /// <c>bb</c> &#8594; <see cref="Bottom"/>, <c>bl</c> &#8594; <see cref="Left"/>,
    /// <c>br</c> &#8594; <see cref="Right"/>.
    /// </summary>
    public enum BoreSurface
    {
        Face,
        Top,
        Bottom,
        Left,
        Right
    }
}
