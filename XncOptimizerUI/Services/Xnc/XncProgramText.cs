using System.Net;
using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.Extensions;

namespace XncOptimizerUI.Services.Xnc
{
    /// <summary>
    /// Reads and writes the XNC program carried, escaped, by the <c>program</c> attribute of an
    /// <c>&lt;operation typeId="XNC"&gt;</c>. Every rewriter goes through here, so a rewritten
    /// program keeps the <c>&lt;?xml ...?&gt;</c> declaration GibLab writes.
    /// </summary>
    internal static class XncProgramText
    {
        /// <summary>Parses the program of <paramref name="operation"/>.</summary>
        /// <exception cref="XncProgramFormatException">No <c>program</c> attribute, not valid XML, or no <c>&lt;program&gt;</c> root.</exception>
        public static XDocument Load(XElement operation)
        {
            var raw = operation.GetProgramValue()
                ?? throw new XncProgramFormatException("XNC operation has no 'program' attribute.");

            XDocument document;

            try
            {
                // The attribute value is entity-escaped once and LINQ-to-XML already decoded it;
                // HtmlDecode is kept so programs read exactly as before.
                document = XDocument.Parse(WebUtility.HtmlDecode(raw));
            }
            catch (XmlException ex)
            {
                throw new XncProgramFormatException("The 'program' attribute is not valid XML.", ex);
            }

            _ = Root(document);

            return document;
        }

        /// <summary>The <c>&lt;program&gt;</c> element of a loaded program document.</summary>
        public static XElement Root(XDocument document) =>
            document.Element(XncTags.Program)
                ?? throw new XncProgramFormatException("XNC program has no <program> root element.");

        /// <summary>Program text to store in the <c>program</c> attribute: the declaration (when present) plus the root.</summary>
        public static string Serialize(XDocument document) =>
            document.Declaration is { } declaration
                ? declaration + Root(document).ToString()
                : Root(document).ToString();

        /// <summary>Stores <paramref name="programText"/> as the program of <paramref name="operation"/>.</summary>
        public static void Write(XElement operation, string programText) =>
            operation.SetAttributeValue("program", programText);
    }
}
