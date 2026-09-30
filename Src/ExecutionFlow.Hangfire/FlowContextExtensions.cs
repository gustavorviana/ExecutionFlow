using ExecutionFlow.Abstractions;
using Hangfire.Server;
using System;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Hangfire-specific access to a <see cref="FlowContext"/>. Prefer the processor-neutral
    /// <see cref="FlowContext.JobId"/> and <see cref="FlowContext.AttemptNumber"/> when they're enough.
    /// </summary>
    public static class FlowContextExtensions
    {
        /// <summary>
        /// Gets Hangfire's <see cref="PerformContext"/> for the running job, or <c>null</c> when the context
        /// wasn't created by the Hangfire processor.
        /// </summary>
        /// <param name="context">The flow context.</param>
        public static PerformContext GetPerformContext(this FlowContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            return context.Parameters != null
                && context.Parameters.TryGetValue(Infrastructure.ContextConsts.Context, out var value)
                ? value as PerformContext
                : null;
        }
    }
}
