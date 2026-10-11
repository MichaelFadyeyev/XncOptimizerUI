using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using static XncOptimizerUI.Services.Xnc.Conversions.ElementOutcome;

namespace XncOptimizerUI.Services.Xnc.Conversions
{
    /// <summary>
    /// Rewrites one decoded XNC program between grooves and mills. Only axis-parallel paths
    /// convert; every other candidate is counted as ignored and left untouched.
    /// <list type="bullet">
    /// <item><b>Grooves → mills</b>: a primary-pass <c>&lt;gr&gt;</c> (<c>p</c> absent or <c>0</c>)
    /// whose width equals a configured milling cutter becomes an <c>&lt;ms&gt;</c> + <c>&lt;ml&gt;</c>
    /// path cut with it; otherwise a rectangular pocket (<c>&lt;mr c="3"&gt;</c>) milled with the
    /// smallest cutter when that one fits. Ends on or beyond the outline overshoot by half a cutter.</item>
    /// <item><b>Mills → grooves</b>: a shallow, single straight-segment, non-pocket contour becomes a
    /// <c>&lt;gr&gt;</c> as wide as its cutter, cut with the configured grooving cutter, ends clamped
    /// to the outline; with pockets on, axis-parallel rectangular pockets (<c>&lt;mr&gt;</c> or a
    /// rectangular contour) become a groove down their long side.</item>
    /// </list>
    /// </summary>
    internal static class GrooveMillConverter
    {
        /// <summary>Tolerance (mm) for telling an axis-parallel path from a diagonal one.</summary>
        public const double AxisEpsilon = 1e-6;

        /// <param name="millingToolDiameters">Available cutters, ascending; not empty.</param>
        public static ConversionStats GroovesToMills(XElement program, IReadOnlyList<double> millingToolDiameters)
        {
            var context = ConversionContext.Of(program);
            var outcomes = program.Elements(XncTags.Groove).ToList()
                .Select(groove => ConvertGroove(groove, context, millingToolDiameters))
                .ToList();

            return context.Finish(outcomes);
        }

        public static ConversionStats MillsToGrooves(XElement program, bool processPockets, double groovingToolDiameter)
        {
            var context = ConversionContext.Of(program);
            var outcomes = XncMillElements.CollectMills(program)
                .Select(mill => mill.Head.Name.LocalName switch
                {
                    XncTags.Rectangle => ConvertRectanglePocket(mill.Head, processPockets, groovingToolDiameter, context),
                    XncTags.ContourStart => ConvertContour(mill, processPockets, groovingToolDiameter, context),
                    _ => NotApplicable,
                })
                .ToList();

            return context.Finish(outcomes);
        }

        #region Grooves to mills

        private static ElementOutcome ConvertGroove(XElement groove, ConversionContext context, IReadOnlyList<double> millingToolDiameters)
        {
            // Only a primary-pass groove (p="0", or absent) may become a mill.
            if (groove.GetPValue() is { } pass && pass.Trim() != "0")
            {
                return Ignored;
            }

            var x1 = context.Eval(groove.GetX1Value());
            var y1 = context.Eval(groove.GetY1Value());
            var x2 = context.Eval(groove.GetX2Value());
            var y2 = context.Eval(groove.GetY2Value());

            if (AxisOf(x1, y1, x2, y2) is not { } horizontal)
            {
                return Ignored;
            }

            var width = context.Eval(groove.GetTValue());
            var depth = groove.GetDpValue() ?? "0";

            context.Replacing(groove.GetNameValue());

            if (millingToolDiameters.Any(d => Math.Abs(d - width) <= ConversionTools.DiameterEpsilon))
            {
                InsertMillPath(groove, x1, y1, x2, y2, width, depth, horizontal, context);
            }
            else if (millingToolDiameters[0] <= width)
            {
                // No cutter of the groove's exact width: mill it out as a pocket with the smallest one.
                InsertGroovePocket(groove, x1, y1, x2, y2, width, depth, horizontal, millingToolDiameters[0], context);
            }
            else
            {
                return Ignored;
            }

            groove.Remove();

            return Converted;
        }

