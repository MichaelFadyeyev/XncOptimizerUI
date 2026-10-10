using System.Collections;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>
    /// Base of a program-table row edited cell by cell (<see cref="ICellEditableRow"/>). Editors
    /// write into draft properties; each draft keeps one validation error reported through
    /// <see cref="INotifyDataErrorInfo"/> (which turns the bound editor red). A cell commit saves
    /// every changed draft through its owner and is refused while any draft is invalid; beginning
    /// or cancelling an edit resets the drafts to the committed values.
    /// </summary>
    public abstract class EditableRowVM : ObservableObject, INotifyDataErrorInfo, ICellEditableRow
    {
        private readonly Dictionary<string, string> _errors = [];

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public bool HasErrors => _errors.Count > 0;

        public IEnumerable GetErrors(string? propertyName) =>
            propertyName is not null && _errors.TryGetValue(propertyName, out var error) ? new[] { error } : [];

        public void BeginCellEdit() => ResetInputs();

        public bool TryCommitCellEdit() =>
            !HasErrors && Drafts().Where(draft => draft.Text != draft.Committed).ToList().All(Save);

        public void CancelCellEdit() => ResetInputs();

        /// <summary>
        /// One editable value: the draft <paramref name="Text"/> (already normalized, e.g. trimmed),
        /// the <paramref name="Committed"/> text it started from, and how to save it.
        /// </summary>
        protected sealed record Draft(string Property, string Text, string Committed, Func<string, bool> SaveText);

        /// <summary>Every editable value of the row.</summary>
        protected abstract IEnumerable<Draft> Drafts();

        /// <summary>Sets every draft property back to its committed text.</summary>
        protected abstract void ResetInputs();

        /// <summary>Sets (or with <c>null</c> clears) the error of <paramref name="property"/>.</summary>
        protected void SetError(string property, string? error)
        {
            var changed = error is null
                ? _errors.Remove(property)
                : !_errors.TryGetValue(property, out var current) || current != error;

            if (error is not null)
            {
                _errors[property] = error;
            }

            if (changed)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        private bool Save(Draft draft)
        {
            if (draft.SaveText(draft.Text))
            {
                return true;
            }

            SetError(draft.Property, "Saving failed - see the log");
            return false;
        }
    }
}
