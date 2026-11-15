using System.Collections;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>
    /// One editable numeric cell of a program table (a bore, groove or mill attribute). Shows the
    /// evaluated value (<see cref="Display"/>) with <c>expression = exact value</c> as
    /// <see cref="ToolTip"/> when the attribute is authored as an expression or variable; its editor
    /// writes <see cref="Input"/>, validated live by <see cref="MachiningExpression"/> against the
    /// element's symbols and reported through <see cref="INotifyDataErrorInfo"/> (red editor).
    /// The committed (authored) text is trusted as is: it was read from the file and may use a
    /// construct the editor would not accept, and it must never block committing another cell.
    /// </summary>
    public sealed partial class ExpressionCellVM : ObservableObject, INotifyDataErrorInfo
    {
        private string? _authoredText;
        private double _value;
        private XncSymbolTable _symbols;
        private string? _error;

        public ExpressionCellVM(MachiningAttribute attribute, string? authoredText, double value, XncSymbolTable symbols)
        {
            Attribute = attribute;
            _authoredText = authoredText;
            _value = value;
            _symbols = symbols;
            Reset();
        }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        #region Properties

        /// <summary>The element attribute this cell edits.</summary>
        public MachiningAttribute Attribute { get; }

        /// <summary>Evaluated value, rounded for display.</summary>
        public string Display => MachiningNumber.Format(_value);

        /// <summary>Hover tip: <c>expression = exact value</c>, <c>null</c> for a plain number.</summary>
        public string? ToolTip =>
            _authoredText is { } text && !IsPlainNumber(text)
                ? $"{text} = {_value.ToString(CultureInfo.InvariantCulture)}"
                : null;

        /// <summary>The text the editor starts from: the authored attribute text, else the formatted value.</summary>
        public string Committed => _authoredText ?? MachiningNumber.Format(_value);

        /// <summary>The draft as it would be written (trimmed).</summary>
        public string Draft => Input.Trim();

        /// <summary>Whether the draft differs from <see cref="Committed"/>.</summary>
        public bool IsChanged => Draft != Committed;

        public bool HasErrors => _error is not null;

        #endregion

        #region ObservableProperties

        /// <summary>Draft text of the cell editor.</summary>
        [ObservableProperty]
        private string _input = string.Empty;

        partial void OnInputChanged(string value) => Validate(value);

        #endregion

        #region Methods

        public IEnumerable GetErrors(string? propertyName) =>
            _error is not null && propertyName is null or "" or nameof(Input) ? new[] { _error } : [];

        /// <summary>Sets the draft back to the committed text (clearing any error).</summary>
        public void Reset()
        {
            Input = Committed;
            Validate(Input);
        }

        /// <summary>Refreshes the cell in place from the re-read element after a save.</summary>
        public void Apply(string? authoredText, double value, XncSymbolTable symbols)
        {
            _authoredText = authoredText;
            _value = value;
            _symbols = symbols;
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged(nameof(ToolTip));
            OnPropertyChanged(nameof(Committed));
            Reset();
        }

        /// <summary>Marks the draft as not saved (the owner refused or failed to write it).</summary>
        public void SetSaveFailed() => SetError("Saving failed - see the log");

        private void Validate(string text)
        {
            if (text.Trim() == Committed)
            {
                SetError(null);
                return;
            }

            var valid = MachiningExpression.TryEvaluate(text, Attribute, _symbols, out _, out var error);
            SetError(valid ? null : error);
        }

        private void SetError(string? error)
        {
            if (_error == error)
            {
                return;
            }

            _error = error;
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Input)));
            OnPropertyChanged(nameof(HasErrors));
        }

        private static bool IsPlainNumber(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        #endregion
    }
}
