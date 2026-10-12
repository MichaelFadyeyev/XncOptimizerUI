using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>
    /// The XNC programs of the selected part: the program tables (bores, grooves, mills, tools,
    /// variables), their check boxes, the "XNC List" text, and the in-cell bore and variable edits,
    /// which are written to the program and saved in place.
    /// </summary>
    public sealed partial class SelectedPartMachiningVM : ObservableObject
    {
        private readonly ProjectServices _project;
        private readonly ProjectWorkspace _workspace;
        private readonly PartsListVM _parts;
        private readonly PartEditSaver _saver;

        public SelectedPartMachiningVM(ProjectServices project, ProjectWorkspace workspace, PartsListVM parts, PartEditSaver saver)
        {
            _project = project;
            _workspace = workspace;
            _parts = parts;
            _saver = saver;

            _parts.SelectedPartChanged += Refresh;
        }

        /// <summary>
        /// Raised whenever <see cref="Programs"/> was (re-)read for a part, even when
        /// the list is unchanged, so the preview can follow every refresh.
        /// </summary>
        public event Action<PartVM?, IReadOnlyList<XncProgram>>? ProgramsRead;

        #region Properties
        public bool HasBores => Bores.Count > 0;

        public bool HasGrooves => Grooves.Count > 0;

        public bool HasTools => Tools.Count > 0;

        public bool HasVariables => Variables.Count > 0;

        public bool HasMillingContours => MillingContours.Count > 0;

        public bool HasMillingEllipses => MillingEllipses.Count > 0;

        public bool HasMillingRectangles => MillingRectangles.Count > 0;

        /// <summary>
        /// Bores checked via the bores <c>DataGrid</c>'s checkbox column. Reset whenever the
        /// selected part changes. Not consumed by anything yet - reserved for future features.
        /// </summary>
        public CheckedSet<XncBore> CheckedBores { get; } = [];

        /// <summary>Grooves checked via the grooves table. Reset when the selected part changes.</summary>
        public CheckedSet<XncGrooving> CheckedGrooves { get; } = [];

        /// <summary>Contours checked via the milling contours table.</summary>
        public CheckedSet<XncMillingContour> CheckedMillingContours { get; } = [];

        /// <summary>Ellipses checked via the milling ellipses table.</summary>
        public CheckedSet<XncMillingEllipse> CheckedMillingEllipses { get; } = [];

        /// <summary>Rectangles checked via the milling rectangles table.</summary>
        public CheckedSet<XncMillingRectangle> CheckedMillingRectangles { get; } = [];
        #endregion

        #region ObservableProperties
        /// <summary>
        /// The bores of the part's XNC programs, flattened across all programs and numbered in
        /// order, for the bores <c>DataGrid</c>.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasBores))]
        private ObservableCollection<BoreRowVM> _bores = [];

        /// <summary>
        /// The bore row selected in the bores <c>DataGrid</c>; its X / Y / Depth cells are edited
        /// in place (see <see cref="OnBoreRowEdited"/>).
        /// </summary>
        [ObservableProperty]
        private BoreRowVM? _selectedBore;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasGrooves))]
        private ObservableCollection<GrooveRowVM> _grooves = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasTools))]
        private ObservableCollection<ToolRowVM> _tools = [];

        /// <summary>
        /// The <c>&lt;var&gt;</c> declarations of the part's XNC programs, numbered in order, for
        /// the Variables <c>DataGrid</c> (name / type / expr / comment editable).
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasVariables))]
        private ObservableCollection<VariableRowVM> _variables = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMillingContours))]
        private ObservableCollection<MillingContourRowVM> _millingContours = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMillingEllipses))]
        private ObservableCollection<MillingEllipseRowVM> _millingEllipses = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMillingRectangles))]
        private ObservableCollection<MillingRectangleRowVM> _millingRectangles = [];

        /// <summary>The "XNC List" text: one line per program element (see <see cref="ProgramsSummary"/>).</summary>
        [ObservableProperty]
        private string _programsText = ProgramsSummary.NoPart;

        /// <summary>The XNC programs attached to the part, as last read.</summary>
        [ObservableProperty]
        private IReadOnlyList<XncProgram> _programs = [];
        #endregion

        #region Methods
        /// <summary>
        /// Re-reads the XNC programs of <paramref name="part"/> and rebuilds everything derived
        /// from them: the programs text, the program tables and (through <see cref="ProgramsRead"/>)
        /// the preview.
        /// </summary>
        public void Refresh(PartVM? part)
        {
            var (programs, readError) = ReadPrograms(part);

            Show(part, programs, readError);
            Variables = BuildVariableRows(programs);
        }

        private (IReadOnlyList<XncProgram> Programs, string? ReadError) ReadPrograms(PartVM? part)
        {
            if (part is null || !_workspace.IsOpen)
            {
                return ([], null);
            }

            try
            {
                return (_project.Reader.ReadXncPrograms(part.Id), null);
            }
            catch (Exception e)
            {
                return ([], e.Message);
            }
        }

        /// <summary>Programs text, preview and every program table but the variables.</summary>
        private void Show(PartVM? part, IReadOnlyList<XncProgram> programs, string? readError)
        {
            ShowPrograms(part, programs, readError);

            CheckedBores.Clear();
            CheckedGrooves.Clear();
            CheckedMillingContours.Clear();
            CheckedMillingEllipses.Clear();
            CheckedMillingRectangles.Clear();
            Bores = BuildBoreRows(programs);
            Grooves = BuildGrooveRows(programs);
            Tools = BuildToolRows(programs);
            MillingContours = BuildMillingContourRows(programs);
            MillingEllipses = BuildMillingEllipseRows(programs);
            MillingRectangles = BuildMillingRectangleRows(programs);
        }

        private void ShowPrograms(PartVM? part, IReadOnlyList<XncProgram> programs, string? readError)
        {
            ProgramsText = part is null || !_workspace.IsOpen
                ? ProgramsSummary.NoPart
                : ProgramsSummary.Describe(programs, readError);
            Programs = programs;
            ProgramsRead?.Invoke(part, programs);
        }

        /// <summary>
        /// Writes one committed bore cell edit into its program, saves the file in place (together
        /// with any pending grid edit of the part) and refreshes the bores and the preview.
        /// Returns <c>false</c> when nothing was written, so the cell stays in edit mode.
        /// </summary>
        private bool OnBoreRowEdited(BoreRowVM row, BoreAttribute attribute, string expression) =>
            SaveProgramEdit(
                Programs.FirstOrDefault(p => p.Bores.Contains(row.Bore))?.OperationId,
                $"Bore #{row.Number} {attribute.XmlName()} = \"{expression}\"",
                operationId => _project.Editor.UpdateBore(operationId, row.Bore.Index, attribute, expression),
                RefreshBoresInPlace);

        /// <summary>
        /// Writes one committed variable cell edit into its program, saves the file in place and
        /// refreshes everything derived from the programs (a variable may drive bores, mills, ...).
        /// Returns <c>false</c> when nothing was written, so the cell stays in edit mode.
        /// </summary>
        private bool OnVariableRowEdited(VariableRowVM row, XncVariableAttribute attribute, string value) =>
            SaveProgramEdit(
                Programs.FirstOrDefault(p => p.DeclaredVariables.Contains(row.Variable))?.OperationId,
                $"Variable #{row.Number} {attribute.XmlName()} = \"{value}\"",
                operationId => _project.Editor.UpdateVariable(operationId, row.Variable.Index, attribute, value),
                RefreshAfterVariableEdit);

        /// <summary>
        /// Applies <paramref name="edit"/> to the program of XNC operation <paramref name="operationId"/>
        /// of the selected part, saves the file in place and runs <paramref name="refresh"/>.
        /// </summary>
        private bool SaveProgramEdit(int? operationId, string description, Func<int, OperationResult> edit, Action<PartVM> refresh)
        {
            var part = _parts.SelectedPart;

            if (part is null || operationId is null || !_workspace.IsOpen)
            {
                return false;
            }

            if (!_saver.SaveWithPendingEdits(part, () => edit(operationId.Value), description))
            {
                return false;
            }

            refresh(part);

            return true;
        }

        /// <summary>
        /// Re-reads <paramref name="part"/>'s programs after a bore edit and updates the bore rows
        /// in place - the collection is not replaced, because this runs while the bores
        /// <c>DataGrid</c> is still ending its cell edit (replacing its items source then throws),
        /// and in-place rows keep the grid's selection and the checkboxes. Bores do not affect the
        /// other machining tables, so those are left alone.
        /// </summary>
        private void RefreshBoresInPlace(PartVM part)
        {
            var programs = _project.Reader.ReadXncPrograms(part.Id);
            var bores = programs.SelectMany(program => program.Bores.Select(bore => (Bore: bore, Program: program))).ToList();

            if (bores.Count != Bores.Count)
            {
                Refresh(part);
                return;
            }

            ShowPrograms(part, programs, null);

            Bores
                .Zip(bores)
                .ToList()
                .ForEach(pair => ApplyBoreRow(pair.First, pair.Second.Bore, pair.Second.Program));
        }

        /// <summary>Refreshes one row and keeps <see cref="CheckedBores"/> pointing at its current bore.</summary>
        private void ApplyBoreRow(BoreRowVM row, XncBore bore, XncProgram program)
        {
            CheckedBores.Replace(row.Bore, bore);
            row.Apply(bore, program);
        }

        /// <summary>
        /// Re-reads <paramref name="part"/>'s programs after a variable edit and rebuilds every
        /// table derived from them except the variables, whose rows are updated in place (this
        /// runs while the Variables <c>DataGrid</c> is still ending its cell edit, when replacing
        /// its items source throws).
        /// </summary>
        private void RefreshAfterVariableEdit(PartVM part)
        {
            var (programs, readError) = ReadPrograms(part);
            var variables = programs.SelectMany(p => p.DeclaredVariables.Select(v => (Variable: v, Program: p))).ToList();

            if (readError is not null || variables.Count != Variables.Count)
            {
                Refresh(part);
                return;
            }

            Show(part, programs, null);

            Variables
                .Zip(variables)
                .ToList()
                .ForEach(pair => pair.First.Apply(pair.Second.Variable, pair.Second.Program));
        }

        /// <summary>
        /// One numbered row per element that <paramref name="elements"/> picks from each program,
        /// across all of the part's programs in order.
        /// </summary>
        private static ObservableCollection<TRow> BuildRows<TElement, TRow>(
            IReadOnlyList<XncProgram> programs,
            Func<XncProgram, IEnumerable<TElement>> elements,
            Func<int, XncProgram, TElement, TRow> createRow) =>
            new(programs
                .SelectMany(program => elements(program).Select(element => (Program: program, Element: element)))
                .Select((entry, i) => createRow(i + 1, entry.Program, entry.Element)));

        private ObservableCollection<BoreRowVM> BuildBoreRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.Bores, (number, program, bore) => new BoreRowVM(
                number,
                MachiningDisplay.BoreSide(bore, program),
                ToolDiameter(program, bore.ToolName),
                bore,
                program,
                CheckedBores.Set,
                OnBoreRowEdited));

        private static ObservableCollection<ToolRowVM> BuildToolRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.Tools,
                (number, program, tool) => new ToolRowVM(number, MachiningDisplay.SideName(program.Side), tool));

        private ObservableCollection<VariableRowVM> BuildVariableRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.DeclaredVariables,
                (number, program, variable) => new VariableRowVM(
                    number, MachiningDisplay.SideName(program.Side), variable, program, OnVariableRowEdited));

        private ObservableCollection<GrooveRowVM> BuildGrooveRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.Groovings,
                (number, program, groove) => new GrooveRowVM(
                    number, MachiningDisplay.GrooveSide(groove, program), groove, CheckedGrooves.Set));

        private ObservableCollection<MillingContourRowVM> BuildMillingContourRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.MillingContours,
                (number, program, contour) => new MillingContourRowVM(
                    number, MachiningDisplay.SideName(program.Side), contour, CheckedMillingContours.Set));

        private ObservableCollection<MillingEllipseRowVM> BuildMillingEllipseRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.MillingEllipses,
                (number, program, ellipse) => new MillingEllipseRowVM(
                    number, MachiningDisplay.SideName(program.Side), ellipse, CheckedMillingEllipses.Set));

        private ObservableCollection<MillingRectangleRowVM> BuildMillingRectangleRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.MillingRectangles,
                (number, program, rectangle) => new MillingRectangleRowVM(
                    number, MachiningDisplay.SideName(program.Side), rectangle, CheckedMillingRectangles.Set));

        /// <summary>Diameter of the program's tool <paramref name="toolName"/>, or <c>null</c> when it is not declared.</summary>
        private static string? ToolDiameter(XncProgram program, string toolName) =>
            program.Tools.FirstOrDefault(t => t.Name == toolName)?.Diameter is { } diameter
                ? MachiningNumber.Format(diameter)
                : null;
        #endregion
    }
}
