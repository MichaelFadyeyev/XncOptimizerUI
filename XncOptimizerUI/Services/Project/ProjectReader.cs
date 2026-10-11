using System.Globalization;
using System.IO;
using System.Xml.Linq;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Extensions;
using XncOptimizerUI.Helpers.Enums;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.Services.Project
{
    /// <summary>
    /// Reads the open project into models: bands (EL operations + band goods), sheets (CS
    /// operations + sheet goods), products, parts and their XNC programs. Every read is
    /// self-contained - parts resolve their band names and sheet themselves - and returns an
    /// empty list when no project is open.
    /// </summary>
    public sealed class ProjectReader(ProjectDocument document) : IProjectReader
    {
        /// <summary>One band per EL operation, named after its band good; external symbols A-Z are assigned by thickness.</summary>
        public List<Band> ReadBands()
        {
            if (!document.IsOpen)
            {
                return [];
            }

            var bandGoods = document.Goods(GibLabTypes.BandGood);
            var bands = document.Operations(GibLabTypes.BandOperation)
                .Select(operation => CreateBand(operation, MaterialGood(operation, bandGoods)))
                .ToList();

            AssignExternalSymbols(bands);

            return bands;
        }

        /// <summary>One sheet per CS operation, named after its sheet good.</summary>
        public List<Sheet> ReadSheets()
        {
            if (!document.IsOpen)
            {
                return [];
            }

            var sheetGoods = document.Goods(GibLabTypes.SheetGood);

            return document.Operations(GibLabTypes.CutOperation)
                .Select(operation => CreateSheet(operation, MaterialGood(operation, sheetGoods)))
                .ToList();
        }

        public List<Product> ReadProducts()
        {
            if (!document.IsOpen)
            {
                return [];
            }

            return document.Goods(GibLabTypes.ProductGood)
                .Select(good => new Product
                {
                    Id = int.Parse(good.GetIdValue()!, CultureInfo.InvariantCulture),
                    Name = good.GetNameValue() ?? string.Empty,
                    Code = good.GetCodeValue() ?? string.Empty,
                    Count = int.Parse(good.Attribute("count")?.Value ?? "0", CultureInfo.InvariantCulture),
                })
                .ToList();
        }

        /// <summary>Every part of every product good, with band names and the sheet it is cut from.</summary>
        /// <exception cref="InvalidDataException">A part is not cut from any sheet.</exception>
        public List<Part> ReadParts()
        {
            if (!document.IsOpen)
            {
                return [];
            }

            var bands = ReadBands();
            var cutOperations = document.Operations(GibLabTypes.CutOperation);

            return document.Goods(GibLabTypes.ProductGood)
                .SelectMany(good => good.GetParts().Select(element => CreatePart(element, good)))
                .Select(part => WithBandNames(part, bands))
                .Select(part => WithSheet(part, cutOperations))
                .ToList();
        }

        public int GetXncProgramsCount(int partId) =>
            document.IsOpen ? document.XncOperationsOf(partId).Count : 0;

        /// <summary>Reads and parses every XNC program attached to the part with the given id.</summary>
        public IReadOnlyList<XncProgram> ReadXncPrograms(int partId) =>
            document.IsOpen ? document.XncOperationsOf(partId).Select(XncProgramReader.Read).ToList() : [];

        /// <summary>
        /// Names of parts whose several XNC operations disagree on <c>turn</c> (compared as the raw
        /// attribute, absent = <c>"0"</c>) - a processing hazard the operator must see on open.
        /// </summary>
        public IReadOnlyList<string> GetPartsWithXncTurnDiscordance()
        {
            if (!document.IsOpen)
            {
                return [];
            }

            var nameById = document.ProductParts()
                .GroupBy(p => p.GetIdIntValue())
                .ToDictionary(g => g.Key, g => g.First().GetNameValue());

            return document.XncOperations()
                .Select(o => (Id: o.GetPart()?.GetIdIntValue(), Turn: o.GetTurnValue() ?? "0"))
                .Where(x => x.Id is not null)
                .GroupBy(x => x.Id!.Value)
                .Where(g => g.Count() > 1 && g.Select(x => x.Turn).Distinct().Count() > 1)
                .Select(g => nameById.TryGetValue(g.Key, out var name) && name != null ? name : $"id={g.Key}")
                .ToList();
        }

        #region Methods

        /// <summary>The good referenced by an operation's <c>&lt;material id&gt;</c>.</summary>
        private static XElement MaterialGood(XElement operation, IEnumerable<XElement> goods)
        {
            var materialId = operation.GetOperationMaterialIdValue();

            return goods.Single(g => g.GetIdValue() == materialId);
        }

        private static Band CreateBand(XElement operation, XElement good) => new()
        {
            Id = operation.GetIdIntValue(),
            InternalSymbol = operation.Attribute("elSymbol")!.Value,
            Name = good.GetNameValue()!,
            Code = good.GetCodeValue()!,
            Thickness = good.GetThicknessDecimalValue(),
            Width = good.GetWidthDecimalValue(),
        };

        /// <summary>Distinct band codes, ordered by thickness, get the letters A, B, C, ...</summary>
        private static void AssignExternalSymbols(List<Band> bands)
        {
            var orderedCodes = bands
                .OrderBy(b => b.Thickness)
                .Select(b => b.Code)
                .Distinct()
                .ToList();
            var symbols = Enum.GetNames<BandSymbols>();

            bands.ForEach(band => band.ExternalSymbol = symbols[orderedCodes.IndexOf(band.Code)]);
        }

        private static Sheet CreateSheet(XElement operation, XElement good) => new()
        {
            Id = operation.GetMat()!.GetIdIntValue(),
            Name = good.GetNameValue()!,
            Code = good.GetCodeValue()!,
            Thickness = good.GetThicknessDecimalValue(),
        };

        private static Part CreatePart(XElement element, XElement good) => new()
        {
            Id = element.GetIdIntValue(),
            GoodId = int.Parse(good.GetIdValue()!, CultureInfo.InvariantCulture),
            Name = element.GetNameValue()!,
            Count = int.Parse(element.Attribute("count")!.Value, CultureInfo.InvariantCulture),
            Length = element.GetLengthDecimalValue(),
            Width = element.GetWidthDecimalValue(),
            ConsiderTexture = element.GetTxtBoolValue(),
            TopBandingId = element.GetEltIdIntValue(),
            BottomBandingId = element.GetElbIdIntValue(),
            LeftBandingId = element.GetEllIdIntValue(),
            RightBandingId = element.GetElrIdIntValue(),
        };

        private static Part WithBandNames(Part part, List<Band> bands)
        {
            string? NameOf(int? bandId) => bandId == null ? null : bands.Find(b => b.Id == bandId)!.Name;

            part.TopBandingMat = NameOf(part.TopBandingId);
            part.BottomBandingMat = NameOf(part.BottomBandingId);
            part.LeftBandingMat = NameOf(part.LeftBandingId);
            part.RightBandingMat = NameOf(part.RightBandingId);

            return part;
        }

        /// <summary>Sets the sheet material of the first CS operation listing the part (its last <c>&lt;part&gt;</c> is the sheet itself).</summary>
        private static Part WithSheet(Part part, List<XElement> cutOperations)
        {
            var operation = cutOperations.FirstOrDefault(o => o.GetParts().SkipLast(1).Any(p => p.GetIdIntValue() == part.Id))
                ?? throw new InvalidDataException($"No Sheet found for part with Id={part.Id}");

            part.SheetId = operation.GetMat()!.GetIdIntValue();

            return part;
        }

        #endregion
    }
}
