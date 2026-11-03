using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Hosting.Tests.Support
{
    /// <summary>An event handler: the local host ignores it.</summary>
    internal sealed class SomeEventHandler : IHandler<SomeEvent>
    {
        public Task HandleAsync(FlowContext<SomeEvent> context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