        /// <summary>An <c>&lt;ms&gt;</c> + <c>&lt;ml&gt;</c> along the groove, cut by the cutter of its width.</summary>
        private static void InsertMillPath(
            XElement groove, double x1, double y1, double x2, double y2, double width, string depth, bool horizontal, ConversionContext context)
        {
            var toolName = ConversionTools.Ensure(groove, width, ConversionTools.MillPrefix, context.Tools);

            // The tool centre must clear the edge, but only along the axis the groove runs.
            var overshoot = width / 2d;

            if (horizontal)
            {
                x1 = OvershootAlongAxis(x1, context.Dx, overshoot);
                x2 = OvershootAlongAxis(x2, context.Dx, overshoot);
            }
            else
            {
                y1 = OvershootAlongAxis(y1, context.Dy, overshoot);
                y2 = OvershootAlongAxis(y2, context.Dy, overshoot);
            }

            groove.AddBeforeSelf(
                WithComment(new XElement(XncTags.ContourStart,
                    new XAttribute("x", XmlConvert.ToString(x1)),
                    new XAttribute("y", XmlConvert.ToString(y1)),
                    new XAttribute("dp", depth),
                    new XAttribute("in", "0"),
                    new XAttribute("out", "0"),
                    new XAttribute("sxy", "tool.dia/2"),
                    new XAttribute("fwd", "true"),
                    new XAttribute("c", groove.GetCValue() ?? "0"),
                    new XAttribute("name", toolName)), groove.GetCommentValue()),
                new XElement(XncTags.Line,
                    new XAttribute("x", XmlConvert.ToString(x2)),
                    new XAttribute("y", XmlConvert.ToString(y2)),
                    new XAttribute("dp", depth)));
        }

        /// <summary>
        /// A rectangular pocket (<c>&lt;mr c="3" a="0"&gt;</c>) along the groove: the running side carries
        /// the groove length, the other side its <paramref name="width"/>. A running-axis end on or
        /// beyond the border is pushed half the cutter past it; the constant axis keeps the centre line.
        /// </summary>
        private static void InsertGroovePocket(
            XElement groove, double x1, double y1, double x2, double y2, double width, string depth, bool horizontal,
            double toolDiameter, ConversionContext context)
        {
            var overshoot = toolDiameter / 2d;
            var (runStart, runEnd, runSize, constCoord) = horizontal
                ? (x1, x2, context.Dx, (y1 + y2) / 2d)
                : (y1, y2, context.Dy, (x1 + x2) / 2d);

            runStart = OvershootAlongAxis(runStart, runSize, overshoot);
            runEnd = OvershootAlongAxis(runEnd, runSize, overshoot);

            var length = Math.Abs(runEnd - runStart);
            var runCentre = (runStart + runEnd) / 2d;
            var (cx, cy, l, w) = horizontal
                ? (runCentre, constCoord, length, width)
                : (constCoord, runCentre, width, length);

            var toolName = ConversionTools.Ensure(groove, toolDiameter, ConversionTools.MillPrefix, context.Tools);

            groove.AddBeforeSelf(WithComment(new XElement(XncTags.Rectangle,
                new XAttribute("x", XmlConvert.ToString(cx)),
                new XAttribute("y", XmlConvert.ToString(cy)),
                new XAttribute("dp", depth),
                new XAttribute("in", "0"),
                new XAttribute("out", "0"),
                new XAttribute("sxy", "tool.dia/2"),
                new XAttribute("fwd", "true"),
                new XAttribute("l", XmlConvert.ToString(l)),
                new XAttribute("w", XmlConvert.ToString(w)),
                new XAttribute("a", "0"),
                new XAttribute("r", "0"),
                new XAttribute("c", "3"),
                new XAttribute("name", toolName)), groove.GetCommentValue()));
        }

        /// <summary>An end on or beyond the part border moves <paramref name="offset"/> past it; an inner end stays.</summary>
        private static double OvershootAlongAxis(double value, double size, double offset) => value switch
        {
            <= 0d => -offset,
            _ when value >= size => size + offset,
            _ => value,
        };

        #endregion

        #region Mills to grooves

        private static ElementOutcome ConvertContour(XncMillElements.Mill mill, bool processPockets, double groovingToolDiameter, ConversionContext context)
        {
            var (start, segments) = (mill.Head, mill.Segments);
            var isPocket = XncProgramMath.ParsePositionCode(start.GetCValue()) == ToolPosition.Pocket;

            // A closed axis-parallel rectangular pocket contour becomes a groove, like an <mr> pocket.
            if (processPockets
                && isPocket
                && segments.Count >= 3
                && segments.All(s => s.Name.LocalName == XncTags.Line)
                && TryConvertContourPocket(start, segments, groovingToolDiameter, context))
            {
                return Converted;
            }

            if (segments.Count != 1 || segments[0].Name.LocalName != XncTags.Line)
            {
                return Ignored;
            }

            return TryConvertStraightMill(start, segments[0], isPocket, groovingToolDiameter, context) ? Converted : Ignored;
        }

