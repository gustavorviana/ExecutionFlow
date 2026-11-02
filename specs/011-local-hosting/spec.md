# Feature Specification: Local hosting (running handlers without Hangfire)

**Feature Branch**: `feat/execution-plan-dependencies`

**Created**: 2026-09-30

**Status**: Draft

**Mode**: to-be (v1.3.0). Design in [plan.md](plan.md).

**Input**: Migrated from `docs/specs/local-hosting/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC`, `RN` and `NFR` numbers kept).

**Packages**: `ExecutionFlow.Hosting`

**Amends**: none. (ADR-0008, now [research.md](research.md) R-001, amended the old constitution P-002/NFR-001 for multi-targeting; that rule is now part of [constitution](../../.specify/memory/constitution.md) principle I.)

## Context

Some apps need scheduled work without a job server or a storage: a Windows service or a console app on a user's machine, or a one-off run started by hand. `ExecutionFlow.Hosting` runs the same recurring handlers (`IHandler`, `[Recurring]`, `FlowContext`, execution loggers) inside the .NET Generic Host, and respects the [execution plan](../010-execution-plan/spec.md).

Related: [005-recurring-jobs](../005-recurring-jobs/spec.md) (schedule declaration), [010-execution-plan](../010-execution-plan/spec.md), [009-logging-and-console](../009-logging-and-console/spec.md), [research.md](research.md) R-001 (was ADR-0008), [010-execution-plan research.md](../010-execution-plan/research.md) R-003 (was ADR-0009).

**Actors**:

| Actor | Description |
| ----- | ----------- |
| Consumer app | A Generic Host app that calls `AddExecutionFlowHosting`. |
| Operator | Starts the app as a service or runs the plan once, and reads its logs. |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run recurring handlers inside my host (Priority: P1)

As a consumer app, I want my recurring handlers to run on their cron or interval inside my host, without Hangfire.

**Why this priority**: it is the reason the package exists.

**Independent Test**: register an interval handler with a `FakeTimeProvider`, advance time, and check that it ran.

**Acceptance Scenarios**: AC-001.1..AC-001.4, AC-002.2, AC-002.3, AC-003.1..AC-003.4, AC-009.1 (see Requirements).

---

### User Story 2 - Dependents wait for a new cycle of their prerequisites (Priority: P1)

As a consumer app, I want a dependent handler to wait for a new cycle of its prerequisites, without blocking threads, and I want a log line while it waits.

**Why this priority**: without it the execution plan isn't respected locally.

**Independent Test**: signal a prerequisite on the `DependencyGate` and check that the waiting dependent is released.

**Acceptance Scenarios**: AC-005.1..AC-005.6.

---

### User Story 3 - Run every handler once (Priority: P2)

As an operator, I want to run every handler once, in dependency order, and have the process exit when it's done.

**Why this priority**: an on-demand mode; the scheduled mode works without it.

**Independent Test**: call `RunOnceAsync` on a plan and check the order and outcomes.

**Acceptance Scenarios**: AC-007.1..AC-007.6.

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

**Independent Test**: make a handler throw and check that scheduling continues and no signal is sent.

**Acceptance Scenarios**: AC-004.1, AC-004.2.

---

### User Story 6 - Startup report (Priority: P3)

As an operator, I want to see at startup which handlers were scheduled, and why the others weren't.

**Why this priority**: diagnostics only.

**Independent Test**: start the host with a disabled handler and check the logged lines.

**Acceptance Scenarios**: AC-002.1, AC-008.1.

### Edge Cases

- A cancellation during shutdown while a handler is running: the run ends with no error log and no signal.
- A cron with no future occurrence: the scheduler logs a warning and stops for that handler.
- A time zone that isn't found on the host: `AddExecutionFlowHosting` throws.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Registration**: `services.AddExecutionFlowHosting(o => ...)` takes `HostingOptions`, which derive from `ExecutionFlowOptions` (`Scan`, `Add`, `AddLogger`, `Plan`). It validates the options and the plan when called, and registers everything the host needs. *(Story 1)*
  - **AC-001.1**: **Given** valid options, **When** `AddExecutionFlowHosting` is called, **Then** the system registers every recurring handler as scoped, the `ExecutionPlan`, and `IExecutionPlanRunner`. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_RegistersHandlersPlanAndRunner`
  - **AC-001.2**: **Given** an invalid plan (see [010-execution-plan](../010-execution-plan/spec.md) FR-002), **When** `AddExecutionFlowHosting` is called, **Then** the system throws. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenPlanIsInvalid`
  - **AC-001.3**: **Given** a recurring handler with no schedule, **When** `AddExecutionFlowHosting` is called, **Then** the system throws, naming the handler. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenHandlerHasNoSchedule`
  - **AC-001.4**: **Given** a service collection where `AddExecutionFlowHosting` was already called, **When** it is called again, **Then** the system throws. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenCalledTwice`

