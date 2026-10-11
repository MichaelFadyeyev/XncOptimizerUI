using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;

namespace XncOptimizerUI.Services.Xnc.Conversions
{
    /// <summary>
    /// Tool bookkeeping shared by the program rewriters: declaring the cutter a converted element
    /// needs (<c>Mill6</c>, <c>Cut2.8</c>, <c>Bore40</c>, ...) and dropping tools no element uses
    /// any more.
    /// </summary>
    internal static class ConversionTools
    {
        /// <summary>Diameter tolerance (mm) when matching a cutter to a groove width or an existing tool.</summary>
        public const double DiameterEpsilon = 1e-6;

        /// <summary>
        /// The tools of one program while it is rewritten: their diameters by name (any case) and
        /// the names this conversion declared.
        /// </summary>
        public sealed class ToolSet(Dictionary<string, double> diametersByName)
        {
            public Dictionary<string, double> DiametersByName { get; } = diametersByName;

            public HashSet<string> Added { get; } = new(StringComparer.OrdinalIgnoreCase);

            public static ToolSet Of(XElement program) => new(XncProgramMath.ReadToolDiameters(program));
        }

        /// <summary>
        /// Name of the <paramref name="prefix"/> tool of <paramref name="diameter"/>: the canonical
        /// <c>{prefix}{diameter}</c> when the program declares it with that diameter, otherwise a new
        /// declaration inserted before <paramref name="anchor"/> (named <c>{prefix}{diameter}_n</c>
        /// when the canonical name is taken by another diameter).
        /// </summary>
        public static string Ensure(XElement anchor, double diameter, string prefix, ToolSet tools)
        {
            var canonicalName = prefix + XmlConvert.ToString(diameter);

            if (tools.DiametersByName.TryGetValue(canonicalName, out var canonicalDiameter)
                && Math.Abs(canonicalDiameter - diameter) <= DiameterEpsilon)
            {
                return canonicalName;
            }

            var toolName = FreeName(canonicalName, tools.DiametersByName);

            anchor.AddBeforeSelf(new XElement(XncTags.Tool,
                new XAttribute("name", toolName),
                new XAttribute("d", XmlConvert.ToString(diameter))));
            tools.DiametersByName[toolName] = diameter;
            tools.Added.Add(toolName);

            return toolName;
        }

        /// <summary>
        /// Removes the <c>&lt;tool&gt;</c> declarations named in <paramref name="candidateNames"/> (the
        /// tools the rewritten elements used) that no remaining element references. Returns the
        /// number removed.
        /// </summary>
        public static int RemoveUnreferenced(XElement program, IReadOnlySet<string> candidateNames)
        {
            if (candidateNames.Count == 0)
            {
                return 0;
            }

            // A <var> @name is a variable and contour segments carry no tool name.
            var referenced = program.Elements()
                .Where(e => e.Name.LocalName is not (XncTags.Tool or XncTags.Variable) && !XncTags.IsContourSegment(e.Name.LocalName))
                .Select(e => e.GetNameValue())
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var unused = program.Elements(XncTags.Tool)
                .Where(tool => tool.GetNameValue() is { } name && candidateNames.Contains(name) && !referenced.Contains(name))
                .ToList();

            unused.ForEach(tool => tool.Remove());

            return unused.Count;
        }

        /// <summary>The tool names (any case) used by <paramref name="elements"/>.</summary>
        public static HashSet<string> NamesUsedBy(IEnumerable<XElement> elements) =>
            elements
                .Select(e => e.GetNameValue())
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        /// <summary><paramref name="baseName"/>, or the first free <c>{baseName}_n</c>.</summary>
        private static string FreeName(string baseName, Dictionary<string, double> diametersByName) =>
            diametersByName.ContainsKey(baseName)
                ? Enumerable.Range(1, int.MaxValue - 1).Select(n => $"{baseName}_{n}").First(name => !diametersByName.ContainsKey(name))
                : baseName;
    }
}
