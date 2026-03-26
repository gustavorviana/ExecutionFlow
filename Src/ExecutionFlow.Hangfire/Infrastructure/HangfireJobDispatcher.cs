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

            await handler.HandleAsync(CreateContextBuilder(performContext).Build(), ct);
        }

        public async Task DispatchEventAsync<TEvent>(TEvent @event, string eventCustomName, PerformContext performContext, CancellationToken ct)
        {
            var eventType = typeof(TEvent);
            if (!_executionRegistry.EventHandlers.TryGetValue(eventType, out var handlerInfo))
                throw new InvalidOperationException($"No handler registered for event type '{eventType.FullName}'.");

            var handler = (IHandler<TEvent>)_serviceProvider.GetService(handlerInfo.HandlerType);
            if (handler == null)
                throw new InvalidOperationException($"Could not activate handler instance for type '{handlerInfo.HandlerType}'.");

            var builder = CreateContextBuilder(performContext);
            builder.AddReadOnly(ContextConsts.EventName, eventCustomName);

            using (var context = CreateEvent(@event, performContext, builder))
                await handler.HandleAsync(context, ct);
        }

        private FlowContext<TEvent> CreateEvent<TEvent>(TEvent @event, PerformContext performContext, FlowContextBuilder contextBuilder)
        {
            var context = contextBuilder.Build(@event, customId =>
            {
                performContext.Connection.SetJobParameter(performContext.BackgroundJob.Id, ContextConsts.CustomId, customId);
            });

            if (@event is ICustomIdEvent customIdEvent)
                context.SetCustomId(customIdEvent.CustomId);

            return context;
        }

        private FlowContextBuilder CreateContextBuilder(PerformContext performContext)
        {
            var builder = new FlowContextBuilder((ExecutionLoggerFactory)_serviceProvider.GetService(typeof(ExecutionLoggerFactory)));
            builder.AddReadOnly(ContextConsts.Context, performContext);

            return builder;
        }
    }
}