namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Implement on an event to give its job a custom display name (e.g. "Reminder for order 42"),
    /// shown in the Hangfire dashboard and in job metadata.
    /// </summary>
    public interface ICustomNameEvent
    {
        /// <summary>Gets the custom display name for this job. A null or empty value is ignored.</summary>
        string CustomName { get; }
    }
}
