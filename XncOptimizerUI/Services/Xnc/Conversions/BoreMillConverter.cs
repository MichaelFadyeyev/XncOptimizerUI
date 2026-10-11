using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using static XncOptimizerUI.Services.Xnc.Conversions.ElementOutcome;
using static XncOptimizerUI.Services.Xnc.XncProgramMath;

namespace XncOptimizerUI.Services.Xnc.Conversions
{
    /// <summary>
    /// Rewrites one decoded XNC program between large face bores and round mills.
    /// <list type="bullet">
    /// <item><b>Bores → mills</b>: a face bore (<c>&lt;bf&gt;</c>) wider than
    /// <see cref="MinDiameter"/> is milled out with a <see cref="CutterDiameter"/> mm cutter
    /// (<c>Mill6</c>): a closed contour of four quarter arcs - <c>&lt;ms&gt;</c> at the circle's top,
    /// then <c>&lt;mac dir="false"&gt;</c> to its left, bottom, right and top, declared
    /// counter-clockwise on screen - or a single <c>&lt;me&gt;</c> when ellipses are asked for. A
    /// through bore keeps the tool inside the hole (<c>c="2"</c> for the contour, <c>c="1"</c> for
    /// the clockwise ellipse); a blind bore is milled as a pocket (<c>c="3"</c>). The mill depth
    /// equals the bore depth.</item>
    /// <item><b>Mills → bores</b>: a round mill wider than <see cref="MinDiameter"/> - an
    /// <c>&lt;ms&gt;</c> plus at least two arcs (<c>&lt;mac&gt;</c> or <c>&lt;ma&gt;</c>) sharing one
    /// centre and radius and closing onto the entry, or an <c>l == w</c> <c>&lt;me&gt;</c> - becomes a
    /// face bore with a <c>Bore&lt;diameter&gt;</c> tool.</item>
    /// </list>
    /// <c>&lt;me&gt;</c> <c>l</c>/<c>w</c> are semi-axes, so a round mill has <c>l == w</c> and diameter <c>2l</c>.
    /// </summary>
    internal static class BoreMillConverter
    {
        /// <summary>A bore or round mill only converts when its diameter exceeds this (mm).</summary>
        public const double MinDiameter = 34.0;

        /// <summary>Diameter of the cutter a bore is milled out with (mm).</summary>
        public const double CutterDiameter = 6.0;

        /// <summary>Slack (mm) when matching arc centres and radii.</summary>
        private const double GeomTolerance = 1e-3;

        public static ConversionStats BoresToMills(XElement program, bool useEllipse)
        {
            var context = ConversionContext.Of(program);
            var outcomes = program.Elements(XncTags.FaceBore).ToList()
                .Select(bore => ConvertBore(bore, useEllipse, context))
                .ToList();

            return context.Finish(outcomes);
        }

        public static ConversionStats MillsToBores(XElement program)
        {
            var context = ConversionContext.Of(program);
            var outcomes = XncMillElements.CollectMills(program)
                .Select(mill => mill.Head.Name.LocalName switch
                {
                    XncTags.Ellipse => TryConvertRoundEllipse(mill.Head, context) ? Converted : Ignored,
                    XncTags.ContourStart => TryConvertRoundContour(mill.Head, mill.Segments, context) ? Converted : Ignored,
                    _ => NotApplicable,
                })
                .ToList();

            return context.Finish(outcomes);
        }

        #region Bores to mills

        private static ElementOutcome ConvertBore(XElement bore, bool useEllipse, ConversionContext context)
        {
            if (bore.GetNameValue() is not { } boreToolName
                || !context.Tools.DiametersByName.TryGetValue(boreToolName, out var diameter)
                || diameter <= MinDiameter)
            {
                return NotApplicable;
            }

            context.Replacing(boreToolName);

            var centre = (X: context.Eval(bore.GetXValue()), Y: context.Eval(bore.GetYValue()));
            var depth = bore.GetDpValue() ?? "0";
            var radius = diameter / 2d;
            var millTool = ConversionTools.Ensure(bore, CutterDiameter, ConversionTools.MillPrefix, context.Tools);

            // "Through" = the av flag is set, or the depth reaches the far face.
            var through = string.Equals(bore.GetAvValue(), "true", StringComparison.OrdinalIgnoreCase)
                || context.Eval(depth) >= context.Dz - GeomTolerance;

            bore.AddBeforeSelf(useEllipse
                ? [RoundEllipse(centre, radius, depth, positionCode: through ? "1" : "3", millTool)]
                : RoundContour(centre, radius, depth, positionCode: through ? "2" : "3", millTool));
            bore.Remove();

            return Converted;
        }

