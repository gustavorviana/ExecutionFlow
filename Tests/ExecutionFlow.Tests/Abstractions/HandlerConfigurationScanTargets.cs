using ExecutionFlow.Abstractions;

// Configuration classes for HandlerConfigurationTests' assembly scans, each set in its own namespace so a scan can be
// limited to it with ApplyConfigurationsFromAssembly(assembly, predicate).

namespace ExecutionFlow.Tests.Abstractions.ScanTarget
{
    public abstract class ScanHandlerBase : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class ScanAHandler : ScanHandlerBase { }
    public class ScanBHandler : ScanHandlerBase { }
    public class ScanCHandler : ScanHandlerBase { }

    public class ScanAConfiguration : IHandlerConfiguration<ScanAHandler>
    {
        public void Configure(HandlerBuilder<ScanAHandler> handler) => handler.Id("scan-a");
    }

    public class ScanBConfiguration : IHandlerConfiguration<ScanBHandler>
    {
        public void Configure(HandlerBuilder<ScanBHandler> handler) => handler.Id("scan-b");
    }

    public class ScanCConfiguration : IHandlerConfiguration<ScanCHandler>
    {
        public void Configure(HandlerBuilder<ScanCHandler> handler) => handler.Id("scan-c");
    }

    /// <summary>Abstract: the scan skips it.</summary>
    public abstract class AbstractConfiguration : IHandlerConfiguration<ScanAHandler>
    {
        public void Configure(HandlerBuilder<ScanAHandler> handler) => handler.Id("never");
    }
}

namespace ExecutionFlow.Tests.Abstractions.NoDefaultConstructorTarget
{
    public class Marker { }

    public class NoDefaultConstructorHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class NoDefaultConstructorConfiguration : IHandlerConfiguration<NoDefaultConstructorHandler>
    {
        public NoDefaultConstructorConfiguration(string id) { }

        public void Configure(HandlerBuilder<NoDefaultConstructorHandler> handler) { }
    }
}

namespace ExecutionFlow.Tests.Abstractions.PolicyTarget
{
    public class MissingPrerequisiteHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class PolicyDependentHandler : IHandler
    {
        public Task HandleAsync(FlowContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Depends on a handler that no configuration in this namespace configures.</summary>
    public class PolicyDependentConfiguration : IHandlerConfiguration<PolicyDependentHandler>
    {
        public void Configure(HandlerBuilder<PolicyDependentHandler> handler) => handler.DependsOn<MissingPrerequisiteHandler>();
    }
}
