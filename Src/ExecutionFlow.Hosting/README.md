# ExecutionFlow.Hosting

Runs ExecutionFlow recurring handlers (`IHandler`) inside the .NET Generic Host, without Hangfire and without a storage: a Windows service, a console app, or a one-off run.

```csharp
builder.Services.AddExecutionFlowHosting(options =>
{
    options.Scan(typeof(MyHandler).Assembly);   // [Recurring("cron")] or [RecurringInterval("hh:mm:ss")]
    options.UsePlan(plan);                       // optional: dependencies, same meaning as in Hangfire
    options.MaxConcurrency = 4;                  // default: Environment.ProcessorCount
});
```

## Local and isolated

The local host runs **locally and isolated**. It coordinates nothing outside itself:

- Two apps or processes using it never communicate (unlike Hangfire servers that share a storage). Each runs its own schedules.
- Two registrations in the same process (separate service collections) don't coordinate either: each has its own plan cycles, overlap guards, concurrency limit and schedulers. A handler registered in both runs in both.
- Plan cycles live in memory and start from zero on every start: after a restart, a dependent waits for a new cycle of its prerequisites.

## Schedules

- A cron is evaluated in the handler's time zone: the plan's or `GetPlanOf<T>().TimeZone(...)`, then `[Recurring(TimeZone = ...)]`, then `HostingOptions.RecurringTimeZone`, then UTC. A cron with 6 fields includes seconds.
- An interval counts on a fixed cadence **from the host start**, unlike Hangfire's clock-aligned exact crons. `HostingOptions.IntervalFromEnd` (or `handler.IntervalFromEnd()` in the plan or `GetPlanOf<T>()`) counts from the end of the previous run instead.
- The first run is at the first occurrence after the host starts, never at startup.

## Overlap

Runs of the same handler may overlap, as in Hangfire. `[NoOverlap(seconds)]` or `NoOverlap(...)` in the plan turns it off: a new run waits for the running one up to the timeout, then fails with an error log. `HostingOptions.SkipWhenOverlapping` (or `handler.SkipWhenOverlapping()`) skips the occurrence instead. Dependents in the plan never overlap themselves.

## Execution plan

The plan's run modes behave as in Hangfire: a dependent is triggered when its last prerequisite completes a new cycle; with `MinInterval` an early trigger is postponed; with `OwnSchedule` it keeps its schedule and skips occurrences without a new cycle of every prerequisite. A failing run never counts as a cycle, and never stops the other handlers or the host.

## Run once

`HostingOptions.RunOnce = true` runs every enabled handler once, in plan order (handlers outside the plan after it, in registration order), then stops the application. The exit code is 1 when any handler failed. `IExecutionPlanRunner.RunOnceAsync` does the same on demand.

## Enabled handlers

Per-handler configuration goes through `GetPlanOf<T>()` or the execution plan. `HostingOptions.GlobalRecurringAutoRun` (default `true`) is the global default. At startup, the host logs one line per handler saying whether it was scheduled and, if not, why.

Event handlers (`IHandler<TEvent>`) found by `Scan` are ignored by this provider.
