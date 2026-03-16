using System.Threading;
using System.Threading.Tasks;

namespace ExecutionFlow.Abstractions
{
    public interface IHandler<TEvent>
    {
        Task HandleAsync(ExecutionContext<TEvent> context, CancellationToken cancellationToken);
    }

    public interface IHandler
    {
        Task HandleAsync(ExecutionContext context, CancellationToken cancellationToken);
    }
}
