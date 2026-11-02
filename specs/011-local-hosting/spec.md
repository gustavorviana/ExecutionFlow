# Feature Specification: Local hosting (running handlers without Hangfire)

**Feature Branch**: `feat/execution-plan-dependencies`

**Created**: 2026-09-30

**Status**: Draft (revised 2026-11-02 after `/speckit-analyze`, see Clarifications)

**Mode**: to-be (v1.3.0). Design in [plan.md](plan.md).

**Input**: Migrated from `docs/specs/local-hosting/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC`, `RN` and `NFR` numbers kept).

**Packages**: `ExecutionFlow.Hosting`

**Depends on**: [012-interval-and-overlap](../012-interval-and-overlap/spec.md) (interval schedules, the overlap attribute and `ExecutionFlowOptions.UsePlan`).

**Amends**: none. (ADR-0008, now [research.md](research.md) R-001, amended the old constitution P-002/NFR-001 for multi-targeting; that rule is now part of [constitution](../../.specify/memory/constitution.md) principle I.)

## Context

Some apps need scheduled work without a job server or a storage: a Windows service or a console app on a user's machine, or a one-off run started by hand. `ExecutionFlow.Hosting` runs the same recurring handlers (`IHandler`, `[Recurring]`, `FlowContext`, execution loggers) inside the .NET Generic Host, and respects the [execution plan](../010-execution-plan/spec.md) **with the same meaning as Hangfire** ([005-recurring-jobs](../005-recurring-jobs/spec.md) FR-009..FR-012).

Related: [005-recurring-jobs](../005-recurring-jobs/spec.md), [010-execution-plan](../010-execution-plan/spec.md), [012-interval-and-overlap](../012-interval-and-overlap/spec.md), [009-logging-and-console](../009-logging-and-console/spec.md), [research.md](research.md) R-001 (was ADR-0008), [010-execution-plan research.md](../010-execution-plan/research.md) R-003 (was ADR-0009).

**Actors**:

| Actor | Description |
| ----- | ----------- |
| Consumer app | A Generic Host app that calls `AddExecutionFlowHosting`. |
| Operator | Starts the app as a service or runs the plan once, and reads its logs. |

## Clarifications

### Session 2026-11-02

- Q: How does the local host decide whether a handler is enabled? → A: A handler in the plan uses the plan's `Enabled`. A handler registered manually or by `Scan` uses the same mechanism as Hangfire today (`SetJobAutoRun<T>` / `GlobalRecurringAutoRun`), as `HostingOptions`' own copy; setting both for the same handler throws, as in Hangfire.
- Q: Which run modes of a dependent are supported? → A: The three of 010 FR-007 (default triggered, `MinInterval`, `RunOnOwnSchedule`), with the same behavior as Hangfire. The previous "wait on its own schedule until a new cycle" behavior is removed.
- Q: Interval schedules? → A: Supported (declared in the core, 012). In the local host the default counts on a fixed cadence from the start, like a clock; an option counts from the end of the previous run.
- Q: How is the plan registered? → A: `UsePlan` on `ExecutionFlowOptions` (core, 012 FR-004).
- Q: Overlapping runs? → A: Mirror Hangfire: allowed by default; dependents never overlap; the core overlap attribute or plan option turns it off. When off, the default is to wait (like Hangfire) with a mandatory timeout; the local host has an option to skip the occurrence instead.
- Q: Things that could be documented or tested? → A: Tested.
- Q: Interval cadence in the local host? → A: From the host start (not clock-aligned); a global option and a per-handler override switch to counting from the end.
- Q: Overlap skip option? → A: Global and per handler, per handler wins.
- Q: Does run-once include handlers outside the plan? → A: Yes, every enabled handler registered with `Add` or `Scan`, after the plan steps, in registration order. A handler that is also in the plan runs once, in its plan position: the plan replaces its standalone execution.
- Q: How to make clear that the local host is isolated? → A: Documentation only (README and XML docs), no runtime log; isolation itself is specified and tested in FR-010.
- Q: Do intervals have a time zone? → A: No; an interval is "every N of time", not a time of day (012 FR-001).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run recurring handlers inside my host (Priority: P1)

As a consumer app, I want my recurring handlers to run on their cron or interval inside my host, without Hangfire.

**Why this priority**: it is the reason the package exists.

