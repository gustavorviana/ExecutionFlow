using Hangfire.Common;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Resolves display names for Hangfire jobs on the dashboard.
    /// </summary>
    public interface IHangfireJobName
    {
        /// <summary>
        /// Gets the display name for a Hangfire job.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        /// <returns>The display name string.</returns>
        string GetName(Job job);
    }
}
