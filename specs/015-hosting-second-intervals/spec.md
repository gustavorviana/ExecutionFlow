# Feature Specification: Intervals of 1 second in the local host

**Feature Branch**: `feat/hosting-second-intervals`

**Created**: 2026-11-20

**Status**: Implemented (unreleased)

**Input**: User description: "Make the local host support intervals of 1 second. As a consequence: officially support 6-field cron expressions (with seconds) in both providers, and accept intervals with seconds. The local host accepts intervals from 1 second. Hangfire keeps minute logic for intervals without seconds; for intervals with seconds its minimum follows Hangfire's scheduler polling interval, and anything below it is raised to that minimum with a warning."

**Packages**: `ExecutionFlow`, `ExecutionFlow.Hangfire`, `ExecutionFlow.Hosting`

**Amends**:
- [005-recurring-jobs](../005-recurring-jobs/spec.md) Assumptions ("seconds-level cron precision" is no longer out of scope).
- [012-interval-and-overlap](../012-interval-and-overlap/spec.md) FR-001 (the 1-minute minimum becomes 1 second in the core), FR-002 (intervals with seconds in Hangfire), Assumptions ("second-level intervals in Hangfire" is no longer out of scope).
- [011-local-hosting](../011-local-hosting/spec.md) FR-003 (intervals from 1 second), FR-003 and FR-005 (skip log lines can be turned off).

## Context

Today a schedule can't run more often than once a minute through a documented path:

1. A 6-field cron (with seconds, e.g. `*/30 * * * * *`) already works in both providers, by accident: the local host parses it with seconds ([011](../011-local-hosting/spec.md)), and Hangfire 1.8 accepts it natively. But 005 lists "seconds-level cron precision" as out of scope, and no test covers it, so it may break without notice.
2. Intervals (`[RecurringInterval]`, `HandlerBuilder.Interval`) are rejected below 1 minute in the core, for every provider (012 FR-001). The local host could run them precisely; Hangfire can't below its scheduler polling interval.

Hangfire doesn't run a recurring job at the exact second: its recurring scheduler checks for due jobs once per polling cycle (`SchedulePollingInterval`, 15 s by default) and fires a due job once per check. Intervals are not a Hangfire feature: ExecutionFlow translates them into crons or a ticker (012 FR-002). So the Hangfire part of this feature is about what ExecutionFlow registers and warns about, not about changing Hangfire.

## Clarifications

### Session 2026-11-20

- Q: In Hangfire, an interval with seconds below the polling minimum? → A: It doesn't throw: it's raised to the minimum, with a warning saying the value was set but Hangfire doesn't support less than the minimum (maintainer, 2026-11-20).
- Q: In Hangfire, a 6-field cron whose occurrences are closer than the polling minimum? → A: Same warning; the cron is registered unchanged (maintainer, 2026-11-20).
- Q: Is the Hangfire minimum fixed at 15 s? → A: No, it follows the configured `SchedulePollingInterval` (maintainer, 2026-11-20).
- Q: Intervals without a seconds part in Hangfire? → A: Unchanged: minute logic (exact cron or per-minute ticker, 012 FR-002) (maintainer, 2026-11-20).
- Q: Is lowering the core minimum from 1 minute to 1 second (012 AC-001.2 no longer throws for 1–59 s) a breaking change? → A: No, it's compatible: a minor version (maintainer, 2026-11-20).
- Q: How does ExecutionFlow learn Hangfire's polling interval? → A: A new `HangfireOptions.SchedulePollingInterval` (default 15 s) that the app keeps in sync. Reading the server's options was rejected for now: they're built after `Configure`, may live in another process or in several servers with different values, and aren't reachable without DI. Reading them in the DI package when possible is a possible later evolution (maintainer, 2026-11-20).
- Q: Must `Interval(TimeSpan)` be whole seconds? → A: No: requiring it would break code that passes fractions today. Fractions keep working; the local host honors them, Hangfire truncates to the second (maintainer, 2026-11-20).
- Q: With a polling interval of 1 minute or more, what about crons and whole-minute intervals below it? → A: They get the same warning, unchanged; only intervals with a seconds part are raised (maintainer, 2026-11-20).
- Q: A 1-second handler that depends on a 3-second one: does it run 3 times per cycle? → A: No. A dependent runs once per new cycle of its prerequisites and never overlaps itself (011 FR-005). Overlap applies only to a handler's own schedule, which stays allowed by default (AC-004.4) (maintainer, 2026-11-20).
- Q: Skip log lines can repeat every second; allow turning them off? → A: Yes, with `HostingOptions.LogSkippedOccurrences` and a per-handler builder extension, default on. A separate log category was rejected: all local-host logs share `ExecutionFlow.Hosting`, and moving a line to a new category could break existing filters (maintainer, 2026-11-20).
- Q: Which handlers get the Hangfire schedule warnings? → A: Every handler whose own schedule is registered, including auto-run off and `RunOnOwnSchedule` dependents; not dependents triggered by the plan (maintainer, 2026-11-20).
- Q: When are the Hangfire schedule warnings emitted? → A: When recurring jobs are registered (`Build`), not at `Configure`: `Configure` runs before the app chooses `Build` or `BuildDispatcherOnly`, and producer-only mode must not warn (AC-005.11). Found during implementation (2026-11-20).
- Q: Do intervals and 6-field crons need their own specs? → A: One spec. Its goal is 1-second intervals in the local host (Story 1); Hangfire's handling of intervals with seconds (Story 2) and 6-field crons (Story 3) follow from it and share the same Hangfire minimum (maintainer, 2026-11-20).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Intervals of 1 second in the local host (Priority: P1)

