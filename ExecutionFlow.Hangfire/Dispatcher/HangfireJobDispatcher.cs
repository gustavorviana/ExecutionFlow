using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire;
using Hangfire.Server;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Hangfire.Dispatcher
{
    internal class HangfireJobDispatcher
    {
        private readonly JobActivator _activator;
        private readonly IExecutionFlowRegistry _executionRegistry;

        public HangfireJobDispatcher(JobActivator activator, IExecutionFlowRegistry executionRegistry)
        {
            _activator = activator ?? throw new ArgumentNullException(nameof(activator));
            _executionRegistry = executionRegistry ?? throw new ArgumentNullException(nameof(executionRegistry));
        }

        public async Task DispatchRecurringAsync(PerformContext performContext, Type handlerType, CancellationToken ct)
        {
            var handler = (IHandler)_activator.ActivateJob(handlerType);
            if (handler == null)
                throw new InvalidOperationException($"Could not activate handler instance for type '{handlerType}'.");

            var logger = new HangfireExecutionLogger(performContext);
            var context = new FlowContext(logger);
            context.Items["PerformContext"] = performContext;

            await handler.HandleAsync(context, ct);
        }

        public async Task DispatchEventAsync<TEvent>(TEvent @event, PerformContext performContext, CancellationToken ct)
        {
            var eventType = typeof(TEvent);
            var handlerType = _executionRegistry.Registrations.FirstOrDefault(x => x.EventType == eventType)?.HandlerType;
            if (handlerType == null)
                throw new InvalidOperationException($"Could not resolve handler type '{handlerType}'.");

            var handler = (IHandler<TEvent>)_activator.ActivateJob(handlerType);
            if (handler == null)
                throw new InvalidOperationException($"Could not activate handler instance for type '{handlerType}'.");

            var logger = new HangfireExecutionLogger(performContext);
            using (var context = CreateEvent(@event, performContext, logger))
            {
                context.Items["PerformContext"] = performContext;
                await handler.HandleAsync(context, ct);
            }
        }

        private FlowContext<TEvent> CreateEvent<TEvent>(TEvent @event, PerformContext performContext, HangfireExecutionLogger logger)
        {
            var context = new FlowContext<TEvent>(logger, @event, customId =>
            {
                performContext.Connection.SetJobParameter(performContext.BackgroundJob.Id, HangfireDispatcher.EventId, customId);
            });

            if (@event is ICustomIdEvent customIdEvent)
                context.SetCustomId(customIdEvent.GetCustomId());

            return context;
        }
    }
}