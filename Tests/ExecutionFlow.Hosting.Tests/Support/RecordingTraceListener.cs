using System.Diagnostics;

namespace ExecutionFlow.Hosting.Tests.Support
{
    /// <summary>Collects the trace messages written while it's registered.</summary>
    internal sealed class RecordingTraceListener : TraceListener
    {
        private readonly List<string> _messages = new();

        public IReadOnlyList<string> Messages
        {
            get { lock (_messages) return _messages.ToList(); }
        }

        public override void Write(string? message) => Add(message);

        public override void WriteLine(string? message) => Add(message);

        private void Add(string? message)
        {
            if (message != null)
                lock (_messages) _messages.Add(message);
        }
    }
}
