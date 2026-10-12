using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Contracts
{
    /// <summary>
    /// Rewrites of the XNC programs of the given parts. Each logs a line per part and a summary,
    /// saves the result as a new suffixed file (which becomes <see cref="IProjectSession.FullPath"/>),
    /// and returns an unchanged result - saving nothing - when nothing was rewritten. Programs are all
    /// rewritten before any is written back, so a failing program leaves the document untouched.
    /// </summary>
    public interface IXncBatchOperations
    {
        /// <summary>
        /// Grooves → mills: an axis-parallel primary-pass groove becomes a straight mill cut with the
        /// configured milling cutter of its width, or a rectangular pocket milled with the smallest one.
        /// Mills → grooves: a shallow, axis-parallel, single straight-segment mill becomes a groove cut with
        /// the first configured grooving cutter; with <paramref name="processPockets"/> axis-parallel
        /// rectangular pockets too. Saved with the <c>_gm</c> suffix.
        /// </summary>
        OperationResult ConvertGroovesAndMills(IList<Part> parts, GrooveMillDirection direction, bool processPockets);

        /// <summary>
        /// Bores → mills: a face bore wider than 34 mm becomes a round mill cut with a 6 mm cutter - a
        /// counter-clockwise four-quarter-arc contour, or an ellipse when <paramref name="useEllipse"/>; a
        /// through bore keeps the tool inside the hole, a blind one is milled as a pocket. Mills → bores:
        /// a round contour (arcs sharing one centre and radius, closing onto the entry) or an <c>l == w</c>
        /// ellipse wider than 34 mm becomes a face bore. Saved with the <c>_bm</c> suffix.
        /// </summary>
        OperationResult ConvertBoresAndMills(IList<Part> parts, BoreMillDirection direction, bool useEllipse);

        /// <summary>
        /// Re-sequences the straight axis-parallel mill passes of each program per tool by a greedy
        /// nearest-neighbour walk (a serpentine path). Saved with the <c>_mo</c> suffix.
        /// </summary>
        OperationResult OptimizeMillTraversal(IList<Part> parts);

        /// <summary>
        /// Moves each program's tool declarations to its start, ordered Bore - Cut - Mill, then by
        /// diameter. Programs whose meaning would change are skipped and logged. Saved with the <c>_to</c> suffix.
        /// </summary>
        OperationResult SortToolDeclarations(IList<Part> parts);

        /// <summary>
        /// Shifts every mill of the selected <paramref name="kinds"/> by <paramref name="offset"/> mm to the
        /// <paramref name="side"/> of its traversal direction (see <see cref="Services.Xnc.MillPathOffsetter"/>).
        /// Saved with the <c>_off</c> suffix.
        /// </summary>
        OperationResult OffsetMillPaths(IList<Part> parts, double offset, MillOffsetSide side, MillPathKinds kinds);

        /// <summary>
        /// Removes the elements of the selected <paramref name="kinds"/> lying outside the part (see
        /// <see cref="Services.Xnc.Outside.OutsideOperationDetector"/>) and the tools no longer used; every removed
        /// element is logged. Saved with the <c>_cln</c> suffix.
        /// </summary>
        OperationResult CleanOutsideOperations(IList<Part> parts, OutsideOperationKinds kinds);
    }
}
