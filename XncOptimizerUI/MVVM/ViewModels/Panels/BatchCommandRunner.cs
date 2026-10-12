using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Panels
{
    /// <summary>
    /// The steps every menu command that saves its result as a new file shares: refuse without an
    /// open project or when a check fails, pick the parts, run the operation, log its report, and
    /// then open the saved file - or, when it changed nothing, show its messages as a warning.
    /// </summary>
    public sealed class BatchCommandRunner(ProjectWorkspace workspace, PartsListVM parts, ActivityLog log, IDialogService dialogs)
    {
        private const string NoFileMessage = "No file selected!";

        #region Methods
        /// <summary>
        /// Runs <paramref name="operation"/> unless the project is closed or one of
        /// <paramref name="checks"/> refuses (returns a message, which is logged).
        /// Returns whether the operation changed the project.
        /// </summary>
        public bool Run(Func<OperationResult> operation, params Func<string?>[] checks) =>
            Passes(checks) && RunSaving(operation);

        /// <summary>
        /// Like <see cref="Run"/>, on the parts <paramref name="selectParts"/> picks once the checks
        /// pass; logs <paramref name="noPartsMessage"/> instead when it picks none.
        /// </summary>
        public bool RunOnParts(
            Func<List<Part>> selectParts,
            string noPartsMessage,
            Func<List<Part>, OperationResult> operation,
            params Func<string?>[] checks)
        {
            if (!Passes(checks))
            {
                return false;
            }

            var parts = selectParts();

            if (parts.Count == 0)
            {
                Refuse(noPartsMessage);
                return false;
            }

            return RunSaving(() => operation(parts));
        }

        private bool Passes(IEnumerable<Func<string?>> checks)
        {
            var refusal = checks
                .Prepend(() => workspace.IsOpen ? null : NoFileMessage)
                .Select(check => check())
                .FirstOrDefault(message => message is not null);

            if (refusal is null)
            {
                return true;
            }

            Refuse(refusal);
            return false;
        }

        private void Refuse(string message) => log.Append($"{message}\n");

        /// <summary>
        /// Runs <paramref name="operation"/> with no part selected: releasing the selection first
        /// saves the selected part's pending grid edits into the open file, before the operation
        /// reads it. Otherwise reopening the saved file would release the selection then, saving the
        /// part's view model - still holding the values of the file it was read from - into the
        /// new file and undoing what the operation changed in that part. When nothing changed, the
        /// file stays open and the part is selected again.
        /// </summary>
        private bool RunSaving(Func<OperationResult> operation)
        {
            var selected = parts.SelectedPart;
            parts.SelectedPart = null;

            var result = operation();

            log.Append(result);

            if (!result.Changed)
            {
                parts.SelectedPart = selected;
                WarnAbout(result);
                return false;
            }

            workspace.Reopen();

            return true;
        }

        /// <summary>
        /// The service layer reports failures in its log rather than popping its own dialog,
        /// so the messages it just logged are what the user needs to see.
        /// </summary>
        private void WarnAbout(OperationResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.MessageText))
            {
                dialogs.ShowWarning(result.MessageText);
            }
        }
        #endregion
    }
}
