using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc.Batch;
using XncOptimizerUI.Services.Xnc.Conversions;
using XncOptimizerUI.Services.Xnc.Geometry;
using static XncOptimizerUI.Services.Xnc.XncProgramMath;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>Counters of a mill traversal optimization.</summary>
    internal readonly record struct TraversalStats(int Reordered, int Ignored, int ChangedGroups) : IBatchStats<TraversalStats>
    {
        public static TraversalStats Zero => default;

        public TraversalStats Add(TraversalStats other) =>
            new(Reordered + other.Reordered, Ignored + other.Ignored, ChangedGroups + other.ChangedGroups);
    }

    /// <summary>
    /// Re-sequences the straight milling passes of one decoded XNC program so the entry of each pass
    /// is next to the exit of the previous one (a serpentine path). A pass is an <c>&lt;ms&gt;</c>
    /// followed by exactly one straight <c>&lt;ml&gt;</c> parallel to X or Y that is not a pocket.
    /// Passes sharing a tool are re-sequenced together by a greedy nearest-neighbour walk: the first
    /// pass in document order keeps its direction and seeds the walk, then each remaining pass is
    /// taken in whichever direction puts its entry closest to the current tool position. Passes keep
    /// their slots in the program (each slot receives the attributes of the pass ordered into it);
    /// arcs, multi-segment contours, pockets and <c>&lt;mr&gt;</c> are counted as ignored.
    /// </summary>
    internal static class MillTraversalOptimizer
    {
        /// <summary>The attributes of one pass, copied as text so they can move to another slot.</summary>
        private readonly record struct Pass(
            string ToolName,
            string MsX, string MsY, string? MsDp,
            string MlX, string MlY, string? MlDp,
            string? In, string? Out, string? Sxy, string? Fwd, string? C, string? Comment,
            Vec2 Entry, Vec2 Exit);

        /// <summary>A pass's place in the program: its <c>&lt;ms&gt;</c> and <c>&lt;ml&gt;</c>.</summary>
        private readonly record struct Slot(XElement Start, XElement Line);

        public static TraversalStats Optimize(XElement program)
        {
            var symbols = SeedProgramSymbols(program);
            var candidates = XncMillElements.CollectMills(program)
                .Where(mill => mill.Head.Name.LocalName is XncTags.ContourStart or XncTags.Rectangle)
                .Select(mill => ReadPass(mill, symbols))
                .ToList();

            var passes = candidates.OfType<(Slot Slot, Pass Pass)>().ToList();
            var ignored = candidates.Count - passes.Count;

            if (passes.Count < 2)
            {
                return new TraversalStats(0, ignored, 0);
            }

            var groupResults = passes
                .Select((pass, index) => (pass, index))
                .GroupBy(p => p.pass.Pass.ToolName, StringComparer.OrdinalIgnoreCase)
                .Select(group => ReorderGroup(group.Select(p => p.pass).ToList()))
                .Where(reordered => reordered > 0)
                .ToList();

            return new TraversalStats(groupResults.Sum(), ignored, groupResults.Count);
        }

        #region Methods

        /// <summary>The mill as a pass, or <c>null</c> when it is not a straight, axis-parallel, non-pocket single segment with a tool.</summary>
        private static (Slot Slot, Pass Pass)? ReadPass(XncMillElements.Mill mill, XncSymbolTable symbols)
        {
            var (start, segments) = (mill.Head, mill.Segments);

            if (start.Name.LocalName != XncTags.ContourStart
                || segments.Count != 1
                || segments[0].Name.LocalName != XncTags.Line
                || ParsePositionCode(start.GetCValue()) == ToolPosition.Pocket
                || start.GetNameValue() is not { } toolName)
            {
                return null;
            }

            var line = segments[0];

            if (TryReadEnds(start, line, symbols) is not { } ends
                || (Math.Abs(ends.Entry.Y - ends.Exit.Y) <= GrooveMillConverter.AxisEpsilon) == (Math.Abs(ends.Entry.X - ends.Exit.X) <= GrooveMillConverter.AxisEpsilon))
            {
                return null; // unresolvable, diagonal or degenerate
            }

            return (new Slot(start, line), new Pass(
                toolName,
                start.GetXValue()!, start.GetYValue()!, start.GetDpValue(),
                line.GetXValue()!, line.GetYValue()!, line.GetDpValue(),
                start.GetInValue(), start.GetOutValue(), start.GetSxyValue(), start.GetFwdValue(), start.GetCValue(), start.GetCommentValue(),
                ends.Entry, ends.Exit));
        }

        /// <summary>The pass's entry and exit, or <c>null</c> when they don't resolve to numbers.</summary>
        private static (Vec2 Entry, Vec2 Exit)? TryReadEnds(XElement start, XElement line, XncSymbolTable symbols)
        {
            try
            {
                return (XncMillElements.ReadPoint(start, symbols), XncMillElements.ReadPoint(line, symbols));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Re-sequences one tool's passes in place; returns how many slots got another pass or direction.</summary>
        private static int ReorderGroup(List<(Slot Slot, Pass Pass)> group)
        {
            if (group.Count < 2)
            {
                return 0;
            }

            var plan = NearestNeighbourOrder(group.Select(g => g.Pass).ToList());
            var reordered = plan.Where((step, position) => step.Source != position || step.Reversed).Count();

            if (reordered > 0)
            {
                // The passes hold attribute text, not element references, so overwriting the slots in any order is safe.
                plan.Select((step, position) => (step, position))
                    .ToList()
                    .ForEach(p => Apply(group[p.position].Slot, group[p.step.Source].Pass, p.step.Reversed));
            }

            return reordered;
        }

        /// <summary>The order (index into <paramref name="passes"/>) and direction of the greedy walk seeded by the first pass.</summary>
        private static List<(int Source, bool Reversed)> NearestNeighbourOrder(List<Pass> passes)
        {
            var order = new List<(int, bool)> { (0, false) };
            var remaining = Enumerable.Range(1, passes.Count - 1).ToList();
            var current = passes[0].Exit;

            while (remaining.Count > 0)
            {
                var bestPosition = 0;
                var bestReversed = false;
                var bestDistance = double.MaxValue;

                for (var r = 0; r < remaining.Count; r++)
                {
                    var pass = passes[remaining[r]];
                    var forward = current.SquaredDistanceTo(pass.Entry);
                    var backward = current.SquaredDistanceTo(pass.Exit);

                    if (forward < bestDistance)
                    {
                        (bestDistance, bestPosition, bestReversed) = (forward, r, false);
                    }

                    if (backward < bestDistance)
                    {
                        (bestDistance, bestPosition, bestReversed) = (backward, r, true);
                    }
                }

                var chosen = passes[remaining[bestPosition]];

                order.Add((remaining[bestPosition], bestReversed));
                remaining.RemoveAt(bestPosition);
                current = bestReversed ? chosen.Entry : chosen.Exit;
            }

            return order;
        }

        /// <summary>
        /// Writes <paramref name="source"/> into <paramref name="slot"/>. Reversing swaps which authored
        /// end the tool enters from; the constant axis is identical at both ends, so swapping the text is exact.
        /// </summary>
        private static void Apply(Slot slot, Pass source, bool reversed)
        {
            var (entryX, entryY, entryDp) = reversed ? (source.MlX, source.MlY, source.MlDp ?? source.MsDp) : (source.MsX, source.MsY, source.MsDp);
            var (exitX, exitY, exitDp) = reversed ? (source.MsX, source.MsY, source.MsDp) : (source.MlX, source.MlY, source.MlDp);

            slot.Start.SetAttributeValue("x", entryX);
            slot.Start.SetAttributeValue("y", entryY);
            slot.Start.SetAttributeValue("dp", entryDp);
            slot.Start.SetAttributeValue("in", source.In);
            slot.Start.SetAttributeValue("out", source.Out);
            slot.Start.SetAttributeValue("sxy", source.Sxy);
            slot.Start.SetAttributeValue("fwd", source.Fwd);
            slot.Start.SetAttributeValue("c", source.C);
            slot.Start.SetAttributeValue("name", source.ToolName);
            slot.Start.SetAttributeValue("comment", source.Comment);

            slot.Line.SetAttributeValue("x", exitX);
            slot.Line.SetAttributeValue("y", exitY);
            slot.Line.SetAttributeValue("dp", exitDp);
        }

        #endregion
    }
}
