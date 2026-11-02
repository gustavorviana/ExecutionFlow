using ExecutionFlow.Abstractions;
using Hangfire;

namespace ExecutionFlow.Hangfire
{
    /// <summary>
    /// Hangfire settings for a handler in the execution plan. They win over the same Hangfire attributes on the handler class
    /// (<c>[Queue]</c>, <c>[AutomaticRetry]</c>) and over <see cref="HangfireOptions.DisableRecurringRetries"/>.
    /// </summary>
    public static class HandlerBuilderExtensions
    {
        internal const string QueueKey = "Hangfire:Queue";
        internal const string AutomaticRetryKey = "Hangfire:AutomaticRetry";

        /// <summary>Runs the handler's jobs on <paramref name="queue"/>, like <c>[Queue]</c> on the class.</summary>
        /// <typeparam name="THandler">The recurring handler.</typeparam>
        /// <param name="handler">The handler's builder.</param>
        /// <param name="queue">The queue name.</param>
        /// <returns>The builder, for chaining.</returns>
        public static HandlerBuilder<THandler> Queue<THandler>(this HandlerBuilder<THandler> handler, string queue) where THandler : class, IHandler
        {
            return handler.HasSetting(QueueKey, new QueueAttribute(queue));
        }

        /// <summary>Retries a failed job of the handler, like <c>[AutomaticRetry]</c> on the class.</summary>
        /// <typeparam name="THandler">The recurring handler.</typeparam>
        /// <param name="handler">The handler's builder.</param>
        /// <param name="attempts">How many times a failed job is retried; 0 disables retries.</param>
        /// <param name="delaysInSeconds">The delay before each retry, in seconds; empty uses Hangfire's default delays.</param>
        /// <returns>The builder, for chaining.</returns>
        public static HandlerBuilder<THandler> AutomaticRetry<THandler>(this HandlerBuilder<THandler> handler, int attempts, params int[] delaysInSeconds)
            where THandler : class, IHandler
        {
            var retry = new AutomaticRetryAttribute { Attempts = attempts };
            if (delaysInSeconds != null && delaysInSeconds.Length > 0)
                retry.DelaysInSeconds = delaysInSeconds;

            return handler.HasSetting(AutomaticRetryKey, retry);
        }
    }
}
