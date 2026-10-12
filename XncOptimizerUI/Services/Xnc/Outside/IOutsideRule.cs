using System.Xml.Linq;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;

namespace XncOptimizerUI.Services.Xnc.Outside
{
    /// <summary>One element found outside the part, with the contour segments removed along with it.</summary>
    internal sealed record OutsideElement(XElement Element, IReadOnlyList<XElement> Segments, OutsideOperationKinds Kind);

    /// <summary>The part volume <c>[0, dx] x [0, dy] x [0, dz]</c> in raw program coordinates.</summary>
    internal readonly record struct PartVolume(double Dx, double Dy, double Dz)
    {
        public Box Face => Box.Of(Dx, Dy);
    }

    /// <summary>Decides, for one kind of element, which elements of a program lie outside the part.</summary>
    internal interface IOutsideRule
    {
        /// <summary>The kind of element the rule judges.</summary>
        OutsideOperationKinds Kind { get; }

        /// <summary>The program's elements of <see cref="Kind"/>, in document order.</summary>
        IEnumerable<OutsideElement> Candidates(XElement program);

        /// <summary>
        /// Whether <paramref name="candidate"/> removes no material of <paramref name="part"/>;
        /// <c>null</c> when its geometry does not resolve. <paramref name="symbols"/> has
        /// <c>tool.dia</c> set to <paramref name="toolDiameter"/>, the diameter of its tool.
        /// </summary>
        /// <exception cref="XncProgramFormatException">A value does not resolve to a number.</exception>
        bool? IsOutside(OutsideElement candidate, PartVolume part, XncSymbolTable symbols, double toolDiameter);
    }
}
