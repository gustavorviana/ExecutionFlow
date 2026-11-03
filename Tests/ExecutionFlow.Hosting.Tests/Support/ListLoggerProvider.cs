using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;

namespace ExecutionFlow.Hosting.Tests.Support
{
    internal sealed class ListLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

        public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries.ToList();

        public IEnumerable<string> At(LogLevel level) => Entries.Where(e => e.Level == level).Select(e => e.Message);

        public ILogger CreateLogger(string categoryName) => new ListLogger(this);

        public void Dispose()
        {
        }

        private sealed class ListLogger : ILogger
        {
            private readonly ListLoggerProvider _provider;

            public ListLogger(ListLoggerProvider provider) => _provider = provider;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _provider._entries.Enqueue((logLevel, formatter(state, exception)));
            }
        }
    }
}
