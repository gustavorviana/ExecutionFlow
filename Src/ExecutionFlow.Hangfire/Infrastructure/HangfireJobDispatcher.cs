using ExecutionFlow.Abstractions;
using Hangfire;
using Hangfire.Server;
using System;
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

            await handler.HandleAsync(CreateContextBuilder(performContext).SetHandler(handlerType).Build(), ct);
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