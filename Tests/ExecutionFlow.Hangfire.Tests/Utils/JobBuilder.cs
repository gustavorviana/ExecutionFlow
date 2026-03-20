using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Common;

namespace ExecutionFlow.Hangfire.Tests.Utils
{
    public static class JobBuilder
    {
        public static Job CreateEventJob<TEvent>(TEvent @event)
        {
            var method = typeof(HangfireJobDispatcher)
                .GetMethod(nameof(HangfireJobDispatcher.DispatchEventAsync))!
                .MakeGenericMethod(typeof(TEvent));

            return new Job(
                typeof(HangfireJobDispatcher),
                method,
                new object?[] { @event, null!, null!, CancellationToken.None });
        }


        public static Job CreateRecurringJob(Type? handlerType)
        {
            var method = typeof(HangfireJobDispatcher)
                .GetMethod(nameof(HangfireJobDispatcher.DispatchRecurringAsync))!;

            return new Job(
                typeof(HangfireJobDispatcher),
                method,
                new object?[] { null!, handlerType!, CancellationToken.None });
        }
    }
}
