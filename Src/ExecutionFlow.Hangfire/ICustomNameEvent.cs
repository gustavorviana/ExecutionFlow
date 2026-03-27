namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Implement on an event to provide a custom display name for the Hangfire dashboard.
    /// </summary>
    public interface ICustomNameEvent
    {
        /// <summary>Gets the custom display name for this job.</summary>
        string CustomName { get; }
    }
}
