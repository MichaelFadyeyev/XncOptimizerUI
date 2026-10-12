using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;
using static XncOptimizerUI.Services.Xnc.Geometry.ContourOffsetGeometry;
using static XncOptimizerUI.Services.Xnc.XncAttributeParsing;
using static XncOptimizerUI.Services.Xnc.XncMillElements;
using static XncOptimizerUI.Services.Xnc.XncSymbols;

namespace XncOptimizerUI.Services.Xnc.Outside
{
    /// <summary>
    /// A closed pocket (<c>c="3"</c>) removes the area its contour encloses; any other mill
    /// everything within half the tool diameter of the tool-centre path - the centre line for
    /// <c>c="0"</c>, the line shifted by half the diameter to the tool side for <c>c="1"</c>/<c>c="2"</c>
    /// (sides as the part preview places them). Arcs and ellipses are flattened to chords, with the
    /// chord error added. Lead-in/out moves (<c>in</c>/<c>out</c>, <c>sxy</c>) are not modelled.
    /// </summary>
    internal sealed class MillOutsideRule : IOutsideRule
    {
        /// <summary>Chords an ellipse is flattened to; its chord error is added to the tool reach.</summary>
        private const int EllipseSegments = 2048;

        private const double Tolerance = PlanarGeometry.Tolerance;

        /// <summary>What a mill removes on the face.</summary>
        private abstract record MillFootprint;

        /// <summary>Everything within <paramref name="Reach"/> of the tool-centre path <paramref name="ToolCentres"/> (flattened to chords).</summary>
        private sealed record SweptFootprint(IReadOnlyList<Primitive> ToolCentres, double Reach) : MillFootprint;

        /// <summary>
        /// The area a closed pocket contour encloses (flattened to chords; the true outline lies at most
        /// <paramref name="ChordError"/> beyond them). The tool stays inside, so nothing beyond the contour is cut.
        /// </summary>
        private sealed record PocketFootprint(IReadOnlyList<Primitive> Boundary, double ChordError) : MillFootprint;

        /// <summary>A mill's programmed centre line in authored order, and how far the real shape may lie from it (an ellipse's chords).</summary>
        private sealed record CentreLine(IReadOnlyList<Primitive> Path, double ShapeError);

        public OutsideOperationKinds Kind => OutsideOperationKinds.Mills;

        public IEnumerable<OutsideElement> Candidates(XElement program) =>
            CollectMills(program).Select(m => new OutsideElement(m.Head, m.Segments, Kind));

        public bool? IsOutside(OutsideElement candidate, PartVolume part, XncSymbolTable symbols, double toolDiameter)
        {
            var head = candidate.Element;

            if (!(Eval(head.GetDpValue(), symbols) > 0d) && !candidate.Segments.Any(s => s.GetDpValue() is { } dp && Eval(dp, symbols) > 0d))
            {
                return false;
            }

            var centreLine = head.Name.LocalName switch
            {
                XncTags.Rectangle => RectangleCentreLine(head, symbols),
                XncTags.Ellipse => EllipseCentreLine(head, symbols),
                _ => ContourCentreLine(candidate, symbols),
            };

            return centreLine is null ? null : Footprint(head, centreLine, toolDiameter) switch
            {
                PocketFootprint pocket => IsPocketOutside(pocket, part.Face),
                SweptFootprint swept => IsSweptOutside(swept, part.Face),
                _ => null,
            };
        }

        #region Centre lines

        private static CentreLine? ContourCentreLine(OutsideElement mill, XncSymbolTable symbols) =>
            mill.Segments.Count > 0 && ReadPath(ReadPoint(mill.Element, symbols), mill.Segments, symbols) is { Count: > 0 } path
                ? new CentreLine(path, 0d)
                : null;

        private static CentreLine? RectangleCentreLine(XElement mr, XncSymbolTable symbols)
        {
            var halfLength = Eval(mr.GetLengthValue(), symbols) / 2d;
            var halfWidth = Eval(mr.GetWidthValue(), symbols) / 2d;

            if (halfLength <= Tolerance || halfWidth <= Tolerance)
            {
                return null;
            }

            var radius = mr.GetRValue() is { } rawRadius
                ? Math.Clamp(Eval(rawRadius, symbols), 0d, Math.Min(halfLength, halfWidth))
                : 0d;

            var path = MillShapeGeometry.RoundedRectangle(halfLength, halfWidth, radius)
                .Select(p => MillShapeGeometry.Place(p, ReadPoint(mr, symbols), ReadAngle(mr, symbols)))
                .ToList();

            return new CentreLine(path, 0d);
        }

        private static CentreLine? EllipseCentreLine(XElement me, XncSymbolTable symbols)
        {
            // <me> l/w are semi-axes.
            var semiX = Eval(me.GetLengthValue(), symbols);
            var semiY = Eval(me.GetWidthValue(), symbols);

            if (semiX <= Tolerance || semiY <= Tolerance)
            {
                return null;
            }

            var path = MillShapeGeometry.EllipseChords(semiX, semiY, EllipseSegments)
                .Select(p => MillShapeGeometry.Place(p, ReadPoint(me, symbols), ReadAngle(me, symbols)))
                .ToList();

            return new CentreLine(path, MillShapeGeometry.EllipseChordSagitta(semiX, semiY, EllipseSegments));
        }

