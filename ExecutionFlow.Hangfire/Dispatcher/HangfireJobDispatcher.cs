using System;
using System.Threading;
using System.Threading.Tasks;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Server;

namespace ExecutionFlow.Hangfire.Dispatcher
{
    public class HangfireJobDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public HangfireJobDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task DispatchRecurringAsync(string displayName, PerformContext performContext, string handlerTypeName, CancellationToken ct)
        {
            var handlerType = Type.GetType(handlerTypeName);
            if (handlerType == null)
                throw new InvalidOperationException($"Could not resolve handler type '{handlerTypeName}'.");

            var handler = (IHandler)_serviceProvider.GetService(handlerType);
            if (handler == null)
                throw new InvalidOperationException($"Could not resolve handler instance for type '{handlerTypeName}' from the service provider.");

            var logger = new HangfireExecutionLogger(performContext);
            var context = new Abstractions.ExecutionContext(logger);
            context.Items["PerformContext"] = performContext;

            await handler.HandleAsync(context, ct);
        }

        public async Task DispatchEventAsync<TEvent>(TEvent @event, PerformContext performContext, string handlerTypeName, CancellationToken ct)
        {
            var handlerType = Type.GetType(handlerTypeName);
            if (handlerType == null)
                throw new InvalidOperationException($"Could not resolve handler type '{handlerTypeName}'.");

            var handler = (IHandler<TEvent>)_serviceProvider.GetService(handlerType);
            if (handler == null)
                throw new InvalidOperationException($"Could not resolve handler instance for type '{handlerTypeName}' from the service provider.");

            var logger = new HangfireExecutionLogger(performContext);
            var context = new ExecutionContext<TEvent>(logger, @event);
            context.Items["PerformContext"] = performContext;

            await handler.HandleAsync(context, ct);

            if (!string.IsNullOrEmpty(context.CustomId))
            {
                performContext.Connection.SetJobParameter(performContext.BackgroundJob.Id, "customId", context.CustomId);
            }
        }
    }
}
