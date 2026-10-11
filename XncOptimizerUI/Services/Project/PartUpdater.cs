using System.Text.RegularExpressions;
using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>
    /// Writes an edited <see cref="Part"/> (name, size, texture, edge bands) back into its
    /// <c>&lt;part&gt;</c>, keeping the dependent data in step: the XNC operation names, every EL
    /// operation's part list and the joint size. Does not save.
    /// </summary>
    internal sealed partial class PartUpdater(ProjectDocument document, ProjectReader reader)
    {
        /// <summary>
        /// The part's four banded edges: XML reference attribute, matching <see cref="Part"/> id
        /// getter and band-name setter. Top/bottom bands run along the length, so their thickness
        /// reduces the joint width (<c>jw</c>); left/right ones reduce the joint length (<c>jl</c>).
        /// </summary>
        private static readonly (string Attribute, Func<Part, int?> GetId, Action<Part, string?> SetMat)[] BandEdges =
        [
            ("elt", p => p.TopBandingId, (p, mat) => p.TopBandingMat = mat),
            ("elb", p => p.BottomBandingId, (p, mat) => p.BottomBandingMat = mat),
            ("ell", p => p.LeftBandingId, (p, mat) => p.LeftBandingMat = mat),
            ("elr", p => p.RightBandingId, (p, mat) => p.RightBandingMat = mat),
        ];

        /// <summary>Applies <paramref name="part"/>; unchanged when the part is unknown or nothing differs.</summary>
        public OperationResult Update(Part part)
        {
            var log = new OperationLog();
            var element = document.ProductParts().FirstOrDefault(p => p.GetIdIntValue() == part.Id);

            if (element == null || !HasChanged(element, part))
            {
                return log.Unchanged();
            }

            var bands = reader.ReadBands();

            RenameXncOperations(log, part);
            ApplyNameSizeAndTexture(element, part);
            ApplyBanding(log, element, part, bands);
            SyncBandOperationParts(element);
            ApplyJointSizes(element, bands);

            return log.Changed();
        }

        #region Methods

        private static bool HasChanged(XElement element, Part part) =>
            element.GetNameValue() != part.Name
            || element.GetLengthDecimalValue() != part.Length
            || element.GetWidthDecimalValue() != part.Width
            || element.GetTxtBoolValue() != part.ConsiderTexture
            || BandEdges.Any(edge => element.GetBandingIdIntValue(edge.Attribute) != edge.GetId(part));

        private static void ApplyNameSizeAndTexture(XElement element, Part part)
        {
            element.SetNameValue(part.Name);
            element.SetLengthValue(part.Length);
            element.SetDLengthValue(part.Length);
            element.SetWidthValue(part.Width);
            element.SetDWidthValue(part.Width);
            element.SetTxtValue(part.ConsiderTexture);
        }

        /// <summary>Renames the part's XNC operations after the part, keeping their own <c>[group code]</c> prefix.</summary>
        private void RenameXncOperations(OperationLog log, Part part)
        {
            foreach (var operation in document.XncOperationsOf(part.Id))
            {
                var oldName = operation.GetTypeNameValue()!;

                if (RenamedXncTypeName(oldName, part.Name) is { } newName)
                {
                    operation.SetTypeNameValue(newName);
                    continue;
                }

                log.Info($"""Cannot set new XNC typeName for part "{part.Name}" with old XNC typeName "{oldName}".""");
            }
        }

        /// <summary>
        /// The XNC operation name for a part renamed to <paramref name="partName"/>: the part name
        /// itself, or - when both carry a <c>[group code]</c> - the operation's code plus the part's
        /// new name. <c>null</c> when only one of them has a code.
        /// </summary>
        private static string? RenamedXncTypeName(string xncName, string partName)
        {
            var partMatch = GroupCodePattern().Match(partName);
            var xncMatch = GroupCodePattern().Match(xncName);

            return (partMatch.Success, xncMatch.Success) switch
            {
                (true, true) => xncMatch.Groups[1].Value + partMatch.Groups[2].Value,
                (false, false) => partName,
                _ => null,
            };
        }

        /// <summary>
        /// Writes each edge's band reference + name from <paramref name="part"/>'s banding ids and
        /// mirrors the resolved band name back into the model. An id that matches no EL operation
        /// leaves that edge untouched.
        /// </summary>
        private static void ApplyBanding(OperationLog log, XElement element, Part part, List<Band> bands)
        {
            foreach (var (attribute, getId, setMat) in BandEdges)
            {
                var bandId = getId(part);
                var band = bandId == null ? null : bands.Find(b => b.Id == bandId);

                if (bandId != null && band == null)
                {
                    log.Info($"""Cannot assign band operation #{bandId} to "{attribute}" of part "{part.Name}": no such band.""");
                    continue;
                }

                element.SetBandingValue(attribute, bandId, band?.Name);
                setMat(part, band?.Name);
            }
        }

        /// <summary>
        /// Keeps every EL operation's <c>&lt;part id&gt;</c> list in step with the part's edges: the
        /// part is listed (in ascending id order) by each operation it uses on any edge, and only by those.
        /// </summary>
        private void SyncBandOperationParts(XElement element)
        {
            var partId = element.GetIdIntValue();
            var usedOperationIds = BandEdges
                .Select(edge => element.GetBandingIdIntValue(edge.Attribute))
                .OfType<int>()
                .ToHashSet();

            foreach (var operation in document.Operations(GibLabTypes.BandOperation))
            {
                var listed = operation.GetParts().Where(p => p.GetIdIntValue() == partId).ToList();
                var isUsed = usedOperationIds.Contains(operation.GetIdIntValue());

                if (isUsed && listed.Count == 0)
                {
                    InsertPartReference(operation, partId);
                }
                else if (!isUsed)
                {
                    listed.ForEach(p => p.Remove());
                }
            }
        }

        private static void InsertPartReference(XElement operation, int partId)
        {
            var reference = new XElement("part", new XAttribute("id", partId));
            var next = operation.GetParts().FirstOrDefault(p => p.GetIdIntValue() > partId);

            if (next != null)
            {
                next.AddBeforeSelf(reference);
            }
            else if (operation.GetParts().LastOrDefault() is { } last)
            {
                last.AddAfterSelf(reference);
            }
            else
            {
                operation.Add(reference);
            }
        }

        /// <summary>
        /// Recalculates the part's joint size (size before edge banding): <c>jl = l − t(ell) − t(elr)</c>,
        /// <c>jw = w − t(elt) − t(elb)</c>, with each band's thickness taken from its band good.
        /// </summary>
        private static void ApplyJointSizes(XElement element, List<Band> bands)
        {
            decimal Thickness(string edgeAttribute)
            {
                var bandId = element.GetBandingIdIntValue(edgeAttribute);

                return bands.Find(b => b.Id == bandId)?.Thickness ?? 0m;
            }

            element.SetJLengthValue(element.GetLengthDecimalValue() - Thickness("ell") - Thickness("elr"));
            element.SetJWidthValue(element.GetWidthDecimalValue() - Thickness("elt") - Thickness("elb"));
        }

        [GeneratedRegex(@"^(\[.*?\])(.*)", RegexOptions.CultureInvariant)]
        private static partial Regex GroupCodePattern();

        #endregion
    }
}
