---
description: "Tasks for intervals of 1 second in the local host"
---

# Tasks: Intervals of 1 second in the local host

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md)

**Tests**: mandatory (constitution VII). Write each task's tests first; a task is done when the tests of the acceptance scenarios it covers pass, together with the existing suite (`dotnet test --no-build`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task).
- **[Story]**: US1 = intervals of 1 s in the local host (P1), US2 = intervals with seconds in Hangfire (P2), US3 = 6-field cron (P3).

## Phase 1: Setup

No setup: existing packages and test projects (plan, Structure Decision).

## Phase 2: Foundational (blocks every story)

- [X] T001 Lower the core interval minimum from 1 minute to 1 second in `Src/ExecutionFlow/Abstractions/HandlerBuilder.cs` (`Interval`: `interval < TimeSpan.FromSeconds(1)` → `ArgumentOutOfRangeException`, message "The interval must be at least one second.") and `Src/ExecutionFlow/ExecutionFlowOptions.cs` (`ParseInterval`: `InvalidOperationException` "... but the minimum is one second."); keep the exact `TimeSpan` (fractions included, no rounding); update the XML docs of `HandlerBuilder.Interval` and `Src/ExecutionFlow/Attributes/RecurringIntervalAttribute.cs` ("at least one second") (FR-003, research R-001, R-002). Tests: in `Tests/ExecutionFlow.Tests/Attributes/IntervalScheduleTests.cs` rename the Theory `Configure_Throws_WhenIntervalBelowOneMinute` to `Configure_Throws_WhenIntervalBelowOneSecond`, keeping its `NegativeHandler` and `ZeroHandler` cases and removing `ThirtySecondsHandler` from it; add `Configure_Accepts_IntervalOfOneSecond` covering a new `OneSecondHandler` (`"00:00:01"`) and the existing `ThirtySecondsHandler`, which is now accepted; keep `Registry_ExposesInterval`; new `Tests/ExecutionFlow.Tests/Abstractions/HandlerBuilderTests.cs` with `Interval_Throws_WhenBelowOneSecond` and `Interval_Accepts_FractionsOfASecond` (AC-003.1..AC-003.4).

**Checkpoint**: intervals of 1–59 s are accepted by the core for every provider. Until T008 lands, Hangfire registers a sub-minute interval through the old `IntervalCron` (per-minute ticker); don't release between T001 and T008.

## Phase 3: User Story 1 - Intervals of 1 second in the local host (Priority: P1) 🎯 MVP

**Goal**: the local host runs intervals from 1 second, to the second, and skip logs can be turned off.

**Independent Test**: `[RecurringInterval("00:00:05")]` runs every 5 s on a `FakeTimeProvider`, with no warning.

- [X] T002 [P] [US1] Tests for intervals of seconds in `Tests/ExecutionFlow.Hosting.Tests/RecurringHandlerHostTests.cs`: `Execute_RunsIntervalOfSeconds_WhenIntervalElapses`, `Execute_CountsSecondsFromEnd_WhenConfigured`, `Start_DoesNotWarn_ForIntervalOfSeconds`, `Execute_OverlapsIntervalOfSeconds_ByDefault`, `Execute_SkipsIntervalOfSeconds_WhenSkipWhenOverlapping`, plus `Execute_KeepsSecondCadence_WithoutDrift`, advancing 100 occurrences of `"00:00:01"` (SC-002). No production change expected (research R-003); if a test fails, fix the cause in `Src/ExecutionFlow.Hosting/Infrastructure/RecurringHandlerHost.cs` (FR-004, AC-004.1..AC-004.4; depends on T001).
- [X] T003 [P] [US1] `HostingOptions.LogSkippedOccurrences` (`bool`, default `true`, `ThrowIfLocked` setter like `IntervalFromEnd`) and internal `LogSkippedOccurrencesOf(Type)` in `Src/ExecutionFlow.Hosting/HostingOptions.cs`; builder extension `LogSkippedOccurrences<THandler>(this HandlerBuilder<THandler> handler, bool log = true)` storing `Hosting:LogSkippedOccurrences` in `Src/ExecutionFlow.Hosting/HandlerBuilderExtensions.cs`, with XML docs per [contracts/public-api.md](contracts/public-api.md) (FR-007, research R-009). Test: `HostingOptionsTests.PerHandlerLogSkippedOccurrences_WinsOverGlobal` in `Tests/ExecutionFlow.Hosting.Tests/HostingOptionsTests.cs` (AC-007.3).
- [X] T004 [US1] Guard both "ExecutionFlow: skipped ..." `LogInformation` calls (`RunOnOwnSchedule` gate and `SkipWhenOverlapping`) with `_setup.Options.LogSkippedOccurrencesOf(handlerType)` in `Src/ExecutionFlow.Hosting/Infrastructure/RunCoordinator.cs`; errors, warnings and the startup report stay unconditional (FR-007; depends on T003). Tests: new `Tests/ExecutionFlow.Hosting.Tests/RunCoordinatorTests.cs` with `Skip_Logs_ByDefault`, `Skip_DoesNotLog_WhenTurnedOffGlobally` (both skip reasons), `Failure_StillLogs_WhenSkipLogsAreOff` (AC-007.1, AC-007.2).

