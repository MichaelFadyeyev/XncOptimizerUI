using System.Collections.ObjectModel;
using XncOptimizerUI.MVVM.Models;

namespace XncOptimizerUI.Contracts
{
    public interface IProjectService
    {
        void OpenProject(string path);
        void CloseProject();
        void GroupIdenticalElements(ref string log);
        void PrepForSplitAlongX(ref string log, string[] selectedPartsIds);
        bool ReplaceXncPrograms(ref string log, Part sourcePart, IList<Part> targetParts);
        int GetXncProgramsCount(int partId);
        void SaveProject();
        bool UpdatePart(Part part);
        List<Part> ReadParts();
        List<Band> ReadBands();
        List<Sheet> ReadSheets();
        string FullPath { get; }

    }
}