- **FR-002 Enabled handlers**: a handler is enabled by `SetEnabled<T>(bool)`, then by the `IsEnabled` predicate, and then by `GlobalEnabled` (default `true`), in that order of precedence. Disabled handlers are registered but never scheduled. *(Stories 1, 6)*
  - **AC-002.1**: **Given** a disabled handler, **When** the host starts, **Then** the system does not schedule it. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_DoesNotScheduleDisabledHandler`
  - **AC-002.2**: **Given** `SetEnabled<T>` and the `IsEnabled` predicate disagree, **When** the handler's state is resolved, **Then** the system uses `SetEnabled<T>`. Test: `HostingOptionsTests.IsHandlerEnabled_PrefersSetEnabled_OverPredicateAndGlobal`
  - **AC-002.3**: **Given** `SetEnabled` references a type that isn't a registered recurring handler, **When** `AddExecutionFlowHosting` is called, **Then** the system throws. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenSetEnabledReferencesUnknownHandler`

- **FR-003 Scheduled execution**: each enabled handler has its own scheduler, which runs it at the next occurrence of its cron (in the resolved time zone) or every `Interval`. The first run happens at the first occurrence after the host starts, never at startup itself. A handler never overlaps itself: occurrences that pass while it's running or waiting are skipped, and the next run is the next occurrence after it finishes. *(Story 1)*
  - **AC-003.1**: **Given** an interval handler, **When** its interval elapses, **Then** the system runs it, and not before then. Test: `RecurringHandlerHostTests.Execute_RunsIntervalHandler_WhenIntervalElapses`
  - **AC-003.2**: **Given** a cron handler, **When** its next occurrence is reached, **Then** the system runs it. Test: `RecurringHandlerHostTests.Execute_RunsCronHandler_AtNextOccurrence`
  - **AC-003.3**: **Given** a handler run, **When** it executes, **Then** the system resolves it from a new DI scope and passes it a `FlowContext` with a job ID, attempt number 1, and the registered execution loggers. Test: `RecurringHandlerHostTests.Execute_PassesFlowContext_WithJobIdAndLoggers`
  - **AC-003.4**: **Given** a time zone set (`[Recurring(TimeZone)]` first, then `HostingOptions.RecurringTimeZone`, then UTC), **When** the next occurrence is computed, **Then** the system evaluates the cron in it. Test: `RecurringHandlerHostTests.Execute_EvaluatesCron_InResolvedTimeZone`

- **FR-004 Failure isolation**: a failing handler never stops the other handlers or the host. (The old spec had no description text for this requirement; this line restates US-005.) *(Story 5)*
  - **AC-004.1**: **Given** a handler that throws, **When** it runs, **Then** the system logs the error and keeps scheduling that handler and the others. Test: `RecurringHandlerHostTests.Execute_KeepsRunning_WhenHandlerThrows`
  - **AC-004.2**: **Given** a handler that throws, **When** it runs, **Then** the system does not count the run as a completed cycle for its dependents. Test: `RecurringHandlerHostTests.Execute_DoesNotSignal_WhenHandlerThrows`

- **FR-005 Dependency gate**: before each run, a dependent waits until every prerequisite has completed a cycle newer than the one it last consumed ([010-execution-plan research.md](../010-execution-plan/research.md) R-003). Waiting holds no thread and no concurrency slot. While it waits, the dependent logs a warning every `DependencyWaitReportInterval` (default 1 minute), naming the prerequisite. *(Story 2)*
  - **AC-005.1**: **Given** a waiting dependent, **When** a prerequisite completes a cycle, **Then** the system releases it. Test: `DependencyGateTests.WaitForNextAsync_Completes_WhenPrerequisiteSignals`
  - **AC-005.2**: **Given** the dependent has already consumed the prerequisite's latest cycle, **When** it waits, **Then** the system waits for the next one. Test: `DependencyGateTests.WaitForNextAsync_Waits_WhenGenerationAlreadyConsumed`
  - **AC-005.3**: **Given** a waiting dependent, **When** the wait exceeds `DependencyWaitReportInterval`, **Then** the system reports it and keeps waiting. Test: `DependencyGateTests.WaitForNextAsync_ReportsWaiting_EveryInterval`
  - **AC-005.4**: **Given** a waiting dependent, **When** the host stops, **Then** the system ends the wait with cancellation. Test: `DependencyGateTests.WaitForNextAsync_Throws_WhenCancelled`
  - **AC-005.5**: **Given** a signal between the dependent's check and its wait, **When** the dependent waits, **Then** the system does not lose it. Test: `DependencyGateTests.WaitForNextAsync_Completes_WhenSignalledBeforeWaiting`
  - **AC-005.6**: **Given** a dependent with prerequisites, **When** it runs, **Then** each prerequisite has run since the dependent's previous run. Test: `RecurringHandlerHostTests.Execute_RunsDependent_OnlyAfterNewPrerequisiteCycle`

