# Public API contract: Handler configuration classes

Constitution V: removes `ExecutionPlanEntry` and changes the `SetJobAutoRun` + plan error to a warning, both in the unreleased 1.3.0 and confirmed by the maintainer (spec Clarifications, 2026-11-02).

```csharp
namespace ExecutionFlow.Abstractions
{
    public interface IHandlerConfiguration<THandler> where THandler : class, IHandler
    {
        void Configure(HandlerBuilder<THandler> handler);
    }

    public class HandlerBuilder                      // returned by Add(Type)
    {
        public Type HandlerType { get; }
        public HandlerBuilder HasSetting(string key, object value);   // for provider extensions
        // + the same fluent methods as below, returning HandlerBuilder
    }

    public sealed class HandlerBuilder<THandler> : HandlerBuilder where THandler : class, IHandler
    {
        public HandlerBuilder<THandler> Cron(string cron);
        public HandlerBuilder<THandler> Interval(TimeSpan interval);
        public HandlerBuilder<THandler> Id(string id);
        public HandlerBuilder<THandler> TimeZone(string timeZoneId);
        public HandlerBuilder<THandler> DisplayName(string displayName);
        public HandlerBuilder<THandler> Enabled(bool enabled = true);
        public HandlerBuilder<THandler> DependsOn<TPrerequisite>() where TPrerequisite : class, IHandler;
        public HandlerBuilder<THandler> DependsOn(Type prerequisite);
        public HandlerBuilder<THandler> RemoveDependency<TPrerequisite>() where TPrerequisite : class, IHandler;
        public HandlerBuilder<THandler> ClearDependencies();
        public HandlerBuilder<THandler> RunMode(DependentRunMode mode, TimeSpan? interval = null);
        public HandlerBuilder<THandler> NoOverlap(TimeSpan timeout);
        public HandlerBuilder<THandler> AllowOverlap();
    }

    public sealed class ExecutionPlanner
    {
        public HandlerBuilder<THandler> Add<THandler>() where THandler : class, IHandler; // was ExecutionPlanEntry
        public HandlerBuilder Add(Type handlerType);                                        // was ExecutionPlanEntry
        public ExecutionPlanner ApplyConfiguration<THandler>(IHandlerConfiguration<THandler> configuration) where THandler : class, IHandler;
        public ExecutionPlanner ApplyConfigurationsFromAssembly(Assembly assembly);
    }

    // REMOVED: ExecutionPlanEntry
}

namespace ExecutionFlow.Hangfire
{
    public static class HandlerBuilderExtensions
    {
        public static HandlerBuilder<T> Queue<T>(this HandlerBuilder<T> handler, string queue) where T : class, IHandler;
        public static HandlerBuilder<T> AutomaticRetry<T>(this HandlerBuilder<T> handler, int attempts, params int[] delaysInSeconds) where T : class, IHandler;
    }
}
```
