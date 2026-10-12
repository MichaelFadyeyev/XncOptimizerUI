using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>
    /// "Prep for split along X": each selected part becomes a double-width blank that is sawn in
    /// two after machining. Its width becomes <c>2w + saw kerf</c>, the derived widths
    /// (<c>dw</c>/<c>cw</c>) grow with it, the count halves (rounded up), and each of its XNC
    /// programs gets the new <c>dy</c> plus a mirrored copy of every bore for the second half.
    /// Saved as a new <see cref="ProjectFileNames.SplitSuffix"/> file.
    /// </summary>
    internal sealed class SplitAlongXPreparer(ProjectDocument document, IConfigService config)
    {
        public OperationResult Prepare(IReadOnlyCollection<string> partIds)
        {
            var log = new OperationLog();

            try
            {
                document.ProductParts()
                    .Where(part => partIds.Contains(part.GetIdValue()!))
                    .ToList()
                    .ForEach(part => log.Info(PreparePart(part)));
            }
            catch (Exception e)
            {
                return log.Fail(e.Message);
            }

            document.AppendDescription("specified parts prep for split along X");

            return log.Highlight($"Stored to: {document.SaveAsSuffixed(ProjectFileNames.SplitSuffix)}").Changed();
        }

        #region Methods

        /// <summary>Resizes one part and its programs; returns its log line.</summary>
        private string PreparePart(XElement part)
        {
            var width = RequireDecimal(part, "w");
            var newWidth = width * 2 + config.SawWidth;
            var jointShrink = width - RequireDecimal(part, "jw");

            part.SetAttributeValue("w", XmlConvert.ToString(newWidth));
            part.SetAttributeValue("cw", XmlConvert.ToString(RequireDecimal(part, "cw") - jointShrink));
            part.SetAttributeValue("dw", XmlConvert.ToString(RequireDecimal(part, "dw") - jointShrink));

            var count = int.Parse(part.Attribute("count")!.Value, CultureInfo.InvariantCulture);
            var newCount = count / 2 + count % 2;
            part.Attribute("count")!.SetValue(newCount);

            foreach (var operation in document.XncOperationsOf(part.GetIdIntValue()))
            {
                PrepareProgram(operation, newWidth, newCount);
            }

            return $"{part.GetNameValue()} resized to {XmlConvert.ToString(newWidth)}; count changed: {count} -> {newCount}";
        }

        /// <summary>Sets the program's <c>dy</c>, adds a mirrored copy of each bore, and updates the operation's counts.</summary>
        private static void PrepareProgram(XElement operation, decimal newWidth, int newCount)
        {
            var programDocument = XncProgramText.Load(operation);
            var program = XncProgramText.Root(programDocument);

            program.Attribute("dy")!.SetValue(newWidth);

            var bores = program.Elements().Where(e => XncTags.IsBore(e.Name.LocalName)).ToList();
            bores.ForEach(bore => program.Add(MirroredBore(bore, newWidth)));

            operation.Attribute("count")!.SetValue($"{newCount}");
            operation.Attribute("countBore")!.SetValue($"{bores.Count * 2}");

            XncProgramText.Write(operation, XncProgramText.Serialize(programDocument));
        }

        /// <summary>
        /// The bore for the other half: a face or side (<c>bl</c>/<c>br</c>) bore keeps its copy at
        /// the authored place and moves to <c>y' = width - y</c>; a top/bottom bore is repeated on
        /// the opposite edge.
        /// </summary>
        private static XElement MirroredBore(XElement bore, decimal newWidth)
        {
            if (bore.Name.LocalName is XncTags.TopBore or XncTags.BottomBore)
            {
                return new XElement(bore.Name.LocalName == XncTags.TopBore ? XncTags.BottomBore : XncTags.TopBore, bore.Attributes());
            }

            var copy = new XElement(bore);
            var y = bore.Attribute("y");
            var yValue = XmlConvert.ToDecimal(y?.Value ?? throw new ArgumentException("""XAttribute "y" not found or has no value"""));

            y.SetValue(XmlConvert.ToString(newWidth - yValue));

            return copy;
        }

        private static decimal RequireDecimal(XElement part, string attribute) =>
            XmlConvert.ToDecimal(part.Attribute(attribute)?.Value
                ?? throw new ArgumentException($"""XAttribute "{attribute}" not found or has no value"""));

        #endregion
    }
}
