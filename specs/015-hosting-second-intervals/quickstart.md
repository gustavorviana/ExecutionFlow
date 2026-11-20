# Quickstart: validating intervals of 1 second

## Prerequisites

.NET 10 SDK; from the repo root:

```
dotnet restore
dotnet build --no-restore
```

## 1. Automated validation

```
dotnet test --no-build
```

Expected: all tests green, including the ACs of [spec.md](spec.md). Focused runs:

```
dotnet test --no-build --filter "FullyQualifiedName~IntervalScheduleTests|FullyQualifiedName~HandlerBuilderTests"
dotnet test --no-build --filter "FullyQualifiedName~RecurringHandlerHostTests|FullyQualifiedName~RunCoordinatorTests|FullyQualifiedName~HostingOptionsTests"
dotnet test --no-build --filter "FullyQualifiedName~IntervalRegistrationTests|FullyQualifiedName~IntervalTickerTests|FullyQualifiedName~RecurringRegistrationTests|FullyQualifiedName~CronGapTests"
```

## 2. Local host, by hand

In an example app with `ExecutionFlow.Hosting`:

```csharp
[RecurringInterval("00:00:01")]
public class HeartbeatHandler : IHandler { /* logs a line */ }

builder.Services.AddExecutionFlowHosting(o =>
{
    o.Scan(typeof(HeartbeatHandler).Assembly);
    o.LogSkippedOccurrences = false;
});
```

Expected: the startup report says "every 00:00:01"; one log line per second from the handler; no warning.

## 3. Hangfire, by hand

```csharp
[RecurringInterval("00:00:05")]
public class FastHandler : IHandler { }
```

Expected at startup: one trace warning naming `FastHandler`, `00:00:05` and the 15 s minimum; in the dashboard the recurring job has the cron `*/15 * * * * *`. With `o.SchedulePollingInterval = TimeSpan.FromSeconds(5)` (and the server's `SchedulePollingInterval` set to 5 s): no warning, cron `*/5 * * * * *`.

See [contracts/public-api.md](contracts/public-api.md) and [data-model.md](data-model.md) for the full rules.
