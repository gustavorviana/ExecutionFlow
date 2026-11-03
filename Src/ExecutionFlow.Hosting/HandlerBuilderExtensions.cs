using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Hosting
{
    /// <summary>
    /// Local host settings for a handler, in the execution plan or <see cref="ExecutionFlowOptions.GetPlanOf{THandler}"/>.
    /// They win over the global defaults of <see cref="HostingOptions"/>. Other providers ignore them.
    /// </summary>
    public static class HandlerBuilderExtensions
    {
        internal const string IntervalFromEndKey = "Hosting:IntervalFromEnd";
        internal const string SkipWhenOverlappingKey = "Hosting:SkipWhenOverlapping";

        /// <summary>
        /// Counts the handler's interval from the end of its previous run (<c>true</c>) or on a fixed cadence from the host
        /// start (<c>false</c>). Wins over <see cref="HostingOptions.IntervalFromEnd"/>.
        /// </summary>
        /// <typeparam name="THandler">The recurring handler.</typeparam>
        /// <param name="handler">The handler's builder.</param>
        /// <param name="fromEnd">Whether to count from the end of the previous run.</param>
        /// <returns>The builder, for chaining.</returns>
        public static HandlerBuilder<THandler> IntervalFromEnd<THandler>(this HandlerBuilder<THandler> handler, bool fromEnd = true)
            where THandler : class, IHandler
        {
            return handler.HasSetting(IntervalFromEndKey, fromEnd);
        }

        /// <summary>
        /// When the handler's runs may not overlap, skips an occurrence that arrives during a run (<c>true</c>) instead of
        /// waiting for it up to the overlap timeout (<c>false</c>). Wins over <see cref="HostingOptions.SkipWhenOverlapping"/>.
        /// </summary>
        /// <typeparam name="THandler">The recurring handler.</typeparam>
        /// <param name="handler">The handler's builder.</param>
        /// <param name="skip">Whether to skip the occurrence.</param>
        /// <returns>The builder, for chaining.</returns>
        public static HandlerBuilder<THandler> SkipWhenOverlapping<THandler>(this HandlerBuilder<THandler> handler, bool skip = true)
            where THandler : class, IHandler
        {
            return handler.HasSetting(SkipWhenOverlappingKey, skip);
        }
    }
}
