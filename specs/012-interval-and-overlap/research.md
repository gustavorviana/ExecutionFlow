# Research: Interval schedules, overlap control and the plan in the core

All decisions taken with the maintainer on 2026-11-02.

## R-001: Intervals in Hangfire — exact cron, otherwise a ticker

- **Decision**: an interval with a single exact cron equivalent is registered as that cron (UTC); any other interval is registered as a per-minute ticker whose occurrences are cancelled at creation until the interval has elapsed.
- **Rationale**: common intervals (5, 15, 30 min, 1 h, 6 h) stay pure cron with no extra storage traffic; intervals such as 7 or 90 min, which no single cron can express (90 min needs two crons; 7 min doesn't divide the hour), still run and remain normal recurring jobs (dashboard, "Trigger now", on/off).
- **Alternatives considered**: (a) convert and reject the rest — rejected by the maintainer as less useful; (b) reschedule at the end of each run with `BackgroundJob.Schedule` — drops the recurring job from the dashboard, loses "Trigger now", on/off and orphan cleanup, and needs multi-server design; (c) reject every interval in Hangfire.
- **Consequences**: ticker precision is about 1 minute (Hangfire polls recurring jobs about every 15 s). Cancelled occurrences leave no job. Hangfire's exact crons are aligned to the UTC clock, while the local host counts from its start; the maintainer accepted the difference.

## R-002: Intervals have no time zone

- **Decision**: an interval means "every N of time", never a time of day, so it has no time zone and is evaluated in UTC.
- **Rationale**: the maintainer's intent; it also keeps intervals exact across daylight-saving changes.
- **Alternatives considered**: a `TimeZone` property mirroring `[Recurring]` — only useful to align hour-based exact crons to local time.

## R-003: Overlap control is a core declaration with a mandatory timeout

- **Decision**: `[NoOverlap(timeoutSeconds)]` on the handler and `ExecutionPlanEntry.NoOverlap(TimeSpan)` on the plan (plan wins). Default: overlap allowed (current Hangfire behavior). When off, a new run waits for the running one, and fails when the timeout expires. In Hangfire it is a distributed lock held for the run.
- **Rationale**: handler authors shouldn't need Hangfire's `[DisableConcurrentExecution]`, and both providers must behave the same. Waiting with a mandatory timeout mirrors Hangfire's attribute.
- **Alternatives considered**: an optional timeout (rejected by the maintainer); skipping by default (kept only as a local-host option, 011 FR-003).

## R-004: `UsePlan` moves to the core options; auto-run stays per provider

- **Decision**: `ExecutionFlowOptions.UsePlan` stores the plan and calls a protected hook that each provider overrides. `SetJobAutoRun`/`GlobalRecurringAutoRun` stay in `HangfireOptions`; the local host has its own copy.
- **Rationale**: the core declares, providers execute (011 R-001); one place to register the plan for every provider.
- **Alternatives considered**: `UsePlan` in each provider (duplicate registration code); moving auto-run to the core too (the maintainer chose to move only `UsePlan`).
