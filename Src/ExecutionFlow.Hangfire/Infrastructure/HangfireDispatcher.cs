using ExecutionFlow.Abstractions;
using ExecutionFlow.Attributes;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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
        private readonly HangfireOptions _options;

        public HangfireDispatcher(IBackgroundJobClient jobClient, JobStorage jobStorage, IJobIdGenerator jobIdGenerator, IExecutionFlowRegistry registry, HangfireOptions options)
        {
            _jobClient = jobClient ?? throw new ArgumentNullException(nameof(jobClient));
            _jobStorage = jobStorage ?? throw new ArgumentNullException(nameof(jobStorage));
            _jobIdGenerator = jobIdGenerator ?? throw new ArgumentNullException(nameof(jobIdGenerator));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _recurringJobManager = new RecurringJobManager(jobStorage);
            _options = options ?? new HangfireOptions();
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

            if (!_registry.RecurringHandlers.TryGetValue(handlerType, out var registration))
                throw new InvalidOperationException(
                    $"No recurring handler registered for type '{handlerType.FullName}'.");

            var jobId = RecurringJobResolver.ResolveId(registration, _jobIdGenerator);
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

            var customName = GetCustomName(@event);
            var job = Job.FromExpression<HangfireJobDispatcher>(x => x.DispatchEventAsync(@event, customName, null, default));

            if (!JobParameters.TryGetCustomId(@event, out var customId))
                return new PublishResult(CreateJob(job, state, null), true);

            var behavior = GetDeduplicationBehavior(@event);
            if (behavior == DeduplicationBehavior.Disabled)
            {
                CreateJob(job, state, customId);
                return new PublishResult(customId, true);
            }

            return DispatchDeduplicated(job, state, customId, behavior);
        }

        private PublishResult DispatchDeduplicated(Job job, IState state, string customId, DeduplicationBehavior behavior)
        {
            using (var connection = _jobStorage.GetConnection())
            {
                IDisposable distributedLock;
                try
                {
                    distributedLock = connection.AcquireDistributedLock(DeduplicationStore.GetLockResource(customId), _options.DeduplicationLockTimeout);
                }
                catch (DistributedLockTimeoutException) when (_options.CreateOnDeduplicationLockTimeout)
                {
                    Trace.TraceWarning("ExecutionFlow: deduplication lock for custom ID '{0}' timed out; creating the job without deduplication.", customId);
                    CreateJob(job, state, customId);
                    return new PublishResult(customId, true);
                }

                using (distributedLock)
                {
                    var activeJobId = DeduplicationStore.FindActiveJobId(connection, customId);
                    if (activeJobId != null)
                    {
                        if (behavior == DeduplicationBehavior.SkipIfExists)
                            return new PublishResult(null, false);

                        _jobClient.Delete(activeJobId);
                    }

                    var jobId = CreateJob(job, state, customId);
                    DeduplicationStore.Reserve(connection, customId, jobId, GetReservationExpiration(state));
                    return new PublishResult(customId, true);
                }
            }
        }

        private DeduplicationBehavior GetDeduplicationBehavior(object @event)
        {
            var attribute = (DeduplicationAttribute)Attribute.GetCustomAttribute(@event.GetType(), typeof(DeduplicationAttribute), inherit: true);
            return attribute?.Behavior ?? _options.DeduplicationBehavior;
        }

        private static TimeSpan GetReservationExpiration(IState state)
        {
            if (state is ScheduledState scheduled && scheduled.EnqueueAt > DateTime.UtcNow)
                return DeduplicationStore.BaseExpiration + (scheduled.EnqueueAt - DateTime.UtcNow);

            return DeduplicationStore.BaseExpiration;
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

        private static string GetCustomName<TEvent>(TEvent @event)
        {
            return @event is ICustomNameEvent customNameEvent ? customNameEvent.CustomName : null;
        }
    }
}
