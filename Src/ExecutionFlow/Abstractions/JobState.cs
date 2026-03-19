namespace ExecutionFlow.Abstractions
{
    public enum JobState
    {
        Enqueued,
        Processing,
        Succeeded,
        Failed,
        Cancelled
    }
}
