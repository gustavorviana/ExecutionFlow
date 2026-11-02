# Public API contract: Handler configuration classes

Constitution V: removes `ExecutionPlanEntry` and replaces the `SetJobAutoRun` + plan error with "the user plan replaces the options layer" (unreleased 1.3.0, confirmed in spec Clarifications, 2026-11-02). `SetJobAutoRun` (v1.0.2) and `SetJobTimeZone` become `[Obsolete]`, not removed.

```csharp
namespace ExecutionFlow.Abstractions
{
    public interface IHandlerConfiguration<THandler> where THandler : class, IHandler
    {
        void Configure(HandlerBuilder<THandler> handler);
    }

    public abstract class HandlerBuilder             // returned by Add(Type); internal constructor
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

```csharp
namespace ExecutionFlow
{
    public abstract class ExecutionFlowOptions
    {
        public void UsePlanFromAssembly(Assembly assembly, Func<Type, bool> predicate = null,
            MissingPrerequisite policy = MissingPrerequisite.Throw);                          // FR-005
        public HandlerBuilder<THandler> GetPlanOf<THandler>() where THandler : class, IHandler; // FR-006
        public HandlerBuilder GetPlanOf(Type handlerType);                                     // FR-006, R-008

        // Provider hooks (R-009), replacing IsRecurringHandlerEnabled(Type) added in 013 (unreleased):
        protected virtual bool IsRecurringHandlerEnabledByDefault();                            // default true
        protected virtual string DefaultRecurringTimeZone { get; }                              // default null (UTC)
    }
}

namespace ExecutionFlow.Hangfire
{
    public class HangfireOptions
    {
        [Obsolete("Use GetPlanOf<T>().Enabled(...)")]   public void SetJobAutoRun<T>(bool autoRun);
        [Obsolete("Use GetPlanOf(type).Enabled(...)")]  public void SetJobAutoRun(Type handlerType, bool autoRun);
        [Obsolete("Use GetPlanOf<T>().TimeZone(...)")]  public void SetJobTimeZone<T>(string timeZoneId);
        [Obsolete("Use GetPlanOf(type).TimeZone(...)")] public void SetJobTimeZone(Type handlerType, string timeZoneId);
    }

    public static class HandlerBuilderExtensions   // Queue(...), AutomaticRetry(...) as above
}
```
