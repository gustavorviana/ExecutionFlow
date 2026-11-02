# Feature Specification: Interval schedules, overlap control and the plan in the core

**Feature Branch**: `feat/execution-plan-dependencies`

**Created**: 2026-11-02

**Status**: Draft

**Input**: Decisions taken with the maintainer on 2026-11-02 while resolving the `/speckit-analyze` findings of [011-local-hosting](../011-local-hosting/spec.md) (I1, I2, U1, U2).

**Packages**: `ExecutionFlow`, `ExecutionFlow.Hangfire`

**Amends**:
- [005-recurring-jobs](../005-recurring-jobs/spec.md) FR-001, FR-002 (a schedule can be an interval), FR-009 (`UsePlan` moves to the core options), RN-004 (overlap can be turned off with a core attribute).
- [010-execution-plan](../010-execution-plan/spec.md) FR-001 (an entry can turn overlap off).

## Context

Two execution providers (Hangfire, and the local host of [011](../011-local-hosting/spec.md)) must run the same recurring handlers with the same meaning. Three things were missing or provider-specific:

1. Schedules could only be cron expressions. Handlers also need "every N minutes", including intervals that no single cron expression can express (7 min, 90 min).
2. Turning off overlapping runs of a handler was only possible with Hangfire's own `[DisableConcurrentExecution]`, so a handler author had to depend on Hangfire.
3. The execution plan was registered with `HangfireOptions.UsePlan`, so every provider would need its own copy.

This feature puts the declarations in the core (constitution principle I: the core declares, providers execute) and specifies how Hangfire implements them. The local host implements them in 011.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run a handler every N minutes (Priority: P1)

As a handler author, I want to declare that a recurring handler runs every N minutes, so that I don't have to translate intervals into cron expressions.

**Why this priority**: it is the most requested schedule after cron.

**Independent Test**: declare an interval of 15 min and of 90 min, build Hangfire, and check the registered recurring job and the ticker behavior.

**Acceptance Scenarios**: AC-001.1..AC-001.5, AC-002.1..AC-002.5.

---

### User Story 2 - Turn off overlapping runs without depending on Hangfire (Priority: P1)

As a handler author, I want to say in the core that a handler never overlaps itself, so that every provider respects it.

**Why this priority**: overlapping runs of the same handler corrupt data in many real handlers.

**Independent Test**: mark a handler, start a run, trigger another while it runs, and check that the second one waits and fails after the timeout.

**Acceptance Scenarios**: AC-003.1..AC-003.5.

---

### User Story 3 - Register the plan once, for any provider (Priority: P2)

As a consumer app, I want to register the execution plan on the core options, so that the same configuration works with Hangfire and with the local host.

**Why this priority**: removes duplication; the plan already works in Hangfire.

**Independent Test**: call `UsePlan` on a `HangfireOptions` through the base type and check that the Hangfire behavior of 005 FR-009 is unchanged.

**Acceptance Scenarios**: AC-004.1..AC-004.3.

### Edge Cases