        /// <summary>A shallow, non-pocket, axis-parallel <c>&lt;ms&gt;</c> + <c>&lt;ml&gt;</c> with a known cutter becomes a groove of the cutter's width.</summary>
        private static bool TryConvertStraightMill(XElement start, XElement line, bool isPocket, double groovingToolDiameter, ConversionContext context)
        {
            var entryDepth = context.Eval(start.GetDpValue());
            var segmentDepth = line.GetDpValue() is { } rawDepth ? context.Eval(rawDepth) : entryDepth;

            if (Math.Max(entryDepth, segmentDepth) >= context.Dz || isPocket)
            {
                return false;
            }

            var x1 = context.Eval(start.GetXValue());
            var y1 = context.Eval(start.GetYValue());
            var x2 = context.Eval(line.GetXValue());
            var y2 = context.Eval(line.GetYValue());

            if (AxisOf(x1, y1, x2, y2) is not { } horizontal
                || start.GetNameValue() is not { } millToolName
                || !context.Tools.DiametersByName.TryGetValue(millToolName, out var millToolDiameter))
            {
                return false;
            }

            // The slot is cut by the grooving cutter (several passes when wider); the mill's tool only gives the width.
            var grooveToolName = ConversionTools.Ensure(start, groovingToolDiameter, ConversionTools.GroovePrefix, context.Tools);

            // An end outside the part is pulled onto the outline; only the running axis can be out of range.
            if (horizontal)
            {
                x1 = Math.Clamp(x1, 0d, context.Dx);
                x2 = Math.Clamp(x2, 0d, context.Dx);
            }
            else
            {
                y1 = Math.Clamp(y1, 0d, context.Dy);
                y2 = Math.Clamp(y2, 0d, context.Dy);
            }

            start.AddBeforeSelf(WithComment(new XElement(XncTags.Groove,
                new XAttribute("x1", XmlConvert.ToString(x1)),
                new XAttribute("y1", XmlConvert.ToString(y1)),
                new XAttribute("dp", line.GetDpValue() ?? start.GetDpValue() ?? "0"),
                new XAttribute("x2", XmlConvert.ToString(x2)),
                new XAttribute("y2", XmlConvert.ToString(y2)),
                new XAttribute("t", XmlConvert.ToString(millToolDiameter)),
                new XAttribute("c", start.GetCValue() ?? "0"),
                new XAttribute("p", "0"),
                new XAttribute("name", grooveToolName)), start.GetCommentValue()));

            context.Replacing(millToolName);
            line.Remove();
            start.Remove();

            return true;
        }

        /// <summary>An unrotated, blind <c>&lt;mr c="3"&gt;</c> pocket (centred at x/y) becomes a groove.</summary>
        private static ElementOutcome ConvertRectanglePocket(XElement rectangle, bool processPockets, double groovingToolDiameter, ConversionContext context)
        {
            if (!processPockets
                || XncProgramMath.ParsePositionCode(rectangle.GetCValue()) != ToolPosition.Pocket
                || (rectangle.GetAValue() is { } angle && Math.Abs(context.Eval(angle)) > AxisEpsilon)
                || context.Eval(rectangle.GetDpValue()) >= context.Dz)
            {
                return Ignored;
            }

            var length = context.Eval(rectangle.GetLengthValue());
            var breadth = context.Eval(rectangle.GetWidthValue());

            if (length <= 0d || breadth <= 0d)
            {
                return Ignored;
            }

            var cx = context.Eval(rectangle.GetXValue());
            var cy = context.Eval(rectangle.GetYValue());

            InsertPocketGroove(rectangle,
                cx - length / 2d, cx + length / 2d, cy - breadth / 2d, cy + breadth / 2d,
                rectangle.GetDpValue() ?? "0", groovingToolDiameter, context);
            rectangle.Remove();

            return Converted;
        }

