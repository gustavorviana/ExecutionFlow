# Research: Job naming and dashboard

Decisions and brownfield findings for [spec.md](spec.md). `R-001` was `DEC-001` of the old plan; `R-002` was ADR-0006.

## R-001: `{0}` in a handler's `[JobDisplayName]` is the event (was DEC-001)

- **Decision**: `{0}` = the event. Other placeholders, or recurring jobs, → the text as is.
- **Rationale**: Hangfire fills placeholders with the job method's arguments. For ExecutionFlow those are the dispatcher's internal arguments, which mean nothing to the reader. This was the proposal left open in the spec, and it was applied when the maintainer asked to implement without choosing.
- **Alternatives considered**: Hangfire's own behavior (the dispatcher's arguments); rejected because those arguments are internal.

## R-002: Job display-name fallback chain (was ADR-0006, accepted retroactively 2026-09-30)

**Context**: Every ExecutionFlow job runs through the same internal dispatcher method, so by default the Hangfire dashboard shows the same meaningless method name for all of them. Names have to be resolved from the job's arguments. They also have to work on hosts that don't have the handler registry, such as producer-only hosts or a dashboard-only host.

This decision was first documented on the Wiki (PR #1, `5b337b2`). The Wiki was removed in `8428818`, and the ADR replaced it.

**Decision**: `DefaultHangfireJobName.GetName(job)` (`Src/ExecutionFlow.Hangfire/DefaultHangfireJobName.cs:29-36`, line numbers as of the ADR) resolves a name in this order:

1. **Custom name for this job.** For event jobs, a non-empty custom name that the event provides through `ICustomNameEvent` and that is stored in the job arguments at publish time (`Infrastructure/HangfireDispatcher.cs:146`, read back in `Infrastructure/HangfireJobInfo.cs:25-30, 47-53`).
2. **Registered display name** from the handler registry, when the handler is registered on this host (`HangfireJobInfo.GetExpectedName` base implementation).
3. **Type display name.** The event type for event jobs, or the handler type for recurring jobs (`HangfireJobInfo.cs:52, 95`).
4. **ID generator.** Since 1.2.0 this is the last fallback for every job (ExecutionFlow or not) when no name is defined anywhere: `CustomName`, `[DisplayName]`, or Hangfire's `[JobDisplayName]` (on the handler's `HandleAsync`, or on a native job's method). The full chain is in [spec.md](spec.md) FR-001, which supersedes this step.

The dashboard uses this through `DashboardOptions.UseExecutionFlowJobNames(IHangfireJobName | IServiceProvider)` (`DashboardOptionsExtensions.cs`). The `IServiceProvider` overload resolves the name generator lazily, on the first render, and throws `InvalidOperationException` if no `IHangfireJobName` is registered. (Superseded in 1.2.0: a missing `IHangfireJobName` now falls back to default names, FR-004 / AC-004.2.)

**Consequences**:
- Names are always non-null for ExecutionFlow jobs, even on hosts without handlers.
- The same job can show different names on different hosts. A host with the handler registered shows its registered title, while a producer-only host shows the event type name.
- `IHangfireJobName` can be replaced (`HangfireOptions.JobNameType`) to use a different strategy.

**Related**: producer-only mode ([002-setup-and-configuration research](../002-setup-and-configuration/research.md), was ADR-0005); this feature.

## Findings (brownfield, was findings.md)

- **F-001** inconsistency: `ICustomNameEvent` lives in `ExecutionFlow.Hangfire`, while `ICustomIdEvent` lives in the core. Event classes reference only the core (old P-003, now [constitution](../../.specify/memory/constitution.md) principle I), so naming a job forces the events project to reference the Hangfire package.
  - Related: FR-002
  - Resolution: fixed (FR-002). The example events project no longer references the Hangfire package

- **F-002** bug: `UseExecutionFlowJobNames` replaces `DashboardOptions.DisplayNameFunc` for **every** job, so non-ExecutionFlow jobs lose their Hangfire names. Non-generic native jobs show `Namespace.Class`, generic ones are named after their type argument, and `[JobDisplayName]` is ignored.
  - Related: AC-003.3 [as written in the old findings; no AC-003.3 exists, the covering criterion is AC-003.1]
  - Resolution: fixed (FR-003)

- **F-003** root cause of F-002: `HangfireJobInfo.Create` classifies jobs by method shape only (generic → event, otherwise recurring), without checking that the method is ExecutionFlow's dispatcher. `GetJobs` had the same issue, fixed in [007-execution-manager](../007-execution-manager/spec.md).
  - Related: RN-001
  - Resolution: fixed (RN-001)

- **F-004** fragile: with the `IServiceProvider` overload, a missing `IHangfireJobName` throws on every job render, breaking the dashboard page instead of degrading to default names.
  - Related: AC-003.2 [as written in the old findings; no AC-003.2 exists, the covering criterion is AC-004.2]
  - Resolution: fixed (AC-004.2)

- **F-005** misuse: `IJobIdGenerator`, which generates recurring job IDs, is also the last naming fallback (`DefaultHangfireJobName.GetName`). A custom generator changes the display name of unrelated jobs.
  - Related: RN-002
  - Resolution: revised by the maintainer (2026-09-30): `IJobIdGenerator` stays, but only as the **last** fallback, when no name is defined (`CustomName`, `[DisplayName]`, `[JobDisplayName]`). Native jobs with `[JobDisplayName]` no longer reach it

- **F-006** design correction (found while implementing): Hangfire's `JobDisplayNameAttribute` is `AttributeUsage(Method)`, so to-be item 4 ("`[JobDisplayName]` on the handler") can't be placed on the class. It's read from the handler's `HandleAsync`, which is also where Hangfire users would put it.
  - Related: RN-002, AC-001.3
  - Resolution: applied (plan updated)
