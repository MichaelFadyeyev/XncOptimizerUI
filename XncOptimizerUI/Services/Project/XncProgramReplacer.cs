using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>
    /// "Replace XNC programs": copies the source part's programs onto identical target parts, face
    /// by face. Every target is validated first - same size and bands, an XNC operation for exactly
    /// the source's faces (side + turn) - and nothing is copied unless all pass. Saved as a new
    /// <see cref="ProjectFileNames.ReplacedSuffix"/> file.
    /// </summary>
    internal sealed class XncProgramReplacer(ProjectDocument document)
    {
        /// <summary>What must match between source and target, and how a mismatch is described.</summary>
        private static readonly (Func<Part, Part, bool> Matches, Func<Part, Part, string> Reason)[] IdentityRules =
        [
            ((s, t) => s.Length == t.Length, (s, t) => $"length {t.Length} != {s.Length}"),
            ((s, t) => s.Width == t.Width, (s, t) => $"width {t.Width} != {s.Width}"),
            ((s, t) => s.TopBandingId == t.TopBandingId, (_, _) => "top band differs"),
            ((s, t) => s.BottomBandingId == t.BottomBandingId, (_, _) => "bottom band differs"),
            ((s, t) => s.LeftBandingId == t.LeftBandingId, (_, _) => "left band differs"),
            ((s, t) => s.RightBandingId == t.RightBandingId, (_, _) => "right band differs"),
        ];

        public OperationResult Replace(Part source, IList<Part>? targets)
        {
            var log = new OperationLog();

            if (targets == null || targets.Count == 0)
            {
                return log.Fail("No target parts selected for XNC replacement.");
            }

            var sourceOperations = document.XncOperationsOf(source.Id);

            if (sourceOperations.Count == 0)
            {
                return log.Fail($"""Source part "{source.Name}" (id={source.Id}) has no XNC operation.""");
            }

            if (sourceOperations.GroupBy(FaceKey).Any(g => g.Count() > 1))
            {
                return log.Fail($"""Source part "{source.Name}" (id={source.Id}) has ambiguous XNC operations (duplicate face).""");
            }

            var problems = targets.Select(target => FindProblem(source, sourceOperations, target)).OfType<string>().ToList();

            if (problems.Count > 0)
            {
                return log.Fail("Copy aborted. The following parts are not identical to the source part:\n\n" + string.Join("\n", problems));
            }

            var sourceByFace = sourceOperations.ToDictionary(FaceKey);

            foreach (var target in targets)
            {
                var targetOperations = document.XncOperationsOf(target.Id);

                targetOperations.ForEach(operation => CopyProgram(sourceByFace[FaceKey(operation)], operation));
                log.Info($"XNC replaced: \"{target.Name}\" <- \"{source.Name}\" ({targetOperations.Count} face(s))");
            }

            document.AppendDescription("replaced XNC programs with source part");

            return log.Highlight($"Stored to: {document.SaveAsSuffixed(ProjectFileNames.ReplacedSuffix)}").Changed();
        }

        #region Methods

        /// <summary>Why <paramref name="target"/> cannot receive the source's programs, or <c>null</c>.</summary>
        private string? FindProblem(Part source, List<XElement> sourceOperations, Part target)
        {
            var targetOperations = document.XncOperationsOf(target.Id);

            if (targetOperations.Count == 0)
            {
                return $"""Part "{target.Name}" (id={target.Id}) has no XNC operation.""";
            }

            if (IdentityRules.FirstOrDefault(rule => !rule.Matches(source, target)) is { Reason: { } reason })
            {
                return $"""Part "{target.Name}" (id={target.Id}) differs from source: {reason(source, target)}.""";
            }

            var sourceFaces = sourceOperations.Select(FaceKey).Order();
            var targetFaces = targetOperations.Select(FaceKey).Order();

            return targetFaces.SequenceEqual(sourceFaces)
                ? null
                : $"""Part "{target.Name}" (id={target.Id}) has different XNC faces/turn than the source — source [{DescribeFaces(sourceOperations)}] vs target [{DescribeFaces(targetOperations)}].""";
        }

        /// <summary>Overwrites the target's program and bore count with the source's.</summary>
        private static void CopyProgram(XElement source, XElement target)
        {
            target.GetProgram()!.Value = source.GetProgramValue()!;

            if (source.Attribute("countBore")?.Value is { } countBore)
            {
                target.Attribute("countBore")?.SetValue(countBore);
            }
        }

        /// <summary>
        /// A program is only valid on a part oriented the same way: operations are matched by the
        /// drilled face (<c>side</c>, front/back) and the part's rotation in the layout (<c>turn</c>).
        /// </summary>
        private static string FaceKey(XElement operation) =>
            $"{operation.Attribute("side")?.Value ?? string.Empty}|{operation.Attribute("turn")?.Value ?? string.Empty}";

        private static string DescribeFaces(IEnumerable<XElement> operations) =>
            string.Join("; ", operations
                .OrderBy(FaceKey)
                .Select(o => $"side={o.Attribute("side")?.Value ?? "?"},turn={o.Attribute("turn")?.Value ?? "?"}"));

        #endregion
    }
}
