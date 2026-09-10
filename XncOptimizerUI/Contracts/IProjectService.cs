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
        /// When <paramref name="processPockets"/> is <c>true</c> and the direction is
        /// <see cref="GrooveMillDirection.MillsToGrooves"/>, axis-parallel rectangular pocket
        /// mills (<c>&lt;mr&gt;</c> with <c>c="3"</c>) are also turned into grooves.
        /// </summary>
        bool ConvertGroovesAndMills(ref string log, IList<Part> parts, GrooveMillDirection direction, bool processPockets);

        /// <summary>
        /// For every supplied part, rewrites each XNC machining program so that face bores wider
        /// than 35 mm become round mills cut with a fixed 6 mm cutter ("Mill6"), or vice versa.
        /// <see cref="BoreMillDirection.BoresToMills"/> emits a closed two-arc contour per bore,
        /// or an elliptical mill when <paramref name="useEllipse"/> is <c>true</c>; the mill depth
        /// equals the bore depth, traversal is clockwise and the cutter sits right of the centre
        /// line. <see cref="BoreMillDirection.MillsToBores"/> turns a closed all-arc contour (an
        /// entry point plus at least two centre-defined arcs sharing one centre and radius that
        /// close onto the entry) or an <c>l == w</c> ellipse back into a face bore, declaring a
        /// "Bore&lt;diameter&gt;" tool sized to the mill. Non-compliant elements are left
        /// untouched and counted as ignored. Returns <c>false</c> (and saves nothing) when
        /// nothing was converted.
        /// </summary>
        bool ConvertBoresAndMills(ref string log, IList<Part> parts, BoreMillDirection direction, bool useEllipse);

        /// <summary>
        /// For every supplied part, re-sequences the straight axis-parallel milling contours in
        /// each XNC machining program so that the entry point of every pass sits next to the exit
        /// point of the previous one (a serpentine path that removes the long rapid returns).
        /// Passes are grouped per XNC operation (one part face) and per tool; a greedy
        /// nearest-neighbour walk picks the order and the direction of each pass. Arcs,
        /// multi-segment contours, pockets and rectangles are left untouched and counted as
        /// ignored. Returns <c>false</c> (and saves nothing) when nothing was reordered.
        /// </summary>
        bool OptimizeMillTraversal(ref string log, IList<Part> parts);

        int GetXncProgramsCount(int partId);

        /// <summary>Reads and parses every XNC machining program attached to the part with the given id.</summary>
        IReadOnlyList<XncProgram> ReadXncPrograms(int partId);
        void SaveProject();
        bool UpdatePart(ref string log, Part part);
        List<Part> ReadParts();
        List<Band> ReadBands();
        List<Sheet> ReadSheets();
        List<Product> ReadProducts();
        string FullPath { get; }

    }
}
