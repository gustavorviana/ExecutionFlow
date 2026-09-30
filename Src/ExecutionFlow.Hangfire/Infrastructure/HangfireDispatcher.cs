using ExecutionFlow.Abstractions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using System;
using System.Collections.Generic;

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
        /// <typeparam name="TEvent">The event type. The handler is resolved by this type, not by the runtime type of <paramref name="event"/>.</typeparam>
        /// <param name="event">The event payload.</param>
        /// <returns>A <see cref="PublishResult"/> containing the job ID and whether the job was enqueued.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="event"/> is <c>null</c>.</exception>
        public PublishResult Publish<TEvent>(TEvent @event)
        {
            return Dispatch(@event, new EnqueuedState());
        }

        /// <summary>
        /// Schedules an event for processing after the specified delay.
        /// </summary>
        /// <typeparam name="TEvent">The event type. The handler is resolved by this type, not by the runtime type of <paramref name="event"/>.</typeparam>
        /// <param name="event">The event payload.</param>
        /// <param name="delay">The delay before the job is enqueued.</param>
        /// <returns>A <see cref="PublishResult"/> containing the job ID and whether the job was enqueued.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="event"/> is <c>null</c>.</exception>
        public PublishResult Schedule<TEvent>(TEvent @event, TimeSpan delay)
        {
            return Dispatch(@event, new ScheduledState(delay));
        }

        /// <summary>
        /// Schedules an event for processing at the specified date and time.
        /// </summary>
        /// <typeparam name="TEvent">The event type. The handler is resolved by this type, not by the runtime type of <paramref name="event"/>.</typeparam>
        /// <param name="event">The event payload.</param>
        /// <param name="enqueueAt">The date and time when the job should be enqueued.</param>
        /// <returns>A <see cref="PublishResult"/> containing the job ID and whether the job was enqueued.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="event"/> is <c>null</c>.</exception>
        public PublishResult Schedule<TEvent>(TEvent @event, DateTimeOffset enqueueAt)
        {
            return Dispatch(@event, new ScheduledState(enqueueAt.UtcDateTime));
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

        private PublishResult Dispatch<TEvent>(TEvent @event, IState state)
        {
            if (@event == null) throw new ArgumentNullException(nameof(@event));

            var hasCustomId = JobParameters.TryGetCustomId(@event, out var customId);
            if (hasCustomId && !CheckDeduplication(customId))
                return new PublishResult(null, false);

            var customName = GetCustomName(@event);
            var job = Job.FromExpression<HangfireJobDispatcher>(x => x.DispatchEventAsync(@event, customName, null, default));
            var jobId = CreateJob(job, state, hasCustomId ? customId : null);

            return new PublishResult(hasCustomId ? customId : jobId, true);
        }

        private string CreateJob(Job job, IState state, string customId)
        {
            if (customId == null)
                return _jobClient.Create(job, state);

            // V2 persists the job and its parameters in one storage transaction.
            if (_jobClient is IBackgroundJobClientV2 clientV2)
                return clientV2.Create(job, state, new Dictionary<string, object> { [ContextConsts.CustomId] = customId });

            var jobId = _jobClient.Create(job, state);
            using (var connection = _jobStorage.GetConnection())
                JobParameters.WriteCustomId(connection, jobId, customId);

            return jobId;
        }

        /// <returns><c>true</c> if the event should be enqueued; <c>false</c> if it should be skipped.</returns>
        private bool CheckDeduplication(string customId)
        {
            if (_deduplicationBehavior == DeduplicationBehavior.Disabled)
                return true;

            var manager = _executionManager.Value;
            var exists = manager.IsRunning(customId) || manager.IsPending(customId);

            if (!exists)
                return true;

            if (_deduplicationBehavior == DeduplicationBehavior.SkipIfExists)
                return false;

            // ReplaceExisting: cancel and let the caller proceed with enqueue
            manager.Cancel(customId);
            return true;
        }

        private static string GetCustomName<TEvent>(TEvent @event)
        {
            return @event is ICustomNameEvent customNameEvent ? customNameEvent.CustomName : null;
        }
    }
}
