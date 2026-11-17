using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>
    /// Reads a <c>.project</c> file in whatever encoding it was written: a byte-order mark wins, then
    /// a non-UTF-8 encoding named by the XML declaration (e.g. <c>windows-1251</c>), then UTF-8 when the
    /// bytes are valid UTF-8, otherwise <c>windows-1251</c> (Cyrillic files without a declaration or
    /// with a wrong one).
    /// </summary>
    public static partial class ProjectFileEncoding
    {
        private const int FallbackCodePage = 1251;
        private const int DeclarationProbeLength = 256;

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        static ProjectFileEncoding()
        {
            // Code-page encodings (windows-1251, ...) are not available on .NET until registered.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>Parses the file at <paramref name="path"/> and returns it with the encoding it was read in.</summary>
        public static (XDocument Document, Encoding Source) Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var encoding = DetectEncoding(bytes);

            return (XDocument.Parse(Decode(bytes, encoding)), encoding);
        }

        /// <summary>Whether <paramref name="encoding"/> is UTF-8.</summary>
        public static bool IsUtf8(Encoding encoding) => encoding.CodePage == Encoding.UTF8.CodePage;

        /// <summary>The encoding <paramref name="bytes"/> were written in (see the class summary for the order).</summary>
        public static Encoding DetectEncoding(byte[] bytes) =>
            EncodingFromByteOrderMark(bytes)
            ?? EncodingFromDeclaration(bytes)
            ?? (IsValidUtf8(bytes) ? Encoding.UTF8 : Encoding.GetEncoding(FallbackCodePage));

        private static Encoding? EncodingFromByteOrderMark(byte[] bytes) =>
            new Encoding[] { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode }
                .FirstOrDefault(encoding => bytes.AsSpan().StartsWith(encoding.Preamble));

        private static Encoding? EncodingFromDeclaration(byte[] bytes)
        {
            var name = DeclaredEncodingName(bytes);

            if (name == null)
            {
                return null;
            }

            var encoding = Encoding.GetEncoding(name);

            // A declared UTF-8 is not trusted: the bytes decide between UTF-8 and the fallback.
            return IsUtf8(encoding) ? null : encoding;
        }

        private static string? DeclaredEncodingName(byte[] bytes)
        {
            var probe = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, DeclarationProbeLength));
            var match = DeclarationEncodingPattern().Match(probe);

            return match.Success ? match.Groups["name"].Value : null;
        }

        private static bool IsValidUtf8(byte[] bytes)
        {
            try
            {
                StrictUtf8.GetCharCount(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        private static string Decode(byte[] bytes, Encoding encoding)
        {
            var preamble = encoding.Preamble;
            var start = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;

            return encoding.GetString(bytes, start, bytes.Length - start);
        }

        [GeneratedRegex("""^\s*<\?xml[^>]*\bencoding\s*=\s*["'](?<name>[A-Za-z0-9._:-]+)["']""")]
        private static partial Regex DeclarationEncodingPattern();
    }
}
