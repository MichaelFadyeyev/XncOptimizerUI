using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.Helpers;
using XncOptimizerUI.MVVM.Models.Xnc;
using XncOptimizerUI.MVVM.ViewModels.Parts;

namespace XncOptimizerUI.MVVM.ViewModels.Machining
{
    /// <summary>
    /// The XNC programs of the selected part: the program tables (bores, grooves, mills, tools,
    /// variables), the rows selected in them, the "XNC List" text, deleting the selected bores, grooves
    /// or mills of one table, and the in-cell bore, groove, mill and
    /// variable edits, which are written to the program and saved in place.
    /// </summary>
    public sealed partial class SelectedPartMachiningVM : ObservableObject
    {
        private readonly ProjectServices _project;
        private readonly ProjectWorkspace _workspace;
        private readonly PartsListVM _parts;
        private readonly PartEditSaver _saver;
        private readonly IDialogService _dialogs;

        // The part the program tables were last built for: a rebuild for the same part keeps the
        // selected rows (see Show).
        private PartVM? _shownPart;

        // Set while one selection request updates several rows, so SelectionChanged is raised once.
        private bool _isBatchingSelection;
        private bool _isSelectionChangePending;

        public SelectedPartMachiningVM(ProjectServices project, ProjectWorkspace workspace, PartsListVM parts, PartEditSaver saver, IDialogService dialogs)
        {
            _project = project;
            _workspace = workspace;
            _parts = parts;
            _saver = saver;
            _dialogs = dialogs;

            _parts.SelectedPartChanged += Refresh;
        }

        /// <summary>
        /// Raised whenever <see cref="Programs"/> was (re-)read for a part, even when
        /// the list is unchanged, so the preview can follow every refresh.
        /// </summary>
        public event Action<PartVM?, IReadOnlyList<XncProgram>>? ProgramsRead;

        /// <summary>
        /// Raised when the selected bores, grooves or mills changed, or their
        /// rows now show re-read elements, so the preview can restyle them.
        /// </summary>
        public event Action? SelectionChanged;

        #region Properties
        public bool HasBores => Bores.Count > 0;

        public bool HasGrooves => Grooves.Count > 0;

        public bool HasTools => Tools.Count > 0;

        public bool HasVariables => Variables.Count > 0;

        public bool HasMillingContours => MillingContours.Count > 0;

        public bool HasMillingEllipses => MillingEllipses.Count > 0;

        public bool HasMillingRectangles => MillingRectangles.Count > 0;

        public bool HasSelectedBores => HasSelectedRow(Bores);

        public bool HasSelectedGrooves => HasSelectedRow(Grooves);

        public bool HasSelectedMillingContours => HasSelectedRow(MillingContours);

        public bool HasSelectedMillingEllipses => HasSelectedRow(MillingEllipses);

        public bool HasSelectedMillingRectangles => HasSelectedRow(MillingRectangles);
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

        /// <summary>The groove row selected in the grooves <c>DataGrid</c>, whose cells are edited in place.</summary>
        [ObservableProperty]
        private GrooveRowVM? _selectedGroove;

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

