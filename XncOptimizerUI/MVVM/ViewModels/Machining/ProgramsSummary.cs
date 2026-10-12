using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>
    /// The "XNC List" text of a part's programs: a <c>Programs: N</c> header, then one line of
    /// the shape <c>#/type/side/&lt;params&gt;</c> per program, tool, variable, bore, groove,
    /// milling contour, rectangle and ellipse.
    /// </summary>
    public static class ProgramsSummary
    {
        /// <summary>Shown while no part of an open project is selected.</summary>
        public const string NoPart = "Programs: -";

        public static string Describe(IReadOnlyList<XncProgram> programs, string? readError)
        {
            if (readError != null)
            {
                return $"Programs: <read error: {readError}>";
            }

            if (programs.Count == 0)
            {
                return "Programs: none";
            }

            var lines = programs
                .SelectMany(ProgramLines)
                .Select((line, i) => $"{i + 1}/{line}");

            return string.Join("\n", [$"Programs: {programs.Count}", .. lines]);
        }

        /// <summary>The lines of one program, without their running number.</summary>
        private static IEnumerable<string> ProgramLines(XncProgram program)
        {
            var side = program.Side ? "front" : "back";

            string Line(string type, string parameters) => $"{type}/{side}/{parameters}";

            yield return Line("xnc", $"dx{Num(program.Dx)} dy{Num(program.Dy)} dz{Num(program.Dz)}");

            foreach (var tool in program.Tools)
            {
                yield return Line("tool", $"{tool.Name} Ø{Num(tool.Diameter)}");
            }

            foreach (var variable in program.DeclaredVariables)
            {
                yield return Line("var", DescribeVariable(variable));
            }

            foreach (var bore in program.Bores)
            {
                var through = bore.Through ? " through" : string.Empty;
                yield return Line("bore", $"{bore.Surface} {bore.ToolName} "
                    + $"({Num(bore.X)},{Num(bore.Y)},{Num(bore.Z)}) dp{Num(bore.Depth)}{through}");
            }

            foreach (var groove in program.Groovings)
            {
                yield return Line("groove", $"{groove.ToolName} "
                    + $"({Num(groove.Start.X)},{Num(groove.Start.Y)})-({Num(groove.End.X)},{Num(groove.End.Y)}) "
                    + $"dp{Num(groove.Depth)} w{Num(groove.Width)} {groove.Position}");
            }

            foreach (var contour in program.MillingContours)
            {
                yield return Line("mill", $"{contour.ToolName} "
                    + $"({Num(contour.Entry.X)},{Num(contour.Entry.Y)}) dp{Num(contour.EntryDepth)} "
                    + $"{contour.Position} {DescribeSegments(contour.Segments)}");
            }

            foreach (var rect in program.MillingRectangles)
            {
                yield return Line("rect", $"{rect.ToolName} "
                    + $"({Num(rect.Origin.X)},{Num(rect.Origin.Y)}) {Num(rect.Length)}x{Num(rect.Width)} "
                    + $"dp{Num(rect.Depth)} {rect.Position}");
            }

            foreach (var ellipse in program.MillingEllipses)
            {
                yield return Line("ellipse", $"{ellipse.ToolName} "
                    + $"({Num(ellipse.Center.X)},{Num(ellipse.Center.Y)}) "
                    + $"{Num(ellipse.Length)}x{Num(ellipse.Width)} a{Num(ellipse.Angle)} "
                    + $"dp{Num(ellipse.Depth)} {ellipse.Position}");
            }
        }

        /// <summary>
        /// XNC list text of a variable: <c>name type = expr</c>, then <c>(= value)</c> when a
        /// numeric expr is not a plain number, then <c>// comment</c>.
        /// </summary>
        private static string DescribeVariable(XncVariable variable)
        {
            var value = variable.Value is { } number && Num(number) != variable.Expr.Trim()
                ? $" (= {Num(number)})"
                : string.Empty;
            var comment = string.IsNullOrEmpty(variable.Comment) ? string.Empty : $" // {variable.Comment}";

            return $"{variable.Name} {variable.Type.XmlName()} = {variable.Expr}{value}{comment}";
        }

        private static string DescribeSegments(IReadOnlyList<XncMillingSegment> segments)
        {
            var lines = segments.Count(s => s is XncLineSegment);
            var arcs = segments.Count(s => s is XncArcSegment);

            var parts = new List<string>(2);
            if (lines > 0) parts.Add($"{lines} line");
            if (arcs > 0) parts.Add($"{arcs} arc");

            return parts.Count == 0 ? "0 seg" : string.Join("+", parts);
        }

        private static string Num(double value) => MachiningNumber.Format(value);
    }
}
