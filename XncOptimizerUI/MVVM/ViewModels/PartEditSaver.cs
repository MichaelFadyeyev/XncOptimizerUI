using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>
    /// Saves edits of one part into the open project file in place: its grid edits (name, size,
    /// Txt, bands) and edits of its programs, and logs each save with its time.
    /// </summary>
    public sealed class PartEditSaver(ProjectServices project, ActivityLog log, TimeProvider clock)
    {
        #region Methods
        /// <summary>
        /// Writes <paramref name="part"/>'s pending grid edits into the document and saves the
        /// file in place when anything changed.
        /// </summary>
        public void SavePendingEdits(PartVM part)
        {
            var result = project.Editor.UpdatePart(part.Part);

            log.Append(result);

            if (result.Changed)
            {
                project.Session.Save();
                log.Append($"Updates saved: {Now}\n");
            }
        }

        /// <summary>
        /// Applies <paramref name="edit"/> of <paramref name="part"/>, together with any pending grid
        /// edit of the part, and saves the file in place; logs <paramref name="description"/> with the
        /// save outcome. Returns <c>false</c> when the edit changed nothing.
        /// </summary>
        public bool SaveWithPendingEdits(PartVM part, Func<OperationResult> edit, string description)
        {
            log.Append(project.Editor.UpdatePart(part.Part));

            var result = edit();

            log.Append(result);

            if (!result.Changed)
            {
                return false;
            }

            try
            {
                project.Session.Save();
                log.Append($"{description}, saved: {Now}\n");
            }
            catch (Exception e)
            {
                log.Append($"***\n{description}, but saving failed: {e.Message}\n");
            }

            return true;
        }

        /// <summary>The local clock time, logged in the current culture's format.</summary>
        private DateTime Now => clock.GetLocalNow().DateTime;
        #endregion
    }
}