- An interval with an exact cron equivalent is registered as that cron in Hangfire, so it is aligned to the UTC clock (every 15 min runs at :00, :15, :30, :45). The local host counts from its own start instead (011 FR-003); this difference between providers is accepted (maintainer, 2026-11-02).
- A handler that is still running when the overlap timeout expires: the waiting run fails; the running one is not affected.
- An interval handler that is a prerequisite in the plan: its completed cycles count like any other handler's.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Interval schedule declaration (core)**: a recurring handler declares either a cron expression (`[Recurring("cron")]`) or an interval with the new attribute `[RecurringInterval("hh:mm:ss")]`, never both. The interval is a `TimeSpan` string (invariant format, e.g. `"00:15:00"`, `"10:00:00"`, `"1.00:00:00"`) and must be at least 1 minute, for every provider. An interval has no time zone: it means "every N of time", not a time of day, and is always evaluated in UTC. `[RecurringInterval]` has an `Id` property with the same rule as `[Recurring]` (005 FR-007: explicit `Id`, otherwise `IJobIdGenerator`). *(Story 1)*
  - **AC-001.1**: **Given** a handler with both a cron and an interval, **When** the options are configured, **Then** it throws naming the handler. Test: `IntervalScheduleTests.Configure_Throws_WhenCronAndIntervalAreBothSet`
  - **AC-001.2**: **Given** a handler with an interval below 1 minute, **When** the options are configured, **Then** it throws naming the handler. Test: `IntervalScheduleTests.Configure_Throws_WhenIntervalBelowOneMinute`
  - **AC-001.5**: **Given** `[RecurringInterval("00:15:00", Id = "price-sync")]`, **When** the recurring ID is resolved, **Then** it is `price-sync`; without `Id`, it comes from `IJobIdGenerator`. Test: `IntervalScheduleTests.Registry_ResolvesId_LikeRecurring`
  - **AC-001.4**: **Given** an interval string that isn't a valid `TimeSpan`, **When** the options are configured, **Then** it throws naming the handler and the value. Test: `IntervalScheduleTests.Configure_Throws_WhenIntervalIsInvalid`
  - **AC-001.3**: **Given** a handler with an interval, **When** the registry is read, **Then** the registration exposes the interval and no cron. Test: `IntervalScheduleTests.Registry_ExposesInterval`

- **FR-002 Interval schedules in Hangfire**: an interval with an exact single-cron equivalent (it divides an hour evenly when below one hour, or divides a day evenly in whole hours) is registered as that cron, in UTC. Any other interval is registered as a "ticker": a recurring job with the cron `* * * * *`, whose occurrences are cancelled at creation (no job is created, nothing appears in the dashboard) until the interval has elapsed since the last run, which is kept in the job storage. The ticker counts from the start of the last run, and this isn't configurable in Hangfire (the "count from the end" option exists only in the local host, 011 FR-003). Manual triggers (005 FR-004) always run. *(Story 1)*
  - **AC-002.1**: **Given** an interval of 15 min, **When** Hangfire is built, **Then** the recurring job is registered with `*/15 * * * *`. Test: `IntervalRegistrationTests.Build_RegistersExactCron_WhenIntervalDividesTheHour`
  - **AC-002.2**: **Given** an interval of 6 h, **When** Hangfire is built, **Then** the recurring job is registered with `0 */6 * * *`. Test: `IntervalRegistrationTests.Build_RegistersExactCron_WhenIntervalDividesTheDay`
  - **AC-002.3**: **Given** an interval of 90 min, **When** Hangfire is built, **Then** the recurring job is registered with `* * * * *` as a ticker. Test: `IntervalRegistrationTests.Build_RegistersTicker_WhenNoExactCronExists`
  - **AC-002.4**: **Given** a ticker whose interval hasn't elapsed, **When** an occurrence is about to be created, **Then** it is cancelled and no job is stored; **Given** the interval has elapsed, **Then** the job is created and the last-run time is updated. Tests: `IntervalTickerTests.OnCreating_Cancels_WhenIntervalNotElapsed`, `IntervalTickerTests.OnCreating_Creates_WhenIntervalElapsed`
  - **AC-002.5**: **Given** a ticker handler, **When** it is triggered manually, **Then** it runs even if the interval hasn't elapsed. Test: `IntervalTickerTests.Trigger_Runs_WhenIntervalNotElapsed`

