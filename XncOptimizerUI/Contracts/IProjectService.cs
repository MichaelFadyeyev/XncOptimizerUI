using System.Collections.ObjectModel;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Contracts
{
    public interface IProjectService
    {
        void OpenProject(string path);
        void CloseProject();
        bool GroupIdenticalElements(ref string log);
        void PrepForSplitAlongX(ref string log, string[] selectedPartsIds);
        bool ReplaceXncPrograms(ref string log, Part sourcePart, IList<Part> targetParts);
        int GetXncProgramsCount(int partId);

        /// <summary>Reads and parses every XNC machining program attached to the part with the given id.</summary>
        IReadOnlyList<XncProgram> ReadXncPrograms(int partId);
        void SaveProject();
        bool UpdatePart(ref string log, Part part);
        List<Part> ReadParts();
        List<Band> ReadBands();
        List<Sheet> ReadSheets();
        string FullPath { get; }

    }
}
