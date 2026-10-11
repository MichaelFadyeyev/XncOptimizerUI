namespace XncOptimizerUI.Contracts
{
    /// <summary>How a log entry is shown: a plain line, or a highlighted message (summary or problem) after a <c>***</c> separator.</summary>
    public enum LogLevel
    {
        Info,
        Highlight
    }

    /// <summary>One message an operation reports to the user.</summary>
    public readonly record struct LogEntry(LogLevel Level, string Text);

    /// <summary>
    /// Outcome of a project operation: whether it changed the document, and what it reports.
    /// An operation that changes nothing (refused, failed or found nothing to do) explains why in
    /// its <see cref="Log"/>.
    /// </summary>
    public sealed record OperationResult(bool Changed, IReadOnlyList<LogEntry> Log)
    {
        private const string HighlightSeparator = "***\n";

        /// <summary>The log as text for the log box: one line per entry, highlighted ones after a <c>***</c> line.</summary>
        public string LogText => string.Concat(Log.Select(FormatEntry));

        /// <summary>The log's messages without separators, e.g. for a warning dialog.</summary>
        public string MessageText => string.Join("\n", Log.Select(e => e.Text)).Trim();

        private static string FormatEntry(LogEntry entry) =>
            entry.Level == LogLevel.Highlight ? $"{HighlightSeparator}{entry.Text}\n" : $"{entry.Text}\n";
    }
}
