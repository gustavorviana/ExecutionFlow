using System;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Console;
using Hangfire.Server;

namespace ExecutionFlow.Hangfire.Console
{
    public static class ExecutionContextExtensions
    {
        public static ExecutionProgressBar CreateProgressBar(this FlowContext context)
        {
            var performContext = GetPerformContext(context);
            var progressBar = performContext.WriteProgressBar();
            return new ExecutionProgressBar(progressBar);
        }

        public static ExecutionProgressBar CreateProgressBar(this FlowContext context, string title)
        {
            var performContext = GetPerformContext(context);
            var progressBar = performContext.WriteProgressBar(title);
            return new ExecutionProgressBar(progressBar);
        }

        private static PerformContext GetPerformContext(FlowContext context)
        {
            if (context.Parameters.TryGetValue(ContextConsts.Context, out var value) && value is PerformContext performContext)
                return performContext;

            throw new InvalidOperationException(
                $"{ContextConsts.Context} is not available. Progress bars can only be created within a Hangfire job execution context.");
        }
    }
}
