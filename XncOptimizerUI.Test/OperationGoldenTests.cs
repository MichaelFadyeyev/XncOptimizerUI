using NSubstitute;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services;
using XncOptimizerUI.Test.Fakes;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Characterization ("golden file") tests: every document-changing operation of
    /// <see cref="GibLabProjectService"/> runs on a fixture, and the saved <c>.project</c>
    /// plus the log are compared with the snapshots in <c>Golden/Operations/</c>. They lock in
    /// the exact output while the service is refactored; a difference means behaviour changed.
    /// See <see cref="GoldenSnapshot"/> for creating and accepting snapshots.
    /// </summary>
    [TestFixture]
    public class OperationGoldenTests : TempFixtureTestBase
    {
        private const string SnapshotFolder = "Operations";
        private const string TempPlaceholder = "<temp>";

        /// <summary>Frozen so the description timestamp written into saved XML is deterministic.</summary>
        private static readonly TimeProvider _clock =
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 30, 13, 45, 0, TimeSpan.Zero));

        /// <summary>One operation run against an opened service; <c>parts</c> are all parts of the fixture.</summary>
        public delegate bool Operation(GibLabProjectService service, ref string log, List<Part> parts);

        /// <param name="ReadsParts">False for fixtures without sheet goods (ReadParts throws there); the operation then gets no parts.</param>
        public sealed record GoldenCase(string Name, string Fixture, Operation Run, bool ReadsParts = true)
        {
            public override string ToString() => Name;
        }

        public static IEnumerable<TestCaseData> Cases() => AllCases().Select(c => new TestCaseData(c).SetName(c.Name));

        [TestCaseSource(nameof(Cases))]
        public void Output_MatchesGoldenSnapshot(GoldenCase goldenCase)
        {
            var (result, log, outputPath) = Execute(goldenCase);

            var actualLog = $"result: {result}\noutput: {DescribeOutput(outputPath)}\n---\n{Normalize(log)}\n";
            var actualProject = outputPath is null ? null : Normalize(File.ReadAllText(outputPath));

            Assert.Multiple(() =>
            {
                AssertMatchesSnapshot($"{goldenCase.Name}.log.txt", actualLog);

                if (actualProject is not null)
                {
                    AssertMatchesSnapshot($"{goldenCase.Name}.project", actualProject);
                }
            });
        }

        #region Cases

        private static IEnumerable<GoldenCase> AllCases()
        {
            // Group identical elements
            yield return new("group_td", "td.project", (GibLabProjectService s, ref string log, List<Part> _) => s.GroupIdenticalElements(ref log));
            yield return new("group_execute-optimize", "td-execute-optimize.project", (GibLabProjectService s, ref string log, List<Part> _) => s.GroupIdenticalElements(ref log));

            // Prep for split along X
            yield return new("split-first-part_execute-optimize", "td-execute-optimize.project", SplitFirstPart);

            // Replace XNC programs
            yield return new("replace_execute-optimize", "td-execute-optimize.project", ReplaceFromFirstProgrammedPart);

            // Grooves -> mills
            foreach (var fixture in new[]
            {
                "td-grooving.project", "td-grooving-mixed.project", "td-grooving-interior.project",
                "td-grooving-diagonal.project", "td-grooving-secondary-pass.project", "td-gr-ml-conversion.project",
            })
            {
                yield return new($"grooves-to-mills_{Stem(fixture)}", fixture,
                    (GibLabProjectService s, ref string log, List<Part> parts) => s.ConvertGroovesAndMills(ref log, parts, GrooveMillDirection.GroovesToMills, false));
            }

            // Mills -> grooves, with and without pockets
            foreach (var fixture in new[]
            {
                "td-gr-ml-conversion.project", "td-milling.project", "td-milling-shallow.project",
                "td-milling-pocket.project", "td-pocket-to-groove.project",
            })
            {
                yield return new($"mills-to-grooves_{Stem(fixture)}", fixture,
                    (GibLabProjectService s, ref string log, List<Part> parts) => s.ConvertGroovesAndMills(ref log, parts, GrooveMillDirection.MillsToGrooves, false));
                yield return new($"mills-to-grooves-pockets_{Stem(fixture)}", fixture,
                    (GibLabProjectService s, ref string log, List<Part> parts) => s.ConvertGroovesAndMills(ref log, parts, GrooveMillDirection.MillsToGrooves, true));
            }

            // Bores -> mills, as contours and as ellipses
            foreach (var fixture in new[] { "td-bores-large.project", "td-br-ml-conversion.project", "td-bore-depth-variable.project" })
            {
                yield return new($"bores-to-mills_{Stem(fixture)}", fixture,
                    (GibLabProjectService s, ref string log, List<Part> parts) => s.ConvertBoresAndMills(ref log, parts, BoreMillDirection.BoresToMills, false));
                yield return new($"bores-to-ellipses_{Stem(fixture)}", fixture,
                    (GibLabProjectService s, ref string log, List<Part> parts) => s.ConvertBoresAndMills(ref log, parts, BoreMillDirection.BoresToMills, true));
            }

            // Mills -> bores
            foreach (var fixture in new[] { "td-br-ml-conversion.project", "td-mill-circle.project", "td-mill-ellipse.project" })
            {
                yield return new($"mills-to-bores_{Stem(fixture)}", fixture,
                    (GibLabProjectService s, ref string log, List<Part> parts) => s.ConvertBoresAndMills(ref log, parts, BoreMillDirection.MillsToBores, false));
            }

            // Mill traversal
            foreach (var fixture in new[] { "td-mills-optimization.project", "td-milling.project" })
            {
                yield return new($"traversal_{Stem(fixture)}", fixture,
                    (GibLabProjectService s, ref string log, List<Part> parts) => s.OptimizeMillTraversal(ref log, parts));
            }

            // Tool declaration sort
            foreach (var fixture in new[] { "td-programs.project", "td-gr-ml-conversion.project", "td-mill-offset.project" })
            {
                yield return new($"sort-tools_{Stem(fixture)}", fixture,
                    (GibLabProjectService s, ref string log, List<Part> parts) => s.SortToolDeclarations(ref log, parts));
            }

            // Mill path offset
            yield return new("offset-right-3-all_mill-offset", "td-mill-offset.project",
                (GibLabProjectService s, ref string log, List<Part> parts) => s.OffsetMillPaths(ref log, parts, 3, MillOffsetSide.Right, MillPathKinds.All));
            yield return new("offset-left-2-all_mill-offset", "td-mill-offset.project",
                (GibLabProjectService s, ref string log, List<Part> parts) => s.OffsetMillPaths(ref log, parts, 2, MillOffsetSide.Left, MillPathKinds.All));
            yield return new("offset-right-1.5-open_mill-offset", "td-mill-offset.project",
                (GibLabProjectService s, ref string log, List<Part> parts) => s.OffsetMillPaths(ref log, parts, 1.5, MillOffsetSide.Right, MillPathKinds.Open));
            yield return new("offset-left-4-closed_mill-circle", "td-mill-circle.project",
                (GibLabProjectService s, ref string log, List<Part> parts) => s.OffsetMillPaths(ref log, parts, 4, MillOffsetSide.Left, MillPathKinds.Closed));

            // Clean outside operations
            yield return new("clean-all_clean-outside", "td-clean-outside.project",
                (GibLabProjectService s, ref string log, List<Part> parts) => s.CleanOutsideOperations(ref log, parts, OutsideOperationKinds.All));
            yield return new("clean-bores_clean-outside", "td-clean-outside.project",
                (GibLabProjectService s, ref string log, List<Part> parts) => s.CleanOutsideOperations(ref log, parts, OutsideOperationKinds.Bores));
            yield return new("clean-grooves-mills_clean-outside", "td-clean-outside.project",
                (GibLabProjectService s, ref string log, List<Part> parts) => s.CleanOutsideOperations(ref log, parts, OutsideOperationKinds.Grooves | OutsideOperationKinds.Mills));

            // Part rotation (saved in place, as the view model does)
            foreach (var turn in new[] { 1, 2, 3 })
            {
                foreach (var flip in new[] { true, false })
                {
                    yield return new($"rotate-{turn}-flip-{flip}_rotation", "td-rotation.project", RotateAndSave(2, turn, flip), ReadsParts: false);
                }
            }

            yield return new("rotate-discordant-0_rotation", "td-rotation.project", RotateAndSave(3, 0, true), ReadsParts: false);

            // In-cell program edits (saved in place)
            yield return new("edit-bore-x_bore-depth-variable", "td-bore-depth-variable.project",
                EditFirstProgram((s, ref log, operationId) => s.UpdateBore(ref log, operationId, 0, BoreAttribute.X, "dx/2-10")));
            yield return new("edit-bore-depth_bore-depth-variable", "td-bore-depth-variable.project",
                EditFirstProgram((s, ref log, operationId) => s.UpdateBore(ref log, operationId, 0, BoreAttribute.Depth, "throughBoreDepth-10")));
            yield return new("edit-variable-name_bore-depth-variable", "td-bore-depth-variable.project",
                EditFirstProgram((s, ref log, operationId) => s.UpdateVariable(ref log, operationId, 0, XncVariableAttribute.Name, "holeDepth")));
            yield return new("edit-variable-expr_bore-depth-variable", "td-bore-depth-variable.project",
                EditFirstProgram((s, ref log, operationId) => s.UpdateVariable(ref log, operationId, 0, XncVariableAttribute.Expr, "dz+4")));
        }

        private static bool SplitFirstPart(GibLabProjectService service, ref string log, List<Part> parts)
        {
            service.PrepForSplitAlongX(ref log, [parts[0].Id.ToString(System.Globalization.CultureInfo.InvariantCulture)]);

            return true;
        }

        /// <summary>Source: first part with an XNC program; targets: every other part of the same size.</summary>
        private static bool ReplaceFromFirstProgrammedPart(GibLabProjectService service, ref string log, List<Part> parts)
        {
            var source = parts.First(p => service.GetXncProgramsCount(p.Id) > 0);
            var targets = parts
                .Where(p => p.Id != source.Id && p.Length == source.Length && p.Width == source.Width)
                .ToList();

            return service.ReplaceXncPrograms(ref log, source, targets);
        }

        private static Operation RotateAndSave(int partId, int turn, bool flipEdgeGrooveTcl) =>
            (GibLabProjectService service, ref string log, List<Part> _) =>
            {
                var result = service.RotatePart(ref log, partId, turn, flipEdgeGrooveTcl);
                service.SaveProject();

                return result;
            };

        private delegate bool ProgramEdit(GibLabProjectService service, ref string log, int operationId);

        private static Operation EditFirstProgram(ProgramEdit edit) =>
            (GibLabProjectService service, ref string log, List<Part> parts) =>
            {
                var operationId = parts
                    .SelectMany(p => service.ReadXncPrograms(p.Id))
                    .First()
                    .OperationId;
                var result = edit(service, ref log, operationId);
                service.SaveProject();

                return result;
            };

        #endregion

        #region Helpers

        private (bool Result, string Log, string? OutputPath) Execute(GoldenCase goldenCase)
        {
            var sourcePath = CopyFixture(goldenCase.Fixture);
            var sourceStamp = File.GetLastWriteTimeUtc(sourcePath);

            var service = new GibLabProjectService(CreateConfig(), _clock);
            service.OpenProject(sourcePath);
            service.ReadBands();
            service.ReadSheets();
            var parts = goldenCase.ReadsParts ? service.ReadParts().OrderBy(p => p.Id).ToList() : [];

            var log = string.Empty;
            var result = goldenCase.Run(service, ref log, parts);

            var written = service.FullPath != sourcePath || File.GetLastWriteTimeUtc(sourcePath) != sourceStamp;

            return (result, log, written ? service.FullPath : null);
        }

        private static IConfigService CreateConfig()
        {
            var config = Substitute.For<IConfigService>();
            config.SawWidth.Returns(4.0m);
            config.MillingToolDiams.Returns(new List<decimal> { 6.0m, 10.0m, 20.0m });
            config.GroovingToolWidths.Returns(new List<decimal> { 2.8m, 3.2m });

            return config;
        }

        private string DescribeOutput(string? outputPath) =>
            outputPath is null ? "(none)" : Path.GetRelativePath(TempDirectory, outputPath);

        /// <summary>Unifies line endings and hides the per-test temp directory.</summary>
        private string Normalize(string text) =>
            GoldenSnapshot.NormalizeLineEndings(text)
                .Replace(TempDirectory, TempPlaceholder, StringComparison.OrdinalIgnoreCase);

        private static void AssertMatchesSnapshot(string fileName, string actual) =>
            GoldenSnapshot.AssertMatches(Path.Combine(SnapshotFolder, fileName), actual);

        private static string Stem(string fixture) =>
            Path.GetFileNameWithoutExtension(fixture).Replace("td-", string.Empty, StringComparison.Ordinal);

        #endregion
    }
}
