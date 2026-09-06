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

        /// <summary>
        /// For every supplied part, rewrites each XNC machining program so that axis-parallel
        /// grooves become single-segment milling contours (or vice versa). Non-axis-parallel or
        /// otherwise non-compliant elements are left untouched; the counts of converted and
        /// ignored elements are appended to <paramref name="log"/>. Returns <c>false</c> (and
        /// saves nothing) when nothing was converted.
        /// </summary>
        bool ConvertGroovesAndMills(ref string log, IList<Part> parts, GrooveMillDirection direction);

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