        /// <summary>A clockwise (<c>fwd="true"</c>) circle as an <c>&lt;me&gt;</c> with both semi-axes = <paramref name="radius"/>.</summary>
        private static XElement RoundEllipse((double X, double Y) centre, double radius, string depth, string positionCode, string toolName) =>
            new(XncTags.Ellipse,
                new XAttribute("x", XmlConvert.ToString(centre.X)),
                new XAttribute("y", XmlConvert.ToString(centre.Y)),
                new XAttribute("dp", depth),
                new XAttribute("in", "0"),
                new XAttribute("out", "1"),
                new XAttribute("sxy", "tool.dia/2"),
                new XAttribute("fwd", "true"),
                new XAttribute("l", XmlConvert.ToString(radius)),
                new XAttribute("w", XmlConvert.ToString(radius)),
                new XAttribute("a", "0"),
                new XAttribute("c", positionCode),
                new XAttribute("name", toolName));

        /// <summary>
        /// A circle declared counter-clockwise on screen from its top: <c>&lt;ms&gt;</c> at the top, then
        /// four <c>&lt;mac dir="false"&gt;</c> quarter arcs to the left, bottom, right and back to the top.
        /// </summary>
        private static IEnumerable<XElement> RoundContour((double X, double Y) centre, double radius, string depth, string positionCode, string toolName)
        {
            yield return new XElement(XncTags.ContourStart,
                new XAttribute("x", XmlConvert.ToString(centre.X)),
                new XAttribute("y", XmlConvert.ToString(centre.Y - radius)),
                new XAttribute("dp", depth),
                new XAttribute("in", "0"),
                new XAttribute("out", "1"),
                new XAttribute("sxy", "tool.dia/2"),
                new XAttribute("fwd", "true"),
                new XAttribute("c", positionCode),
                new XAttribute("name", toolName));

            (double X, double Y)[] quarterEnds =
            [
                (centre.X - radius, centre.Y),
                (centre.X, centre.Y + radius),
                (centre.X + radius, centre.Y),
                (centre.X, centre.Y - radius),
            ];

            foreach (var (x, y) in quarterEnds)
            {
                yield return new XElement(XncTags.CentreArc,
                    new XAttribute("x", XmlConvert.ToString(x)),
                    new XAttribute("y", XmlConvert.ToString(y)),
                    new XAttribute("cx", XmlConvert.ToString(centre.X)),
                    new XAttribute("cy", XmlConvert.ToString(centre.Y)),
                    new XAttribute("dp", depth),
                    new XAttribute("dir", "false"));
            }
        }

        #endregion

        #region Mills to bores

        private static bool TryConvertRoundEllipse(XElement ellipse, ConversionContext context)
        {
            var l = context.Eval(ellipse.GetLengthValue());
            var w = context.Eval(ellipse.GetWidthValue());
            var diameter = l * 2d;

            if (Math.Abs(l - w) > GeomTolerance || diameter <= MinDiameter)
            {
                return false;
            }

            var x = context.Eval(ellipse.GetXValue());
            var y = context.Eval(ellipse.GetYValue());
            var depth = context.Eval(ellipse.GetDpValue());

            ReplaceWithBore(ellipse, [], x, y, depth, diameter, context);

            return true;
        }

