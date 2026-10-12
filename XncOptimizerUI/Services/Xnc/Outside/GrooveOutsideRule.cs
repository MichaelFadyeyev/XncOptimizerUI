using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;
using static XncOptimizerUI.Services.Xnc.XncAttributeParsing;
using static XncOptimizerUI.Services.Xnc.XncSymbols;

namespace XncOptimizerUI.Services.Xnc.Outside
{
    /// <summary>
    /// A face groove (<c>p="0"</c>) removes its <c>t</c>-wide footprint along start-end, placed by
    /// <c>c</c> exactly as the part preview draws it; an edge groove (<c>p</c> 1..4) its span along
    /// the edge and the band <c>z +- t</c> through the thickness (<c>c</c> ignored, so the band is
    /// never narrower than the cut).
    /// </summary>
    internal sealed class GrooveOutsideRule : IOutsideRule
    {
        private const double Tolerance = PlanarGeometry.Tolerance;

        public OutsideOperationKinds Kind => OutsideOperationKinds.Grooves;

        public IEnumerable<OutsideElement> Candidates(XElement program) =>
            program.Elements(XncTags.Groove).Select(e => new OutsideElement(e, [], Kind));

        public bool? IsOutside(OutsideElement candidate, PartVolume part, XncSymbolTable symbols, double toolDiameter)
        {
            var groove = candidate.Element;

            if (!(Eval(groove.GetDpValue(), symbols) > 0d))
            {
                return false;
            }

            var start = new Vec2(Eval(groove.GetX1Value(), symbols), Eval(groove.GetY1Value(), symbols));
            var end = new Vec2(Eval(groove.GetX2Value(), symbols), Eval(groove.GetY2Value(), symbols));
            var width = Eval(groove.GetTValue(), symbols);

            if (!(width > 0d))
            {
                return null;
            }

            return ParsePlane(groove.GetPValue()) switch
            {
                0 => IsFaceGrooveOutside(start, end, width, ParsePositionCode(groove.GetCValue()), part.Face),
                1 or 2 => IsEdgeGrooveOutside(start.Y, end.Y, part.Dy, Eval(groove.GetZValue(), symbols), width, part.Dz),
                3 or 4 => IsEdgeGrooveOutside(start.X, end.X, part.Dx, Eval(groove.GetZValue(), symbols), width, part.Dz),
                _ => null,
            };
        }

        /// <summary>The groove plane <c>p</c> (absent = 0, the face), or <c>null</c> when it is not an integer.</summary>
        private static int? ParsePlane(string? raw) => raw is null ? 0 : TryParseInt(raw);

        /// <summary>The groove footprint (same geometry as the part preview) is separated from the face.</summary>
        private static bool? IsFaceGrooveOutside(Vec2 start, Vec2 end, double width, ToolPosition position, Box face)
        {
            if (start.DistanceTo(end) <= Tolerance)
            {
                return null;
            }

            var normal = ContourOffsetGeometry.RightNormal(end - start);
            var (offsetA, offsetB) = position switch
            {
                ToolPosition.Right => (0d, width),
                ToolPosition.Left => (-width, 0d),
                _ => (-width / 2d, width / 2d),
            };

            Vec2[] footprint =
            [
                start + (normal * offsetA),
                end + (normal * offsetA),
                end + (normal * offsetB),
                start + (normal * offsetB),
            ];

            return face.SeparationFrom(footprint) >= -Tolerance;
        }

        /// <summary>The span along the edge, or the <c>z +- t</c> band through the thickness, misses the edge rectangle.</summary>
        private static bool IsEdgeGrooveOutside(double along1, double along2, double edgeLength, double z, double width, double thickness) =>
            IntervalGap(Math.Min(along1, along2), Math.Max(along1, along2), edgeLength) >= -Tolerance
                || IntervalGap(z - width, z + width, thickness) >= -Tolerance;

        /// <summary>Gap between <c>[low, high]</c> and <c>[0, length]</c>; negative when they overlap.</summary>
        private static double IntervalGap(double low, double high, double length) => Math.Max(-high, low - length);
    }
}
