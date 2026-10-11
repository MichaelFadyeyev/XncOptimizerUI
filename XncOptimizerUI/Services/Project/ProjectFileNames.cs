using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>Output file names of the operations that save their result as a new project file.</summary>
    internal static class ProjectFileNames
    {
        public const string Extension = ".project";

        public const string GroupedSuffix = "_opt";
        public const string SplitSuffix = "_spl";
        public const string ReplacedSuffix = "_ren";
        public const string GrooveMillSuffix = "_gm";
        public const string BoreMillSuffix = "_bm";
        public const string MillOrderSuffix = "_mo";
        public const string ToolOrderSuffix = "_to";
        public const string MillOffsetSuffix = "_off";
        public const string CleanedSuffix = "_cln";

        /// <summary>
        /// Output name for an operation run on <paramref name="fileName"/>: <c>name.project</c> becomes
        /// <c>name{suffix}.project</c>; re-running on <c>name{suffix}.project</c> gives
        /// <c>name{suffix}(1).project</c>, then <c>(2)</c>, and so on.
        /// </summary>
        public static string Suffixed(string fileName, string suffix)
        {
            var escaped = Regex.Escape(suffix);
            var plain = new Regex($@"{escaped}\.project$");
            var numbered = new Regex($@"{escaped}\((\d+)\)\.project$");

            if (plain.IsMatch(fileName))
            {
                return plain.Replace(fileName, $"{suffix}(1){Extension}");
            }

            if (numbered.Match(fileName) is { Success: true } match)
            {
                var version = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

                return numbered.Replace(fileName, $"{suffix}({version + 1}){Extension}");
            }

            return Path.GetFileNameWithoutExtension(fileName) + suffix + Extension;
        }
    }
}
