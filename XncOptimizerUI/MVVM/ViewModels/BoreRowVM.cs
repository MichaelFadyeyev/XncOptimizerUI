using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>
    /// One row of the selected part's bores <c>DataGrid</c>: a flattened, display-ready view of
    /// an <see cref="XncBore"/> plus the checkbox state that feeds
    /// <see cref="AppViewModel.CheckedBores"/>. X / Y / Depth are editable: the editors write
    /// into <see cref="XInput"/> / <see cref="YInput"/> / <see cref="DepthInput"/> (a number or an
    /// expression with <c>dx</c>/<c>dy</c>/<c>dz</c>, validated live by <see cref="BoreExpression"/>),
    /// and a committed change is handed to the owner, which writes it to the program.
    /// </summary>
    public partial class BoreRowVM : EditableRowVM
    {
        private readonly Action<BoreRowVM, bool> _onSelectionChanged;
        private readonly Func<BoreRowVM, BoreAttribute, string, bool> _onEdited;

        /// <summary>Symbols drafts are validated against: the program's dx/dy/dz and declared vars.</summary>
        private XncSymbolTable _symbols;

        public BoreRowVM(
            int number, string side, string? diameter, XncBore bore, XncProgram program,
            Action<BoreRowVM, bool> onSelectionChanged,
            Func<BoreRowVM, BoreAttribute, string, bool> onEdited)
        {
            Number = number;
            Side = side;
            Diameter = diameter;
            _bore = bore;
            _symbols = XncProgramMath.ProgramSymbols(program);
            _onSelectionChanged = onSelectionChanged;
            _onEdited = onEdited;
            ResetInputs();
        }

        #region Properties

        public int Number { get; }

        public string Side { get; }

        public string? Diameter { get; }

        /// <summary>Attribute behind the X column (<c>z</c> for a Left/Right edge bore, else <c>x</c>).</summary>
        public BoreAttribute HorizontalAttribute => BoreAttributes.HorizontalAttribute(Bore.Surface);

        /// <summary>Attribute behind the Y column (<c>z</c> for a Top/Bottom edge bore, else <c>y</c>).</summary>
        public BoreAttribute VerticalAttribute => BoreAttributes.VerticalAttribute(Bore.Surface);

        public string X => MachiningNumber.Format(ValueOf(HorizontalAttribute));

        public string Y => MachiningNumber.Format(ValueOf(VerticalAttribute));

        public string Depth => MachiningNumber.Format(Bore.Depth);

        /// <summary>Hover tip of the X cell: <c>expression = exact value</c>, <c>null</c> for a plain number.</summary>
        public string? XToolTip => ExpressionToolTip(HorizontalAttribute);

        /// <summary>Hover tip of the Y cell: <c>expression = exact value</c>, <c>null</c> for a plain number.</summary>
        public string? YToolTip => ExpressionToolTip(VerticalAttribute);

        /// <summary>Hover tip of the Depth cell: <c>expression = exact value</c>, <c>null</c> for a plain number.</summary>
        public string? DepthToolTip => ExpressionToolTip(BoreAttribute.Depth);

        #endregion

        #region ObservableProperties

        /// <summary>The bore as last read from the program; replaced by <see cref="Apply"/> after a save.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(X), nameof(Y), nameof(Depth), nameof(XToolTip), nameof(YToolTip), nameof(DepthToolTip))]
        private XncBore _bore;

        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value) => _onSelectionChanged(this, value);

        /// <summary>Draft text of the X column editor.</summary>
        [ObservableProperty]
        private string _xInput = string.Empty;

        partial void OnXInputChanged(string value) => Validate(nameof(XInput), value, HorizontalAttribute);

        /// <summary>Draft text of the Y column editor.</summary>
        [ObservableProperty]
        private string _yInput = string.Empty;

        partial void OnYInputChanged(string value) => Validate(nameof(YInput), value, VerticalAttribute);

        /// <summary>Draft text of the Depth column editor.</summary>
        [ObservableProperty]
        private string _depthInput = string.Empty;

        partial void OnDepthInputChanged(string value) => Validate(nameof(DepthInput), value, BoreAttribute.Depth);

        #endregion

        #region Methods

        /// <summary>Refreshes the row in place from the re-read program after a successful save.</summary>
        public void Apply(XncBore bore, XncProgram program)
        {
            _symbols = XncProgramMath.ProgramSymbols(program);
            Bore = bore;
            ResetInputs();
        }

        protected override IEnumerable<Draft> Drafts() =>
        [
            BoreDraft(nameof(XInput), HorizontalAttribute, XInput),
            BoreDraft(nameof(YInput), VerticalAttribute, YInput),
            BoreDraft(nameof(DepthInput), BoreAttribute.Depth, DepthInput)
        ];

        private Draft BoreDraft(string property, BoreAttribute attribute, string input) =>
            new(property, input.Trim(), CommittedText(attribute), text => _onEdited(this, attribute, text));

        protected override void ResetInputs()
        {
            XInput = CommittedText(HorizontalAttribute);
            YInput = CommittedText(VerticalAttribute);
            DepthInput = CommittedText(BoreAttribute.Depth);
        }

        /// <summary>Authored attribute text (keeps an expression such as <c>dx-32</c>), else the formatted value.</summary>
        private string CommittedText(BoreAttribute attribute) =>
            AuthoredText(attribute) ?? MachiningNumber.Format(ValueOf(attribute));

        private string? AuthoredText(BoreAttribute attribute) => attribute switch
        {
            BoreAttribute.X => Bore.XText,
            BoreAttribute.Y => Bore.YText,
            BoreAttribute.Z => Bore.ZText,
            _ => Bore.DepthText
        };

        /// <summary>
        /// <c>expression = exact value</c> when the attribute is authored as an expression or a
        /// variable (e.g. <c>throughBoreDepth = 20</c>); <c>null</c> (no tip) for a plain number.
        /// </summary>
        private string? ExpressionToolTip(BoreAttribute attribute) =>
            AuthoredText(attribute) is { } text && !IsPlainNumber(text)
                ? $"{text} = {ValueOf(attribute).ToString(CultureInfo.InvariantCulture)}"
                : null;

        private static bool IsPlainNumber(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        private double ValueOf(BoreAttribute attribute) => attribute switch
        {
            BoreAttribute.X => Bore.X,
            BoreAttribute.Y => Bore.Y,
            BoreAttribute.Z => Bore.Z,
            _ => Bore.Depth
        };

        /// <summary>
        /// Checks a draft against the program's symbols. The committed (authored) text is trusted
        /// as is: it was read from the file and may use a construct the editor would not accept,
        /// and it must never block committing the row's other cells.
        /// </summary>
        private void Validate(string property, string text, BoreAttribute attribute)
        {
            if (text.Trim() == CommittedText(attribute))
            {
                SetError(property, null);
                return;
            }

            var valid = BoreExpression.TryEvaluate(text, attribute, _symbols, out _, out var error);
            SetError(property, valid ? null : error);
        }

        #endregion
    }
}
