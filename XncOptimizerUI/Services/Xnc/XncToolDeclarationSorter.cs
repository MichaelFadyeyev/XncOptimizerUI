using System.Xml.Linq;
using XncOptimizerUI.Extensions;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Moves every <c>&lt;tool&gt;</c> declaration of one decoded XNC <c>&lt;program&gt;</c> to the
    /// start of the program, so each tool is declared before every element that uses it. The block
    /// is ordered by tool kind (name prefix <c>Bore</c>, then <c>Cut</c>, then <c>Mill</c>, then any
    /// other name), then by diameter ascending, then by name. Repeated declarations of the same tool
    /// (same name, same <c>d</c>) collapse into the first one.
    /// <para>The program is left untouched when hoisting would change its meaning: a tool name
    /// declared with different diameters (the reader's "last wins" would no longer hold per element),
    /// a <c>d</c> that is not a number, or a declaration splitting a milling contour (removing it
    /// would join the following segments to the contour before it).</para>
    /// </summary>
    internal static class XncToolDeclarationSorter
    {
        private static readonly string[] KindPrefixes = XncToolKinds.Ordered;

        private static readonly HashSet<string> ContourParts = ["ms", "ml", "mac", "ma"];

        private static readonly HashSet<string> ContourSegments = ["ml", "mac", "ma"];

        /// <summary>Outcome for one program: whether it was rewritten, and why it was skipped if so.</summary>
        public readonly record struct Result(bool Sorted, IReadOnlyList<string> Problems)
        {
            public bool Skipped => Problems.Count > 0;
        }

        /// <summary>
        /// Hoists and orders the tool declarations of <paramref name="program"/> in place. Returns
        /// <see cref="Result.Sorted"/> = <c>false</c> when the program already starts with the
        /// ordered block, or when it was skipped (see <see cref="Result.Problems"/>).
        /// </summary>
        public static Result SortInProgram(XElement program)
        {
            var tools = program.Elements("tool").ToList();
            var problems = FindProblems(tools);

            if (problems.Count > 0)
            {
                return new Result(false, problems);
            }

            var ordered = OrderDeclarations(tools);

            if (StartsWithExactly(program, tools, ordered))
            {
                return new Result(false, []);
            }

            tools.ForEach(tool => tool.Remove());
            program.AddFirst(ordered);

            return new Result(true, []);
        }

        private static List<string> FindProblems(IReadOnlyList<XElement> tools) =>
            tools.Where(tool => ParseDiameter(tool) is null).Select(DescribeInvalidDiameter)
                .Concat(FindConflictingNames(tools).Select(name => $"tool \"{name}\" declared with different diameters"))
                .Concat(tools.Where(SplitsContour).Select(tool => $"tool \"{ToolName(tool)}\" declared inside a milling contour"))
                .Distinct()
                .ToList();

        private static string DescribeInvalidDiameter(XElement tool) =>
            $"tool \"{tool.GetNameValue()}\" has a non-numeric diameter \"{tool.GetDValue()}\"";

        private static IEnumerable<string> FindConflictingNames(IEnumerable<XElement> tools) =>
            tools
                .Where(tool => ParseDiameter(tool) is not null)
                .GroupBy(ToolName, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Select(tool => ParseDiameter(tool)!.Value).Distinct().Count() > 1)
                .Select(group => group.Key);

        /// <summary>
        /// A tool (or a run of tools) between a contour element and a following segment ends that
        /// contour early.
        /// </summary>
        private static bool SplitsContour(XElement tool) =>
            tool.ElementsBeforeSelf().LastOrDefault(IsNotTool) is { } previous
            && ContourParts.Contains(previous.Name.LocalName)
            && tool.ElementsAfterSelf().FirstOrDefault(IsNotTool) is { } next
            && ContourSegments.Contains(next.Name.LocalName);

        private static bool IsNotTool(XElement element) => element.Name.LocalName != "tool";

        private static List<XElement> OrderDeclarations(IEnumerable<XElement> tools) =>
            tools
                .DistinctBy(ToolName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(tool => KindRank(ToolName(tool)))
                .ThenBy(tool => ParseDiameter(tool)!.Value)
                .ThenBy(ToolName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static bool StartsWithExactly(XElement program, IReadOnlyCollection<XElement> tools, IReadOnlyList<XElement> ordered) =>
            tools.Count == ordered.Count
            && program.Elements().Take(ordered.Count).SequenceEqual(ordered);

        /// <summary>Position of the tool's kind in Bore - Cut - Mill order; other names go last.</summary>
        private static int KindRank(string name)
        {
            var index = Array.FindIndex(KindPrefixes, prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            return index < 0 ? KindPrefixes.Length : index;
        }

        private static string ToolName(XElement tool) => tool.GetNameValue() ?? string.Empty;

        private static double? ParseDiameter(XElement tool) => XncAttributeParsing.ToolDiameter(tool);
    }
}
