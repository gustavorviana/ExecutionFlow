using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Abstractions
{
    public interface IHandler<TEvent>
    {
        Task HandleAsync(FlowContext<TEvent> context, CancellationToken cancellationToken);
    }

    public interface IHandler
    {
        Task HandleAsync(FlowContext context, CancellationToken cancellationToken);
    }
}
