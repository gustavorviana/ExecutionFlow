using System;
using ExecutionFlow.Abstractions;
using Hangfire.Console;
using Hangfire.Server;

namespace ExecutionFlow.Hangfire.Console
{
    public static class ExecutionContextExtensions
    {
        public static ExecutionProgressBar CreateProgressBar(this ExecutionContext context)
        {
            var performContext = GetPerformContext(context);
            var progressBar = performContext.WriteProgressBar();
            return new ExecutionProgressBar(progressBar);
        }

        public static ExecutionProgressBar CreateProgressBar(this ExecutionContext context, string title)
        {
            var performContext = GetPerformContext(context);
            var progressBar = performContext.WriteProgressBar(title);
            return new ExecutionProgressBar(progressBar);
        }

        private static PerformContext GetPerformContext(ExecutionContext context)
        {
            if (context.Items.TryGetValue("PerformContext", out var value) && value is PerformContext performContext)
                return performContext;

            throw new InvalidOperationException(
                "PerformContext is not available. Progress bars can only be created within a Hangfire job execution context.");
        }
    }
}
