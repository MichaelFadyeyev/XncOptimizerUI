using System.Xml.Linq;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models.Xnc;
using static XncOptimizerUI.Services.Xnc.XncAttributeParsing;

namespace XncOptimizerUI.Services.Xnc.Outside
{
    /// <summary>
    /// Finds the machining elements of one decoded XNC <c>&lt;program&gt;</c> that lie outside the
    /// part volume (<c>[0, dx] x [0, dy] x [0, dz]</c>) and never intersect it - the material they
    /// would remove has no common volume with the part (touching a face counts as outside). Each
    /// kind has its rule (raw program frame, Y-down): <see cref="BoreOutsideRule"/>,
    /// <see cref="GrooveOutsideRule"/>, <see cref="MillOutsideRule"/>.
    /// <para>An element whose values do not resolve (unknown tool, unresolvable expression,
    /// degenerate geometry) is never reported as outside; it is counted as unresolved.</para>
    /// </summary>
    internal static class OutsideOperationDetector
    {
        private static readonly IOutsideRule[] Rules = [new BoreOutsideRule(), new GrooveOutsideRule(), new MillOutsideRule()];

        /// <summary>Elements of the selected kinds found outside, and how many could not be checked.</summary>
        internal sealed record Result(IReadOnlyList<OutsideElement> Outside, int Unresolved);

        private enum Verdict
        {
            Inside,
            Outside,
            Unresolved
        }

        /// <summary>Checks every element of <paramref name="program"/> whose kind is in <paramref name="kinds"/>.</summary>
        public static Result Find(XElement program, OutsideOperationKinds kinds)
        {
            var symbols = XncSymbols.SeedProgramSymbols(program);
            var tools = ReadToolDiameters(program);
            var part = new PartVolume(
                RequireDimension(program, "dx"),
                RequireDimension(program, "dy"),
                RequireDimension(program, "dz"));

            var verdicts = Rules
                .Where(rule => kinds.HasFlag(rule.Kind))
                .SelectMany(rule => rule.Candidates(program).Select(candidate => (Rule: rule, Candidate: candidate)))
                .Select(c => (c.Candidate, Verdict: Judge(c.Rule, c.Candidate, part, symbols, tools)))
                .ToList();

            return new Result(
                verdicts.Where(v => v.Verdict == Verdict.Outside).Select(v => v.Candidate).ToList(),
                verdicts.Count(v => v.Verdict == Verdict.Unresolved));
        }

        private static Verdict Judge(
            IOutsideRule rule, OutsideElement candidate, PartVolume part, XncSymbolTable symbols, Dictionary<string, double> tools)
        {
            try
            {
                var diameter = ToolDiameter(candidate.Element, tools);

                if (diameter is not > 0d)
                {
                    return Verdict.Unresolved;
                }

                // tool.dia resolves to the element's own tool, as XncProgramReader does.
                symbols.SetToolDiameter(diameter.Value);

                return rule.IsOutside(candidate, part, symbols, diameter.Value) switch
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
    }
}
