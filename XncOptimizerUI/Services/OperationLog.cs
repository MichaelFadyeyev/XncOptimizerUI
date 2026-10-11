using XncOptimizerUI.Contracts;

namespace XncOptimizerUI.Services
{
    /// <summary>Collects the log entries of one operation and builds its <see cref="OperationResult"/>.</summary>
    internal sealed class OperationLog
    {
        private readonly List<LogEntry> _entries = [];

        public OperationLog Info(string text)
        {
            _entries.Add(new LogEntry(LogLevel.Info, text));
            return this;
        }

        public OperationLog Info(IEnumerable<string> lines)
        {
            _entries.AddRange(lines.Select(line => new LogEntry(LogLevel.Info, line)));
            return this;
        }

        public OperationLog Highlight(string text)
        {
            _entries.Add(new LogEntry(LogLevel.Highlight, text));
            return this;
        }

        public OperationResult Changed() => new(true, [.. _entries]);

        public OperationResult Unchanged() => new(false, [.. _entries]);

        /// <summary>Reports <paramref name="problem"/> and returns an unchanged result.</summary>
        public OperationResult Fail(string problem) => Highlight(problem).Unchanged();
    }
}
