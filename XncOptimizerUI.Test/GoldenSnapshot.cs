using System.Runtime.CompilerServices;
using System.Text;

namespace XncOptimizerUI.Test
{
    /// <summary>
    /// Compares text with a committed snapshot in <c>Golden/</c> (next to this source file). A
    /// missing snapshot is written and the assertion fails, so new snapshots get reviewed and
    /// committed. With the environment variable <see cref="UpdateVariable"/> set to <c>1</c>,
    /// every snapshot is rewritten instead, to accept an intended change (review the diff).
    /// Line endings are ignored.
    /// </summary>
    internal static class GoldenSnapshot
    {
        public const string UpdateVariable = "XNC_UPDATE_GOLDEN";

        public static void AssertMatches(string relativePath, string actual)
        {
            var path = Path.Combine(Directory(), relativePath);
            var normalized = NormalizeLineEndings(actual);
            var update = Environment.GetEnvironmentVariable(UpdateVariable) == "1";

            if (update || !File.Exists(path))
            {
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                if (!update)
                {
                    Assert.Fail($"Golden snapshot {relativePath} did not exist and was created; review and commit it.");
                }

                return;
            }

            Assert.That(normalized, Is.EqualTo(NormalizeLineEndings(File.ReadAllText(path))),
                $"{relativePath} differs from its golden snapshot. If the change is intended, rerun with {UpdateVariable}=1 and review the diff.");
        }

        public static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

        private static string Directory([CallerFilePath] string sourceFile = "") =>
            Path.Combine(Path.GetDirectoryName(sourceFile)!, "Golden");
    }
}
