# Spec: Local hosting (running handlers without Hangfire)

Status: draft
Mode: to-be (v1.3.0). Design in [plan.md](plan.md).
Packages: `ExecutionFlow.Hosting`

## Context
Some apps need scheduled work without a job server or a storage: a Windows service or a console app on a user's machine, or a one-off run started by hand. `ExecutionFlow.Hosting` runs the same recurring handlers (`IHandler`, `[Recurring]`, `FlowContext`, execution loggers) inside the .NET Generic Host, and respects the [execution plan](../execution-plan/spec.md).

Related: [recurring-jobs](../recurring-jobs/spec.md) (schedule declaration), [execution-plan](../execution-plan/spec.md), [logging-and-console](../logging-and-console/spec.md), [ADR-0008](../adr/ADR-0008-local-hosting-provider.md), [ADR-0009](../adr/ADR-0009-dependencies-by-generation.md).

## Actors
| Actor | Description |
| ----- | ----------- |
| Consumer app | A Generic Host app that calls `AddExecutionFlowHosting`. |
| Operator | Starts the app as a service or runs the plan once, and reads its logs. |

## User stories
- US-001: As a consumer app, I want my recurring handlers to run on their cron or interval inside my host, without Hangfire.
- US-002: As a consumer app, I want a dependent handler to wait for a new cycle of its prerequisites, without blocking threads, and I want a log line while it waits.
- US-003: As an operator, I want to run every handler once, in dependency order, and have the process exit when it's done.
- US-004: As a consumer app, I want to cap how many handlers run at the same time.
- US-005: As an operator, I want a failing handler to never stop the other handlers or the host.
- US-006: As an operator, I want to see at startup which handlers were scheduled, and why the others weren't.

## Requirements

### REQ-001: Registration
`services.AddExecutionFlowHosting(o => ...)` takes `HostingOptions`, which derive from `ExecutionFlowOptions` (`Scan`, `Add`, `AddLogger`, `Plan`). It validates the options and the plan when called, and registers everything the host needs.
Source: US-001

Acceptance criteria:
- AC-001.1: WHEN `AddExecutionFlowHosting` is called, THE SYSTEM SHALL register every recurring handler as scoped, the `ExecutionPlan`, and `IExecutionPlanRunner`. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_RegistersHandlersPlanAndRunner`
- AC-001.2: WHEN the plan is invalid (see execution-plan REQ-002), THE SYSTEM SHALL throw from `AddExecutionFlowHosting`. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenPlanIsInvalid`
- AC-001.3: WHEN a recurring handler has no schedule, THE SYSTEM SHALL throw from `AddExecutionFlowHosting`, naming the handler. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenHandlerHasNoSchedule`
- AC-001.4: WHEN `AddExecutionFlowHosting` is called twice on one service collection, THE SYSTEM SHALL throw. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenCalledTwice`

### REQ-002: Enabled handlers
A handler is enabled by `SetEnabled<T>(bool)`, then by the `IsEnabled` predicate, and then by `GlobalEnabled` (default `true`), in that order of precedence. Disabled handlers are registered but never scheduled.
Source: US-001, US-006

