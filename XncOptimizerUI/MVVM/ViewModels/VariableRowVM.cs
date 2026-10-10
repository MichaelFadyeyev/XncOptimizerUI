using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.Services.Xnc;

namespace XncOptimizerUI.MVVM.ViewModels
{
    /// <summary>
    /// One row of the selected part's Variables <c>DataGrid</c>: a <c>&lt;var&gt;</c> declaration
    /// with editable <c>name</c>, <c>type</c> (selected from <see cref="TypeOptions"/>), <c>expr</c>
    /// and <c>comment</c>. Drafts are validated live by <see cref="XncVariableRules"/>; a committed
    /// change is handed to the owner, which writes it to the program.
    /// </summary>
    public partial class VariableRowVM : EditableRowVM
    {
        private readonly Func<VariableRowVM, XncVariableAttribute, string, bool> _onEdited;

        private XncProgram _program;

        public VariableRowVM(
            int number, string side, XncVariable variable, XncProgram program,
            Func<VariableRowVM, XncVariableAttribute, string, bool> onEdited)
        {
            Number = number;
            Side = side;
            _variable = variable;
            _program = program;
            _onEdited = onEdited;
            ResetInputs();
        }

        #region Properties

        /// <summary>The type selector's options, as written to the <c>type</c> attribute.</summary>
        public static IReadOnlyList<string> TypeOptions { get; } = XncVariableTypes.All.Select(t => t.XmlName()).ToList();

        public int Number { get; }

        public string Side { get; }

        public string Name => Variable.Name;

        public string Type => Variable.Type.XmlName();

        public string Expr => Variable.Expr;

        public string? Comment => Variable.Comment;

        /// <summary>Resolved value of a numeric variable (the expr itself for string/bool).</summary>
        public string Value => Variable.Value is { } value ? value.ToString(CultureInfo.InvariantCulture) : Variable.Expr;

        #endregion

        #region ObservableProperties

        /// <summary>The declaration as last read from the program; replaced by <see cref="Apply"/> after a save.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Name), nameof(Type), nameof(Expr), nameof(Comment), nameof(Value))]
        private XncVariable _variable;

        /// <summary>Draft text of the Name column editor.</summary>
        [ObservableProperty]
        private string _nameInput = string.Empty;

        partial void OnNameInputChanged(string value) => ValidateDrafts();

        /// <summary>Draft of the Type column selector.</summary>
        [ObservableProperty]
        private string _typeInput = string.Empty;

        partial void OnTypeInputChanged(string value) => ValidateDrafts();

        /// <summary>Draft text of the Expr column editor.</summary>
        [ObservableProperty]
        private string _exprInput = string.Empty;

        partial void OnExprInputChanged(string value) => ValidateDrafts();

        /// <summary>Draft text of the Comment column editor (no rules; empty removes the attribute).</summary>
        [ObservableProperty]
        private string _commentInput = string.Empty;

        #endregion

        #region Methods

        /// <summary>Refreshes the row in place from the re-read program after a successful save.</summary>
        public void Apply(XncVariable variable, XncProgram program)
        {
            _program = program;
            Variable = variable;
            ResetInputs();
        }

        protected override IEnumerable<Draft> Drafts() =>
        [
            VariableDraft(nameof(NameInput), XncVariableAttribute.Name, NameInput.Trim(), Name),
            VariableDraft(nameof(TypeInput), XncVariableAttribute.Type, TypeInput, Type),
            VariableDraft(nameof(ExprInput), XncVariableAttribute.Expr, XncVariableRules.NormalizeExpr(ExprInput, DraftType), Expr),
            VariableDraft(nameof(CommentInput), XncVariableAttribute.Comment, CommentInput, Comment ?? string.Empty)
        ];

        private Draft VariableDraft(string property, XncVariableAttribute attribute, string text, string committed) =>
            new(property, text, committed, value => _onEdited(this, attribute, value));

        protected override void ResetInputs()
        {
            NameInput = Name;
            TypeInput = Type;
            ExprInput = Expr;
            CommentInput = Comment ?? string.Empty;
        }

        /// <summary>The drafted type (the committed one while the draft is not a known type).</summary>
        private XncVariableType DraftType =>
            XncVariableTypes.TryParseExact(TypeInput, out var type) ? type : Variable.Type;

        /// <summary>
        /// Name, type and expr depend on each other (an expr is checked for the drafted type), so
        /// all three are re-checked on any change. An unchanged draft is trusted as authored.
        /// </summary>
        private void ValidateDrafts()
        {
            SetError(nameof(NameInput), NameInput.Trim() == Name ? null : CheckName());
            SetError(nameof(TypeInput), TypeInput == Type ? null : CheckType());
            SetError(nameof(ExprInput), ExprInput == Expr && TypeInput == Type ? null : CheckExpr(nameof(ExprInput)));
        }

        private string? CheckName() =>
            XncVariableRules.CheckName(
                NameInput,
                _program.DeclaredVariables.Where(v => v.Index != Variable.Index).Select(v => v.Name));

        private string? CheckType() =>
            XncVariableTypes.TryParseExact(TypeInput, out _)
                ? CheckExpr(nameof(TypeInput))
                : $"Unknown type '{TypeInput}'";

        /// <summary>The drafted expr checked for the drafted type, worded for the column it is shown in.</summary>
        private string? CheckExpr(string property)
        {
            if (XncVariableRules.TryCheckExpr(ExprInput, DraftType, XncProgramMath.SymbolsBefore(_program, Variable), out _, out var error))
            {
                return null;
            }

            return property == nameof(TypeInput) ? $"Expr '{ExprInput}' does not fit type {TypeInput}: {error}" : error;
        }

        #endregion
    }
}
