using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Test.Fakes
{
    /// <summary>
    /// Hand-written stand-in for every project service role, recording each call and its
    /// arguments; <see cref="Services"/> hands it to the view model. Operations return
    /// <see cref="ResultMessage"/> as a highlighted log entry and the configured <c>*Result</c>
    /// as <see cref="OperationResult.Changed"/>.
    /// </summary>
    public class FakeProjectService : IProjectSession, IProjectReader, IProjectEditor, IProjectRestructuring, IXncBatchOperations
    {
        public FakeProjectService()
        {
            Services = new ProjectServices(this, this, this, this, this);
        }

        /// <summary>This fake in every role.</summary>
        public ProjectServices Services { get; }

        public List<Part> Parts { get; set; } = [];
        public List<Band> Bands { get; set; } = [];
        public List<Sheet> Sheets { get; set; } = [];
        public List<Product> Products { get; set; } = [];
        public IReadOnlyList<XncProgram> XncPrograms { get; set; } = [];
        public IReadOnlyList<string> PartsWithXncTurnDiscordance { get; set; } = [];

        public string FullPath { get; set; } = string.Empty;

        /// <summary>Message every operation reports (as a highlighted entry); <c>null</c> reports nothing.</summary>
        public string? ResultMessage { get; set; }

        public bool GroupIdenticalElementsResult { get; set; } = true;
        public bool PrepForSplitAlongXResult { get; set; } = true;
        public bool ReplaceXncProgramsResult { get; set; } = true;
        public bool ConvertGroovesAndMillsResult { get; set; } = true;
        public bool ConvertBoresAndMillsResult { get; set; } = true;
        public bool OptimizeMillTraversalResult { get; set; } = true;
        public bool SortToolDeclarationsResult { get; set; } = true;
        public bool OffsetMillPathsResult { get; set; } = true;
        public bool CleanOutsideOperationsResult { get; set; } = true;
        public bool UpdatePartResult { get; set; } = true;
        public bool RotatePartResult { get; set; } = true;
        public bool UpdateVariableResult { get; set; } = true;
        public bool UpdateBoreResult { get; set; } = true;

        /// <summary>Programs <see cref="ReadXncPrograms"/> returns after a successful <see cref="RotatePart"/>; <c>null</c> keeps them.</summary>
        public IReadOnlyList<XncProgram>? XncProgramsAfterRotate { get; set; }

        /// <summary>Programs <see cref="ReadXncPrograms"/> returns after a successful <see cref="UpdateVariable"/>; <c>null</c> keeps them.</summary>
        public IReadOnlyList<XncProgram>? XncProgramsAfterUpdateVariable { get; set; }

        /// <summary>Programs <see cref="ReadXncPrograms"/> returns after a successful <see cref="UpdateBore"/>; <c>null</c> keeps them.</summary>
        public IReadOnlyList<XncProgram>? XncProgramsAfterUpdateBore { get; set; }

        public List<string> Calls { get; } = [];

        public int SaveCount { get; private set; }
        public Part? LastUpdatedPart { get; private set; }
        public IReadOnlyCollection<string>? LastSplitPartIds { get; private set; }
        public IList<Part>? LastReplaceTargets { get; private set; }
        public IList<Part>? LastGrooveMillParts { get; private set; }
        public GrooveMillDirection? LastGrooveMillDirection { get; private set; }
        public bool? LastGrooveMillProcessPockets { get; private set; }
        public IList<Part>? LastBoreMillParts { get; private set; }
        public BoreMillDirection? LastBoreMillDirection { get; private set; }
        public bool? LastBoreMillUseEllipse { get; private set; }
        public IList<Part>? LastMillTraversalParts { get; private set; }
        public IList<Part>? LastToolSortParts { get; private set; }
        public IList<Part>? LastMillOffsetParts { get; private set; }
        public double? LastMillOffset { get; private set; }
        public MillOffsetSide? LastMillOffsetSide { get; private set; }
        public MillPathKinds? LastMillOffsetKinds { get; private set; }
        public IList<Part>? LastCleanOutsideParts { get; private set; }
        public OutsideOperationKinds? LastCleanOutsideKinds { get; private set; }
        public int? LastRotatePartId { get; private set; }
        public int? LastRotateTargetTurn { get; private set; }
        public bool? LastRotateFlipEdgeGrooveTcl { get; private set; }
        public (int OperationId, int VariableIndex, XncVariableAttribute Attribute, string Value)? LastUpdateVariable { get; private set; }
        public (int OperationId, int BoreIndex, BoreAttribute Attribute, string Expression)? LastUpdateBore { get; private set; }

        #region IProjectSession

        public void Open(string path)
        {
            Calls.Add(nameof(Open));
            FullPath = path;
        }

        public void Close() => Calls.Add(nameof(Close));

        public void Save()
        {
            Calls.Add(nameof(Save));
            SaveCount++;
        }

        #endregion

        #region IProjectReader

        public List<Part> ReadParts() => Record(nameof(ReadParts), Parts);

        public List<Band> ReadBands() => Record(nameof(ReadBands), Bands);

        public List<Sheet> ReadSheets() => Record(nameof(ReadSheets), Sheets);

        public List<Product> ReadProducts() => Record(nameof(ReadProducts), Products);

        public int GetXncProgramsCount(int partId) => Record(nameof(GetXncProgramsCount), 0);

        public IReadOnlyList<XncProgram> ReadXncPrograms(int partId) => Record(nameof(ReadXncPrograms), XncPrograms);

        public IReadOnlyList<string> GetPartsWithXncTurnDiscordance() =>
            Record(nameof(GetPartsWithXncTurnDiscordance), PartsWithXncTurnDiscordance);

        #endregion

        #region IProjectEditor

        public OperationResult UpdatePart(Part part)
        {
            LastUpdatedPart = part;

            return new OperationResult(Record(nameof(UpdatePart), UpdatePartResult), []);
        }

        public OperationResult RotatePart(int partId, int targetTurn, bool flipEdgeGrooveTcl)
        {
            (LastRotatePartId, LastRotateTargetTurn, LastRotateFlipEdgeGrooveTcl) = (partId, targetTurn, flipEdgeGrooveTcl);

            return Result(nameof(RotatePart), RotatePartResult, XncProgramsAfterRotate);
        }

        public OperationResult UpdateBore(int operationId, int boreIndex, BoreAttribute attribute, string expression)
        {
            LastUpdateBore = (operationId, boreIndex, attribute, expression);

            return Result(nameof(UpdateBore), UpdateBoreResult, XncProgramsAfterUpdateBore);
        }

        public OperationResult UpdateVariable(int operationId, int variableIndex, XncVariableAttribute attribute, string value)
        {
            LastUpdateVariable = (operationId, variableIndex, attribute, value);

            return Result(nameof(UpdateVariable), UpdateVariableResult, XncProgramsAfterUpdateVariable);
        }

        #endregion

        #region IProjectRestructuring

        public OperationResult GroupIdenticalElements() => Result(nameof(GroupIdenticalElements), GroupIdenticalElementsResult);

        public OperationResult PrepForSplitAlongX(IReadOnlyCollection<string> partIds)
        {
            LastSplitPartIds = partIds;

            return Result(nameof(PrepForSplitAlongX), PrepForSplitAlongXResult);
        }

        public OperationResult ReplaceXncPrograms(Part sourcePart, IList<Part> targetParts)
        {
            LastReplaceTargets = targetParts;

            return Result(nameof(ReplaceXncPrograms), ReplaceXncProgramsResult);
        }

        #endregion

        #region IXncBatchOperations

        public OperationResult ConvertGroovesAndMills(IList<Part> parts, GrooveMillDirection direction, bool processPockets)
        {
            (LastGrooveMillParts, LastGrooveMillDirection, LastGrooveMillProcessPockets) = (parts, direction, processPockets);

            return Result(nameof(ConvertGroovesAndMills), ConvertGroovesAndMillsResult);
        }

        public OperationResult ConvertBoresAndMills(IList<Part> parts, BoreMillDirection direction, bool useEllipse)
        {
            (LastBoreMillParts, LastBoreMillDirection, LastBoreMillUseEllipse) = (parts, direction, useEllipse);

            return Result(nameof(ConvertBoresAndMills), ConvertBoresAndMillsResult);
        }

        public OperationResult OptimizeMillTraversal(IList<Part> parts)
        {
            LastMillTraversalParts = parts;

            return Result(nameof(OptimizeMillTraversal), OptimizeMillTraversalResult);
        }

        public OperationResult SortToolDeclarations(IList<Part> parts)
        {
            LastToolSortParts = parts;

            return Result(nameof(SortToolDeclarations), SortToolDeclarationsResult);
        }

        public OperationResult OffsetMillPaths(IList<Part> parts, double offset, MillOffsetSide side, MillPathKinds kinds)
        {
            (LastMillOffsetParts, LastMillOffset, LastMillOffsetSide, LastMillOffsetKinds) = (parts, offset, side, kinds);

            return Result(nameof(OffsetMillPaths), OffsetMillPathsResult);
        }

        public OperationResult CleanOutsideOperations(IList<Part> parts, OutsideOperationKinds kinds)
        {
            (LastCleanOutsideParts, LastCleanOutsideKinds) = (parts, kinds);

            return Result(nameof(CleanOutsideOperations), CleanOutsideOperationsResult);
        }

        #endregion

        #region Methods

        private T Record<T>(string call, T value)
        {
            Calls.Add(call);

            return value;
        }

        /// <summary>Records the call; a changed result swaps in <paramref name="programsAfter"/> when given.</summary>
        private OperationResult Result(string call, bool changed, IReadOnlyList<XncProgram>? programsAfter = null)
        {
            Calls.Add(call);

            if (changed && programsAfter is { } programs)
            {
                XncPrograms = programs;
            }

            return new OperationResult(changed, ResultMessage is { } message ? [new LogEntry(LogLevel.Highlight, message)] : []);
        }

        #endregion
    }
}