- **FR-006 Concurrency limit**: `HostingOptions.MaxConcurrency` (default `Environment.ProcessorCount`, minimum 1) caps how many handler runs execute at the same time. *(Story 4)*
  - **AC-006.1**: **Given** `MaxConcurrency` runs are active, **When** another run starts, **Then** the system makes it wait for a free slot. Test: `ConcurrencyLimiterTests.WaitAsync_Blocks_WhenLimitReached`
  - **AC-006.2**: **Given** a value less than 1, **When** `MaxConcurrency` is set, **Then** the system throws `ArgumentOutOfRangeException`. Test: `HostingOptionsTests.MaxConcurrency_Throws_WhenLessThanOne`

- **FR-007 Run once**: `IExecutionPlanRunner.RunOnceAsync(ct)` runs each handler once, in the plan's `ExecutionOrder`, one after the other. It skips disabled handlers, and handlers with a prerequisite that didn't complete in the same run. It returns a `RunOnceResult` with each handler's outcome (`Completed`, `Failed`, `Skipped`, with the reason). When `HostingOptions.RunOnce` is `true`, no scheduler is registered: a hosted service runs the plan once, sets `Environment.ExitCode = 1` if any handler failed, and stops the application. *(Story 3)*
  - **AC-007.1**: **Given** a plan, **When** `RunOnceAsync` runs, **Then** the system runs the handlers in `ExecutionOrder`. Test: `ExecutionPlanRunnerTests.RunOnceAsync_RunsHandlers_InExecutionOrder`
  - **AC-007.2**: **Given** a prerequisite that fails or is skipped, **When** `RunOnceAsync` runs, **Then** the system skips its dependents and names the prerequisite in the reason. Test: `ExecutionPlanRunnerTests.RunOnceAsync_SkipsDependents_WhenPrerequisiteFails`
  - **AC-007.3**: **Given** a disabled handler, **When** `RunOnceAsync` runs, **Then** the system skips it. Test: `ExecutionPlanRunnerTests.RunOnceAsync_SkipsDisabledHandlers`
  - **AC-007.4**: **Given** a handler that fails, **When** `RunOnceAsync` runs, **Then** the system keeps running the handlers that don't depend on it. Test: `ExecutionPlanRunnerTests.RunOnceAsync_ContinuesWithIndependentHandlers_WhenOneFails`
  - **AC-007.5**: **Given** `RunOnce` is `true`, **When** the host starts, **Then** the system runs the plan once, registers no schedulers, and stops the application at the end. Test: `RunOnceHostedServiceTests.StartAsync_RunsPlanAndStopsApplication`
  - **AC-007.6**: **Given** run-once mode, **When** any handler failed, **Then** the system sets a non-zero exit code. Test: `RunOnceHostedServiceTests.StartAsync_SetsExitCode_WhenAHandlerFails`

- **FR-008 Startup report**: at startup in scheduled mode, the host reports which handlers were scheduled and why the others weren't. (The old spec had no description text for this requirement; this line restates US-006.) *(Story 6)*
  - **AC-008.1**: **Given** scheduled mode, **When** the host starts, **Then** the system logs one line per recurring handler, saying whether it was scheduled and, if not, why (disabled). Test: `StartupReportHostedServiceTests.StartAsync_LogsEachHandler_WithReason`

- **FR-009 Time source**: on `net8.0`, `HostingOptions.TimeProvider` (default `TimeProvider.System`) is used for every clock read and delay. On `netstandard2.0` the system clock is used, with no time abstraction. *(Story 1)*
  - **AC-009.1**: **Given** a configured `TimeProvider` (net8.0), **When** handlers are scheduled, **Then** the system schedules with it. Test: `RecurringHandlerHostTests.Execute_RunsIntervalHandler_WhenIntervalElapses` (uses `FakeTimeProvider`)

### Business Rules

- **RN-001**: A handler never runs concurrently with itself.
- **RN-002**: A dependent's wait for its prerequisites happens before it takes a concurrency slot, so a waiting dependent never starves the other handlers.
- **RN-003**: The generations of the dependency gate live in memory and start at zero on every process start, so after a restart each dependent waits for a new cycle of its prerequisites.
- **RN-004**: Only recurring handlers run. Event handlers registered through `Scan` are ignored by this provider.

### Key Entities

- **HostingOptions**: `ExecutionFlowOptions` plus enabling, concurrency, run-once, time zone and time source settings.
- **IExecutionPlanRunner / RunOnceResult / HandlerRunOutcome / HandlerRunStatus**: run-once execution and its result.
- **DependencyGate** (internal): per-handler generations and waits.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** (was NFR-001): A waiting dependent consumes no thread-pool thread.
- **SC-002** (was NFR-002): The package builds for `netstandard2.0` and `net8.0` ([research.md](research.md) R-001, was ADR-0008).

## Assumptions

- Out of scope: event handlers and a local `IEventDispatcher`; retries of failed runs; persisting generations across restarts; distributing runs across several processes; a running-state API (`IExecutionManager`) for the local provider.
- Open questions: none.
- AC-001.2 references [010-execution-plan](../010-execution-plan/spec.md) FR-002 as converted from the old "execution-plan REQ-002"; plan validation errors are specified in 010 FR-004 and FR-005.
