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
        public bool SaveWithPendingEdits(PartVM part, Func<OperationResult> edit, string description) =>
            SaveWithPendingEdits(part, edit, result =>
            {
                log.Append(result);
                return description;
            });

        /// <summary>
        /// As <see cref="SaveWithPendingEdits(PartVM, Func{OperationResult}, string)"/>, for an edit whose
        /// result describes it: its log becomes the save line, e.g. <c>Deleted 2 bore(s) ..., saved: time</c>.
        /// An edit that changed nothing logs its result as it is.
        /// </summary>
        public bool SaveDescribedEdit(PartVM part, Func<OperationResult> edit) =>
            SaveWithPendingEdits(part, edit, result => result.MessageText);

        /// <summary>
        /// Applies <paramref name="edit"/> with any pending grid edit of <paramref name="part"/>; when it
        /// changed something, saves in place and logs <paramref name="describe"/>'s text with the save outcome.
        /// </summary>
        private bool SaveWithPendingEdits(PartVM part, Func<OperationResult> edit, Func<OperationResult, string> describe)
        {
            log.Append(project.Editor.UpdatePart(part.Part));

            var result = edit();

            if (!result.Changed)
            {
                log.Append(result);
                return false;
            }

            var description = describe(result);

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
