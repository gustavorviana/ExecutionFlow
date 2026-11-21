# Public API contract: Intervals of 1 second in the local host

All changes are additive or relax an existing check (compatible, spec Clarifications 2026-11-20).

## ExecutionFlow (core)

```csharp
public class HandlerBuilder
{
    /// <param name="interval">The interval, at least one second. Fractions of a second are kept.</param>
    /// <exception cref="ArgumentOutOfRangeException">The interval is below one second.</exception>
    public HandlerBuilder Interval(TimeSpan interval);   // was: at least one minute
}

public class RecurringIntervalAttribute : Attribute
{
    /// <param name="interval">[d.]hh:mm:ss, at least one second.</param>
    public RecurringIntervalAttribute(string interval);  // was: at least one minute
}
```

`[Recurring]` / `HandlerBuilder.Cron`: XML docs state that a 6-field cron (with seconds) is supported. No signature change.

## ExecutionFlow.Hangfire

```csharp
public class HangfireOptions : ExecutionFlowOptions
{
    /// <summary>
    /// The scheduler polling interval of the app's Hangfire servers (BackgroundJobServerOptions.SchedulePollingInterval).
    /// Keep it equal to the servers' value. Schedules below it get a warning when Hangfire is built, and intervals with a seconds
    /// part are raised to it. Default 15 seconds.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Set below one second.</exception>
    /// <exception cref="InvalidOperationException">Set after the options were locked.</exception>
    public TimeSpan SchedulePollingInterval { get; set; }   // new
}
```

## ExecutionFlow.Hosting

```csharp
public class HostingOptions : ExecutionFlowOptions
{
    /// <summary>Whether skipped occurrences are logged. Default true. A handler overrides it with
    /// HandlerBuilderExtensions.LogSkippedOccurrences.</summary>
    public bool LogSkippedOccurrences { get; set; }        // new
}

public static class HandlerBuilderExtensions
{
    /// <summary>Whether this handler's skipped occurrences are logged; wins over HostingOptions.LogSkippedOccurrences.</summary>
    public static HandlerBuilder<THandler> LogSkippedOccurrences<THandler>(
        this HandlerBuilder<THandler> handler, bool log = true) where THandler : class, IHandler;   // new
}
```

## Observable behavior (non-API)

| Where | Behavior |
|---|---|
| Hangfire `Build` | `Trace.TraceWarning` per handler whose schedule is below `SchedulePollingInterval` (FR-002, FR-005) |
| Hangfire registration | intervals with a seconds part → `*/n * * * * *` or `* * * * * *` ticker; whole minutes unchanged |
| Hosting logs | "ExecutionFlow: skipped …" lines suppressed when `LogSkippedOccurrences` resolves to `false` |