        /// <summary>The contour row selected in the milling contours <c>DataGrid</c> (its depth is edited in place).</summary>
        [ObservableProperty]
        private MillingContourRowVM? _selectedMillingContour;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMillingEllipses))]
        private ObservableCollection<MillingEllipseRowVM> _millingEllipses = [];

        /// <summary>The ellipse row selected in the milling ellipses <c>DataGrid</c>, whose cells are edited in place.</summary>
        [ObservableProperty]
        private MillingEllipseRowVM? _selectedMillingEllipse;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMillingRectangles))]
        private ObservableCollection<MillingRectangleRowVM> _millingRectangles = [];

        /// <summary>The rectangle row selected in the milling rectangles <c>DataGrid</c>, whose cells are edited in place.</summary>
        [ObservableProperty]
        private MillingRectangleRowVM? _selectedMillingRectangle;

        /// <summary>The "XNC List" text: one line per program element (see <see cref="ProgramsSummary"/>).</summary>
        [ObservableProperty]
        private string _programsText = ProgramsSummary.NoPart;

        /// <summary>The XNC programs attached to the part, as last read.</summary>
        [ObservableProperty]
        private IReadOnlyList<XncProgram> _programs = [];
        #endregion

        #region Commands
        [RelayCommand(CanExecute = nameof(HasSelectedBores))]
        private void DeleteSelectedBores() =>
            DeleteSelected(XncElementKind.Bore, Bores, bore => bore.Index);

        [RelayCommand(CanExecute = nameof(HasSelectedGrooves))]
        private void DeleteSelectedGrooves() =>
            DeleteSelected(XncElementKind.Groove, Grooves, element => element.Source.Index);

        [RelayCommand(CanExecute = nameof(HasSelectedMillingContours))]
        private void DeleteSelectedMillingContours() =>
            DeleteSelected(XncElementKind.Contour, MillingContours, element => element.Source.Index);

        [RelayCommand(CanExecute = nameof(HasSelectedMillingEllipses))]
        private void DeleteSelectedMillingEllipses() =>
            DeleteSelected(XncElementKind.Ellipse, MillingEllipses, element => element.Source.Index);

        [RelayCommand(CanExecute = nameof(HasSelectedMillingRectangles))]
        private void DeleteSelectedMillingRectangles() =>
            DeleteSelected(XncElementKind.Rectangle, MillingRectangles, element => element.Source.Index);
        #endregion

        #region Methods
        /// <summary>
        /// Re-reads the XNC programs of <paramref name="part"/> and rebuilds everything derived
        /// from them: the programs text, the program tables and (through <see cref="ProgramsRead"/>)
        /// the preview.
        /// </summary>
        public void Refresh(PartVM? part) => Rebuild(part, clearedTable: null);

        /// <summary>Whether the row showing <paramref name="element"/> (a bore, groove or mill, by reference) is selected.</summary>
        public bool IsSelected(object element) =>
            ElementRows().Any(row => row.IsSelected && ReferenceEquals(row.Element, element));

        /// <summary>
        /// Selects the row showing <paramref name="element"/> as a click on it in the preview does:
        /// only it (<see cref="ElementSelectionMode.Replace"/>, across every table), toggled, or added.
        /// An element no row shows is ignored.
        /// </summary>
        public void SelectElement(object element, ElementSelectionMode mode) => BatchSelection(() =>
        {
            if (ElementRows().FirstOrDefault(row => ReferenceEquals(row.Element, element)) is not { } target)
            {
                return;
            }

            if (mode == ElementSelectionMode.Replace)
            {
                ElementRows().Where(row => !ReferenceEquals(row, target)).ToList().ForEach(row => row.IsSelected = false);
            }

            target.IsSelected = mode != ElementSelectionMode.Toggle || !target.IsSelected;
        });

        /// <summary>Deselects every bore, groove and mill row.</summary>
        public void ClearSelection() =>
            BatchSelection(() => ElementRows().ToList().ForEach(row => row.IsSelected = false));

        /// <summary>
        /// Re-reads the programs of <paramref name="part"/> and rebuilds every table and the preview, as
        /// <see cref="Refresh"/>; the rows of <paramref name="clearedTable"/> (one of <see cref="ElementTables"/>)
        /// come back unselected.
        /// </summary>
        private void Rebuild(PartVM? part, IReadOnlyList<ISelectableRow>? clearedTable)
        {
            var (programs, readError) = ReadPrograms(part);

            Show(part, programs, readError, clearedTable);
            Variables = BuildVariableRows(programs);
        }

        /// <summary>
        /// Deletes the selected <paramref name="rows"/> of one table (elements of <paramref name="kind"/>,
        /// program position by <paramref name="index"/>) once the user confirms, saves the file in place
        /// and rebuilds the tables: the deleted table's remaining rows unselected, every other table
        /// keeping its selection (its elements are unchanged, so its row positions still match).
        /// </summary>
        private void DeleteSelected<TModel>(
            XncElementKind kind,
            IReadOnlyList<MachiningElementRowVM<TModel>> rows,
            Func<TModel, int> index)
            where TModel : IXncMachiningElement
        {
            var part = _parts.SelectedPart;
            var selected = rows.Where(row => row.IsSelected).ToList();

            if (part is null || !_workspace.IsOpen || selected.Count == 0)
            {
                return;
            }

            var subject = $"{selected.Count} selected {kind.DisplayName().ToLowerInvariant()}(s)";

            if (!_dialogs.Confirm($"Delete {subject}?\nTools no other element uses are deleted too.", "Delete"))
            {
                return;
            }

            var elements = selected
                .Select(row => new XncElementRef(row.OperationId, kind, index(row.Model)))
                .ToList();

            if (_saver.SaveDescribedEdit(part, () => _project.Editor.DeleteMachiningElements(elements)))
            {
                Rebuild(part, rows);
            }
        }

        private static bool HasSelectedRow(IEnumerable<ISelectableRow> rows) => rows.Any(row => row.IsSelected);

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

        /// <summary>
        /// Programs text, preview and every program table but the variables. Rebuilt for the part
        /// already shown (rotation, variable edit), each table keeps its selected row positions
        /// while its row count is unchanged - except <paramref name="clearedTable"/>, which comes back
        /// unselected; another part starts with nothing selected.
        /// </summary>
        private void Show(PartVM? part, IReadOnlyList<XncProgram> programs, string? readError, IReadOnlyList<ISelectableRow>? clearedTable = null)
        {
            var keptSelection = ReferenceEquals(part, _shownPart) ? SelectedPositions(clearedTable) : [];
            _shownPart = part;

            ShowPrograms(part, programs, readError);

            Bores = WatchSelection(BuildBoreRows(programs));
            Grooves = WatchSelection(BuildGrooveRows(programs));
            Tools = BuildToolRows(programs);
            MillingContours = WatchSelection(BuildMillingContourRows(programs));
            MillingEllipses = WatchSelection(BuildMillingEllipseRows(programs));
            MillingRectangles = WatchSelection(BuildMillingRectangleRows(programs));

            RestoreSelection(keptSelection);
        }

        /// <summary>The bore, groove and mill tables, in a fixed order.</summary>
        private IEnumerable<IReadOnlyList<ISelectableRow>> ElementTables() =>
            [Bores, Grooves, MillingContours, MillingEllipses, MillingRectangles];

        private IEnumerable<ISelectableRow> ElementRows() => ElementTables().SelectMany(rows => rows);

        /// <summary>
        /// Per table (see <see cref="ElementTables"/>), its row count and selected row positions; none for
        /// <paramref name="clearedTable"/>.
        /// </summary>
        private List<(int Count, List<int> Selected)> SelectedPositions(IReadOnlyList<ISelectableRow>? clearedTable) =>
            [.. ElementTables().Select(rows => (
                rows.Count,
                ReferenceEquals(rows, clearedTable)
                    ? []
                    : rows.Select((row, i) => (row, i)).Where(entry => entry.row.IsSelected).Select(entry => entry.i).ToList()))];

        /// <summary>Selects the <paramref name="kept"/> rows of the rebuilt tables and tells the preview the rows changed.</summary>
        private void RestoreSelection(List<(int Count, List<int> Selected)> kept) => BatchSelection(() =>
        {
            ElementTables()
                .Zip(kept)
                .Where(pair => pair.First.Count == pair.Second.Count)
                .SelectMany(pair => pair.Second.Selected.Select(i => pair.First[i]))
                .ToList()
                .ForEach(row => row.IsSelected = true);

            RaiseSelectionChanged();
        });

        /// <summary>Runs <paramref name="update"/> raising <see cref="SelectionChanged"/> at most once, after it.</summary>
        private void BatchSelection(Action update)
        {
            _isBatchingSelection = true;

            try
            {
                update();
            }
            finally
            {
                _isBatchingSelection = false;
            }

            if (_isSelectionChangePending)
            {
                RaiseSelectionChanged();
            }
        }

        private void RaiseSelectionChanged()
        {
            if (_isBatchingSelection)
            {
                _isSelectionChangePending = true;
                return;
            }

            _isSelectionChangePending = false;
            NotifyDeleteCommands();
            SelectionChanged?.Invoke();
        }

        /// <summary>Each table's Delete is enabled while that table has a selected row.</summary>
        private void NotifyDeleteCommands()
        {
            DeleteSelectedBoresCommand.NotifyCanExecuteChanged();
            DeleteSelectedGroovesCommand.NotifyCanExecuteChanged();
            DeleteSelectedMillingContoursCommand.NotifyCanExecuteChanged();
            DeleteSelectedMillingEllipsesCommand.NotifyCanExecuteChanged();
            DeleteSelectedMillingRectanglesCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Follows the selection of every row of <paramref name="rows"/>.</summary>
        private ObservableCollection<TRow> WatchSelection<TRow>(ObservableCollection<TRow> rows)
            where TRow : ISelectableRow
        {
            foreach (var row in rows)
            {
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ISelectableRow.IsSelected))
                    {
                        RaiseSelectionChanged();
                    }
                };
            }

            return rows;
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
        private bool OnBoreRowEdited(MachiningElementRowVM<XncBore> row, MachiningAttribute attribute, string expression) =>
            SaveProgramEdit(
                row.OperationId,
                $"Bore #{row.Number} {attribute.XmlName()} = \"{expression}\"",
                operationId => _project.Editor.UpdateBore(operationId, row.Model.Index, attribute, expression),
                part => RefreshRowsInPlace(part, Bores, program => program.Bores));

        /// <summary>
        /// The cell edit handler of a groove or mill table: writes one committed cell edit of an
        /// element of <paramref name="kind"/> into its program, saves the file in place and
        /// refreshes that table (rows from <paramref name="rows"/>) and the preview. The handler
        /// returns <c>false</c> when nothing was written, so the cell stays in edit mode.
        /// </summary>
        private Func<MachiningElementRowVM<TModel>, MachiningAttribute, string, bool> ElementEditHandler<TModel, TRow>(
            XncElementKind kind,
            Func<ObservableCollection<TRow>> rows,
            Func<XncProgram, IEnumerable<TModel>> elements)
            where TModel : IXncEditableElement
            where TRow : MachiningElementRowVM<TModel> =>
            (row, attribute, expression) => SaveProgramEdit(
                row.OperationId,
                $"{kind.DisplayName()} #{row.Number} {attribute.XmlName()} = \"{expression}\"",
                operationId => _project.Editor.UpdateMachiningElement(operationId, kind, row.Model.Source.Index, attribute, expression),
                part => RefreshRowsInPlace(part, rows(), elements));

        /// <summary>
        /// Writes one committed variable cell edit into its program, saves the file in place and
        /// refreshes everything derived from the programs (a variable may drive bores, mills, ...).
        /// Returns <c>false</c> when nothing was written, so the cell stays in edit mode.
        /// </summary>
        private bool OnVariableRowEdited(VariableRowVM row, XncVariableAttribute attribute, string value) =>
            SaveProgramEdit(
                row.OperationId,
                $"Variable #{row.Number} {attribute.XmlName()} = \"{value}\"",
                operationId => _project.Editor.UpdateVariable(operationId, row.Variable.Index, attribute, value),
                RefreshAfterVariableEdit);

        /// <summary>
        /// Applies <paramref name="edit"/> to the program of XNC operation <paramref name="operationId"/>
        /// of the selected part, saves the file in place and runs <paramref name="refresh"/>.
        /// </summary>
        private bool SaveProgramEdit(int operationId, string description, Func<int, OperationResult> edit, Action<PartVM> refresh)
        {
            var part = _parts.SelectedPart;

            if (part is null || !_workspace.IsOpen)
            {
                return false;
            }

            if (!_saver.SaveWithPendingEdits(part, () => edit(operationId), description))
            {
                return false;
            }

            refresh(part);

            return true;
        }

        /// <summary>
        /// Re-reads <paramref name="part"/>'s programs after a bore, groove or mill edit and updates
        /// the edited table's <paramref name="rows"/> in place - the collection is not replaced,
        /// because this runs while that <c>DataGrid</c> is still ending its cell edit (replacing its
        /// items source then throws), and in-place rows keep the grid's selection. One element does
        /// not affect the other machining tables, so those are left alone; the re-read programs
        /// redraw the preview.
        /// </summary>
        private void RefreshRowsInPlace<TModel, TRow>(
            PartVM part,
            ObservableCollection<TRow> rows,
            Func<XncProgram, IEnumerable<TModel>> elements)
            where TModel : IXncMachiningElement
            where TRow : MachiningElementRowVM<TModel>
        {
            var (programs, readError) = ReadPrograms(part);
            var reread = programs.SelectMany(program => elements(program).Select(element => (Element: element, Program: program))).ToList();

            if (readError is not null || reread.Count != rows.Count)
            {
                Refresh(part);
                return;
            }

            ShowPrograms(part, programs, null);

            rows
                .Zip(reread)
                .ToList()
                .ForEach(pair => pair.First.Apply(pair.Second.Element, pair.Second.Program));

            // The rows now show the re-read elements the preview draws.
            RaiseSelectionChanged();
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
                OnBoreRowEdited));

        private static ObservableCollection<ToolRowVM> BuildToolRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.Tools,
                (number, program, tool) => new ToolRowVM(number, MachiningDisplay.SideName(program.Side), tool));

        private ObservableCollection<VariableRowVM> BuildVariableRows(IReadOnlyList<XncProgram> programs) =>
            BuildRows(programs, program => program.DeclaredVariables,
                (number, program, variable) => new VariableRowVM(
                    number, MachiningDisplay.SideName(program.Side), variable, program, OnVariableRowEdited));

        private ObservableCollection<GrooveRowVM> BuildGrooveRows(IReadOnlyList<XncProgram> programs)
        {
            var onEdited = ElementEditHandler<XncGrooving, GrooveRowVM>(
                XncElementKind.Groove, () => Grooves, program => program.Groovings);

            return BuildRows(programs, program => program.Groovings,
                (number, program, groove) => new GrooveRowVM(
                    number, MachiningDisplay.GrooveSide(groove, program), groove, program, onEdited));
        }

        private ObservableCollection<MillingContourRowVM> BuildMillingContourRows(IReadOnlyList<XncProgram> programs)
        {
            var onEdited = ElementEditHandler<XncMillingContour, MillingContourRowVM>(
                XncElementKind.Contour, () => MillingContours, program => program.MillingContours);

            return BuildRows(programs, program => program.MillingContours,
                (number, program, contour) => new MillingContourRowVM(
                    number, MachiningDisplay.SideName(program.Side), contour, program, onEdited));
        }

        private ObservableCollection<MillingEllipseRowVM> BuildMillingEllipseRows(IReadOnlyList<XncProgram> programs)
        {
            var onEdited = ElementEditHandler<XncMillingEllipse, MillingEllipseRowVM>(
                XncElementKind.Ellipse, () => MillingEllipses, program => program.MillingEllipses);

            return BuildRows(programs, program => program.MillingEllipses,
                (number, program, ellipse) => new MillingEllipseRowVM(
                    number, MachiningDisplay.SideName(program.Side), ellipse, program, onEdited));
        }

        private ObservableCollection<MillingRectangleRowVM> BuildMillingRectangleRows(IReadOnlyList<XncProgram> programs)
        {
            var onEdited = ElementEditHandler<XncMillingRectangle, MillingRectangleRowVM>(
                XncElementKind.Rectangle, () => MillingRectangles, program => program.MillingRectangles);

            return BuildRows(programs, program => program.MillingRectangles,
                (number, program, rectangle) => new MillingRectangleRowVM(
                    number, MachiningDisplay.SideName(program.Side), rectangle, program, onEdited));
        }

        /// <summary>Diameter of the program's tool <paramref name="toolName"/>, or <c>null</c> when it is not declared.</summary>
        private static string? ToolDiameter(XncProgram program, string toolName) =>
            program.FindTool(toolName)?.Diameter is { } diameter
                ? MachiningNumber.Format(diameter)
                : null;
        #endregion
    }
}