**Checkpoint**: US1 complete and shippable for local-host users.

## Phase 4: User Story 2 - Intervals with seconds in Hangfire (Priority: P2)

**Goal**: Hangfire registers intervals with seconds at the closest cadence it honors and warns below `SchedulePollingInterval`; whole-minute intervals unchanged (RN-001).

**Independent Test**: `"00:00:30"` → `*/30 * * * * *`; `"00:01:30"` → `* * * * * *` ticker; `"00:00:05"` → warning + `*/15 * * * * *`.

- [X] T005 [US2] Verify research R-010: read Hangfire 1.8.23's `RecurringJobScheduler` (decompile `Hangfire.Core.dll` from the NuGet cache or read the tagged source) and confirm it polls on `BackgroundJobServerOptions.SchedulePollingInterval` (default 15 s) and fires an overdue job once. Record the result under R-010 in `specs/015-hosting-second-intervals/research.md`; if it differs, stop and ask the maintainer before T006 (the default value may change).
- [X] T006 [P] [US2] `HangfireOptions.SchedulePollingInterval` (`TimeSpan`, default `TimeSpan.FromSeconds(15)`, `ThrowIfLocked`, `ArgumentOutOfRangeException` "below one second") with the XML doc of [contracts/public-api.md](contracts/public-api.md) in `Src/ExecutionFlow.Hangfire/HangfireOptions.cs` (FR-005; depends on T005). Tests in `Tests/ExecutionFlow.Hangfire.Tests/HangfireOptionsTests.cs`: `SchedulePollingInterval_DefaultsToFifteenSeconds`, `SchedulePollingInterval_Throws_WhenBelowOneSecond` (AC-005.7).
- [X] T007 [P] [US2] Extend `Src/ExecutionFlow.Hangfire/Infrastructure/IntervalCron.cs`: in `TryGetExactCron`, for an interval below one minute in whole seconds `n` with `60 % n == 0` return `*/n * * * * *` (`n == 1` → `* * * * * *`); keep the minute/hour rules unchanged; add `SecondTicker = "* * * * * *"`; `ToCron` returns the exact cron, else `SecondTicker` when the interval isn't whole minutes, else `Ticker` (research R-005). Unit tests in new `Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/IntervalCronTests.cs` for 1, 15, 30, 45, 90 s and 7 min.
- [X] T008 [US2] Effective interval: internal `HangfireSetup.EffectiveIntervalOf(RecurringJobRegistryInfo)` in `Src/ExecutionFlow.Hangfire/HangfireSetup.cs` = truncate to whole seconds; whole minutes → unchanged; seconds part → `max(value, Options.SchedulePollingInterval)` (research R-004). Use it in `RecurringJobResolver.ResolveCron` (`Src/ExecutionFlow.Hangfire/Infrastructure/RecurringJobResolver.cs`) and in `IntervalTickerFilter` (`Src/ExecutionFlow.Hangfire/Infrastructure/Filters/IntervalTickerFilter.cs`) instead of `registration.Interval.Value` (depends on T006, T007). Tests in `Tests/ExecutionFlow.Hangfire.Tests/IntervalRegistrationTests.cs`: existing `Build_RegistersExactCron_WhenIntervalDividesTheHour` and `Build_RegistersTicker_WhenNoExactCronExists` unchanged; new `Build_RegistersSixFieldCron_WhenIntervalDividesTheMinute`, `Build_RegistersSecondTicker_WhenIntervalHasSecondsAndNoExactCron`, `Build_TruncatesFractionsOfASecond`, `Configure_UsesConfiguredPollingMinimum` (AC-005.1..AC-005.3, AC-005.5, AC-005.9).
- [X] T009 [US2] Second resolution in the ticker: add a resolution parameter (minute or second) to `IntervalTickerStore.TryStartRun` in `Src/ExecutionFlow.Hangfire/Infrastructure/IntervalTickerStore.cs`, truncating `utcNow` to it (minute path unchanged, same hash and value format); `IntervalTickerFilter` passes second for intervals with a seconds part, and gates `SecondTicker` jobs too (research R-005; depends on T008). Tests in `Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/IntervalTickerTests.cs`: `OnCreating_Cancels_WhenSecondIntervalNotElapsed`, `OnCreating_Creates_WhenSecondIntervalElapsed`, `Trigger_Runs_WhenSecondIntervalNotElapsed`; all existing ticker tests unchanged; `IntervalRegistrationTests.Build_SwitchesPath_WhenIntervalGainsSeconds` (AC-005.6, AC-005.10, RN-001).
- [X] T010 [US2] Interval warnings at `Build` (recurring registration; not `Configure`, which also precedes `BuildDispatcherOnly`) in `Src/ExecutionFlow.Hangfire/HangfireSetup.cs`, through one internal helper `HasRegisteredSchedule(Type)` shared with T013: true for every recurring handler whose own schedule is registered, including auto-run off (registered as `Cron.Never()` but its schedule can be turned back on) and a dependent with `RunOnOwnSchedule`; false for a dependent triggered by the plan (`IsTriggeredByPlan`). For each such handler with an interval, one `Trace.TraceWarning` when (a) seconds part and below the minimum — names the handler, the declared interval and the minimum, says it was set but Hangfire doesn't support less and the minimum is used; (b) whole minutes and below the minimum — same, without raising. Producer-only mode (`BuildDispatcherOnly`) emits nothing (research R-006; depends on T008). Tests in `Tests/ExecutionFlow.Hangfire.Tests/IntervalRegistrationTests.cs` with a `TraceListener`: `Configure_WarnsAndRaises_WhenIntervalBelowPollingMinimum`, `Configure_WarnsWithoutRaising_WhenWholeMinuteIntervalIsBelowPollingMinimum`, `BuildDispatcherOnly_DoesNotWarn_ForSubMinuteSchedules`; `Configure_UsesConfiguredPollingMinimum` asserts no warning (AC-005.4, AC-005.5, AC-005.8, AC-005.11, SC-003).