        /// <summary>A blind <c>&lt;ms c="3"&gt;</c> contour tracing an axis-parallel rectangle with straight <c>&lt;ml&gt;</c> sides becomes a groove.</summary>
        private static bool TryConvertContourPocket(XElement start, IReadOnlyList<XElement> segments, double groovingToolDiameter, ConversionContext context)
        {
            var vertices = segments
                .Prepend(start)
                .Select(e => (X: context.Eval(e.GetXValue()), Y: context.Eval(e.GetYValue())))
                .ToList();

            var (minX, maxX) = (vertices.Min(v => v.X), vertices.Max(v => v.X));
            var (minY, maxY) = (vertices.Min(v => v.Y), vertices.Max(v => v.Y));

            if (maxX - minX <= AxisEpsilon || maxY - minY <= AxisEpsilon || !IsAxisParallelRectangle(vertices, minX, maxX, minY, maxY))
            {
                return false;
            }

            var depth = segments
                .Select(s => s.GetDpValue())
                .OfType<string>()
                .Select(context.Eval)
                .Aggregate(context.Eval(start.GetDpValue()), Math.Max);

            if (depth >= context.Dz)
            {
                return false;
            }

            InsertPocketGroove(start, minX, maxX, minY, maxY, start.GetDpValue() ?? "0", groovingToolDiameter, context);
            segments.ToList().ForEach(s => s.Remove());
            start.Remove();

            return true;
        }

        /// <summary>Every vertex on a bounding-box corner, and every step along one axis only.</summary>
        private static bool IsAxisParallelRectangle(List<(double X, double Y)> vertices, double minX, double maxX, double minY, double maxY)
        {
            bool Near(double a, double b) => Math.Abs(a - b) <= AxisEpsilon;

            return vertices.All(v => (Near(v.X, minX) || Near(v.X, maxX)) && (Near(v.Y, minY) || Near(v.Y, maxY)))
                && vertices.Zip(vertices.Skip(1)).All(step => Near(step.First.X, step.Second.X) || Near(step.First.Y, step.Second.Y));
        }

        /// <summary>
        /// A <c>&lt;gr&gt;</c> for an axis-parallel pocket bounded by <c>[minX,maxX] x [minY,maxY]</c>:
        /// it runs along the longer side, its width is the shorter side, and ends beyond the part edge
        /// are pulled onto it.
        /// </summary>
        private static void InsertPocketGroove(
            XElement anchor, double minX, double maxX, double minY, double maxY, string depth, double groovingToolDiameter, ConversionContext context)
        {
            var (x1, y1, x2, y2, slot) = maxX - minX >= maxY - minY
                ? (Math.Clamp(minX, 0d, context.Dx), (minY + maxY) / 2d, Math.Clamp(maxX, 0d, context.Dx), (minY + maxY) / 2d, maxY - minY)
                : ((minX + maxX) / 2d, Math.Clamp(minY, 0d, context.Dy), (minX + maxX) / 2d, Math.Clamp(maxY, 0d, context.Dy), maxX - minX);

            var grooveToolName = ConversionTools.Ensure(anchor, groovingToolDiameter, ConversionTools.GroovePrefix, context.Tools);

            anchor.AddBeforeSelf(WithComment(new XElement(XncTags.Groove,
                new XAttribute("x1", XmlConvert.ToString(x1)),
                new XAttribute("y1", XmlConvert.ToString(y1)),
                new XAttribute("dp", depth),
                new XAttribute("x2", XmlConvert.ToString(x2)),
                new XAttribute("y2", XmlConvert.ToString(y2)),
                new XAttribute("t", XmlConvert.ToString(slot)),
                new XAttribute("c", "0"),
                new XAttribute("p", "0"),
                new XAttribute("name", grooveToolName)), anchor.GetCommentValue()));

            context.Replacing(anchor.GetNameValue());
        }

        #endregion

        #region Methods

        /// <summary><c>true</c> for a horizontal path, <c>false</c> for a vertical one, <c>null</c> for a diagonal or degenerate one.</summary>
        private static bool? AxisOf(double x1, double y1, double x2, double y2)
        {
            var horizontal = Math.Abs(y1 - y2) <= AxisEpsilon;
            var vertical = Math.Abs(x1 - x2) <= AxisEpsilon;

            return horizontal == vertical ? null : horizontal;
        }

        private static XElement WithComment(XElement element, string? comment)
        {
            if (comment != null)
            {
                element.SetAttributeValue("comment", comment);
            }

            return element;
        }

        #endregion
    }
}