As a handler author using the local host, I want to declare intervals from 1 second (e.g. `"00:00:01"`, `"00:00:05"`, `"00:01:30"`), so that a handler runs more often than once a minute, or at a cadence with seconds, without writing a cron.

**Why this priority**: it is the goal of this feature; the local host can run any interval to the second.

**Independent Test**: declare `"00:00:05"` in the local host and check it runs every 5 s on a fake clock, with no warning.

**Acceptance Scenarios**: AC-003.1..AC-003.4, AC-004.1..AC-004.4, AC-007.1..AC-007.3.

---

### User Story 2 - Intervals with seconds in Hangfire (Priority: P2)

As a handler author using Hangfire, I want an interval with seconds to behave predictably: run at the closest cadence Hangfire can honor, and tell me when my value is below what it supports, instead of failing at startup.

**Why this priority**: the core minimum drops to 1 second for the local host (Story 1), so Hangfire must define what it does with those values.

**Independent Test**: declare `"00:00:30"`, `"00:01:30"` and `"00:00:05"` in Hangfire and check the registration and the warning.

**Acceptance Scenarios**: AC-005.1..AC-005.11.

---

### User Story 3 - Cron with seconds, officially (Priority: P3)

As a handler author, I want to write a 6-field cron (`sec min hour day month weekday`) in `[Recurring]` or `HandlerBuilder.Cron`, and have both providers honor it as a supported, tested feature.

**Why this priority**: it already works by accident; making it official costs little and removes the risk of a silent break.

**Independent Test**: register `*/30 * * * * *` on each provider and check the schedule (local host: runs every 30 s on a fake clock; Hangfire: registered unchanged).

**Acceptance Scenarios**: AC-001.1..AC-001.3, AC-002.1..AC-002.6.

### Edge Cases

Each edge case is an acceptance scenario (constitution VII):

- An interval of exactly the Hangfire minimum (e.g. `"00:00:15"`): accepted, no warning (AC-005.2).
- An interval with seconds above one minute (`"00:01:30"`): uses the second-level path, not the minute ticker (AC-005.3).
- An interval below 1 second (e.g. `TimeSpan.FromMilliseconds(500)`, or `"00:00:00"`): rejected in the core, for every provider (AC-003.2).
- A 5-field cron: registered unchanged in both providers (AC-001.3, AC-002.3); in Hangfire it gets a warning only when it's below the polling minimum (AC-002.5).
- An interval of seconds shorter than the handler's run, in the local host: runs overlap by default (AC-004.4).
- Skip log lines repeating every second in the local host: can be turned off (AC-007.2).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 6-field cron in the local host**: a cron with 6 fields is read with a leading seconds field; a cron with 5 fields is read as today. The schedule runs at each occurrence, to the second. *(Story 3)*
  - **AC-001.1**: **Given** a handler with `[Recurring("*/30 * * * * *")]`, **When** 30 s pass on the host clock, **Then** it runs, and not before. Test: `RecurringHandlerHostTests.Execute_RunsSixFieldCron_EverySeconds`
  - **AC-001.2**: **Given** `HandlerBuilder.Cron("*/10 * * * * *")` in the plan, **When** the host starts, **Then** the handler runs every 10 s. Test: `RecurringHandlerHostTests.Execute_RunsSixFieldCron_FromPlan`
  - **AC-001.3**: **Given** a 5-field cron, **When** the next occurrence is computed, **Then** it is on a whole minute, as before. Test: `RecurringHandlerHostTests.Execute_ReadsFiveFieldCron_AsMinutes`