- **FR-003 Overlap control (core)**: a new core attribute turns off overlapping runs of a recurring handler, with a **mandatory** timeout. The plan entry can set the same option fluently, and the plan wins over the attribute (same precedence as the display name, 010 FR-003). Without either, overlapping runs are allowed (current Hangfire behavior, 005 RN-004). Dependents of the plan never overlap regardless (005 FR-012). When overlap is off, a run that starts while another run of the same handler is in progress **waits** for it; if the timeout expires first, the waiting run fails. The attribute is `[NoOverlap(timeoutSeconds: n)]` and the entry method is `ExecutionPlanEntry.NoOverlap(TimeSpan timeout)`. *(Story 2)*
  - **AC-003.1**: **Given** the attribute without a positive timeout, **When** the options are configured, **Then** it throws. Test: `OverlapOptionTests.Configure_Throws_WhenTimeoutIsNotPositive`
  - **AC-003.2**: **Given** the attribute on the handler and a different value on the plan entry, **When** the plan is built, **Then** the step exposes the plan's value. Test: `ExecutionPlannerTests.Build_PrefersEntryOverlap_OverAttribute`
  - **AC-003.3**: **Given** a Hangfire handler with overlap off and a run in progress, **When** a second run starts, **Then** it waits until the first ends and then runs. Test: `OverlapFilterTests.Perform_Waits_WhenRunInProgress`
  - **AC-003.4**: **Given** a Hangfire handler with overlap off and a run that outlasts the timeout, **When** a second run waits, **Then** the second run fails (and follows the retry rules of 005 FR-005). Test: `OverlapFilterTests.Perform_Fails_WhenTimeoutExpires`
  - **AC-003.5**: **Given** a handler without the attribute or option, **When** two runs start, **Then** they run concurrently in Hangfire. Test: `OverlapFilterTests.Perform_AllowsOverlap_ByDefault`

- **FR-004 The plan is registered on the core options**: `UsePlan(ExecutionPlan)` moves from `HangfireOptions` to `ExecutionFlowOptions`, keeping its signature, so existing Hangfire code compiles unchanged. The core only stores and validates the plan (one plan per options); each provider applies it. In Hangfire the behavior of 005 FR-009 is unchanged, including the conflict with `SetJobAutoRun`, which stays in `HangfireOptions`. *(Story 3)*
  - **AC-004.1**: **Given** options of any provider, **When** `UsePlan` is called twice, **Then** it throws. Test: `ExecutionFlowOptionsTests.UsePlan_Throws_WhenCalledTwice`
  - **AC-004.2**: **Given** `HangfireOptions`, **When** `UsePlan` is called through the `ExecutionFlowOptions` base type, **Then** the existing `ExecutionPlanHangfireTests` behavior holds. Test: `ExecutionPlanHangfireTests.UsePlan_ThroughBaseOptions_RegistersPlan`
  - **AC-004.3**: **Given** a null plan, **When** `UsePlan` is called, **Then** `ArgumentNullException` is thrown. Test: `ExecutionFlowOptionsTests.UsePlan_Throws_WhenPlanIsNull`

### Business Rules

- **RN-001**: The core only declares (schedule, overlap, plan); providers execute (constitution principle I and 011 research R-001).
- **RN-002**: `SetJobAutoRun` and `GlobalRecurringAutoRun` stay in `HangfireOptions`; the local host has its own equivalents (011 FR-002).
- **RN-003**: Ticker occurrences that don't run leave no trace in the storage besides the last-run timestamp.

### Key Entities

- **Interval schedule**: a `TimeSpan` declared on a recurring handler instead of a cron expression.
- **Overlap option**: on/off plus a mandatory timeout, from the attribute or the plan entry.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Existing Hangfire code that calls `HangfireOptions.UsePlan` compiles and passes its tests without changes (`ExecutionPlanHangfireTests`).
- **SC-002**: A ticker occurrence that doesn't run creates no Hangfire job (`IntervalTickerTests.OnCreating_Cancels_WhenIntervalNotElapsed` asserts the job count is unchanged).

## Assumptions

- **Documentation requirement** (maintainer, 2026-11-02): the README and the XML docs of `[RecurringInterval]` state that **cron (`[Recurring]`) is the preferred schedule in Hangfire**, and `[RecurringInterval]` is an alternative: exact intervals become crons anyway, and the others use the per-minute ticker (FR-002). Covered by T007.

- Out of scope: second-level intervals in Hangfire; listing ticker skips.
- Hangfire's recurring-job polling (about 15 s) plus the 1-minute ticker gives roughly 1-minute precision for ticker intervals.