**Independent Test**: register an interval handler with a `FakeTimeProvider`, advance time, and check that it ran.

**Acceptance Scenarios**: AC-001.1, AC-001.3..AC-001.5, AC-002.2..AC-002.5, AC-003.1..AC-003.11, AC-009.1, AC-009.2, AC-010.1, AC-010.2.

---

### User Story 2 - Dependents follow the plan like in Hangfire (Priority: P1)

As a consumer app, I want a dependent handler to run according to its run mode only after a new cycle of its prerequisites, exactly as it does in Hangfire.

**Why this priority**: without it the execution plan isn't respected locally.

**Independent Test**: complete a prerequisite cycle on the `DependencyGate` and check that the dependent is triggered.

**Acceptance Scenarios**: AC-005.1..AC-005.9.

---

### User Story 3 - Run every handler once (Priority: P2)

As an operator, I want to run every handler once, in dependency order, and have the process exit when it's done.

**Why this priority**: an on-demand mode; the scheduled mode works without it.

**Independent Test**: call `RunOnceAsync` on a plan and check the order and outcomes.

**Acceptance Scenarios**: AC-007.1..AC-007.8.

---

### User Story 4 - Cap concurrent runs (Priority: P2)

As a consumer app, I want to cap how many handlers run at the same time.

**Why this priority**: resource control; a safe default exists.

**Independent Test**: take every slot of the limiter and check that a further wait blocks.

**Acceptance Scenarios**: AC-006.1, AC-006.2.

---

### User Story 5 - A failing handler never stops the others (Priority: P1)

As an operator, I want a failing handler to never stop the other handlers or the host.

**Why this priority**: one bad handler must not take down the service.

**Independent Test**: make a handler throw and check that scheduling continues and no cycle is completed.

**Acceptance Scenarios**: AC-004.1..AC-004.3.

---

### User Story 6 - Startup report (Priority: P3)

As an operator, I want to see at startup which handlers were scheduled, and why the others weren't.

**Why this priority**: diagnostics only.

**Independent Test**: start the host with a disabled handler and check the logged lines.

**Acceptance Scenarios**: AC-002.1, AC-008.1.

### Edge Cases

Each edge case is an acceptance scenario (constitution VII): shutdown during a run (AC-004.3), a cron with no future occurrence (AC-003.5), an unknown time zone (AC-001.5).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Registration**: `services.AddExecutionFlowHosting(o => ...)` takes `HostingOptions`, which derive from `ExecutionFlowOptions` (`Scan`, `Add`, `AddLogger`, and `UsePlan` from [012](../012-interval-and-overlap/spec.md) FR-004). It validates the options and the plan when called, and registers everything the host needs. *(Story 1)*
  - **AC-001.1**: **Given** valid options, **When** `AddExecutionFlowHosting` is called, **Then** the system registers every recurring handler as scoped, the `ExecutionPlan` (when one was registered), and `IExecutionPlanRunner`. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_RegistersHandlersPlanAndRunner`
  - ~~**AC-001.2**~~ (deprecated 2026-11-02: an `ExecutionPlan` only exists after `ExecutionPlanner.Build` succeeded, 010 FR-004/FR-005, so an invalid plan can't reach `AddExecutionFlowHosting`; covered by `ExecutionPlannerTests`.)
  - **AC-001.3**: **Given** a recurring handler with no schedule (neither cron nor interval), **When** `AddExecutionFlowHosting` is called, **Then** the system throws naming the handler. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenHandlerHasNoSchedule`
  - **AC-001.4**: **Given** a service collection where `AddExecutionFlowHosting` was already called, **When** it is called again, **Then** the system throws. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenCalledTwice`
  - **AC-001.5**: **Given** a time zone that isn't found on the host, **When** `AddExecutionFlowHosting` is called, **Then** the system throws naming the time zone. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenTimeZoneNotFound`

