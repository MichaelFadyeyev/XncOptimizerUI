using NSubstitute;
using NSubstitute.Core;
using NSubstitute.Extensions;
using XncOptimizerUI.Contracts;
using XncOptimizerUI.MVVM.Models;
using XncOptimizerUI.MVVM.Models.Xnc;

namespace XncOptimizerUI.Test.Fakes
{
    /// <summary>
    /// Every project service role as one NSubstitute substitute, so <see cref="Calls"/> lists the calls
    /// of all roles in the order they were made. The reader returns the data properties, and every
    /// operation reports a change (all but <see cref="IProjectEditor.UpdatePart"/> with <see cref="ResultMessage"/>)
    /// until a test configures it otherwise,
    /// e.g. <c>Batch.SortToolDeclarations(default!).ReturnsForAnyArgs(_ =&gt; stub.Result(changed: false))</c>.
    /// Arguments are checked the NSubstitute way: <c>Batch.Received(1).SortToolDeclarations(PartsWithIds(100))</c>.
    /// </summary>
    public sealed class ProjectServicesStub
    {
        private readonly object _substitute = Substitute.For(
            [typeof(IProjectSession), typeof(IProjectReader), typeof(IProjectEditor), typeof(IProjectRestructuring), typeof(IXncBatchOperations)],
            []);

        public ProjectServicesStub()
        {
            Services = new ProjectServices(Session, Reader, Editor, Restructuring, Batch);

            ConfigureSession();
            ConfigureReader();
            _substitute.ReturnsForAll<OperationResult>(_ => Result(changed: true));
            Editor.UpdatePart(default!).ReturnsForAnyArgs(_ => new OperationResult(Changed: true, []));

            _substitute.ClearReceivedCalls();
        }

        #region Properties

        /// <summary>The stub in every role, as the view model takes it.</summary>
        public ProjectServices Services { get; }

        public IProjectSession Session => (IProjectSession)_substitute;

        public IProjectReader Reader => (IProjectReader)_substitute;

        public IProjectEditor Editor => (IProjectEditor)_substitute;

        public IProjectRestructuring Restructuring => (IProjectRestructuring)_substitute;

        public IXncBatchOperations Batch => (IXncBatchOperations)_substitute;

        public List<Part> Parts { get; set; } = [];

        public List<Band> Bands { get; set; } = [];

        public List<Sheet> Sheets { get; set; } = [];

        public List<Product> Products { get; set; } = [];

        public IReadOnlyList<XncProgram> XncPrograms { get; set; } = [];

        public IReadOnlyList<string> PartsWithXncTurnDiscordance { get; set; } = [];

        /// <summary>Message every operation reports (as a highlighted entry); <c>null</c> reports nothing.</summary>
        public string? ResultMessage { get; set; }

        /// <summary>Names of the methods called so far, of every role, in call order (property reads left out).</summary>
        public IReadOnlyList<string> Calls =>
            _substitute.ReceivedCalls()
                .Select(call => call.GetMethodInfo())
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .ToList();

        public int SaveCount => Calls.Count(call => call == nameof(IProjectSession.Save));

        #endregion

        #region Methods

        /// <summary>Forgets the calls made so far.</summary>
        public void ClearCalls() => _substitute.ClearReceivedCalls();

        /// <summary>An operation result reporting <see cref="ResultMessage"/>.</summary>
        public OperationResult Result(bool changed) =>
            new(changed, ResultMessage is { } message ? [new LogEntry(LogLevel.Highlight, message)] : []);

        /// <summary>An operation that changes the project and leaves <paramref name="programs"/> for <see cref="IProjectReader.ReadXncPrograms"/>.</summary>
        public Func<CallInfo, OperationResult> ChangingProgramsTo(IReadOnlyList<XncProgram> programs) => _ =>
        {
            XncPrograms = programs;

            return Result(changed: true);
        };

        /// <summary>Argument matcher for a parts list holding exactly the parts with <paramref name="ids"/>, in this order.</summary>
        public static IList<Part> PartsWithIds(params int[] ids) =>
            Arg.Is<IList<Part>>(parts => parts.Select(part => part.Id).SequenceEqual(ids));

        /// <summary>Argument matcher for a parts list holding exactly the parts with <paramref name="ids"/>, in any order.</summary>
        public static IList<Part> PartsWithIdsInAnyOrder(params int[] ids) =>
            Arg.Is<IList<Part>>(parts => parts.Select(part => part.Id).Order().SequenceEqual(ids.Order()));

        private void ConfigureSession()
        {
            var fullPath = string.Empty;

            Session.FullPath.Returns(_ => fullPath);
            Session.When(session => session.Open(Arg.Any<string>())).Do(call => fullPath = call.Arg<string>());
        }

        private void ConfigureReader()
        {
            Reader.ReadParts().Returns(_ => Parts);
            Reader.ReadBands().Returns(_ => Bands);
            Reader.ReadSheets().Returns(_ => Sheets);
            Reader.ReadProducts().Returns(_ => Products);
            Reader.ReadXncPrograms(default).ReturnsForAnyArgs(_ => XncPrograms);
            Reader.GetPartsWithXncTurnDiscordance().Returns(_ => PartsWithXncTurnDiscordance);
        }

        #endregion
    }
}