**Checkpoint**: US2 complete; whole-minute intervals register exactly as before (012 tests unchanged).

## Phase 5: User Story 3 - Cron with seconds, officially (Priority: P3)

**Goal**: 6-field crons are a tested feature in both providers; Hangfire warns about crons below the minimum.

**Independent Test**: `*/30 * * * * *` runs every 30 s in the local host and is registered unchanged in Hangfire.

- [X] T011 [P] [US3] Local-host tests in `Tests/ExecutionFlow.Hosting.Tests/RecurringHandlerHostTests.cs`: `Execute_RunsSixFieldCron_EverySeconds`, `Execute_RunsSixFieldCron_FromPlan`, `Execute_ReadsFiveFieldCron_AsMinutes`. No production change expected (`RecurringHandlerHost.cs:47-48` already uses `CronFormat.IncludeSeconds`) (FR-001, AC-001.1..AC-001.3).
- [X] T012 [P] [US3] Internal `CronGap.MinimumGap(string cron)` in new `Src/ExecutionFlow.Hangfire/Infrastructure/CronGap.cs`: parse only the seconds field (6 fields; 5 fields → seconds `{0}`) and the minutes field, supporting `*`, `n`, `a-b`, `a,b`, `*/s`, `a-b/s`, `a/s`; return the smallest gap between consecutive occurrences (circular within a minute when consecutive minutes are allowed; over minutes for gaps of 60 s or more), or `null` when a field can't be read; conservative (may over-report, never throws) (research R-007). Unit tests in new `Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/CronGapTests.cs`: `*/5 * * * * *` → 5 s, `0,5 * * * * *` → 5 s, `*/30 * * * * *` → 30 s, `* * * * *` → 60 s, `0 * * * *` → 1 h, `*/15 0 * * * *` → 15 s, unreadable → `null`.
- [X] T013 [US3] Cron warnings at `Build` in `Src/ExecutionFlow.Hangfire/HangfireSetup.cs`: for each handler with `HasRegisteredSchedule` (T010) and a cron, if `CronGap.MinimumGap` < `SchedulePollingInterval`, one `Trace.TraceWarning` naming the handler, the cron and the minimum, saying Hangfire doesn't run it more often; the cron is registered unchanged (FR-002, RN-002; depends on T006, T010, T012). Tests in `Tests/ExecutionFlow.Hangfire.Tests/RecurringRegistrationTests.cs`: `Build_RegistersSixFieldCron_Unchanged`, `Configure_Warns_WhenSixFieldCronIsBelowPollingMinimum`, `Configure_DoesNotWarn_WhenCronIsAtOrAbovePollingMinimum`, `Configure_Warns_WhenIrregularCronHasGapBelowPollingMinimum`, `Configure_Warns_WhenFiveFieldCronIsBelowPollingMinimum`, `Configure_WarnsOnlyForRegisteredSchedules` (AC-002.1..AC-002.6).

