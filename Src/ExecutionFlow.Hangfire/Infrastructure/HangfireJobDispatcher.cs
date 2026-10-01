using ExecutionFlow.Abstractions;
using Hangfire;
using Hangfire.Server;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hangfire.Infrastructure
{
    internal class HangfireJobDispatcher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IExecutionFlowRegistry _executionRegistry;

        public HangfireJobDispatcher(IServiceProvider serviceProvider, IExecutionFlowRegistry executionRegistry)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _executionRegistry = executionRegistry ?? throw new ArgumentNullException(nameof(executionRegistry));
        }

        public async Task DispatchRecurringAsync(PerformContext performContext, Type handlerType, CancellationToken ct)
        {
            var handler = (IHandler)_serviceProvider.GetService(handlerType);
            if (handler == null)
                throw new InvalidOperationException($"Could not activate handler instance for type '{handlerType}'.");

            var setup = _executionRegistry as HangfireSetup;
            var plan = setup?.Options?.Plan;
            ExecutionPlanStep step = null;
            var inPlan = plan != null && performContext?.Connection != null && plan.TryGet(handlerType, out step);

            try
            {
                await handler.HandleAsync(CreateContextBuilder(performContext).SetHandler(handlerType).Build(), ct);
            }
            finally
            {
                // A dependent that finishes, even with an exception, is no longer active, and its minimum interval starts.
                // If its prerequisites already completed new cycles meanwhile, it's triggered again (runs never overlap).
                if (inPlan && step.Prerequisites.Count > 0)
                {
                    PrerequisiteGenerationStore.RecordFinished(performContext.Connection, setup.ResolveRecurringJobId(handlerType), setup.UtcNow());
                    TryTriggerDependent(setup, performContext, step);
                }
            }

            if (inPlan && step.Dependents.Count > 0)
                RecordCompletedCycle(setup, performContext, step);
        }

        /// <summary>
        /// A prerequisite that returned without throwing completed a cycle: increment its generation, then try to trigger each
        /// dependent. In a chain C → B → A, C triggers B and B triggers A; a dependent of several prerequisites is triggered
        /// by the last one to complete.
        /// </summary>
        private static void RecordCompletedCycle(HangfireSetup setup, PerformContext performContext, ExecutionPlanStep step)
        {
            PrerequisiteGenerationStore.Increment(performContext.Connection, setup.ResolveRecurringJobId(step.HandlerType));

            foreach (var dependentType in step.Dependents)
                TryTriggerDependent(setup, performContext, setup.Options.Plan.Get(dependentType));
        }

        /// <summary>
        /// Triggers a dependent now, or schedules a trigger for the end of its <see cref="ExecutionPlanStep.MinInterval"/>, when
        /// every prerequisite has a new cycle. Dependents that are disabled or run on their own schedule are never triggered.
        /// </summary>
        private static void TryTriggerDependent(HangfireSetup setup, PerformContext performContext, ExecutionPlanStep dependent)
        {
            if (!dependent.Enabled || dependent.RunsOnOwnSchedule)
                return;

            var connection = performContext.Connection;
            var dependentId = setup.ResolveRecurringJobId(dependent.HandlerType);
            var prerequisiteIds = dependent.Prerequisites.Select(setup.ResolveRecurringJobId).ToList();
            var now = setup.UtcNow();

            var decision = PrerequisiteGenerationStore.DecideTrigger(connection, dependentId, prerequisiteIds, dependent.MinInterval, now, out var dueAt);

            if (decision == TriggerDecision.ScheduleTrigger)
            {
                setup.ScheduleDependentTrigger(performContext.Storage, dependentId, dueAt - now);
            }
            else if (decision == TriggerDecision.TriggerNow)
            {
                try
                {
                    setup.TriggerRecurringJob(performContext.Storage, dependentId);
                }
                catch
                {
                    // Without the trigger, a marker left behind would block every future trigger of this dependent.
                    PrerequisiteGenerationStore.ClearTriggered(connection, dependentId);
                    throw;
                }
            }
        }

        /// <summary>
        /// A trigger postponed by a dependent's <see cref="ExecutionPlanStep.MinInterval"/> fired: trigger the dependent if it's
        /// still ready.
        /// </summary>
        /// <param name="performContext">Injected by Hangfire.</param>
        /// <param name="dependentId">The dependent's recurring job ID.</param>
        public void TriggerPlanDependent(PerformContext performContext, string dependentId)
        {
            var setup = _executionRegistry as HangfireSetup;
            var plan = setup?.Options?.Plan;
            if (plan == null || performContext?.Connection == null)
                return;

            PrerequisiteGenerationStore.ClearScheduled(performContext.Connection, dependentId);

            var dependent = plan.Steps.FirstOrDefault(s => setup.ResolveRecurringJobId(s.HandlerType) == dependentId);
            if (dependent != null)
                TryTriggerDependent(setup, performContext, dependent);
        }

        public async Task DispatchEventAsync<TEvent>(TEvent @event, string eventCustomName, PerformContext performContext, CancellationToken ct)
        {
            var eventType = typeof(TEvent);
            if (!_executionRegistry.EventHandlers.TryGetValue(eventType, out var handlerInfo))
                throw new InvalidOperationException($"No handler registered for event type '{eventType.FullName}'.");

            var handler = (IHandler<TEvent>)_serviceProvider.GetService(handlerInfo.HandlerType);
            if (handler == null)
                throw new InvalidOperationException($"Could not activate handler instance for type '{handlerInfo.HandlerType}'.");

            var builder = CreateContextBuilder(performContext).SetHandler(handlerInfo.HandlerType, eventType);
            builder.AddReadOnly(ContextConsts.EventName, eventCustomName);

            using (var context = CreateEvent(@event, performContext, builder))
                await handler.HandleAsync(context, ct);
        }

        private FlowContext<TEvent> CreateEvent<TEvent>(TEvent @event, PerformContext performContext, FlowContextBuilder contextBuilder)
        {
            var context = contextBuilder.Build(@event, customId =>
            {
                JobParameters.WriteCustomId(performContext.Connection, performContext.BackgroundJob.Id, customId);
            });

            if (JobParameters.TryGetCustomId(@event, out var eventCustomId))
            {
#pragma warning disable CS0618 // Internal use: exposes the publish-time custom ID on the context.
                context.SetCustomId(eventCustomId);
#pragma warning restore CS0618
            }

            return context;
        }

        private FlowContextBuilder CreateContextBuilder(PerformContext performContext)
        {
            var builder = new FlowContextBuilder((ExecutionLoggerFactory)_serviceProvider.GetService(typeof(ExecutionLoggerFactory)));
            builder.AddReadOnly(ContextConsts.Context, performContext);

            if (performContext?.BackgroundJob != null)
                builder.SetJob(performContext.BackgroundJob.Id, GetRetryCount(performContext) + 1);

            return builder;
        }

        /// <summary>Hangfire's automatic retry filter stores the retries done so far in the "RetryCount" job parameter.</summary>
        private static int GetRetryCount(PerformContext performContext)
        {
            try
            {
                var value = performContext.Connection?.GetJobParameter(performContext.BackgroundJob.Id, ContextConsts.RetryCount);
                return int.TryParse(value, out var count) && count > 0 ? count : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}