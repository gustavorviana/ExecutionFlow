namespace ExecutionFlow.Examples.Hosting
{
    /// <summary>The runs of the example's handlers, newest first, kept in memory.</summary>
    public sealed class RunLog
    {
        private readonly object _lock = new();
        private readonly List<HandlerRun> _runs = new();

        public HandlerRun Start(string handler, string? jobId)
        {
            var run = new HandlerRun { Handler = handler, JobId = jobId, StartedAt = DateTimeOffset.UtcNow };
            lock (_lock)
            {
                _runs.Insert(0, run);
                if (_runs.Count > 500)
                    _runs.RemoveAt(_runs.Count - 1);
            }

            return run;
        }

        public void End(HandlerRun run, string status)
        {
            lock (_lock)
            {
                run.EndedAt = DateTimeOffset.UtcNow;
                run.Status = status;
            }
        }

        public IReadOnlyList<HandlerRun> Recent(int count)
        {
            lock (_lock) return _runs.Take(count).ToList();
        }
    }
}
