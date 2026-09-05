using System.Globalization;

namespace XncOptimizerUI.MVVM.Models.Xnc
{
    /// <summary>An (X, Y) position in the part frame, in millimetres, with expressions already resolved.</summary>
    public readonly record struct XncPoint(double X, double Y)
    {
        public override string ToString() =>
            string.Create(CultureInfo.InvariantCulture, $"({X}, {Y})");
    }
}