**Checkpoint**: all stories complete.

## Phase 6: Polish & cross-cutting

- [X] T014 Documentation (FR-006): README "Interval schedules" section (`README.md`) and the XML docs of `Src/ExecutionFlow/Attributes/RecurringAttribute.cs`, `RecurringIntervalAttribute.cs`, `HandlerBuilder.Cron`/`Interval`: 6-field cron format; 1-second minimum; Hangfire precision bounded by `SchedulePollingInterval` (no exact-second guarantee), lower values raised (intervals) or warned (crons); keep `SchedulePollingInterval` equal to the servers' value; overlap allowed by default, so short intervals should consider `NoOverlap`, `SkipWhenOverlapping` or `IntervalFromEnd`; `LogSkippedOccurrences` (depends on T001..T013; done when the README snippets compile in `Examples/`).
- [X] T015 Run [quickstart.md](quickstart.md) section 1 (`dotnet build`, `dotnet test`) and confirm SC-001: every existing interval and cron test passes unchanged, except 012's `Configure_Throws_WhenIntervalBelowOneMinute`, replaced in T001 (depends on T014).

## Dependencies & execution order

- T001 blocks everything.
- US1 (T002–T004) depends only on T001. US2 (T005–T010) depends on T001. US3: T011 depends only on T001; T013 depends on T006 and T010 (US2) and T012.
- Within US2: T005 → T006; T007 ∥ T006; T008 after T006+T007; T009 and T010 after T008.
- Release note: ship US1 together with T008 (see Phase 2 checkpoint), so Hangfire never registers a sub-minute interval through the old per-minute ticker.

## Parallel examples

- After T001: T002, T003, T011 and T012 in parallel (different files).
- US2: T006 and T007 in parallel; then T008; then T009 and T010 in parallel (different files: store/filter vs. setup), if T010 touches only `HangfireSetup.cs` sections T009 doesn't.

## Implementation strategy

1. **MVP**: T001 → US1 (T002–T004). Local host gets intervals of 1 s. Not releasable alone for Hangfire users (Phase 2 checkpoint).
2. **Release-ready**: + US2 (T005–T010).
3. **Complete**: + US3 (T011–T013), then T014–T015. One minor version.

Commits reference the feature and IDs, e.g. `feat(015): core interval minimum of 1 second (FR-003, T001)`.

## Phase 7: Convergence

- [X] T016 Add `RecurringHandlerHostTests.Execute_HonorsFractionalInterval_FromPlan` in `Tests/ExecutionFlow.Hosting.Tests/RecurringHandlerHostTests.cs`: a plan handler with `Interval(TimeSpan.FromMilliseconds(1500))` doesn't run at 1.4 s and runs at 1.5 s on the fake clock (advance in 100 ms steps) per plan: R-002 / FR-003 (partial)
- [X] T017 Use the new APIs in an example under `Examples/` (a 6-field `[Recurring]` or second-level `[RecurringInterval]`, `HangfireOptions.SchedulePollingInterval`, and `HostingOptions.LogSkippedOccurrences` where the example uses the local host) and confirm `dotnet build` passes, completing T014's done criterion per T014 (partial)
