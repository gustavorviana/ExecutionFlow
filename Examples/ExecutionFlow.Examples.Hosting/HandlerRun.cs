namespace ExecutionFlow.Examples.Hosting
{
    /// <summary>One run of a handler, as recorded in the <see cref="RunLog"/>.</summary>
    public sealed class HandlerRun
    {
        public required string Handler { get; init; }
        public required string? JobId { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset? EndedAt { get; set; }
        public string Status { get; set; } = "Running";
    }
}
