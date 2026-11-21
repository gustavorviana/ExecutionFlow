using ExecutionFlow.Abstractions;

namespace ExecutionFlow.Examples.Handlers.Recurring.Plan;

/// <summary>
/// Depends on the disabled legacy import. With MissingPrerequisite.SkipDependents it's disabled in cascade instead of
/// failing the startup.
/// </summary>
public class LegacyCleanupHandler : IHandler
{
    public Task HandleAsync(FlowContext context, CancellationToken cancellationToken)
    {
        context.Log.Warning("Legacy cleanup ran (only through a manual trigger).");
        return Task.CompletedTask;
    }
}
