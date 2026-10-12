using System.Globalization;
using System.Text;

namespace XncOptimizerUI.MVVM.ViewModels.Parts
{
    /// <summary>
    /// The parts list as exported to CSV or copied to the clipboard: one line per part (length,
    /// width, count, the four edge band symbols, name), plus a separator line after every part cut
    /// from a "Сращ.(2)" (spliced) sheet. Sizes use the current culture, as the spreadsheet expects.
    /// </summary>
    public static class PartsListText
    {
        private const string SplicedSheetMarker = "Сращ.(2)";

        public static string Build(IEnumerable<PartVM> parts, ProjectCatalogVM catalog, char separator)
        {
            // todo: add cells with formulas
            // $"=IF(ISNUMBER(AN{row});AN{row}-AU{row};VALUE(LEFT(AN{row};SEARCH(" ";AN{row})-1))-AU{row})	=IF(ISNUMBER(AO{row});AO{row}-AV{row};VALUE(LEFT(AO{row};SEARCH(" ";AO{row})-1))-AV{row})	=L{row}-AW{row}	=IF(ISNUMBER(AN{row});AND(ISBLANK(AX{row});ISBLANK(AY{row}));EXACT(RIGHT(AN{row};LEN(AN{row})-SEARCH(" ";AN{row}));CONCAT(AX{row};AY{row};)))	=IF(ISNUMBER(AO{row});AND(ISBLANK(AZ{row});ISBLANK(BA{row}));EXACT(RIGHT(AO{row};LEN(AO{row})-SEARCH(" ";AO{row}));CONCAT(AZ{row};BA{row};)))"

            var partsList = new StringBuilder();
            var newLine = '\n';

            foreach (var part in parts)
            {
                partsList.AppendFormat(
                    CultureInfo.CurrentCulture,
                    "{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}",
                    separator,
                    part.Length,
                    part.Width,
                    part.Count,
                    catalog.BandSymbol(part.TopBandingId),
                    catalog.BandSymbol(part.BottomBandingId),
                    catalog.BandSymbol(part.LeftBandingId),
                    catalog.BandSymbol(part.RightBandingId),
                    part.Name,
                    newLine);

                var partSheet = catalog.Sheets.First(s => s.Id == part.SheetId);

                if (partSheet.Name.Contains(SplicedSheetMarker))
                {
                    partsList.AppendFormat(CultureInfo.InvariantCulture, "{0}{0}{0}{0}{0}{0}{0}-{1}", separator, newLine);
                }
            }

            partsList.Remove(partsList.Length - 1, 1);

            return partsList.ToString();
        }
    }
}
