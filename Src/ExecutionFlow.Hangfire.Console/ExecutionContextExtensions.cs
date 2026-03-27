using System;
using ExecutionFlow.Abstractions;
using ExecutionFlow.Hangfire.Infrastructure;
using Hangfire.Console;
using Hangfire.Server;

namespace ExecutionFlow.Hangfire.Console
{
    /// <summary>
    /// Extension methods for <see cref="FlowContext"/> that enable creating Hangfire Console progress bars within job execution.
    /// </summary>
    public static class ExecutionContextExtensions
    {
        /// <summary>
        /// Creates a new progress bar in the Hangfire Console for the current job execution.
        /// </summary>
        /// <param name="context">The flow execution context.</param>
        /// <returns>An <see cref="ExecutionProgressBar"/> that can be updated during execution.</returns>
        public static ExecutionProgressBar CreateProgressBar(this FlowContext context)
        {
            var performContext = GetPerformContext(context);
            var progressBar = performContext.WriteProgressBar();
            return new ExecutionProgressBar(progressBar);
        }

        /// <summary>
        /// Creates a new titled progress bar in the Hangfire Console for the current job execution.
        /// </summary>
        /// <param name="context">The flow execution context.</param>
        /// <param name="title">The title displayed next to the progress bar.</param>
        /// <returns>An <see cref="ExecutionProgressBar"/> that can be updated during execution.</returns>
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
