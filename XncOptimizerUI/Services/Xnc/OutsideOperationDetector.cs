using System.Globalization;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Geometry;
using static XncOptimizerUI.Services.Xnc.Geometry.ContourOffsetGeometry;
using static XncOptimizerUI.Services.Xnc.XncMillElements;
using static XncOptimizerUI.Services.Xnc.XncProgramMath;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Finds the machining elements of one decoded XNC <c>&lt;program&gt;</c> that lie outside the
    /// part volume (<c>[0, dx] x [0, dy] x [0, dz]</c>) and never intersect it - the material they
    /// would remove has no common volume with the part (touching a face counts as outside).
    /// <para>Removed material per element (raw program frame, Y-down):</para>
    /// <list type="bullet">
    /// <item>Bores are judged by their centre alone (confirmed with the user): outside when the
    /// centre lies outside the part contour, whatever the tool diameter; a centre on a side counts
    /// as inside.</item>
    /// <item>Face bore <c>bf</c>: the centre <c>x</c>/<c>y</c> (every hole of an
    /// <c>ac</c>/<c>as</c>/<c>av</c> array), compared with the face rectangle.</item>
    /// <item>Edge bore <c>bl</c>/<c>br</c> (<c>bt</c>/<c>bb</c>): the centre <c>y</c> (<c>x</c>)
    /// and <c>z</c> (<c>dz/2</c> for <c>m="true"</c>), compared with the edge rectangle
    /// <c>dy</c> (<c>dx</c>) x <c>dz</c>. Edge-bore arrays are not resolved and kept.</item>
    /// <item>Face groove (<c>p="0"</c>): the <c>t</c>-wide footprint along start-end, placed by
    /// <c>c</c> exactly as the part preview draws it.</item>
    /// <item>Edge groove (<c>p</c> 1..4): its span along the edge, and the band <c>z +- t</c>
    /// through the thickness (<c>c</c> ignored, so the band is never narrower than the cut).</item>
    /// <item>Mills: a closed pocket (<c>c="3"</c>) the area its contour encloses; any other mill
    /// everything within half the tool diameter of the tool-centre path - the centre line for
    /// <c>c="0"</c>, the line shifted by half the diameter to the tool side for <c>c="1"</c>/<c>c="2"</c>
    /// (sides as the part preview places them). Arcs and ellipses are flattened to chords, with
    /// the chord error added. Lead-in/out moves (<c>in</c>/<c>out</c>, <c>sxy</c>) are not modelled.</item>
    /// </list>
    /// <para>An element whose values do not resolve (unknown tool, unresolvable expression,
    /// degenerate geometry) is never reported as outside; it is counted as unresolved.</para>
    /// </summary>
    internal static class OutsideOperationDetector
    {
        /// <summary>Chords an ellipse is flattened to; its chord error is added to the tool reach.</summary>
        private const int EllipseSegments = 2048;

        private const double Tolerance = PlanarGeometry.Tolerance;

        private static readonly string[] BoreTags = ["bf", "bt", "bb", "bl", "br"];

        /// <summary>One element found outside the part, with the contour segments removed along with it.</summary>
        internal sealed record OutsideElement(XElement Element, IReadOnlyList<XElement> Segments, OutsideOperationKinds Kind);

        /// <summary>Elements of the selected <paramref name="kinds"/> found outside, and how many could not be checked.</summary>
        internal sealed record Result(IReadOnlyList<OutsideElement> Outside, int Unresolved);

        /// <summary>What a mill removes on the face.</summary>
        private abstract record MillFootprint;

        /// <summary>Everything within <paramref name="Reach"/> of the tool-centre path <paramref name="ToolCentres"/> (flattened to chords).</summary>
        private sealed record SweptFootprint(IReadOnlyList<Primitive> ToolCentres, double Reach) : MillFootprint;

        /// <summary>
        /// The area a closed pocket contour encloses (flattened to chords; the true outline lies at most
        /// <paramref name="ChordError"/> beyond them). The tool stays inside, so nothing beyond the contour is cut.
        /// </summary>
        private sealed record PocketFootprint(IReadOnlyList<Primitive> Boundary, double ChordError) : MillFootprint;

        private enum Verdict
        {
            Inside,
            Outside,
            Unresolved
        }

        /// <summary>Checks every element of <paramref name="program"/> whose kind is in <paramref name="kinds"/>.</summary>
        public static Result Find(XElement program, OutsideOperationKinds kinds)
        {
            var symbols = SeedProgramSymbols(program);
            var tools = ReadToolDiameters(program);
            var part = new PartVolume(
                RequireProgramDouble(program.GetDxValue(), "dx"),
                RequireProgramDouble(program.GetDyValue(), "dy"),
                RequireProgramDouble(program.GetDzValue(), "dz"));

            var verdicts = Candidates(program, kinds)
                .Select(candidate => (Candidate: candidate, Verdict: Judge(candidate, part, symbols, tools)))
                .ToList();

            return new Result(
                verdicts.Where(v => v.Verdict == Verdict.Outside).Select(v => v.Candidate).ToList(),
                verdicts.Count(v => v.Verdict == Verdict.Unresolved));
        }

        private readonly record struct PartVolume(double Dx, double Dy, double Dz)
        {
            public Box Face => Box.Of(Dx, Dy);
        }

        #region Candidates

        private static IEnumerable<OutsideElement> Candidates(XElement program, OutsideOperationKinds kinds)
        {
            var bores = kinds.HasFlag(OutsideOperationKinds.Bores)
                ? program.Elements().Where(e => BoreTags.Contains(e.Name.LocalName)).Select(e => new OutsideElement(e, [], OutsideOperationKinds.Bores))
                : [];

            var grooves = kinds.HasFlag(OutsideOperationKinds.Grooves)
                ? program.Elements("gr").Select(e => new OutsideElement(e, [], OutsideOperationKinds.Grooves))
                : [];

            var mills = kinds.HasFlag(OutsideOperationKinds.Mills)
                ? CollectMills(program).Select(m => new OutsideElement(m.Head, m.Segments, OutsideOperationKinds.Mills))
                : [];

            return bores.Concat(grooves).Concat(mills);
        }

        private static Verdict Judge(OutsideElement candidate, PartVolume part, XncSymbolTable symbols, Dictionary<string, double> tools)
        {
            try
            {
                var element = candidate.Element;
                var diameter = ToolDiameter(element, tools);

                if (diameter is not > 0d)
                {
                    return Verdict.Unresolved;
                }

                // tool.dia resolves to the element's own tool, as XncProgramReader does.
                symbols.Set("tool.dia", diameter.Value);

                bool? outside = candidate.Kind switch
                {
                    OutsideOperationKinds.Bores => IsBoreOutside(element, part, symbols),
                    OutsideOperationKinds.Grooves => IsGrooveOutside(element, part, symbols),
                    _ => IsMillOutside(candidate, part.Face, symbols, diameter.Value),
                };

                return outside switch
                {
                    true => Verdict.Outside,
                    false => Verdict.Inside,
                    null => Verdict.Unresolved,
                };
            }
            catch (Exception)
            {
                // A value that does not resolve to a number cannot be placed: keep the element.
                return Verdict.Unresolved;
            }
        }

        private static double? ToolDiameter(XElement element, Dictionary<string, double> tools) =>
            element.GetNameValue() is { } name && tools.TryGetValue(name, out var diameter) ? diameter : null;

        #endregion

        #region Bores

        private static bool? IsBoreOutside(XElement bore, PartVolume part, XncSymbolTable symbols)
        {
            if (!(EvalXnc(bore.GetDpValue(), symbols) > 0d))
            {
                return false; // a hole that does not go in is not judged as outside
            }

            var tag = bore.Name.LocalName;

            if (tag == "bf")
            {
                return FaceBoreCentres(bore, symbols)?.All(c => IsCentreOutside(c, part.Face));
            }

            if (ArrayCount(bore) != 1)
            {
                return null; // the step direction of an edge-bore array is unknown
            }

            var z = bool.TryParse(bore.GetMValue(), out var middle) && middle
                ? part.Dz / 2d
                : EvalXnc(bore.GetZValue(), symbols);

            return tag is "bl" or "br"
                ? IsCentreOutside(new Vec2(EvalXnc(bore.GetYValue(), symbols), z), Box.Of(part.Dy, part.Dz))
                : IsCentreOutside(new Vec2(EvalXnc(bore.GetXValue(), symbols), z), Box.Of(part.Dx, part.Dz));
        }

        /// <summary>Every hole of a face bore: <c>ac</c> holes <c>as</c> apart along X, or along Y for <c>av="true"</c>.</summary>
        private static IReadOnlyList<Vec2>? FaceBoreCentres(XElement bore, XncSymbolTable symbols)
        {
            var first = ReadPoint(bore, symbols);
            var count = ArrayCount(bore);

            if (count is not { } holes)
            {
                return null;
            }

            if (holes == 1)
            {
                return [first];
            }

            var step = EvalXnc(bore.Attribute("as")?.Value, symbols);
            var direction = bool.TryParse(bore.GetAvValue(), out var vertical) && vertical ? new Vec2(0d, 1d) : new Vec2(1d, 0d);

            return Enumerable.Range(0, holes).Select(i => first + (direction * (step * i))).ToList();
        }

        /// <summary>The bore array count <c>ac</c> (absent = 1), or <c>null</c> when it is not a positive integer.</summary>
        private static int? ArrayCount(XElement bore) => bore.Attribute("ac")?.Value is not { } raw
            ? 1
            : int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0 ? count : null;

        /// <summary>The bore centre lies outside the box; a centre on a side counts as inside.</summary>
        private static bool IsCentreOutside(Vec2 centre, Box box) => box.DistanceTo(centre) > Tolerance;

        #endregion

        #region Grooves

        private static bool? IsGrooveOutside(XElement groove, PartVolume part, XncSymbolTable symbols)
        {
            if (!(EvalXnc(groove.GetDpValue(), symbols) > 0d))
            {
                return false;
            }

            var start = new Vec2(EvalXnc(groove.GetX1Value(), symbols), EvalXnc(groove.GetY1Value(), symbols));
            var end = new Vec2(EvalXnc(groove.GetX2Value(), symbols), EvalXnc(groove.GetY2Value(), symbols));
            var width = EvalXnc(groove.GetTValue(), symbols);

            if (!(width > 0d))
            {
                return null;
            }

            return ParsePlane(groove.GetPValue()) switch
            {
                0 => IsFaceGrooveOutside(start, end, width, ParsePositionCode(groove.GetCValue()), part.Face),
                1 or 2 => IsEdgeGrooveOutside(start.Y, end.Y, part.Dy, EvalXnc(groove.GetZValue(), symbols), width, part.Dz),
                3 or 4 => IsEdgeGrooveOutside(start.X, end.X, part.Dx, EvalXnc(groove.GetZValue(), symbols), width, part.Dz),
                _ => null,
            };
        }

        private static int? ParsePlane(string? raw) => raw is null
            ? 0
            : int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var plane) ? plane : null;

        /// <summary>The groove footprint (same geometry as the part preview) is separated from the face.</summary>
        private static bool? IsFaceGrooveOutside(Vec2 start, Vec2 end, double width, ToolPosition position, Box face)
        {
            if (start.DistanceTo(end) <= Tolerance)
            {
                return null;
            }

            var normal = RightNormal(end - start);
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

        #endregion

        #region Mills

        private static bool? IsMillOutside(OutsideElement mill, Box face, XncSymbolTable symbols, double diameter)
        {
            var head = mill.Element;

            if (!(EvalXnc(head.GetDpValue(), symbols) > 0d) && !mill.Segments.Any(s => s.GetDpValue() is { } dp && EvalXnc(dp, symbols) > 0d))
            {
                return false;
            }

            var centreLine = head.Name.LocalName switch
            {
                "mr" => RectangleCentreLine(head, symbols),
                "me" => EllipseCentreLine(head, symbols),
                _ => ContourCentreLine(mill, symbols),
            };

            return centreLine is null ? null : Footprint(head, centreLine, diameter) switch
            {
                PocketFootprint pocket => IsPocketOutside(pocket, face),
                SweptFootprint swept => IsSweptOutside(swept, face),
                _ => null,
            };
        }

        /// <summary>A mill's programmed centre line in authored order, and how far the real shape may lie from it (an ellipse's chords).</summary>
        private sealed record CentreLine(IReadOnlyList<Primitive> Path, double ShapeError);

        private static CentreLine? ContourCentreLine(OutsideElement mill, XncSymbolTable symbols) =>
            mill.Segments.Count > 0 && ReadPath(ReadPoint(mill.Element, symbols), mill.Segments, symbols) is { Count: > 0 } path
                ? new CentreLine(path, 0d)
                : null;

        private static CentreLine? RectangleCentreLine(XElement mr, XncSymbolTable symbols)
        {
            var halfLength = EvalXnc(mr.GetLengthValue(), symbols) / 2d;
            var halfWidth = EvalXnc(mr.GetWidthValue(), symbols) / 2d;

            if (halfLength <= Tolerance || halfWidth <= Tolerance)
            {
                return null;
            }

            var radius = mr.GetRValue() is { } rawRadius
                ? Math.Clamp(EvalXnc(rawRadius, symbols), 0d, Math.Min(halfLength, halfWidth))
                : 0d;

            var path = MillShapeGeometry.RoundedRectangle(halfLength, halfWidth, radius)
                .Select(p => MillShapeGeometry.Place(p, ReadPoint(mr, symbols), ReadAngle(mr, symbols)))
                .ToList();

            return new CentreLine(path, 0d);
        }

        private static CentreLine? EllipseCentreLine(XElement me, XncSymbolTable symbols)
        {
            // <me> l/w are semi-axes.
            var semiX = EvalXnc(me.GetLengthValue(), symbols);
            var semiY = EvalXnc(me.GetWidthValue(), symbols);

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
            head.GetAValue() is { } angle ? EvalXnc(angle, symbols) : 0d;

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
            var reversed = head.Name.LocalName == "ms"
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
