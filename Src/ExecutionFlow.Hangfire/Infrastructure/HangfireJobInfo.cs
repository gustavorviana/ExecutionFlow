using ExecutionFlow.Abstractions;
using Hangfire.Common;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Represents job metadata for event-based Hangfire jobs dispatched through ExecutionFlow.
    /// </summary>
    public class HangfireEventJobInfo : HangfireJobInfo
    {
        internal const int CustomNameIndex = 1;
        internal const int JobArgSize = 4;

        /// <summary>Gets the event type associated with this job.</summary>
        public Type EventType { get; }

        /// <summary>Gets the custom job name, if one was provided when the event was published.</summary>
        public string CustomJobName { get; }

        /// <summary>Gets the event published with this job (the job's first argument), if available.</summary>
        public object Event => Job?.Args?.Count > 0 ? Job.Args[0] : null;

        /// <summary>
        /// Initializes a new instance from a Hangfire <see cref="Job"/>.
        /// </summary>
        /// <param name="job">The Hangfire job to extract event information from.</param>
        public HangfireEventJobInfo(Job job) : base(job)
        {
            EventType = job.Method.GetGenericArguments()[0];
            var foundName = job.Args?.Count != JobArgSize ? null : job.Args[CustomNameIndex] as string;
            if (!string.IsNullOrEmpty(foundName))
                CustomJobName = foundName;
        }

        /// <inheritdoc />
        public override Type CarriedType => EventType;

        /// <inheritdoc />
        public override IJobRegistryInfo GetHandler(IExecutionFlowRegistry registry)
        {
            if (registry.EventHandlers.TryGetValue(EventType, out var eventHandler))
                return eventHandler;

            return null;
        }
    }

    /// <summary>
    /// Represents job metadata for recurring Hangfire jobs dispatched through ExecutionFlow.
    /// </summary>
    public class HangfireRecurringJobInfo : HangfireJobInfo
    {
        internal const int EventHandlerIndex = 1;
        internal const int JobArgSize = 3;

        /// <summary>Gets the handler type associated with this recurring job.</summary>
        public Type HandlerType { get; }

        /// <summary>
        /// Initializes a new instance from a Hangfire <see cref="Job"/>.
        /// </summary>
        /// <param name="job">The Hangfire job to extract recurring handler information from.</param>
        public HangfireRecurringJobInfo(Job job) : base(job)
        {
            HandlerType = Job?.Args?.Count != JobArgSize ? null : Job.Args[EventHandlerIndex] as Type;
        }

        /// <inheritdoc />
        public override Type CarriedType => HandlerType;

        /// <inheritdoc />
        public override IJobRegistryInfo GetHandler(IExecutionFlowRegistry registry)
        {
            if (HandlerType == null)
                return null;

            if (registry.RecurringHandlers.TryGetValue(HandlerType, out var eventHandler))
                return eventHandler;

            return null;
        }

        /// <summary>
        /// Extracts the recurring handler type from a Hangfire job's arguments.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        /// <returns>The handler type, or <c>null</c> if not found.</returns>
        public static Type GetJobType(Job job)
        {
            if (job?.Args?.Count == JobArgSize && job.Args[RecurringEventType] is Type jobType)
                return jobType;

            return null;
        }
    }

    /// <summary>
    /// Base class for Hangfire job metadata: the facts an ExecutionFlow job carries (its event or handler type)
    /// and how to find its handler registration. Naming rules live in <see cref="JobDisplayNameResolver"/>.
    /// </summary>
    public abstract class HangfireJobInfo
    {
        internal const int RecurringEventType = 1;

        /// <summary>Gets the underlying Hangfire <see cref="Hangfire.Common.Job"/>.</summary>
        public Job Job { get; }

        internal HangfireJobInfo(Job job)
        {
            Job = job;
        }

        /// <summary>
        /// Gets the type this job carries in its arguments: the event type (event jobs) or the handler type (recurring jobs).
        /// Available even on hosts where the handler isn't registered.
        /// </summary>
        public abstract Type CarriedType { get; }

        /// <summary>
        /// Creates the appropriate <see cref="HangfireJobInfo"/> subclass based on whether the job is event-based or recurring.
        /// </summary>
        /// <param name="job">The Hangfire job.</param>
        /// <returns>
        /// A <see cref="HangfireEventJobInfo"/> or <see cref="HangfireRecurringJobInfo"/>, or <c>null</c> if the job is <c>null</c>
        /// or isn't an ExecutionFlow job (its method isn't the ExecutionFlow dispatcher).
        /// </returns>
        public static HangfireJobInfo Create(Job job)
        {
            if (job.IsEvent())
                return new HangfireEventJobInfo(job);

            if (job.IsRecurring())
                return new HangfireRecurringJobInfo(job);

            return null;
        }

        /// <summary>
        /// Resolves the handler type for this job from the registry.
        /// </summary>
        /// <param name="registry">The execution flow registry.</param>
        /// <returns>The handler type, or <c>null</c> if not found.</returns>
        public Type GetHandlerType(IExecutionFlowRegistry registry)
        {
            return GetHandler(registry)?.HandlerType;
        }

        /// <summary>
        /// Resolves the handler registration info for this job from the registry.
        /// </summary>
        /// <param name="registry">The execution flow registry.</param>
        /// <returns>The handler registration info, or <c>null</c> if not found.</returns>
        public abstract IJobRegistryInfo GetHandler(IExecutionFlowRegistry registry);
    }
}
