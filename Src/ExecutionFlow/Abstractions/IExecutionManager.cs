using System;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Provides methods to query, cancel, and retry background jobs.
    /// Event jobs are identified by custom ID; recurring jobs are identified by handler type.
    /// </summary>
    public interface IExecutionManager
    {
        /// <summary>
        /// Checks whether an event job with the specified custom ID is currently processing.
        /// </summary>
        /// <param name="customId">The custom identifier to search for.</param>
        /// <returns><c>true</c> if a matching job is processing; otherwise, <c>false</c>.</returns>
        bool IsRunning(string customId);

        /// <summary>
        /// Checks whether a recurring job with the specified handler type is currently processing.
        /// </summary>
        /// <param name="handlerType">The recurring handler type to search for.</param>
        /// <returns><c>true</c> if a matching recurring job is processing; otherwise, <c>false</c>.</returns>
        bool IsRunning(Type handlerType);

        /// <summary>
        /// Checks whether an event job with the specified custom ID is enqueued and waiting to be processed.
        /// </summary>
        /// <param name="customId">The custom identifier to search for.</param>
        /// <returns><c>true</c> if a matching job is pending; otherwise, <c>false</c>.</returns>
        bool IsPending(string customId);

        /// <summary>
        /// Checks whether a recurring job with the specified handler type is enqueued and waiting to be processed.
        /// </summary>
        /// <param name="handlerType">The recurring handler type to search for.</param>
        /// <returns><c>true</c> if a matching recurring job is pending; otherwise, <c>false</c>.</returns>
        bool IsPending(Type handlerType);

        /// <summary>
        /// Cancels a pending or processing event job with the specified custom ID.
        /// </summary>
        /// <param name="customId">The custom identifier of the job to cancel.</param>
        void Cancel(string customId);

        /// <summary>
        /// Cancels a pending or processing recurring job with the specified handler type.
        /// </summary>
        /// <param name="handlerType">The recurring handler type of the job to cancel.</param>
        void Cancel(Type handlerType);

        /// <summary>
        /// Re-enqueues a failed event job with the specified custom ID for reprocessing.
        /// </summary>
        /// <param name="customId">The custom identifier of the failed job to retry.</param>
        /// <returns><c>true</c> if the job was found and re-enqueued; otherwise, <c>false</c>.</returns>
        bool Retry(string customId);

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
    }
}
