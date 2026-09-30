# Findings: Job naming and dashboard

- F-001 inconsistency: `ICustomNameEvent` lives in `ExecutionFlow.Hangfire`, while `ICustomIdEvent` lives in the core. Event classes reference only the core (P-003), so naming a job forces the events project to reference the Hangfire package.
  - Related: REQ-002
  - Resolution: fixed (REQ-002). The example events project no longer references the Hangfire package

- F-002 bug: `UseExecutionFlowJobNames` replaces `DashboardOptions.DisplayNameFunc` for **every** job, so non-ExecutionFlow jobs lose their Hangfire names. Non-generic native jobs show `Namespace.Class`, generic ones are named after their type argument, and `[JobDisplayName]` is ignored.
  - Related: AC-003.3
  - Resolution: fixed (REQ-003)

- F-003 root cause of F-002: `HangfireJobInfo.Create` classifies jobs by method shape only (generic → event, otherwise recurring), without checking that the method is ExecutionFlow's dispatcher. `GetJobs` had the same issue, fixed in `execution-manager`.
  - Related: RN-001
  - Resolution: fixed (RN-001)

- F-004 fragile: with the `IServiceProvider` overload, a missing `IHangfireJobName` throws on every job render, breaking the dashboard page instead of degrading to default names.
  - Related: AC-003.2
  - Resolution: fixed (AC-004.2)

- F-005 misuse: `IJobIdGenerator`, which generates recurring job IDs, is also the last naming fallback (`DefaultHangfireJobName.GetName`). A custom generator changes the display name of unrelated jobs.
  - Related: RN-002
  - Resolution: revised by the maintainer (2026-09-30): `IJobIdGenerator` stays, but only as the **last** fallback, when no name is defined (`CustomName`, `[DisplayName]`, `[JobDisplayName]`). Native jobs with `[JobDisplayName]` no longer reach it

- F-006 design correction (found while implementing): Hangfire's `JobDisplayNameAttribute` is `AttributeUsage(Method)`, so to-be item 4 ("`[JobDisplayName]` on the handler") can't be placed on the class. It's read from the handler's `HandleAsync`, which is also where Hangfire users would put it.
  - Related: RN-002, AC-001.3
  - Resolution: applied (plan updated)
