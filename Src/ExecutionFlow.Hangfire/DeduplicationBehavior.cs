namespace ExecutionFlow.Hangfire
{
    public enum DeduplicationBehavior
    {
        Disabled,
        SkipIfExists,
        ReplaceExisting
    }
}