- **FR-002 Enabled handlers**: a handler in the plan is enabled by its step's `Enabled`. A handler registered by `Scan`/`Add` and not in the plan is enabled by `HostingOptions.SetJobAutoRun<T>(bool)`, then `HostingOptions.GlobalRecurringAutoRun` (default `true`), the same names and precedence as `HangfireOptions` (005 FR-003). Disabled handlers are registered but never scheduled. *(Stories 1, 6)*
  - **AC-002.1**: **Given** a disabled handler, **When** the host starts, **Then** the system does not schedule it. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_DoesNotScheduleDisabledHandler`
  - **AC-002.2**: **Given** `SetJobAutoRun<T>` and `GlobalRecurringAutoRun` disagree for a handler outside the plan, **When** its state is resolved, **Then** `SetJobAutoRun<T>` wins. Test: `HostingOptionsTests.IsHandlerEnabled_PrefersSetJobAutoRun_OverGlobal`
  - **AC-002.3**: **Given** `SetJobAutoRun` references a type that isn't a registered recurring handler, **When** `AddExecutionFlowHosting` is called, **Then** the system throws. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenSetJobAutoRunReferencesUnknownHandler`
  - **AC-002.4**: **Given** a handler in the plan that also has `SetJobAutoRun`, **When** the options are configured, **Then** the system throws, as `HangfireOptions` does. Test: `HostingOptionsTests.UsePlan_Throws_WhenHandlerAlsoHasSetJobAutoRun`
  - **AC-002.5**: **Given** a disabled step in the plan, **When** the host starts, **Then** the handler is not scheduled even if `GlobalRecurringAutoRun` is `true`. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_UsesPlanEnabled_ForPlanHandlers`

- **FR-003 Scheduled execution**: each enabled handler that runs on its own schedule (not in the plan, a root of the plan, or a dependent with `RunOnOwnSchedule`) has its own scheduler. It runs at the next occurrence of its cron (in the resolved time zone) or interval. The first run happens at the first occurrence after the host starts, never at startup itself. An **interval** counts on a fixed cadence from the host start by default (unlike Hangfire's clock-aligned exact crons, accepted in 012); `HostingOptions.IntervalFromEnd` (global, default `false`) and `SetIntervalFromEnd<T>(bool)` (per handler, wins over the global one) switch to counting from the end of the previous run. **Overlap** mirrors Hangfire (005 RN-004, 012 FR-003): runs of the same handler may overlap unless overlap is turned off; when off, a new run waits for the running one, up to the mandatory timeout, after which the waiting run fails (logged as an error, the host continues). `HostingOptions.SkipWhenOverlapping` (global, default `false`) and `SetSkipWhenOverlapping<T>(bool)` (per handler, wins over the global one) switch waiting to skipping the occurrence. *(Story 1)*
  - **AC-003.1**: **Given** an interval handler, **When** its interval elapses, **Then** the system runs it, and not before then. Test: `RecurringHandlerHostTests.Execute_RunsIntervalHandler_WhenIntervalElapses`
  - **AC-003.2**: **Given** a cron handler, **When** its next occurrence is reached, **Then** the system runs it. Test: `RecurringHandlerHostTests.Execute_RunsCronHandler_AtNextOccurrence`
  - **AC-003.3**: **Given** a handler run, **When** it executes, **Then** the system resolves it from a new DI scope and passes it a `FlowContext` with a job ID, attempt number 1, and the registered execution loggers. Test: `RecurringHandlerHostTests.Execute_PassesFlowContext_WithJobIdAndLoggers`
  - **AC-003.4**: **Given** a time zone set (`[Recurring(TimeZone)]` first, then `HostingOptions.RecurringTimeZone`, then UTC), **When** the next occurrence is computed, **Then** the system evaluates the cron in it. Test: `RecurringHandlerHostTests.Execute_EvaluatesCron_InResolvedTimeZone`
  - **AC-003.5**: **Given** a cron with no future occurrence, **When** the scheduler computes the next one, **Then** it logs a warning and stops for that handler only. Test: `RecurringHandlerHostTests.Execute_StopsAndWarns_WhenCronHasNoNextOccurrence`
  - **AC-003.6**: **Given** an interval handler whose run outlasts the interval (default cadence), **When** the next occurrence arrives, **Then** it starts on the fixed cadence (overlapping, since overlap is allowed by default). Test: `RecurringHandlerHostTests.Execute_KeepsFixedCadence_ForIntervalByDefault`
  - **AC-003.7**: **Given** an interval handler set to count from the end, **When** a run ends, **Then** the next run is the interval after that end. Test: `RecurringHandlerHostTests.Execute_CountsFromEnd_WhenConfigured`
  - **AC-003.8**: **Given** overlap off and a run in progress, **When** the next occurrence arrives, **Then** the new run waits and starts when the first one ends. Test: `RecurringHandlerHostTests.Execute_WaitsForRunningRun_WhenOverlapOff`
  - **AC-003.9**: **Given** overlap off and a run that outlasts the timeout, **When** the waiting run's timeout expires, **Then** it fails with an error log and the scheduler continues. Test: `RecurringHandlerHostTests.Execute_FailsWaitingRun_WhenOverlapTimeoutExpires`
  - **AC-003.11**: **Given** `IntervalFromEnd = true` and `SetIntervalFromEnd<T>(false)`, **When** `T`'s next run is computed, **Then** the per-handler value wins; the same holds for `SkipWhenOverlapping`. Test: `HostingOptionsTests.PerHandlerSettings_WinOverGlobal`
  - **AC-003.10**: **Given** overlap off and the skip option, **When** an occurrence arrives during a run, **Then** it is skipped. Test: `RecurringHandlerHostTests.Execute_SkipsOccurrence_WhenOverlapOffAndSkipConfigured`

- **FR-004 Failure isolation**: a failing handler never stops the other handlers or the host. *(Story 5)*
  - **AC-004.1**: **Given** a handler that throws, **When** it runs, **Then** the system logs the error and keeps scheduling that handler and the others. Test: `RecurringHandlerHostTests.Execute_KeepsRunning_WhenHandlerThrows`
  - **AC-004.2**: **Given** a handler that throws, **When** it runs, **Then** the system does not count the run as a completed cycle for its dependents. Test: `RecurringHandlerHostTests.Execute_DoesNotCompleteCycle_WhenHandlerThrows`
  - **AC-004.3**: **Given** the host stops while a handler is running, **When** the run is cancelled, **Then** it ends with no error log and no completed cycle. Test: `RecurringHandlerHostTests.Execute_EndsQuietly_WhenHostStopsDuringRun`

- **FR-005 Execution plan**: the local host enforces the generation rule ([010 research.md](../010-execution-plan/research.md) R-003) in memory, and the run modes of [010-execution-plan](../010-execution-plan/spec.md) FR-007 with the same behavior as Hangfire (005 FR-010, FR-012): **default** — the dependent has no schedule and is triggered when its last prerequisite completes a new cycle; **`MinInterval`** — the same, but a trigger earlier than the interval after the previous run's end is postponed to the end of the interval, not lost; **`RunOnOwnSchedule`** — the dependent keeps its schedule and each occurrence without a new cycle of every prerequisite is skipped with a log line naming the pending prerequisites. A dependent never overlaps itself: a dependent that is running isn't triggered, and re-checks when it finishes. *(Story 2)*
  - **AC-005.1**: **Given** a dependent in default mode, **When** its only prerequisite completes a cycle, **Then** the dependent is triggered. Test: `DependencyGateTests.Complete_TriggersDependent_WhenAllPrerequisitesHaveNewCycles`
  - **AC-005.2**: **Given** a dependent that already consumed the latest cycle, **When** it is checked, **Then** it is not triggered. Test: `DependencyGateTests.Complete_DoesNotTrigger_WhenGenerationAlreadyConsumed`
  - **AC-005.3**: **Given** a dependent of two prerequisites, **When** only one completes, **Then** it is not triggered; **When** the second completes, **Then** it is. Test: `DependencyGateTests.Complete_TriggersAfterLastPrerequisite`
  - **AC-005.4**: **Given** `MinInterval` and a trigger earlier than the interval, **When** it arrives, **Then** the run is postponed to the end of the interval. Test: `RecurringHandlerHostTests.Dependent_PostponesTrigger_WhenMinIntervalNotElapsed`
  - **AC-005.5**: **Given** `RunOnOwnSchedule` and no new cycle, **When** an occurrence arrives, **Then** it is skipped with a log naming the pending prerequisites. Test: `RecurringHandlerHostTests.Dependent_SkipsOccurrence_WhenPrerequisitesNotMet`
  - **AC-005.6**: **Given** a dependent with prerequisites, **When** it runs, **Then** each prerequisite has completed a cycle since the dependent's previous run. Test: `RecurringHandlerHostTests.Execute_RunsDependent_OnlyAfterNewPrerequisiteCycle`
  - **AC-005.7**: **Given** a running dependent, **When** a prerequisite completes, **Then** it is not triggered again until it finishes, and re-checks then. Test: `RecurringHandlerHostTests.Dependent_RechecksAfterRun_InsteadOfOverlapping`
  - **AC-005.8**: **Given** two prerequisites completing at the same time, **When** the dependent is checked, **Then** it is triggered exactly once. Test: `DependencyGateTests.Complete_TriggersOnce_WhenPrerequisitesCompleteConcurrently`
  - **AC-005.9**: **Given** the host stops, **When** a postponed or pending trigger exists, **Then** it is cancelled without error. Test: `RecurringHandlerHostTests.Dependent_CancelsPendingTrigger_WhenHostStops`

- **FR-006 Concurrency limit**: `HostingOptions.MaxConcurrency` (default `Environment.ProcessorCount`, minimum 1) caps how many handler runs execute at the same time. *(Story 4)*
  - **AC-006.1**: **Given** `MaxConcurrency` runs are active, **When** another run starts, **Then** the system makes it wait for a free slot. Test: `ConcurrencyLimiterTests.WaitAsync_Blocks_WhenLimitReached`
  - **AC-006.2**: **Given** a value less than 1, **When** `MaxConcurrency` is set, **Then** the system throws `ArgumentOutOfRangeException`. Test: `HostingOptionsTests.MaxConcurrency_Throws_WhenLessThanOne`

- **FR-007 Run once**: `IExecutionPlanRunner.RunOnceAsync(ct)` runs each enabled handler once, one after the other, in the order of the plan's `Steps` (handlers not in the plan run after them, in registration order). It skips disabled handlers, and handlers with a prerequisite that didn't complete in the same run. It returns a `RunOnceResult` with each handler's outcome (`Completed`, `Failed`, `Skipped`, with the reason). When `HostingOptions.RunOnce` is `true`, no scheduler is registered: a hosted service runs once, sets `Environment.ExitCode = 1` if any handler failed, and stops the application. *(Story 3)*
  - **AC-007.1**: **Given** a plan, **When** `RunOnceAsync` runs, **Then** the system runs the handlers in the order of `Steps`. Test: `ExecutionPlanRunnerTests.RunOnceAsync_RunsHandlers_InStepOrder`
  - **AC-007.2**: **Given** a prerequisite that fails or is skipped, **When** `RunOnceAsync` runs, **Then** the system skips its dependents and names the prerequisite in the reason. Test: `ExecutionPlanRunnerTests.RunOnceAsync_SkipsDependents_WhenPrerequisiteFails`
  - **AC-007.3**: **Given** a disabled handler, **When** `RunOnceAsync` runs, **Then** the system skips it. Test: `ExecutionPlanRunnerTests.RunOnceAsync_SkipsDisabledHandlers`
  - **AC-007.4**: **Given** a handler that fails, **When** `RunOnceAsync` runs, **Then** the system keeps running the handlers that don't depend on it. Test: `ExecutionPlanRunnerTests.RunOnceAsync_ContinuesWithIndependentHandlers_WhenOneFails`
  - **AC-007.5**: **Given** `RunOnce` is `true`, **When** the host starts, **Then** the system runs once, registers no schedulers, and stops the application at the end. Test: `RunOnceHostedServiceTests.StartAsync_RunsPlanAndStopsApplication`
  - **AC-007.6**: **Given** run-once mode, **When** any handler failed, **Then** the system sets a non-zero exit code. Test: `RunOnceHostedServiceTests.StartAsync_SetsExitCode_WhenAHandlerFails`

  - **AC-007.7**: **Given** enabled handlers outside the plan (registered with `Add` or `Scan`), **When** `RunOnceAsync` runs, **Then** they run after every plan step, in registration order; disabled ones are `Skipped` with the reason. Test: `ExecutionPlanRunnerTests.RunOnceAsync_RunsHandlersOutsidePlan_AfterPlanInRegistrationOrder`
  - **AC-007.8**: **Given** a handler that is in the plan and also registered with `Add` or `Scan`, **When** `RunOnceAsync` runs, **Then** it runs once, in its plan position, governed by the plan. Test: `ExecutionPlanRunnerTests.RunOnceAsync_RunsPlanHandlerOnce_WhenAlsoRegistered`

- **FR-008 Startup report**: at startup in scheduled mode, the host logs one line per recurring handler, saying whether it was scheduled and, if not, why. *(Story 6)*
  - **AC-008.1**: **Given** scheduled mode, **When** the host starts, **Then** the system logs one line per recurring handler, saying whether it was scheduled and, if not, why (disabled in the plan, `SetJobAutoRun` off, or skipped by the plan's `SkipDependents`). Test: `StartupReportHostedServiceTests.StartAsync_LogsEachHandler_WithReason`

- **FR-009 Time source**: on `net8.0`, `HostingOptions.TimeProvider` (default `TimeProvider.System`) is used for every clock read and delay. On `netstandard2.0` an internal system clock is used. *(Story 1)*
  - **AC-009.1**: **Given** a configured `TimeProvider` (net8.0), **When** handlers are scheduled, **Then** the system schedules with it. Test: `RecurringHandlerHostTests.Execute_UsesConfiguredTimeProvider`
  - **AC-009.2**: **Given** the `netstandard2.0` system clock, **When** a delay and a timed wait are requested, **Then** they complete after the delay and honor cancellation. Test: `SystemClockTests.DelayAndWait_CompleteAndHonorCancellation` (runs against the `netstandard2.0` asset)

- **FR-010 Local, isolated execution**: the local host coordinates nothing outside itself. Two hosts in different applications or processes don't communicate (unlike Hangfire, whose servers share a storage), and two hosts in the same process (separate service collections) don't either: each has its own generations, overlap guards, concurrency limiter and schedulers. Registering the same handler in two hosts runs it in both. *(Story 1)*
  - **AC-010.1**: **Given** two hosts in the same process with the same plan, **When** a prerequisite completes in one host, **Then** the dependent in the other host is not triggered. Test: `HostIsolationTests.TwoHosts_DoNotShareGenerations`
  - **AC-010.2**: **Given** two hosts in the same process with the same handler and overlap off, **When** both run it at the same time, **Then** neither waits for the other. Test: `HostIsolationTests.TwoHosts_DoNotShareOverlapGuards`

### Business Rules

- ~~**RN-001**~~ (deprecated 2026-11-02: "a handler never runs concurrently with itself" is replaced by the Hangfire-mirrored overlap rule in FR-003; dependents still never overlap, FR-005.)
- ~~**RN-002**~~ (deprecated 2026-11-02: dependents no longer wait on their schedule; they are triggered, FR-005. Any run waiting for a slot or for overlap holds no thread, SC-001.)
- **RN-003**: The generations of the plan live in memory and start at zero on every process start, so after a restart each dependent waits for a new cycle of its prerequisites.
- **RN-004**: Only recurring handlers run. Event handlers registered through `Scan` are ignored by this provider.

### Key Entities

- **HostingOptions**: `ExecutionFlowOptions` plus `SetJobAutoRun`/`GlobalRecurringAutoRun`, concurrency, run-once, time zone, time source, interval counting and overlap-skip settings.
- **IExecutionPlanRunner / RunOnceResult / HandlerRunOutcome / HandlerRunStatus**: run-once execution and its result.
- **DependencyGate** (internal): per-handler generations, consumed generations and triggers.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** (was NFR-001, reworded 2026-11-02): A run waiting for a concurrency slot, for an overlapping run, or for a postponed trigger holds no thread-pool thread. Test: `RecurringHandlerHostTests.WaitingRuns_HoldNoThreads` (100 waiting runs; asserts `ThreadPool.ThreadCount` doesn't grow by more than a small constant).
- **SC-002** (was NFR-002): The package builds for `netstandard2.0` and `net8.0` ([research.md](research.md) R-001), and the tests run against both assets.

## Assumptions

- **Documentation requirement** (maintainer, 2026-11-02): the README and the XML docs of `AddExecutionFlowHosting` and `HostingOptions` state clearly that the local host runs **locally and isolated** (FR-010): no communication between hosts in different applications, and no orchestration between two registrations in the same process. Documentation only, no runtime log. Covered by T007.
- Out of scope: event handlers and a local `IEventDispatcher`; retries of failed runs; persisting generations across restarts; distributing runs across several processes; a running-state API (`IExecutionManager`) for the local provider; a manual trigger API for the local provider.
