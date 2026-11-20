# Implementation Plan: Intervals of 1 second in the local host

**Branch**: `feat/hosting-second-intervals` | **Date**: 2026-11-20 | **Spec**: [spec.md](spec.md)

**Input**: [spec.md](spec.md). Decisions in [research.md](research.md).

## Summary

The core lowers the interval minimum from 1 minute to 1 second (R-001), keeping fractions of a second (R-002). The local host already schedules any `TimeSpan` and 6-field crons, so it only gets tests plus a way to turn off skip logs (R-003, R-009). Hangfire computes an effective interval (truncate to seconds; raise intervals with a seconds part to the new `HangfireOptions.SchedulePollingInterval`, R-004), registers it as a 6-field cron or a second-level ticker (R-005), and warns at `Build` about any schedule below the polling minimum (R-006, R-007). Intervals in whole minutes are unchanged in Hangfire (RN-001).

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: core: none; Hangfire: `Hangfire.Core` 1.8.23; Hosting: Generic Host abstractions, Cronos (existing)

**Storage**: Hangfire job storage (existing ticker last-run hash, now also at second resolution)

**Testing**: xUnit + NSubstitute; `FakeTimeProvider` in the Hosting tests; `TraceListener` capture for Hangfire warnings (as in 012)

**Target Platform**: any `netstandard2.0` runtime

**Project Type**: library (4 NuGet packages, lockstep version)

**Performance Goals**: a second-level ticker takes the interval lock at most once per Hangfire polling cycle (R-008)

**Constraints**: no breaking change (spec Clarifications: lowering the minimum is compatible; fractions keep working); core stays dependency-free; no Cronos in the Hangfire package (R-007)

**Scale/Scope**: tens of recurring handlers

## Constitution Check

*Pre-design and post-design: same result.*

- I. Dependency-free core: ✅ only the bound in two existing checks changes.
- II. Processors isolated: ✅ cron/ticker/warning logic in `ExecutionFlow.Hangfire`; skip-log setting in `ExecutionFlow.Hosting`; no Cronos reference added to the Hangfire package (R-007).
- III. Optional integrations: ✅ no new package.
- IV. Producer-only mode: ✅ `BuildDispatcherOnly` registers no recurring jobs, so it emits no schedule warnings and registers no tickers.
- V. Minimal public surface / breaking changes: ✅ additive public API only (see [contracts/public-api.md](contracts/public-api.md)): `HangfireOptions.SchedulePollingInterval`, `HostingOptions.LogSkippedOccurrences`, `HandlerBuilderExtensions.LogSkippedOccurrences`. Accepting 1–59 s and keeping fractions are recorded as compatible in Clarifications (maintainer, 2026-11-20). Minor version.
- VI. Dispatch doesn't block: ✅ nothing on `Publish`/`Schedule`.
- VII. Tested, traceable: ✅ every AC names a test; new test classes: `HandlerBuilderTests` (core), `RunCoordinatorTests` (Hosting), `CronGapTests` (Hangfire, unit tests of R-007 beyond the ACs).

## Project Structure

### Documentation (this feature)

```text
specs/015-hosting-second-intervals/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/public-api.md
├── checklists/requirements.md
└── tasks.md            # /speckit-tasks
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | Interval minimum 1 min → 1 s in `HandlerBuilder.Interval` and `ExecutionFlowOptions.ParseInterval`; XML docs | No (Clarifications) |
| `ExecutionFlow.Hangfire` | `HangfireOptions.SchedulePollingInterval`; effective interval; `IntervalCron` second rules and `SecondTicker`; `IntervalTickerStore` resolution; `CronGap`; `Configure` warnings | No |
| `ExecutionFlow.Hosting` | `HostingOptions.LogSkippedOccurrences`, builder extension, check in `RunCoordinator` | No |

```text
Src/ExecutionFlow/Abstractions/HandlerBuilder.cs
Src/ExecutionFlow/ExecutionFlowOptions.cs
Src/ExecutionFlow/Attributes/RecurringAttribute.cs, RecurringIntervalAttribute.cs   (XML docs)
Src/ExecutionFlow.Hangfire/HangfireOptions.cs, HangfireSetup.cs
Src/ExecutionFlow.Hangfire/Infrastructure/IntervalCron.cs, IntervalTickerStore.cs, RecurringJobResolver.cs, CronGap.cs (new)
Src/ExecutionFlow.Hangfire/Infrastructure/Filters/IntervalTickerFilter.cs
Src/ExecutionFlow.Hosting/HostingOptions.cs, HandlerBuilderExtensions.cs
Src/ExecutionFlow.Hosting/Infrastructure/RunCoordinator.cs
README.md
Tests/ExecutionFlow.Tests/Attributes/IntervalScheduleTests.cs, Abstractions/HandlerBuilderTests.cs (new)
Tests/ExecutionFlow.Hangfire.Tests/IntervalRegistrationTests.cs, RecurringRegistrationTests.cs, HangfireOptionsTests.cs
Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/IntervalTickerTests.cs, IntervalCronTests.cs (new), CronGapTests.cs (new)
Tests/ExecutionFlow.Hosting.Tests/RecurringHandlerHostTests.cs, HostingOptionsTests.cs, RunCoordinatorTests.cs (new)
```

**Structure Decision**: existing package and test layout; no new projects.

## Design

- **Core (FR-003)**: change `TimeSpan.FromMinutes(1)` to `TimeSpan.FromSeconds(1)` in both checks and their messages. 012's test `IntervalScheduleTests.Configure_Throws_WhenIntervalBelowOneMinute` is replaced by `Configure_Throws_WhenIntervalBelowOneSecond` (012 AC-001.2 → 015 AC-003.2).
- **Hosting (FR-001, FR-004, FR-007)**: no scheduler change (R-003). `RunCoordinator` wraps both skip `LogInformation` calls in `if (_setup.Options.LogSkippedOccurrencesOf(handlerType))` (R-009).
- **Hangfire (FR-002, FR-005)**:
  - `EffectiveIntervalOf` (R-004) feeds `RecurringJobResolver.ResolveCron` and `IntervalTickerFilter`.
  - `IntervalCron` (R-005): `*/n * * * * *` for `n` dividing 60; `SecondTicker` for other intervals with a seconds part; minute rules unchanged.
  - `IntervalTickerStore.TryStartRun(..., resolution)` truncates to minute or second (R-005).
  - `Build` (recurring registration) emits at most one `Trace.TraceWarning` per handler (R-006), using `CronGap` for crons (R-007).
- **Docs (FR-006)**: README "Interval schedules" section and the XML docs listed in FR-006.

## Risks

- R-010: if Hangfire's recurring scheduler doesn't follow `SchedulePollingInterval`, the 15 s default and the docs change; the behavior specified (minimum = setting) doesn't.
- `CronGap` over-reporting on unusual crons gives a false warning, never a changed cron (R-007).

## Complexity Tracking

| Item | Why | Simpler alternative rejected |
|---|---|---|
| `CronGap` analyzer (seconds and minutes fields) | FR-002 warns about crons below the minimum, and Hangfire doesn't expose its parser | Cronos in the Hangfire package (constitution II amendment for a warning) |
| Second resolution in the ticker store | intervals with a seconds part and no exact cron (45 s, 90 s) | rejecting those intervals in Hangfire (contradicts FR-005) |
