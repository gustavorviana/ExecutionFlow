using System;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Provides methods to manually trigger recurring jobs.
    /// </summary>
    public interface IRecurringTrigger
    {
        /// <summary>
        /// Triggers a recurring job by its handler type.
        /// </summary>
        /// <param name="handlerType">The handler type to trigger. Must be registered as a recurring handler.</param>
        void Trigger(Type handlerType);

        /// <summary>
        /// Triggers a recurring job by its job identifier.
        /// </summary>
        /// <param name="jobId">The recurring job ID.</param>
        /// <exception cref="ArgumentNullException"><paramref name="jobId"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="jobId"/> is empty, only spaces, or doesn't match the ID format (letters, digits, spaces and <c>_ - . + ` [ ] , =</c>).</exception>
        void Trigger(string jobId);
    }
}
