using System.Diagnostics;

namespace XncOptimizerUI.Test
{
    /// <summary>Collects the messages WPF traces for failed bindings (<c>PresentationTraceSources.DataBindingSource</c>).</summary>
    internal sealed class BindingErrorListener : TraceListener
    {
        private readonly List<string> _messages = [];

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (message is not null)
            {
                _messages.Add(message);
            }
        }

        /// <summary>The messages collected so far; the listener starts over empty.</summary>
        public IReadOnlyList<string> TakeMessages()
        {
            var messages = _messages.ToList();

            _messages.Clear();

            return messages;
        }
    }
}
