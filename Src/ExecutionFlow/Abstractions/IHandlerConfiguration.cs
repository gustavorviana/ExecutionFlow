namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Configures one recurring handler in its own class, like EF Core's <c>IEntityTypeConfiguration&lt;T&gt;</c>. Apply it
    /// with <see cref="ExecutionPlanner.ApplyConfiguration{THandler}"/> or
    /// <see cref="ExecutionPlanner.ApplyConfigurationsFromAssembly"/>.
    /// </summary>
    /// <typeparam name="THandler">The recurring handler.</typeparam>
    /// <example>
    /// <code>
    /// public class ProductSyncConfiguration : IHandlerConfiguration&lt;ProductSyncHandler&gt;
    /// {
    ///     public void Configure(HandlerBuilder&lt;ProductSyncHandler&gt; handler)
    ///     {
    ///         handler.Cron("*/10 * * * *").Id("product-sync").DependsOn&lt;CategorySyncHandler&gt;();
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IHandlerConfiguration<THandler> where THandler : class, IHandler
    {
        /// <summary>Configures the handler. Only what is called here changes; the rest keeps its attribute, option or default.</summary>
        /// <param name="handler">The handler's builder.</param>
        void Configure(HandlerBuilder<THandler> handler);
    }
}
