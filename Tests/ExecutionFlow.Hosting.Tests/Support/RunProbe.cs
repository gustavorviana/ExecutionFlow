using ExecutionFlow.Abstractions;
using ExecutionFlow.Hosting.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;

namespace ExecutionFlow.Hosting.Tests.Support
{
    /// <summary>Records every handler run and lets a test hold, fail or inspect it.</summary>
    internal sealed class RunProbe
    {
        private readonly ConcurrentDictionary<Type, Func<CancellationToken, Task>> _behaviors = new();
        private readonly object _lock = new();
        private readonly List<string> _events = new();
        private readonly Dictionary<Type, int> _running = new();
        private readonly Dictionary<Type, int> _maxRunning = new();

        public ConcurrentBag<FlowContext> Contexts { get; } = new();
        public ConcurrentBag<object> Instances { get; } = new();

        /// <summary>The most runs of <typeparamref name="T"/> at the same time.</summary>
        public int MaxRunning<T>()
        {
            lock (_lock) return _maxRunning.TryGetValue(typeof(T), out var max) ? max : 0;
        }

        public IReadOnlyList<string> Events
        {
            get { lock (_lock) return _events.ToList(); }
        }

        public int Started<T>() => Events.Count(e => e == "start:" + typeof(T).Name);
        public int Ended<T>() => Events.Count(e => e == "end:" + typeof(T).Name);

        /// <summary>Sets what the next runs of <typeparamref name="T"/> do (default: return at once).</summary>
        public void On<T>(Func<CancellationToken, Task> behavior) => _behaviors[typeof(T)] = behavior;

        public void Throw<T>() => On<T>(_ => throw new InvalidOperationException("boom"));

        /// <summary>Holds every run of <typeparamref name="T"/> until the returned source completes.</summary>
        public TaskCompletionSource Hold<T>()
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            On<T>(ct => release.Task.WaitAsync(ct));
            return release;
        }

        public async Task RunAsync(object handler, FlowContext context, CancellationToken cancellationToken)
        {
            var type = handler.GetType();
            Instances.Add(handler);
            Contexts.Add(context);
            Record("start:" + type.Name);

            lock (_lock)
            {
                var running = _running[type] = (_running.TryGetValue(type, out var current) ? current : 0) + 1;
                _maxRunning[type] = Math.Max(_maxRunning.TryGetValue(type, out var max) ? max : 0, running);
            }

            try
            {
                if (_behaviors.TryGetValue(type, out var behavior))
                    await behavior(cancellationToken);
            }
            finally
            {
                lock (_lock) _running[type]--;
            }

            Record("end:" + type.Name);
        }

        private void Record(string entry)
        {
            lock (_lock) _events.Add(entry);
        }
    }
}