- **FR-002 Crons in Hangfire**: a 6-field cron is registered with Hangfire unchanged, in the resolved time zone (005 FR-008). The warnings of this FR and FR-005 apply to every handler whose own schedule is registered, including one with auto-run off (its schedule can be turned back on) and a dependent with `RunOnOwnSchedule`; they don't apply to a dependent triggered by the plan, which has no schedule (005 FR-010). When any two consecutive occurrences of a cron (6 or 5 fields, including irregular ones such as `0,5 * * * * *`) can be closer than the Hangfire minimum (FR-005), `Build` logs a warning naming the handler, the cron and the minimum, and says that Hangfire doesn't run it more often than the minimum. A cron is never changed (RN-002). *(Story 3)*
  - **AC-002.1**: **Given** `[Recurring("*/30 * * * * *")]`, **When** Hangfire is built, **Then** the recurring job is registered with `*/30 * * * * *`. Test: `RecurringRegistrationTests.Build_RegistersSixFieldCron_Unchanged`
  - **AC-002.2**: **Given** `[Recurring("*/5 * * * * *")]` and a minimum of 15 s, **When** Hangfire is built, **Then** a warning names the handler, the cron and the 15 s minimum, and the cron is still registered unchanged. Test: `RecurringRegistrationTests.Configure_Warns_WhenSixFieldCronIsBelowPollingMinimum`
  - **AC-002.3**: **Given** `[Recurring("*/30 * * * * *")]` or any 5-field cron, and a minimum of 15 s, **When** Hangfire is built, **Then** no warning is logged. Test: `RecurringRegistrationTests.Configure_DoesNotWarn_WhenCronIsAtOrAbovePollingMinimum`
  - **AC-002.4**: **Given** the irregular cron `0,5 * * * * *` (5 s, then 55 s) and a minimum of 15 s, **When** Hangfire is built, **Then** the warning is logged. Test: `RecurringRegistrationTests.Configure_Warns_WhenIrregularCronHasGapBelowPollingMinimum`
  - **AC-002.5**: **Given** `SchedulePollingInterval = 90 s` and the 5-field cron `* * * * *`, **When** Hangfire is built, **Then** the warning is logged and the cron is registered unchanged. Test: `RecurringRegistrationTests.Configure_Warns_WhenFiveFieldCronIsBelowPollingMinimum`
  - **AC-002.6**: **Given** `*/5 * * * * *` on a handler with auto-run off and on a dependent with `RunOnOwnSchedule`, and the same cron on a dependent triggered by the plan, **When** Hangfire is built, **Then** the first two get the warning and the third doesn't. Test: `RecurringRegistrationTests.Configure_WarnsOnlyForRegisteredSchedules`

