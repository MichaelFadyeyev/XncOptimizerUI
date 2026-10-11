using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Contracts
{
    /// <summary>Reads the open project into models. Every read returns an empty list when no project is open.</summary>
    public interface IProjectReader
    {
        List<Band> ReadBands();

        List<Sheet> ReadSheets();

        List<Product> ReadProducts();

        /// <summary>Every part, with its band names and the sheet it is cut from.</summary>
        List<Part> ReadParts();

        int GetXncProgramsCount(int partId);

        /// <summary>Reads and parses every XNC machining program attached to the part with the given id.</summary>
        IReadOnlyList<XncProgram> ReadXncPrograms(int partId);

        /// <summary>
        /// Names of parts that carry more than one XNC program whose operations disagree on the
        /// <c>turn</c> value (part orientation vs. the machine coordinate origin) - a processing hazard.
        /// </summary>
        IReadOnlyList<string> GetPartsWithXncTurnDiscordance();
    }
}