        private static double ReadAngle(XElement head, XncSymbolTable symbols) =>
            head.GetAValue() is { } angle ? Eval(angle, symbols) : 0d;

        #endregion

        #region Footprint

        /// <summary>
        /// What the mill removes: a closed pocket (<c>c="3"</c>) the area inside its contour; any other
        /// mill the tool sweep around its tool-centre path - the centre line itself for a centred tool,
        /// or the line shifted by half the diameter to the tool side for <c>c="1"</c>/<c>c="2"</c>
        /// (the whole diameter around the line when that shift does not resolve).
        /// </summary>
        private static MillFootprint Footprint(XElement head, CentreLine centreLine, double diameter)
        {
            var position = ParsePositionCode(head.GetCValue());

            if (position == ToolPosition.Pocket && IsClosed(centreLine.Path))
            {
                var (boundary, boundaryError) = ChordPath.Flatten(centreLine.Path);

                return new PocketFootprint(boundary, boundaryError + centreLine.ShapeError);
            }

            var (toolCentres, reach) = AuthoredToolSide(head, centreLine.Path, position) is { } side
                && ShiftToSide(centreLine.Path, diameter / 2d, side) is { } shifted
                    ? (shifted, diameter / 2d)
                    : (centreLine.Path, ToolReach(position, diameter));

            var (chords, error) = ChordPath.Flatten(toolCentres);

            return new SweptFootprint(chords, reach + error + centreLine.ShapeError);
        }

        /// <summary>How far from the centre line the tool cuts: half its diameter centred on the line, the whole diameter beside it.</summary>
        private static double ToolReach(ToolPosition position, double diameter) => position switch
        {
            ToolPosition.Right or ToolPosition.Left => diameter,
            _ => diameter / 2d,
        };

        /// <summary>
        /// Side of the authored <paramref name="path"/> the tool runs on for <c>c="1"</c> (right) and
        /// <c>c="2"</c> (left), as the part preview places it: relative to the travel of an open
        /// contour (<c>fwd</c>) and of an <c>&lt;mr&gt;</c>/<c>&lt;me&gt;</c>
        /// (<see cref="TravelsCounterClockwise"/>), relative to the declared order of a closed
        /// <c>&lt;ms&gt;</c> contour. <c>null</c> for a centred tool or a pocket.
        /// </summary>
        private static MillOffsetSide? AuthoredToolSide(XElement head, IReadOnlyList<Primitive> path, ToolPosition position)
        {
            if (position is not (ToolPosition.Right or ToolPosition.Left))
            {
                return null;
            }

            var forward = ReadForward(head);
            var reversed = head.Name.LocalName == XncTags.ContourStart
                ? !IsClosed(path) && !forward
                : (OperatorSignedArea(path) > 0d) != TravelsCounterClockwise(forward, isRectangleOrEllipse: true);

            return (position == ToolPosition.Right) != reversed ? MillOffsetSide.Right : MillOffsetSide.Left;
        }

        /// <summary>
        /// <paramref name="path"/> shifted by <paramref name="distance"/> to <paramref name="side"/> of
        /// its authored direction (ends offset perpendicularly), or <c>null</c> when the shift collapses.
        /// </summary>
        private static IReadOnlyList<Primitive>? ShiftToSide(IReadOnlyList<Primitive> path, double distance, MillOffsetSide side)
        {
            // The forward that makes TryOffset travel the path as authored.
            var travelsAsAuthored = !IsClosed(path) || OperatorSignedArea(path) > 0d;

            return TryOffset(path, travelsAsAuthored, outline: null, distance, side)?.Chain();
        }

        #endregion

        #region Verdict

        /// <summary>The tool never comes within reach of the face.</summary>
        private static bool IsSweptOutside(SweptFootprint swept, Box face) =>
            swept.ToolCentres.All(chord => face.DistanceTo(chord.Start, chord.End) >= swept.Reach - Tolerance);

        /// <summary>
        /// The pocket area and the face share no area: no contour chord enters the face (shrunk by the
        /// tolerance and the chord error, so a contour lying on a face side counts as outside), and
        /// the contour does not enclose the face - with no chord inside it, the face lies either
        /// completely inside the contour or completely outside it, so testing its centre is enough.
        /// </summary>
        private static bool IsPocketOutside(PocketFootprint pocket, Box face)
        {
            var core = face.Inset(Tolerance + pocket.ChordError);

            return pocket.Boundary.All(chord => core.DistanceTo(chord.Start, chord.End) > 0d)
                && !ChordPath.Encloses(pocket.Boundary, face.Centre);
        }

        #endregion
    }
}
