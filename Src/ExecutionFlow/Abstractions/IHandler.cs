using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// Defines an event handler that processes a specific event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type to handle.</typeparam>
    public interface IHandler<TEvent>
    {
        /// <summary>
        /// Handles the event asynchronously.
        /// </summary>
        /// <param name="context">The execution context containing the event, logger, and parameters.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        Task HandleAsync(FlowContext<TEvent> context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Defines a recurring handler with no event payload. Typically used with the <see cref="Attributes.RecurringAttribute"/>.
    /// </summary>
    public interface IHandler
    {
        /// <summary>
        /// Handles the recurring job asynchronously.
        /// </summary>
        /// <param name="context">The execution context containing the logger and parameters.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        Task HandleAsync(FlowContext context, CancellationToken cancellationToken);
    }
}
