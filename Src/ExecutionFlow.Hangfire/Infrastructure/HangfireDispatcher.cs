using ExecutionFlow.Abstractions;
using Hangfire;
using System;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    /// <summary>
    /// Dispatches event and recurring jobs to Hangfire, supporting publish, schedule, and trigger operations
    /// with optional deduplication.
    /// </summary>
    public class HangfireDispatcher : IHangfireDispatcher
    {
        private readonly IBackgroundJobClient _jobClient;
        private readonly IJobIdGenerator _jobIdGenerator;
        private readonly IExecutionFlowRegistry _registry;
        private readonly JobStorage _jobStorage;
        private readonly RecurringJobManager _recurringJobManager;
        private readonly DeduplicationBehavior _deduplicationBehavior;
        private readonly Lazy<IExecutionManager> _executionManager;

        public HangfireDispatcher(IBackgroundJobClient jobClient, JobStorage jobStorage, IJobIdGenerator jobIdGenerator, IExecutionFlowRegistry registry, HangfireOptions options)
        {
            _jobClient = jobClient ?? throw new ArgumentNullException(nameof(jobClient));
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
            _jobIdGenerator = jobIdGenerator ?? throw new ArgumentNullException(nameof(jobIdGenerator));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _recurringJobManager = new RecurringJobManager(jobStorage);
            _deduplicationBehavior = options?.DeduplicationBehavior ?? DeduplicationBehavior.Disabled;
            _executionManager = new Lazy<IExecutionManager>(() => new HangfireExecutionManager(jobClient, jobStorage));
        }

        /// <summary>
        /// Enqueues an event for immediate processing by its registered handler.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event payload.</param>
        /// <returns>A <see cref="PublishResult"/> containing the job ID and whether the job was enqueued.</returns>
        public PublishResult Publish<TEvent>(TEvent @event)
        {
            var dedup = CheckDeduplication(@event);
            if (dedup != null) return dedup;

            var customName = GetCustomName(@event);
            var jobId = _jobClient.Enqueue<HangfireJobDispatcher>(x => x.DispatchEventAsync(@event, customName, null, default));
            SetCustomId(@event, jobId);
            return new PublishResult(jobId, true);
        }

        /// <summary>
        /// Schedules an event for processing after the specified delay.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event payload.</param>
        /// <param name="delay">The delay before the job is enqueued.</param>
        /// <returns>A <see cref="PublishResult"/> containing the job ID and whether the job was enqueued.</returns>
        public PublishResult Schedule<TEvent>(TEvent @event, TimeSpan delay)
        {
            var dedup = CheckDeduplication(@event);
            if (dedup != null) return dedup;

            var customName = GetCustomName(@event);
            var jobId = _jobClient.Schedule<HangfireJobDispatcher>(x => x.DispatchEventAsync(@event, customName, null, default), delay);
            SetCustomId(@event, jobId);
            return new PublishResult(jobId, true);
        }

        /// <summary>
        /// Schedules an event for processing at the specified date and time.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="event">The event payload.</param>
        /// <param name="enqueueAt">The date and time when the job should be enqueued.</param>
        /// <returns>A <see cref="PublishResult"/> containing the job ID and whether the job was enqueued.</returns>
        public PublishResult Schedule<TEvent>(TEvent @event, DateTimeOffset enqueueAt)
        {
            var dedup = CheckDeduplication(@event);
            if (dedup != null) return dedup;

            var customName = GetCustomName(@event);
            var jobId = _jobClient.Schedule<HangfireJobDispatcher>(x => x.DispatchEventAsync(@event, customName, null, default), enqueueAt);
            SetCustomId(@event, jobId);
            return new PublishResult(jobId, true);
        }

        /// <summary>
        /// Triggers immediate execution of a registered recurring job by its handler type.
        /// </summary>
        /// <param name="handlerType">The recurring handler type to trigger.</param>
        public void Trigger(Type handlerType)
        {
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));

            if (!_registry.RecurringHandlers.ContainsKey(handlerType))
                throw new InvalidOperationException(
                    $"No recurring handler registered for type '{handlerType.FullName}'.");

            var jobId = _jobIdGenerator.GenerateId(handlerType);
            _recurringJobManager.Trigger(jobId);
        }

        /// <summary>
        /// Triggers immediate execution of a recurring job by its job ID.
        /// </summary>
        /// <param name="jobId">The recurring job identifier.</param>
        public void Trigger(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) throw new ArgumentNullException(nameof(jobId));

            _recurringJobManager.Trigger(jobId);
        }

        private PublishResult CheckDeduplication<TEvent>(TEvent @event)
        {
            if (_deduplicationBehavior == DeduplicationBehavior.Disabled)
                return null;

            if (!(@event is ICustomIdEvent customIdEvent))
                return null;

            var customId = customIdEvent.CustomId;
            var manager = _executionManager.Value;
            var exists = manager.IsRunning(customId) || manager.IsPending(customId);

            if (!exists)
                return null;

            if (_deduplicationBehavior == DeduplicationBehavior.SkipIfExists)
                return new PublishResult(null, false);

            // ReplaceExisting: cancel and let the caller proceed with enqueue
            manager.Cancel(customId);
            return null;
        }

        private static string GetCustomName<TEvent>(TEvent @event)
        {
            return @event is ICustomNameEvent customNameEvent ? customNameEvent.CustomName : null;
        }

        private void SetCustomId<TEvent>(TEvent @event, string jobId)
        {
            if (@event is ICustomIdEvent customIdEvent)
            {
                using (var connection = _jobStorage.GetConnection())
                {
                    connection.SetJobParameter(jobId, ContextConsts.CustomId, customIdEvent.CustomId);
                }
            }
        }
    }
}