Acceptance criteria:
- AC-002.1: WHEN a handler is disabled, THE SYSTEM SHALL NOT schedule it. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_DoesNotScheduleDisabledHandler`
- AC-002.2: WHEN `SetEnabled<T>` and the `IsEnabled` predicate disagree, THE SYSTEM SHALL use `SetEnabled<T>`. Test: `HostingOptionsTests.IsHandlerEnabled_PrefersSetEnabled_OverPredicateAndGlobal`
- AC-002.3: WHEN `SetEnabled` references a type that isn't a registered recurring handler, THE SYSTEM SHALL throw from `AddExecutionFlowHosting`. Test: `ServiceCollectionExtensionsTests.AddExecutionFlowHosting_Throws_WhenSetEnabledReferencesUnknownHandler`

### REQ-003: Scheduled execution
Each enabled handler has its own scheduler, which runs it at the next occurrence of its cron (in the resolved time zone) or every `Interval`. The first run happens at the first occurrence after the host starts, never at startup itself. A handler never overlaps itself: occurrences that pass while it's running or waiting are skipped, and the next run is the next occurrence after it finishes.
Source: US-001

Acceptance criteria:
- AC-003.1: WHEN an interval handler's interval elapses, THE SYSTEM SHALL run it, and it SHALL NOT run it before then. Test: `RecurringHandlerHostTests.Execute_RunsIntervalHandler_WhenIntervalElapses`
- AC-003.2: WHEN a cron handler's next occurrence is reached, THE SYSTEM SHALL run it. Test: `RecurringHandlerHostTests.Execute_RunsCronHandler_AtNextOccurrence`
- AC-003.3: WHEN a handler runs, THE SYSTEM SHALL resolve it from a new DI scope and pass it a `FlowContext` with a job ID, attempt number 1, and the registered execution loggers. Test: `RecurringHandlerHostTests.Execute_PassesFlowContext_WithJobIdAndLoggers`
- AC-003.4: WHEN the time zone is set (`[Recurring(TimeZone)]` first, then `HostingOptions.RecurringTimeZone`, then UTC), THE SYSTEM SHALL evaluate the cron in it. Test: `RecurringHandlerHostTests.Execute_EvaluatesCron_InResolvedTimeZone`

### REQ-004: Failure isolation
Source: US-005

Acceptance criteria:
- AC-004.1: WHEN a handler throws, THE SYSTEM SHALL log the error and keep scheduling that handler and the others. Test: `RecurringHandlerHostTests.Execute_KeepsRunning_WhenHandlerThrows`
- AC-004.2: WHEN a handler throws, THE SYSTEM SHALL NOT count the run as a completed cycle for its dependents. Test: `RecurringHandlerHostTests.Execute_DoesNotSignal_WhenHandlerThrows`

### REQ-005: Dependency gate
Before each run, a dependent waits until every prerequisite has completed a cycle newer than the one it last consumed ([ADR-0009](../adr/ADR-0009-dependencies-by-generation.md)). Waiting holds no thread and no concurrency slot. While it waits, the dependent logs a warning every `DependencyWaitReportInterval` (default 1 minute), naming the prerequisite.
Source: US-002

Acceptance criteria:
- AC-005.1: WHEN a prerequisite completes a cycle, THE SYSTEM SHALL release its waiting dependents. Test: `DependencyGateTests.WaitForNextAsync_Completes_WhenPrerequisiteSignals`
- AC-005.2: WHEN the dependent has already consumed the prerequisite's latest cycle, THE SYSTEM SHALL wait for the next one. Test: `DependencyGateTests.WaitForNextAsync_Waits_WhenGenerationAlreadyConsumed`
- AC-005.3: WHEN the wait exceeds `DependencyWaitReportInterval`, THE SYSTEM SHALL report it and keep waiting. Test: `DependencyGateTests.WaitForNextAsync_ReportsWaiting_EveryInterval`
- AC-005.4: WHEN the host stops during a wait, THE SYSTEM SHALL end the wait with cancellation. Test: `DependencyGateTests.WaitForNextAsync_Throws_WhenCancelled`
- AC-005.5: WHEN a signal happens between the dependent's check and its wait, THE SYSTEM SHALL NOT lose it. Test: `DependencyGateTests.WaitForNextAsync_Completes_WhenSignalledBeforeWaiting`
- AC-005.6: WHEN a dependent runs, THE SYSTEM SHALL have run each prerequisite since the dependent's previous run. Test: `RecurringHandlerHostTests.Execute_RunsDependent_OnlyAfterNewPrerequisiteCycle`

### REQ-006: Concurrency limit
`HostingOptions.MaxConcurrency` (default `Environment.ProcessorCount`, minimum 1) caps how many handler runs execute at the same time.
Source: US-004

Acceptance criteria:
- AC-006.1: WHEN `MaxConcurrency` runs are active, THE SYSTEM SHALL make further runs wait for a free slot. Test: `ConcurrencyLimiterTests.WaitAsync_Blocks_WhenLimitReached`
- AC-006.2: WHEN `MaxConcurrency` is less than 1, THE SYSTEM SHALL throw `ArgumentOutOfRangeException` on set. Test: `HostingOptionsTests.MaxConcurrency_Throws_WhenLessThanOne`

### REQ-007: Run once
`IExecutionPlanRunner.RunOnceAsync(ct)` runs each handler once, in the plan's `ExecutionOrder`, one after the other. It skips disabled handlers, and handlers with a prerequisite that didn't complete in the same run. It returns a `RunOnceResult` with each handler's outcome (`Completed`, `Failed`, `Skipped`, with the reason). When `HostingOptions.RunOnce` is `true`, no scheduler is registered: a hosted service runs the plan once, sets `Environment.ExitCode = 1` if any handler failed, and stops the application.
Source: US-003

Acceptance criteria:
- AC-007.1: WHEN `RunOnceAsync` runs, THE SYSTEM SHALL run the handlers in `ExecutionOrder`. Test: `ExecutionPlanRunnerTests.RunOnceAsync_RunsHandlers_InExecutionOrder`
- AC-007.2: WHEN a prerequisite fails or is skipped, THE SYSTEM SHALL skip its dependents and name the prerequisite in the reason. Test: `ExecutionPlanRunnerTests.RunOnceAsync_SkipsDependents_WhenPrerequisiteFails`
- AC-007.3: WHEN a handler is disabled, THE SYSTEM SHALL skip it. Test: `ExecutionPlanRunnerTests.RunOnceAsync_SkipsDisabledHandlers`
- AC-007.4: WHEN a handler fails, THE SYSTEM SHALL keep running the handlers that don't depend on it. Test: `ExecutionPlanRunnerTests.RunOnceAsync_ContinuesWithIndependentHandlers_WhenOneFails`
- AC-007.5: WHEN `RunOnce` is `true`, THE SYSTEM SHALL run the plan once, register no schedulers, and stop the application at the end. Test: `RunOnceHostedServiceTests.StartAsync_RunsPlanAndStopsApplication`
- AC-007.6: WHEN any handler failed in run-once mode, THE SYSTEM SHALL set a non-zero exit code. Test: `RunOnceHostedServiceTests.StartAsync_SetsExitCode_WhenAHandlerFails`

### REQ-008: Startup report
Source: US-006

Acceptance criteria:
- AC-008.1: WHEN the host starts in scheduled mode, THE SYSTEM SHALL log one line per recurring handler, saying whether it was scheduled and, if not, why (disabled). Test: `StartupReportHostedServiceTests.StartAsync_LogsEachHandler_WithReason`

### REQ-009: Time source
On `net8.0`, `HostingOptions.TimeProvider` (default `TimeProvider.System`) is used for every clock read and delay. On `netstandard2.0` the system clock is used, with no time abstraction.
Source: US-001

Acceptance criteria:
- AC-009.1: WHEN a `TimeProvider` is configured (net8.0), THE SYSTEM SHALL schedule with it. Test: `RecurringHandlerHostTests.Execute_RunsIntervalHandler_WhenIntervalElapses` (uses `FakeTimeProvider`)

## Business rules
- RN-001 A handler never runs concurrently with itself.
- RN-002 A dependent's wait for its prerequisites happens before it takes a concurrency slot, so a waiting dependent never starves the other handlers.
- RN-003 The generations of the dependency gate live in memory and start at zero on every process start, so after a restart each dependent waits for a new cycle of its prerequisites.
- RN-004 Only recurring handlers run. Event handlers registered through `Scan` are ignored by this provider.

## Edge cases and errors
- A cancellation during shutdown while a handler is running: the run ends with no error log and no signal.
- A cron with no future occurrence: the scheduler logs a warning and stops for that handler.
- A time zone that isn't found on the host: `AddExecutionFlowHosting` throws.

## Non-functional requirements
- NFR-001: A waiting dependent consumes no thread-pool thread.
- NFR-002: The package builds for `netstandard2.0` and `net8.0` ([ADR-0008](../adr/ADR-0008-local-hosting-provider.md)).

## Out of scope
- Event handlers and a local `IEventDispatcher`.
- Retries of failed runs.
- Persisting generations across restarts.
- Distributing runs across several processes.
- A running-state API (`IExecutionManager`) for the local provider.

## Open questions
- None.