        private static bool TryConvertRoundContour(XElement start, IReadOnlyList<XElement> segments, ConversionContext context)
        {
            if (segments.Count < 2 || segments.Any(s => s.Name.LocalName is not (XncTags.CentreArc or XncTags.RadiusArc)))
            {
                return false;
            }

            var entry = (X: context.Eval(start.GetXValue()), Y: context.Eval(start.GetYValue()));

            if (FindSharedCircle(entry, segments, context) is not { } circle
                || Math.Abs(Hypot(circle.Cx, circle.Cy, entry.X, entry.Y) - circle.Radius) > GeomTolerance // entry off the circle
                || Hypot(circle.EndX, circle.EndY, entry.X, entry.Y) > GeomTolerance // not closed onto the entry
                || circle.Radius * 2d <= MinDiameter)
            {
                return false;
            }

            var depth = segments
                .Select(s => s.GetDpValue())
                .OfType<string>()
                .Select(context.Eval)
                .Aggregate(context.Eval(start.GetDpValue()), Math.Max);

            ReplaceWithBore(start, segments, circle.Cx, circle.Cy, depth, circle.Radius * 2d, context);

            return true;
        }

        /// <summary>
        /// The one centre and radius every arc of the contour lies on, and where the last arc ends;
        /// <c>null</c> when an arc ends off its own circle, an arc disagrees with the first, or a
        /// radius arc cannot be reconstructed.
        /// </summary>
        private static (double Cx, double Cy, double Radius, double EndX, double EndY)? FindSharedCircle(
            (double X, double Y) entry, IReadOnlyList<XElement> arcs, ConversionContext context)
        {
            (double Cx, double Cy, double Radius)? shared = null;
            var current = entry;

            foreach (var arc in arcs)
            {
                var end = (X: context.Eval(arc.GetXValue()), Y: context.Eval(arc.GetYValue()));

                if (ArcCircle(arc, current, end, context) is not { } circle
                    || Math.Abs(Hypot(circle.Cx, circle.Cy, end.X, end.Y) - circle.Radius) > GeomTolerance)
                {
                    return null;
                }

                shared ??= circle;

                if (Hypot(circle.Cx, circle.Cy, shared.Value.Cx, shared.Value.Cy) > GeomTolerance
                    || Math.Abs(circle.Radius - shared.Value.Radius) > GeomTolerance)
                {
                    return null;
                }

                current = end;
            }

            return shared is { } s && s.Radius > GeomTolerance ? (s.Cx, s.Cy, s.Radius, current.X, current.Y) : null;
        }

        /// <summary>Centre and radius of one arc from <paramref name="start"/>: given by <c>&lt;mac&gt;</c>, reconstructed from the chord for <c>&lt;ma&gt;</c>.</summary>
        private static (double Cx, double Cy, double Radius)? ArcCircle(
            XElement arc, (double X, double Y) start, (double X, double Y) end, ConversionContext context)
        {
            if (arc.Name.LocalName == XncTags.CentreArc)
            {
                var cx = context.Eval(arc.GetCxValue());
                var cy = context.Eval(arc.GetCyValue());

                return (cx, cy, Hypot(cx, cy, start.X, start.Y));
            }

            var radius = context.Eval(arc.GetRValue());

            return TryReconstructArcCentre(start.X, start.Y, end.X, end.Y, radius, ParseDirSign(arc.GetDirValue()), out var centreX, out var centreY)
                ? (centreX, centreY, radius)
                : null;
        }

        /// <summary>Inserts a face bore with a <c>Bore&lt;diameter&gt;</c> tool in place of the mill and its segments.</summary>
        private static void ReplaceWithBore(
            XElement mill, IReadOnlyList<XElement> segments, double x, double y, double depth, double diameter, ConversionContext context)
        {
            context.Replacing(mill.GetNameValue());

            var boreTool = ConversionTools.Ensure(mill, diameter, ConversionTools.BorePrefix, context.Tools);

            mill.AddBeforeSelf(new XElement(XncTags.FaceBore,
                new XAttribute("x", XmlConvert.ToString(x)),
                new XAttribute("y", XmlConvert.ToString(y)),
                new XAttribute("dp", XmlConvert.ToString(depth)),
                new XAttribute("ac", "1"),
                new XAttribute("av", "false"),
                new XAttribute("name", boreTool)));

            segments.ToList().ForEach(s => s.Remove());
            mill.Remove();
        }

        #endregion
    }
}
