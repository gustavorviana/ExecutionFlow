# Implementation Plan: Interval schedules, overlap control and the plan in the core

**Branch**: `feat/execution-plan-dependencies` | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: [spec.md](spec.md). Decisions in [research.md](research.md).

## Summary

Three declarations move to (or are added in) the core: interval schedules (`[RecurringInterval]`), overlap control (`[NoOverlap]` and `ExecutionPlanEntry.NoOverlap`) and `ExecutionFlowOptions.UsePlan`. Hangfire implements them: an interval becomes an exact UTC cron or a per-minute ticker gated by a client filter, and overlap control becomes a server filter holding a distributed lock with the mandatory timeout.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: core: none; Hangfire: `Hangfire.Core` 1.8.23

**Storage**: Hangfire job storage (ticker last-run hash; overlap lock)

**Testing**: xUnit + NSubstitute; Hangfire tests with the existing in-memory/substitute storage helpers

**Target Platform**: any `netstandard2.0` runtime

**Project Type**: library

**Performance Goals**: a cancelled ticker occurrence does one hash read under a lock and creates no job

**Constraints**: no breaking change for `HangfireOptions.UsePlan` callers (SC-001); core stays dependency-free

**Scale/Scope**: tens of recurring handlers

## Constitution Check

- I. Dependency-free core: ✅ attributes, options and plan entry only; no execution logic in the core.
- II. Processors isolated: ✅ the cron conversion, ticker and lock live in `ExecutionFlow.Hangfire`.
- V. Minimal public surface: ⚠️ new public API, all additive: `RecurringIntervalAttribute`, `NoOverlapAttribute`, `ExecutionFlowOptions.UsePlan`, `ExecutionFlowOptions.Plan` (getter), `ExecutionFlowOptions.OnPlanRegistering` (protected), `RecurringJobRegistryInfo.Interval`/`OverlapTimeout` and its new constructor, `ExecutionPlanEntry.NoOverlap`/`ConfiguredOverlapTimeout`, `ExecutionPlanStep.OverlapTimeout`; `HangfireOptions.UsePlan` keeps compiling (inherited). New public API → minor version (v1.3.0).
- VI. Dispatch doesn't block: ✅ the ticker gate runs on recurring-job creation, not on `Publish`/`Schedule`.
- VII. Tested, traceable behavior: ✅ every AC names a test.

## Project Structure

### Documentation (this feature)

```text
specs/012-interval-and-overlap/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | New: `RecurringIntervalAttribute`, `NoOverlapAttribute`, `ExecutionPlanEntry.NoOverlap`, `ExecutionPlanStep.OverlapTimeout`, `RecurringJobRegistryInfo.Interval`/`OverlapTimeout`, `ExecutionFlowOptions.UsePlan` | No |
| `ExecutionFlow.Hangfire` | `HangfireOptions.UsePlan` becomes an override hook of the base method; interval registration; `IntervalTickerFilter`; `NoOverlapFilter` | No (source-compatible) |

```text
Src/ExecutionFlow/Attributes/RecurringIntervalAttribute.cs, NoOverlapAttribute.cs
Src/ExecutionFlow/Abstractions/ExecutionPlanEntry.cs, ExecutionPlanStep.cs, RecurringJobRegistryInfo.cs
Src/ExecutionFlow/ExecutionFlowOptions.cs
Src/ExecutionFlow.Hangfire/HangfireOptions.cs, HangfireSetup.cs
Src/ExecutionFlow.Hangfire/Infrastructure/IntervalCron.cs, IntervalTickerStore.cs
Src/ExecutionFlow.Hangfire/Infrastructure/Filters/IntervalTickerFilter.cs, NoOverlapFilter.cs
Tests/ExecutionFlow.Tests/…/IntervalScheduleTests.cs, OverlapOptionTests.cs, ExecutionFlowOptionsTests.cs, ExecutionPlannerTests.cs
Tests/ExecutionFlow.Hangfire.Tests/…/IntervalRegistrationTests.cs, IntervalTickerTests.cs, OverlapFilterTests.cs, ExecutionPlanHangfireTests.cs
```

## Design

- **`[RecurringInterval("hh:mm:ss")]`**: parses with `TimeSpan.ParseExact`/`TryParse` (invariant culture) at `Configure`; the registry exposes `Interval` (nullable) next to `Cron`. Validation at `Configure`: cron and interval together, invalid string, below 1 minute → `InvalidOperationException` naming the handler.
- **`IntervalCron.TryGetExactCron(TimeSpan, out string)`**: `n` minutes with `60 % n == 0` → `*/n * * * *` (`n = 1` → `* * * * *`); `h` whole hours with `24 % h == 0` → `0 */h * * *` (`h = 24` → `0 0 * * *`); otherwise `false`. Always registered in UTC.
- **Ticker**: registered with `* * * * *`. `IntervalTickerFilter : IClientFilter` runs `OnCreating` only for ticker jobs created by the recurring scheduler (manual triggers are recognized the same way the plan recognizes them, 005 FR-012). Under a distributed lock it reads `executionflow:interval:lastrun` (hash, one field per recurring ID); if `now - lastRun < interval` it sets `context.Canceled = true`; otherwise it writes `now` and lets the job be created. Start-based counting: the timestamp is the creation time.
- **`[NoOverlap(timeoutSeconds)]` / `entry.NoOverlap(TimeSpan)`**: the step exposes `OverlapTimeout` (plan value wins over the attribute). `NoOverlapFilter : IServerFilter` acquires `executionflow:overlap:<recurring id>` with the timeout in `OnPerforming` and releases it in `OnPerformed`; a timeout throws `DistributedLockTimeoutException`, so the job fails and follows 005 FR-005.
- **`ExecutionFlowOptions.UsePlan(ExecutionPlan)`**: non-virtual public method in the core; stores the plan once (second call throws; null throws) and calls `protected virtual void OnPlanRegistering(ExecutionPlan plan)`. `HangfireOptions` removes its own `UsePlan` and overrides `OnPlanRegistering` with the current body (conflict with `SetJobAutoRun`, `Add`, auto-run). Callers of `hangfireOptions.UsePlan(plan)` compile unchanged.

## Complexity Tracking

| Item | Why | Simpler alternative rejected |
|---|---|---|
| Ticker filter + storage hash | intervals with no exact cron (7, 90 min) must run in Hangfire (maintainer decision) | rejecting such intervals at startup |
