# Public API contract: Dependencies by attribute

Constitution V: additions, plus the removal of `ExecutionPlanEntry.MinInterval()` / `RunOnOwnSchedule()` of the unreleased 1.3.0, confirmed by the maintainer (spec Clarifications, 2026-11-02).

```csharp
namespace ExecutionFlow.Abstractions
{
    public enum DependentRunMode
    {
        MinInterval,  // triggered by prerequisites, never less than the interval after the previous run ended
        OwnSchedule   // runs on its own schedule, each occurrence gated by new prerequisite cycles
    }

    public sealed class ExecutionPlanEntry
    {
        public ExecutionPlanEntry RunMode(DependentRunMode mode, TimeSpan? interval = null); // NEW
        // REMOVED: MinInterval(TimeSpan), RunOnOwnSchedule()
        // kept (read-only): ConfiguredMinInterval, RunsOnOwnSchedule
    }
}

namespace ExecutionFlow.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DependentRunAttribute : Attribute
    {
        public DependentRunAttribute(DependentRunMode mode, string interval = null); // "[d.]hh:mm:ss", positive
        public DependentRunMode Mode { get; }
        public string Interval { get; }
    }
}

namespace ExecutionFlow
{
    public abstract class ExecutionFlowOptions
    {
        public MissingPrerequisite AutomaticPlanMissingPrerequisite { get; set; } // default Throw; automatic plan only

        protected internal virtual bool IsRecurringHandlerEnabled(Type handlerType); // default true
        protected virtual void OnAutomaticPlanFormed(ExecutionPlan plan);            // default no-op
    }
}
```

| Situation | Result |
|---|---|
| No `UsePlan`, no `[DependsOn]`/`[DependentRun]` registered | no plan (as before) |
| No `UsePlan`, some `[DependsOn]` or `[DependentRun]` registered | automatic plan in `Options.Plan` |
| `UsePlan` | explicit plan; `[DependsOn]`/`[DependentRun]` outside it ignored |
| Entry `RunMode(...)` and `[DependentRun]` | entry wins as a whole |
| `MinInterval` without interval, or `OwnSchedule` with one | error naming the handler |
