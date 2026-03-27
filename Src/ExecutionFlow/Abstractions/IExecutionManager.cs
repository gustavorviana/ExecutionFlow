using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Provides methods to query, cancel, and retry background jobs.
    /// Event jobs are identified by job ID (matches the internal job ID first, then custom ID);
    /// recurring jobs are identified by handler type.
    /// </summary>
    public interface IExecutionManager
    {
        /// <summary>
        /// Checks whether an event job with the specified ID is currently processing.
        /// Matches against the internal job ID first, then falls back to custom ID.
        /// </summary>
        /// <param name="jobId">The job identifier to search for.</param>
        /// <returns><c>true</c> if a matching job is processing; otherwise, <c>false</c>.</returns>
        bool IsRunning(string jobId);

        /// <summary>
        /// Checks whether a recurring job with the specified handler type is currently processing.
        /// </summary>
        /// <param name="handlerType">The recurring handler type to search for.</param>
        /// <returns><c>true</c> if a matching recurring job is processing; otherwise, <c>false</c>.</returns>
        bool IsRunning(Type handlerType);

        /// <summary>
        /// Checks whether an event job with the specified ID is enqueued and waiting to be processed.
        /// Matches against the internal job ID first, then falls back to custom ID.
        /// </summary>
        /// <param name="jobId">The job identifier to search for.</param>
        /// <returns><c>true</c> if a matching job is pending; otherwise, <c>false</c>.</returns>
        bool IsPending(string jobId);

        /// <summary>
        /// Checks whether a recurring job with the specified handler type is enqueued and waiting to be processed.
        /// </summary>
        /// <param name="handlerType">The recurring handler type to search for.</param>
        /// <returns><c>true</c> if a matching recurring job is pending; otherwise, <c>false</c>.</returns>
        bool IsPending(Type handlerType);

        /// <summary>
        /// Cancels a pending or processing event job with the specified ID.
        /// Matches against the internal job ID first, then falls back to custom ID.
        /// </summary>
        /// <param name="jobId">The job identifier to cancel.</param>
        void Cancel(string jobId);

        /// <summary>
        /// Cancels a pending or processing recurring job with the specified handler type.
        /// </summary>
        /// <param name="handlerType">The recurring handler type of the job to cancel.</param>
        void Cancel(Type handlerType);

        /// <summary>
        /// Re-enqueues a failed event job with the specified ID for reprocessing.
        /// Matches against the internal job ID first, then falls back to custom ID.
        /// </summary>
        /// <param name="jobId">The job identifier to retry.</param>
        /// <returns><c>true</c> if the job was found and re-enqueued; otherwise, <c>false</c>.</returns>
        bool Retry(string jobId);

        /// <summary>
        /// Re-enqueues a failed recurring job with the specified handler type for reprocessing.
        /// </summary>
        /// <param name="handlerType">The recurring handler type of the failed job to retry.</param>
        /// <returns><c>true</c> if the job was found and re-enqueued; otherwise, <c>false</c>.</returns>
        bool Retry(Type handlerType);

        /// <summary>
        /// Retrieves all background jobs in the specified state, including both event and recurring jobs.
        /// </summary>
        /// <param name="state">The job state to filter by.</param>
        /// <returns>A collection of <see cref="JobInfo"/> matching the specified state.</returns>
        IEnumerable<JobInfo> GetJobs(JobState state);

        /// <summary>
        /// Returns the number of background jobs in the specified state.
        /// </summary>
        /// <param name="state">The job state to count.</param>
        /// <returns>The total number of jobs in the given state.</returns>
        long CountJobs(JobState state);

        /// <summary>
        /// Returns a summary with the job count for every <see cref="JobState"/>.
        /// </summary>
        /// <returns>A <see cref="JobStateSummary"/> containing counts for all states.</returns>
        JobStateSummary GetStateSummary();
    }
}