- **FR-003 Interval minimum in the core**: an interval (`[RecurringInterval]`, `HandlerBuilder.Interval`) must be at least **1 second**, for every provider. The attribute's format stays `[d.]hh:mm:ss` (no fractions of a second); `Interval(TimeSpan)` keeps accepting fractions of a second, as today, and each provider decides how to honor them (the local host runs the exact value; Hangfire truncates to the second, FR-005). Each provider may raise its own effective minimum (FR-005). *(Story 1)*
  - *Amends [012](../012-interval-and-overlap/spec.md) FR-001 (was: at least 1 minute) and replaces AC-001.2 of 012.*
  - Not a breaking change (constitution V): code that works today keeps working; only values that threw before (1–59 s) are now accepted. Minor version (see Clarifications).
  - **AC-003.1**: **Given** `[RecurringInterval("00:00:01")]` or `Interval(TimeSpan.FromSeconds(1))`, **When** the options are configured, **Then** it is accepted. Test: `IntervalScheduleTests.Configure_Accepts_IntervalOfOneSecond`
  - **AC-003.2**: **Given** an interval below 1 second (`"00:00:00"`, or `Interval(TimeSpan.FromMilliseconds(500))`), **When** it is set or configured, **Then** it throws naming the handler; `Interval` throws `ArgumentOutOfRangeException`, the attribute `InvalidOperationException`, as today. Tests: `IntervalScheduleTests.Configure_Throws_WhenIntervalBelowOneSecond`, `HandlerBuilderTests.Interval_Throws_WhenBelowOneSecond`
  - **AC-003.3**: **Given** `Interval(TimeSpan.FromMilliseconds(1500))` or `Interval(TimeSpan.FromSeconds(90.5))`, **When** it is set, **Then** it is accepted and the registry exposes the exact value (fractions of a second keep working, as today). Test: `HandlerBuilderTests.Interval_Accepts_FractionsOfASecond`
  - **AC-003.4**: **Given** `[RecurringInterval("00:15:00")]`, **When** the registry is read, **Then** it behaves as before (012 AC-001.3). Test: `IntervalScheduleTests.Registry_ExposesInterval`

- **FR-004 Intervals with seconds in the local host**: the local host runs any interval of 1 second or more, to the second, with the cadence rules of 011 FR-003 (from the host start by default, or from the end of the previous run). No warning. *(Story 1)*
  - **AC-004.1**: **Given** `[RecurringInterval("00:00:05")]`, **When** 5 s pass on the host clock, **Then** it runs, and not before. Test: `RecurringHandlerHostTests.Execute_RunsIntervalOfSeconds_WhenIntervalElapses`
  - **AC-004.2**: **Given** `"00:00:05"` with `IntervalFromEnd`, **When** a run ends, **Then** the next run is 5 s after that end. Test: `RecurringHandlerHostTests.Execute_CountsSecondsFromEnd_WhenConfigured`
  - **AC-004.3**: **Given** `"00:00:05"`, **When** the host starts, **Then** no warning is logged. Test: `RecurringHandlerHostTests.Start_DoesNotWarn_ForIntervalOfSeconds`
  - **AC-004.4**: **Given** `"00:00:01"` on a handler whose run takes 3 s, with no overlap setting, **When** 3 s pass, **Then** a new run starts every second while the previous ones are still running (overlap stays allowed by default, 011 FR-003); **Given** `SkipWhenOverlapping`, **Then** the occurrences during a run are skipped. Tests: `RecurringHandlerHostTests.Execute_OverlapsIntervalOfSeconds_ByDefault`, `RecurringHandlerHostTests.Execute_SkipsIntervalOfSeconds_WhenSkipWhenOverlapping`

