using ExecutionFlow.Abstractions;
using ExecutionFlow.Examples.Configuration.Handlers;
using ExecutionFlow.Hangfire;

namespace ExecutionFlow.Examples.Configuration.Configurations;

/// <summary>Changes only the time zone; the cron, ID and NoOverlap come from the class's attributes.</summary>
public class CategorySyncConfiguration : IHandlerConfiguration<CategorySyncHandler>
{
    public void Configure(HandlerBuilder<CategorySyncHandler> handler)
    {
        handler.TimeZone("Europe/Lisbon");
    }
}
