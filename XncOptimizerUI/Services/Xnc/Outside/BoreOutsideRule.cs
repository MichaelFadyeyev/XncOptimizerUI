using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;
using static XncOptimizerUI.Services.Xnc.XncAttributeParsing;
using static XncOptimizerUI.Services.Xnc.XncSymbols;

namespace XncOptimizerUI.Services.Xnc.Outside
{
    /// <summary>
    /// Bores are judged by their centre alone (confirmed with the user): outside when the centre lies
    /// outside the part contour, whatever the tool diameter; a centre on a side counts as inside.
    /// <list type="bullet">
    /// <item>Face bore <c>bf</c>: the centre <c>x</c>/<c>y</c> (every hole of an
    /// <c>ac</c>/<c>as</c>/<c>av</c> array), compared with the face rectangle.</item>
    /// <item>Edge bore <c>bl</c>/<c>br</c> (<c>bt</c>/<c>bb</c>): the centre <c>y</c> (<c>x</c>)
    /// and <c>z</c> (<c>dz/2</c> for <c>m="true"</c>), compared with the edge rectangle
    /// <c>dy</c> (<c>dx</c>) x <c>dz</c>. Edge-bore arrays are not resolved.</item>
    /// </list>
    /// </summary>
    internal sealed class BoreOutsideRule : IOutsideRule
    {
        public OutsideOperationKinds Kind => OutsideOperationKinds.Bores;

        public IEnumerable<OutsideElement> Candidates(XElement program) =>
            program.Elements()
                .Where(e => XncTags.IsBore(e.Name.LocalName))
                .Select(e => new OutsideElement(e, [], Kind));

        public bool? IsOutside(OutsideElement candidate, PartVolume part, XncSymbolTable symbols, double toolDiameter)
        {
            var bore = candidate.Element;

            if (!(Eval(bore.GetDpValue(), symbols) > 0d))
            {
                return false; // a hole that does not go in is not judged as outside
            }

            return bore.Name.LocalName == XncTags.FaceBore
                ? FaceBoreCentres(bore, symbols)?.All(c => IsCentreOutside(c, part.Face))
                : IsEdgeBoreOutside(bore, part, symbols);
        }

        private static bool? IsEdgeBoreOutside(XElement bore, PartVolume part, XncSymbolTable symbols)
        {
            if (ArrayCount(bore) != 1)
            {
                return null; // the step direction of an edge-bore array is unknown
            }

            var z = ParseBool(bore.GetMValue(), false)
                ? part.Dz / 2d
                : Eval(bore.GetZValue(), symbols);

            return bore.Name.LocalName is XncTags.LeftBore or XncTags.RightBore
                ? IsCentreOutside(new Vec2(Eval(bore.GetYValue(), symbols), z), Box.Of(part.Dy, part.Dz))
                : IsCentreOutside(new Vec2(Eval(bore.GetXValue(), symbols), z), Box.Of(part.Dx, part.Dz));
        }

        /// <summary>Every hole of a face bore: <c>ac</c> holes <c>as</c> apart along X, or along Y for <c>av="true"</c>.</summary>
        private static IReadOnlyList<Vec2>? FaceBoreCentres(XElement bore, XncSymbolTable symbols)
        {
            var first = EvalPoint(bore, symbols);

            if (ArrayCount(bore) is not { } holes)
            {
                return null;
            }

            if (holes == 1)
            {
                return [first];
            }

            var step = Eval(bore.Attribute("as")?.Value, symbols);
            var direction = ParseBool(bore.GetAvValue(), false) ? new Vec2(0d, 1d) : new Vec2(1d, 0d);

            return Enumerable.Range(0, holes).Select(i => first + (direction * (step * i))).ToList();
        }

        /// <summary>The bore centre lies outside the box; a centre on a side counts as inside.</summary>
        private static bool IsCentreOutside(Vec2 centre, Box box) => box.DistanceTo(centre) > PlanarGeometry.Tolerance;
    }
}
