# ADR-0006: Job display-name fallback chain

Status: accepted (retroactive)
Date: 2026-09-30

## Context
Every ExecutionFlow job runs through the same internal dispatcher method, so by default the Hangfire dashboard shows the same meaningless method name for all of them. Names have to be resolved from the job's arguments. They also have to work on hosts that don't have the handler registry, such as producer-only hosts or a dashboard-only host.

This decision was first documented on the Wiki (PR #1, `5b337b2`). The Wiki was removed in `8428818`, so this ADR replaces it.

## Decision
`DefaultHangfireJobName.GetName(job)` (`Src/ExecutionFlow.Hangfire/DefaultHangfireJobName.cs:29-36`) resolves a name in this order:

1. **Custom name for this job.** For event jobs, a non-empty custom name that the event provides through `ICustomNameEvent` and that is stored in the job arguments at publish time (`Infrastructure/HangfireDispatcher.cs:146`, read back in `Infrastructure/HangfireJobInfo.cs:25-30, 47-53`).
2. **Registered display name** from the handler registry, when the handler is registered on this host (`HangfireJobInfo.GetExpectedName` base implementation).
3. **Type display name.** The event type for event jobs, or the handler type for recurring jobs (`HangfireJobInfo.cs:52, 95`).
4. **ID generator.** When the job isn't recognized as an ExecutionFlow job, the name comes from `IJobIdGenerator.GenerateId(job.Method.DeclaringType)`.

The dashboard uses this through `DashboardOptions.UseExecutionFlowJobNames(IHangfireJobName | IServiceProvider)` (`DashboardOptionsExtensions.cs`). The `IServiceProvider` overload resolves the name generator lazily, on the first render, and throws `InvalidOperationException` if no `IHangfireJobName` is registered.

## Consequences
- Names are always non-null for ExecutionFlow jobs, even on hosts without handlers.
- The same job can show different names on different hosts. A host with the handler registered shows its registered title, while a producer-only host shows the event type name.
- `IHangfireJobName` can be replaced (`HangfireOptions.JobNameType`) to use a different strategy.

## Related
- [ADR-0005](ADR-0005-producer-only-mode.md)
- Capability: `job-naming-and-dashboard`