- **FR-005 Intervals with seconds in Hangfire**: *(Story 2)*
  - Fractions of a second are first truncated to the second (`00:01:30.5` → `00:01:30`; `00:01:00.5` → `00:01:00`).
  - An interval **without a seconds part** (whole minutes) is registered exactly as in 012 FR-002 (exact cron or per-minute ticker), never raised (RN-001). If it's shorter than the Hangfire minimum (possible only with a minimum above 1 minute), `Build` logs the same warning as FR-002, without changing it.
  - An interval **with a seconds part** has a minimum equal to Hangfire's scheduler polling interval (the "Hangfire minimum"; 15 s by default). Below it, `Build` logs a warning naming the handler, the declared interval and the minimum, saying that the interval was set but Hangfire doesn't support less than the minimum, and the interval is **raised** to the minimum.
  - The effective interval is then registered as an exact 6-field cron in UTC when it divides a minute evenly (e.g. 15, 20, 30 s → `*/15 * * * * *`), and otherwise as a second-level ticker with the same rules as the minute ticker (012 FR-002: occurrences cancelled at creation until the interval has elapsed, last run in the storage, manual triggers always run and count as the last run), counting to the second.
  - The Hangfire minimum comes from a new setting, `HangfireOptions.SchedulePollingInterval` (default 15 s, Hangfire's default), which the app keeps equal to the polling interval of its Hangfire servers. ExecutionFlow doesn't read or change the servers' options (see Clarifications). A value below 1 second throws when set.
  - **AC-005.1**: **Given** `"00:30:00"` or `"01:30:00"`, **When** Hangfire is built, **Then** the registration is the one of 012 (`*/30 * * * *`, or the per-minute ticker). Tests: `IntervalRegistrationTests.Build_RegistersExactCron_WhenIntervalDividesTheHour`, `IntervalRegistrationTests.Build_RegistersTicker_WhenNoExactCronExists`
  - **AC-005.2**: **Given** `"00:00:15"` or `"00:00:30"` and a minimum of 15 s, **When** Hangfire is built, **Then** the job is registered with `*/15 * * * * *` or `*/30 * * * * *`, without a warning. Test: `IntervalRegistrationTests.Build_RegistersSixFieldCron_WhenIntervalDividesTheMinute`
  - **AC-005.3**: **Given** `"00:01:30"` or `"00:00:45"`, **When** Hangfire is built, **Then** it is registered as the second-level ticker. Test: `IntervalRegistrationTests.Build_RegistersSecondTicker_WhenIntervalHasSecondsAndNoExactCron`
  - **AC-005.4**: **Given** `"00:00:05"` and a minimum of 15 s, **When** Hangfire is built, **Then** a warning names the handler, `00:00:05` and the 15 s minimum, and the job is registered as `*/15 * * * * *`. Test: `IntervalRegistrationTests.Configure_WarnsAndRaises_WhenIntervalBelowPollingMinimum`
  - **AC-005.5**: **Given** `SchedulePollingInterval = 5 s` and `"00:00:05"`, **When** Hangfire is built, **Then** no warning is logged and the job is registered as `*/5 * * * * *`. Test: `IntervalRegistrationTests.Configure_UsesConfiguredPollingMinimum`
  - **AC-005.6**: **Given** a second-level ticker whose interval hasn't elapsed (to the second), **When** an occurrence is about to be created, **Then** it is cancelled; **Given** it has elapsed, **Then** the job is created and the last run is updated; a manual trigger always runs. Tests: `IntervalTickerTests.OnCreating_Cancels_WhenSecondIntervalNotElapsed`, `IntervalTickerTests.OnCreating_Creates_WhenSecondIntervalElapsed`, `IntervalTickerTests.Trigger_Runs_WhenSecondIntervalNotElapsed`
  - **AC-005.7**: **Given** `HangfireOptions` with no setting, **When** `SchedulePollingInterval` is read, **Then** it is 15 s; **Given** a value below 1 second, **When** it is set, **Then** it throws `ArgumentOutOfRangeException`. Tests: `HangfireOptionsTests.SchedulePollingInterval_DefaultsToFifteenSeconds`, `HangfireOptionsTests.SchedulePollingInterval_Throws_WhenBelowOneSecond`
  - **AC-005.8**: **Given** `SchedulePollingInterval = 90 s` and `"00:01:00"`, **When** Hangfire is built, **Then** the warning is logged and the job is registered with `* * * * *`, unchanged. Test: `IntervalRegistrationTests.Configure_WarnsWithoutRaising_WhenWholeMinuteIntervalIsBelowPollingMinimum`
  - **AC-005.9**: **Given** `Interval(TimeSpan.FromSeconds(30.5))`, **When** Hangfire is built, **Then** it is registered as `*/30 * * * * *`. Test: `IntervalRegistrationTests.Build_TruncatesFractionsOfASecond`
  - **AC-005.10**: **Given** a handler whose interval changes from `"00:02:00"` (minute path) to `"00:01:30"` (second path) between two builds, **When** the second build runs, **Then** the job is re-registered on the new path and the stored last run is still honored. Test: `IntervalRegistrationTests.Build_SwitchesPath_WhenIntervalGainsSeconds`
  - **AC-005.11**: **Given** producer-only mode (`BuildDispatcherOnly`) with handlers whose interval or cron is below the minimum, **When** it is built, **Then** no warning is logged and no recurring job or ticker is registered (constitution IV). Test: `IntervalRegistrationTests.BuildDispatcherOnly_DoesNotWarn_ForSubMinuteSchedules`

- **FR-006 Documentation**: the README and the XML docs of `[Recurring]`, `[RecurringInterval]`, `HandlerBuilder.Cron` and `HandlerBuilder.Interval` state: the 6-field cron format; the 1-second minimum; that in Hangfire the precision below one minute is bounded by the polling interval (no exact-second guarantee), and that lower values are raised with a warning; that overlap stays allowed by default, so short intervals should consider `NoOverlap`, `SkipWhenOverlapping` or `IntervalFromEnd`; and how to turn off skip logs (FR-007). Covered by a documentation task (no test).

- **FR-007 Turning off skip logs in the local host**: the local host logs a line each time it skips an occurrence: a dependent with `RunOnOwnSchedule` whose prerequisites have no new cycle (011 FR-005), and a handler with `SkipWhenOverlapping` whose previous run is still running (011 FR-003). With intervals of seconds, these lines can repeat every second. A new global setting `HostingOptions.LogSkippedOccurrences` (default `true`, the current behavior) and, per handler, the builder extension `handler.LogSkippedOccurrences(bool = true)` (in the plan or `GetPlanOf<T>()`, wins over the global one; a `Hosting:`-prefixed builder setting, like `IntervalFromEnd`) turn these lines on or off. Turning them off affects only the skip lines: errors, warnings and the startup report are still logged. *(Story 1)*
  - *Amends [011](../011-local-hosting/spec.md) FR-003 and FR-005: their skip log lines can be turned off.*
  - **AC-007.1**: **Given** default options, **When** an occurrence is skipped (either reason), **Then** the skip line is logged, as today. Test: `RunCoordinatorTests.Skip_Logs_ByDefault`
  - **AC-007.2**: **Given** `LogSkippedOccurrences = false`, **When** an occurrence is skipped for either reason, **Then** no skip line is logged, and a run that fails is still logged as an error. Tests: `RunCoordinatorTests.Skip_DoesNotLog_WhenTurnedOffGlobally`, `RunCoordinatorTests.Failure_StillLogs_WhenSkipLogsAreOff`
  - **AC-007.3**: **Given** `LogSkippedOccurrences = false` and `GetPlanOf<T>().LogSkippedOccurrences(true)` (or the reverse, also in the plan), **When** `T` skips an occurrence, **Then** the per-handler value wins. Test: `HostingOptionsTests.PerHandlerLogSkippedOccurrences_WinsOverGlobal`

### Business Rules

- **RN-001**: Intervals without a seconds part behave in Hangfire exactly as before this feature (no change to registered crons, tickers or storage).
- **RN-002**: The Hangfire minimum never throws: it raises only intervals with a seconds part; every other schedule below it (crons, whole-minute intervals) only gets a warning.
- **RN-003**: `[DependentRun]` / `RunMode(MinInterval)` and `NoOverlap` keep their rules (positive `TimeSpan`); they are out of this feature's scope.

### Key Entities

- **Hangfire minimum**: the smallest gap Hangfire can honor between two runs of a recurring job; equals the scheduler polling interval.
- **Second-level ticker**: like 012's ticker, but registered with a per-second cron (which Hangfire evaluates once per polling cycle) and counting the interval to the second.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every existing interval and cron test passes unchanged, except 012 AC-001.2, which this feature replaces (AC-003.2).
- **SC-002**: In the local host, an interval or 6-field cron of N seconds runs N seconds apart on a fake clock, with no drift over 100 runs (`RecurringHandlerHostTests.Execute_KeepsSecondCadence_WithoutDrift`).
- **SC-003**: In Hangfire, no configuration below the minimum throws; each one produces exactly one warning per handler when Hangfire is built (`Build`; never in `BuildDispatcherOnly`).

## Assumptions

- The polling interval and the second-level ticker cost: a second-level ticker takes the interval lock on each scheduler check (at most once per polling cycle), instead of once a minute. Whether the lock stays global or becomes per recurring job is a plan decision (012 research R-001).
- Hangfire's recurring scheduler fires a due job once per check and doesn't replay missed occurrences, so an interval or cron below the polling interval runs at most once per cycle; this is why the minimum follows the polling interval.
- Out of scope: fractions of a second; exact-second precision in Hangfire; changing Hangfire's polling interval for the app.
