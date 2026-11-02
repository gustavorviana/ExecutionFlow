# Feature Specification: Recurring jobs

**Feature Branch**: N/A (migrated)

**Created**: 2026-09-30

**Status**: Implemented (v1.2.0); FR-009..FR-012 implemented in v1.3.0 (unreleased), in review

**Mode**: to-be (v1.2.0), based on the as-is mapping (see the findings in [research.md](research.md)). Design in [plan.md](plan.md).

**Input**: Migrated from `docs/specs/recurring-jobs/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC`, `RN` numbers kept).

**Packages**: `ExecutionFlow` (`IHandler`, `RecurringAttribute`, registration), `ExecutionFlow.Hangfire` (scheduling, triggers, orphan cleanup)

**Amends**: none. FR-009..FR-012 implement, in Hangfire, the plan specified in [010-execution-plan](../010-execution-plan/spec.md).

## Context

Some work runs on a schedule rather than in response to an event: nightly reports, periodic syncs, cleanups. A recurring handler declares its schedule once. ExecutionFlow registers it with Hangfire when the consumer starts, lets operators turn its schedule on or off, and lets code or the dashboard trigger it on demand.

Related features:
- [001-handlers-and-context](../001-handlers-and-context/spec.md): `IHandler` and `FlowContext`.
- [002-setup-and-configuration](../002-setup-and-configuration/spec.md): `Configure`, `Scan`, and `Build`.
- [008-job-naming-and-dashboard](../008-job-naming-and-dashboard/spec.md): recurring job names ([research.md](../008-job-naming-and-dashboard/research.md), was ADR-0006).
- [007-execution-manager](../007-execution-manager/spec.md): `IsRunning(Type)`, `Cancel(Type)`, `Retry(Type)`.

**Actors**:

| Actor | Description |
| ----- | ----------- |
| Handler author | Writes an `IHandler` class and declares its schedule. |
| Consumer app | Calls `Build()`, which registers the recurring jobs, and runs the Hangfire server. |
| Operator | Turns schedules on or off, and triggers handlers from code or the dashboard. |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Declare a cron schedule on a handler (Priority: P1)

As a handler author, I want to declare a cron schedule on a handler class, so that it runs periodically without extra wiring.

**Why this priority**: it is the core of the feature.

**Independent Test**: add an `IHandler` with `[Recurring("cron")]`, call `Build()`, and check the Hangfire recurring job.

**Acceptance Scenarios**: AC-001.1..AC-001.3, AC-002.1, AC-002.2, AC-005.1.

---

### User Story 2 - Turn a schedule off (Priority: P2)

As an operator, I want to turn a recurring handler's schedule off without removing it from the code.

**Why this priority**: operational control; schedules work without it.

**Independent Test**: set `SetJobAutoRun<T>(false)` and check the job is registered with `Cron.Never()`.

**Acceptance Scenarios**: AC-003.1..AC-003.4.

---

### User Story 3 - Trigger on demand (Priority: P2)

As an operator, I want to trigger a recurring handler on demand, even when its schedule is off.

**Why this priority**: needed for operations and for disabled handlers.

**Independent Test**: call `IRecurringTrigger.Trigger(Type)` and check the job triggered with the resolved ID.

**Acceptance Scenarios**: AC-004.1, AC-004.2 (AC-004.3 deprecated).

---

### User Story 4 - Clean up orphans safely (Priority: P3)

As a consumer app, I want recurring jobs that were removed from the code to be cleaned up from storage, without touching jobs that aren't mine.

**Why this priority**: opt-in housekeeping.

**Independent Test**: seed storage with ExecutionFlow and native jobs, enable `RemoveOrphanRecurringJobs`, and call `Build()`.

**Acceptance Scenarios**: AC-006.1, AC-006.2.

---

### User Story 5 - Stable job ID (Priority: P2)

As a handler author, I want a stable job ID, so that renaming the class doesn't create a new job.

**Why this priority**: avoids orphaned jobs on refactors (F-003).

**Independent Test**: set `[Recurring(Id = "...")]` and check the registered ID.

**Acceptance Scenarios**: AC-007.1..AC-007.3.

---

### User Story 6 - Time zone (Priority: P3)

As a handler author, I want the schedule evaluated in a given time zone.

**Why this priority**: UTC is a usable default.

**Independent Test**: configure time zones at each level and check the one passed to Hangfire.

**Acceptance Scenarios**: AC-008.1..AC-008.3.

---

### User Story 7 - Execution plan in Hangfire (Priority: P1)

From [010-execution-plan](../010-execution-plan/spec.md) User Stories 1 and 3: dependencies between recurring handlers are enforced by Hangfire at run time (v1.3.0).

**Why this priority**: without it the plan is not enforced.

**Independent Test**: `UsePlan` with a dependent, complete the prerequisite, and check the dependent is triggered and gated.

**Acceptance Scenarios**: AC-009.1..AC-009.5, AC-010.1..AC-010.7, AC-011.1, AC-012.1..AC-012.7.

### Edge Cases

- An invalid cron expression: Hangfire throws when `Build()` runs. [INFERRED] Not tested.
- Two consumers with different handler sets on one storage: each `Build()` adds or updates its own jobs. With `RemoveOrphanRecurringJobs`, each removes the other's ExecutionFlow jobs, but never non-ExecutionFlow jobs.
- A time zone ID that's valid on one host but not another (Windows IDs vs IANA on .NET Framework): `Configure` fails on that host.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Declare a recurring handler**: a class that implements `IHandler` (not `IHandler<TEvent>`) is a recurring handler. `[Recurring("cron")]` sets its schedule, and `[DisplayName]` sets its name. *(Story 1)*
  - *Amended by [012-interval-and-overlap](../012-interval-and-overlap/spec.md) FR-001: a schedule can also be an interval.*
  - **AC-001.1**: **Given** a class implementing `IHandler`, **When** it is added or scanned, **Then** it is registered as a recurring handler, with the cron, `Id` and `TimeZone` from `[Recurring]` and the display name from `[DisplayName]` (defaulting to the class name). Tests: `ExecutionFlowOptionsTests.Add_RecurringHandler_Populates_RecurringHandlers`, `HangfireSetupTests.Configure_Registers_RecurringHandler`, `RecurringValidationTests.Add_CopiesIdAndTimeZoneFromAttribute_ToRegistry`
  - **AC-001.2**: **Given** a class that implements both `IHandler` and `IHandler<TEvent>`, **When** it is registered, **Then** `InvalidOperationException` is thrown. Test: `ValidationTests.Add_Throws_WhenTypeImplementsBothIHandlerAndGenericIHandler`
  - **AC-001.3**: **Given** a recurring handler with no `[Recurring]` (or an empty cron), **When** `Configure` runs, **Then** `InvalidOperationException` is thrown, naming the handler and saying `[Recurring]` is required. Test: `RecurringValidationTests.Configure_Throws_WhenRecurringHandlerHasNoCron`

- **FR-002 Registration with Hangfire**: on `Build()`, each recurring handler is added or updated with `RecurringJobManager.AddOrUpdate`, using its resolved ID (FR-007), its cron (or `Cron.Never()`, see FR-003), and its resolved time zone (FR-008). *(Story 1)*
  - *Amended by [012-interval-and-overlap](../012-interval-and-overlap/spec.md) FR-002: an interval is registered as an exact cron or as a ticker.*
  - **AC-002.1**: **Given** registered recurring handlers, **When** `Build()` runs, **Then** one Hangfire recurring job is added or updated per handler. Test: `RecurringRegistrationTests.Build_UsesFullNameAsId_WhenNoExplicitId`
  - **AC-002.2**: **Given** a producer-only setup, **When** `BuildDispatcherOnly()` runs, **Then** no recurring jobs are registered (constitution IV, was P-005). Test: `ServiceCollectionExtensionsTests.DispatcherOnly_DoesNotRegister_IRecurringTrigger` (indirect)

- **FR-003 Schedule on or off**: `HangfireOptions.GlobalRecurringAutoRun` (default `true`) and `SetJobAutoRun<T>(bool)` decide whether a handler runs on its schedule. A per-handler setting wins over the global one. A handler that doesn't auto-run is registered with `Cron.Never()`: it stays visible in the dashboard and runs only when triggered. This applies to the whole storage, and the last `Build()` wins. *(Story 2)*
  - *Amended by [014-handler-configuration](../014-handler-configuration/spec.md) FR-002: for a handler in the plan, the builder's `Enabled` wins over `SetJobAutoRun<T>` (with a trace warning); without it, the option applies.*
  - **AC-003.1**: **Given** auto-run off for a handler (per-handler, or global with no per-handler value), **When** `Build()` runs, **Then** it is registered with `Cron.Never()`. Tests: `RecurringRegistrationTests.Build_RegistersCronNever_WhenAutoRunIsOff`, `RecurringRegistrationTests.Build_RegistersCronNever_WhenGlobalAutoRunIsOff`
  - **AC-003.2**: **Given** the per-handler value `true` and the global `false`, **When** `Build()` runs, **Then** the handler's cron is registered. Test: `RecurringRegistrationTests.Build_PerHandlerAutoRun_OverridesGlobal`
  - **AC-003.3**: **Given** `SetJobAutoRun` referencing a type that isn't a registered recurring handler, **When** `Configure` runs, **Then** it throws. Test: `HangfireOptionsTests.SetJobAutoRun_InvalidHandler_Throws_OnConfigure`
  - **AC-003.4**: **Given** any setup, **When** `Build()` runs, **Then** the obsolete `HangfireAutoRunFilter` is not registered. Test: `RecurringRegistrationTests.Build_DoesNotRegisterAutoRunFilter`

- **FR-004 Manual trigger**: `IRecurringTrigger.Trigger(Type)` and `Trigger(string jobId)` run a recurring job now, whether or not it auto-runs. *(Story 3)*
  - **AC-004.1**: **Given** a registered recurring handler, **When** `Trigger(Type)` is called, **Then** the job is triggered with the handler's resolved ID (FR-007); **Given** a null or unregistered type, **Then** it throws. Tests: `RecurringRegistrationTests.Trigger_UsesExplicitId`, `HangfireValidationTests.HangfireDispatcher_Trigger_Throws_ForNullType`, `HangfireValidationTests.HangfireDispatcher_Trigger_Throws_ForUnregisteredType`
  - **AC-004.2**: **Given** a null or empty ID, **When** `Trigger(string)` is called, **Then** `ArgumentNullException` is thrown. Any other ID is passed to Hangfire unchecked. Test: `HangfireValidationTests.HangfireDispatcher_TriggerById_Throws_ForNullOrEmptyId`
  - ~~**AC-004.3**~~ (deprecated: manual triggers are no longer detected by text, because disabled handlers never fire automatically. See FR-003 and F-004. 2026-09-30)

- **FR-005 Retries for recurring jobs**: with `HangfireOptions.DisableRecurringRetries` (default `true`), recurring jobs get `AutomaticRetry(Attempts = 0)`, unless the handler declares its own `[AutomaticRetry]`. *(Story 1)*
  - **AC-005.1**: **Given** the option on (default), off, or a handler with its own `[AutomaticRetry]`, **When** filters are provided for a job, **Then** recurring jobs get retries disabled only when the option is on and the handler declares none, and event jobs are unaffected. Tests: `HandlerJobFilterProviderTests.DisableRecurringRetries_DefaultsToTrue`, `HandlerJobFilterProviderTests.RecurringJob_GetsRetryDisabled_WhenDisableRecurringRetriesIsTrue`, `HandlerJobFilterProviderTests.RecurringJob_DoesNotGetRetryDisabled_WhenDisableRecurringRetriesIsFalse`, `HandlerJobFilterProviderTests.RecurringJob_WithCustomRetryAttribute_IsNotOverridden`, `HandlerJobFilterProviderTests.EventJob_IsNotAffected_ByDisableRecurringRetries`

- **FR-006 Orphan cleanup**: with `HangfireOptions.RemoveOrphanRecurringJobs` (default `false`), `Build()` removes ExecutionFlow recurring jobs (method `HangfireJobDispatcher.DispatchRecurringAsync`) whose ID isn't registered. *(Story 4)*
  - **AC-006.1**: **Given** the option on, **When** `Build()` runs, **Then** only ExecutionFlow recurring jobs that aren't registered are removed; native Hangfire recurring jobs, other applications' jobs, and jobs that fail to load are left untouched. Test: `RecurringRegistrationTests.Build_RemovesOnlyExecutionFlowOrphans`
  - **AC-006.2**: **Given** the option off (default), **When** `Build()` runs, **Then** nothing is removed. Tests: `RecurringRegistrationTests.Build_KeepsOrphans_WhenOptionIsOff`, `HangfireOptionsTests.RemoveOrphanRecurringJobs_DefaultsToFalse`

- **FR-007 Recurring job ID**: the ID is `[Recurring(Id = "...")]` if set, otherwise `IJobIdGenerator.GenerateId(handlerType)` (by default `handlerType.FullName`). `IJobIdGenerator` is only used for recurring jobs. One resolver serves both registration and `Trigger(Type)`. *(Story 5)*
  - **AC-007.1**: **Given** `Id` set, **When** `Build()` runs, **Then** it is used, even when a custom `IJobIdGenerator` is configured. Test: `RecurringRegistrationTests.Build_UsesExplicitId_OverIdGenerator`
  - **AC-007.2**: **Given** no `Id`, **When** `Build()` runs, **Then** the configured generator is used. Tests: `RecurringRegistrationTests.Build_UsesIdGenerator_WhenNoExplicitId`, `RecurringRegistrationTests.Build_UsesFullNameAsId_WhenNoExplicitId`, `DefaultRecurringServiceIdGeneratorTests.GenerateId_ReturnsFullName`
  - **AC-007.3**: **Given** two handlers that resolve to the same ID, **When** `Build()` runs, **Then** `InvalidOperationException` is thrown. Test: `RecurringRegistrationTests.Build_Throws_WhenTwoHandlersResolveToTheSameId`

- **FR-008 Time zone**: the cron is evaluated in the resolved time zone. Precedence: `HangfireOptions.SetJobTimeZone<T>` > `[Recurring(TimeZone = "...")]` > `HangfireOptions.RecurringTimeZone` > UTC. IDs are validated at `Configure` with `TimeZoneInfo.FindSystemTimeZoneById`. *(Story 6)*
  - *Amended by [014-handler-configuration](../014-handler-configuration/spec.md) FR-002: the plan builder's `TimeZone` wins over `SetJobTimeZone<T>` (with a trace warning).*
  - **AC-008.1**: **Given** time zones configured at different levels, **When** `Build()` runs, **Then** they are resolved in that order of precedence. Tests: `RecurringRegistrationTests.Build_UsesUtc_ByDefault`, `RecurringRegistrationTests.Build_UsesGlobalTimeZone_WhenNothingMoreSpecific`, `RecurringRegistrationTests.Build_UsesAttributeTimeZone_OverGlobal`, `RecurringRegistrationTests.Build_UsesOptionTimeZone_OverAttribute`
  - **AC-008.2**: **Given** a configured time zone ID that doesn't exist on the host, **When** `Configure` runs, **Then** `InvalidOperationException` is thrown, naming the ID. Tests: `RecurringValidationTests.Configure_Throws_WhenGlobalTimeZoneIsUnknown`, `RecurringValidationTests.Configure_Throws_WhenAttributeTimeZoneIsUnknown`, `RecurringValidationTests.Configure_Throws_WhenOptionTimeZoneIsUnknown`, `RecurringValidationTests.Configure_Accepts_ValidTimeZones`
  - **AC-008.3**: **Given** `SetJobTimeZone` referencing a type that isn't a registered recurring handler, **When** `Configure` runs, **Then** it throws. Test: `RecurringValidationTests.Configure_Throws_WhenSetJobTimeZoneReferencesUnregisteredHandler`

- **FR-009 Execution plan in Hangfire (v1.3.0)**: `HangfireOptions.UsePlan(ExecutionPlan)` registers every handler of an [execution plan](../010-execution-plan/spec.md) as a recurring handler right away, as `Add` does. A step's `Enabled` becomes that handler's auto-run (FR-003). A handler that isn't in the plan behaves as before. Source: 010-execution-plan User Stories 1 and 3. *(Story 7)*
  - *Amended by [012-interval-and-overlap](../012-interval-and-overlap/spec.md) FR-004: `UsePlan` moves to `ExecutionFlowOptions`, same signature and Hangfire behavior.*
  - *Amended by [014-handler-configuration](../014-handler-configuration/spec.md) FR-002: a handler in the plan with `SetJobAutoRun` no longer throws; the builder decides when it sets `Enabled`, else the option.*
  - **AC-009.1**: **Given** a plan, **When** `UsePlan` is called, **Then** every step's handler is registered as a recurring handler, even when it wasn't scanned. Test: `ExecutionPlanHangfireTests.UsePlan_RegistersPlanHandlers`
  - **AC-009.2**: **Given** a disabled step, **When** `UsePlan` is called, **Then** it is treated as `SetJobAutoRun(false)`; an enabled step auto-runs, whatever `GlobalRecurringAutoRun` says. Test: `ExecutionPlanHangfireTests.UsePlan_SetsAutoRunFromStepEnabled`
  - **AC-009.3**: **Given** a handler in the plan that also gets `SetJobAutoRun`, before or after `UsePlan`, **When** it is configured, **Then** `InvalidOperationException` is thrown. Tests: `ExecutionPlanHangfireTests.UsePlan_Throws_WhenHandlerAlreadyHasSetJobAutoRun`, `ExecutionPlanHangfireTests.SetJobAutoRun_Throws_WhenHandlerIsInPlan`
  - **AC-009.4**: **Given** `UsePlan` already called, **When** it is called again, **Then** `InvalidOperationException` is thrown. Test: `ExecutionPlanHangfireTests.UsePlan_Throws_WhenCalledTwice`
  - **AC-009.5**: **Given** a step whose display name differs from the class name, **When** the job is named, **Then** the display name is used. Test: `ExecutionPlanHangfireTests.GetName_UsesPlanDisplayName`

- **FR-010 Prerequisite gate (v1.3.0)**: Hangfire enforces the generation rule ([010 research.md](../010-execution-plan/research.md) R-003, was ADR-0009), keeping the generations in the job storage, under a distributed lock. Source: 010-execution-plan User Story 1. *(Story 7)*
  - When a recurring handler that is a prerequisite returns without throwing, its generation is incremented.
  - A dependent (a plan step with prerequisites) has no schedule of its own: it's registered with `Cron.Never()`, and its `[Recurring]` is optional. A cron on it is ignored, with a warning at `Configure`.
  - When a prerequisite completes, each enabled dependent of it whose prerequisites **all** have a generation newer than the one it last consumed is triggered (`RecurringJob.TriggerJob`). In a chain `C → B → A`, C triggers B and B triggers A. A dependent of several prerequisites is triggered by the last one to complete.
  - When a new job of a dependent is about to be enqueued (triggered by the plan, or by its schedule in `RunOnOwnSchedule`; manual runs skip the gate, see FR-012), every prerequisite must have a generation newer than the one the dependent last consumed. If they do, the dependent records them as consumed and is enqueued. Otherwise, the job goes to the final state `PrerequisitesNotMet` instead, with the pending prerequisites as its reason. It never reaches a worker, and no lifecycle hook fires for it.
  - Retries of an enqueued dependent aren't gated again.
  - **AC-010.1**: **Given** a prerequisite's handler, **When** it completes, **Then** its generation is incremented; **When** it throws, **Then** it is not. Tests: `PrerequisiteGateTests.DispatchRecurring_IncrementsGeneration_WhenPrerequisiteCompletes`, `PrerequisiteGateTests.DispatchRecurring_DoesNotIncrement_WhenHandlerThrows`
  - **AC-010.2**: **Given** a prerequisite with no new generation, **When** a dependent is about to be enqueued, **Then** `PrerequisitesNotMet` is elected, naming the prerequisite. Test: `PrerequisiteGateTests.OnStateElection_ElectsPrerequisitesNotMet_WhenNoNewGeneration`
  - **AC-010.3**: **Given** every prerequisite with a new generation, **When** a dependent is about to be enqueued, **Then** `Enqueued` is kept and the consumed generations are recorded, so that the next attempt without new cycles is gated. Test: `PrerequisiteGateTests.OnStateElection_EnqueuesOncePerGeneration`
  - **AC-010.4**: **Given** a job re-enqueued from another state (retry, requeue), **When** it is enqueued, **Then** it is not gated. Test: `PrerequisiteGateTests.OnStateElection_DoesNotGate_WhenJobIsNotNew`
  - **AC-010.5**: **Given** a handler that isn't a dependent in the plan, **When** it is enqueued, **Then** it is not gated. Test: `PrerequisiteGateTests.OnStateElection_DoesNotGate_WhenHandlerHasNoPrerequisites`
  - **AC-010.6**: **Given** a step with prerequisites, **When** it is configured and built, **Then** it is registered with `Cron.Never()` and accepted without `[Recurring]`. Tests: `RecurringRegistrationTests.Build_RegistersDependentWithCronNever`, `ExecutionPlanHangfireTests.Configure_Accepts_DependentWithoutRecurringAttribute`, `ExecutionPlanHangfireTests.Configure_Throws_WhenIndependentHandlerHasNoRecurringAttribute`
  - **AC-010.7**: **Given** a dependent with a new generation of every prerequisite, **When** a prerequisite completes, **Then** the dependent is triggered; **Given** a prerequisite still pending or a disabled dependent, **Then** it is not. Tests: `PrerequisiteGateTests.DispatchRecurring_TriggersDependent_WhenAllPrerequisitesHaveNewCycle`, `PrerequisiteGateTests.DispatchRecurring_DoesNotTrigger_WhenAnotherPrerequisiteIsPending`, `PrerequisiteGateTests.DispatchRecurring_DoesNotTrigger_DisabledDependent`, `PrerequisiteGateTests.DispatchRecurring_TriggersChainInOrder_WhenDependentAlsoDependsOnRoot`

- **FR-011 `JobState.PrerequisitesNotMet` (v1.3.0)**: `JobState` gets the value `PrerequisitesNotMet`, for jobs in that state. Source: 010-execution-plan User Story 1. *(Story 7)*
  - **AC-011.1**: **Given** `PrerequisitesNotMet`, **When** `GetJobs` or `CountJobs` is called, **Then** an empty sequence and 0 are returned, because Hangfire keeps no list for custom states (RN-007). Test: `ExecutionManagerTests.GetJobsAndCount_ReturnEmpty_ForPrerequisitesNotMet`

- **FR-012 Run modes and manual runs in Hangfire (v1.3.0)**: how Hangfire implements [010-execution-plan](../010-execution-plan/spec.md) FR-007. Source: 010-execution-plan User Story 1. *(Story 7)*
  - **Default:** a dependent is registered with `Cron.Never()` and triggered by its prerequisites (FR-010).
  - **`RunOnOwnSchedule`:** the dependent is registered with its `[Recurring]` cron, which is required in this mode. Its prerequisites don't trigger it, and each occurrence goes through the gate, ending in `PrerequisitesNotMet` when a prerequisite has no new cycle.
  - **`MinInterval`:** when the dependent is ready but its previous run finished less than the interval ago, a trigger is scheduled for the end of the interval. Only one is pending per dependent. When it fires, the dependent is triggered if it's still ready.
  - **No overlap:** a dependent that is running isn't triggered. When it finishes, whether it succeeded or failed, it's triggered again if it's already ready (respecting `MinInterval`).
  - **Manual run:** a job of a dependent that the plan didn't trigger, and that the scheduler didn't fire, is manual. It runs without the gate, and consumes the prerequisites' current cycles. The plan's own triggers are recognized by a marker in storage. Scheduler occurrences (`RunOnOwnSchedule` only) are recognized by Hangfire's enqueue reason "Triggered by recurring job scheduler", the only signal Hangfire records. If that text ever changes, manual runs in that mode are gated like occurrences, which is the safe side.
  - **AC-012.1**: **Given** a dependent that runs on its own schedule, **When** it is built and its prerequisites complete, **Then** its cron is registered, it is not triggered from its prerequisites, and each occurrence is gated. Tests: `RecurringRegistrationTests.Build_RegistersOwnScheduleDependentWithItsCron`, `PrerequisiteGateTests.DispatchRecurring_DoesNotTrigger_OwnScheduleDependent`, `PrerequisiteGateTests.OnStateElection_GatesSchedulerOccurrence_OfOwnScheduleDependent`
  - **AC-012.2**: **Given** a dependent that runs on its own schedule without `[Recurring]`, **When** `Configure` runs, **Then** it throws. Test: `ExecutionPlanHangfireTests.Configure_Throws_WhenOwnScheduleDependentHasNoCron`
  - **AC-012.3**: **Given** a `MinInterval` dependent that is ready before the interval has passed since its previous run finished, **When** a prerequisite completes, **Then** one trigger is scheduled for the end of the interval; **Given** the interval has passed, **Then** it is triggered now. Tests: `PrerequisiteGateTests.DispatchRecurring_SchedulesTrigger_WhenMinIntervalNotElapsed`, `PrerequisiteGateTests.DispatchRecurring_SchedulesOnlyOneTrigger_PerDependent`, `PrerequisiteGateTests.DispatchRecurring_TriggersNow_WhenMinIntervalElapsed`
  - **AC-012.4**: **Given** a dependent still ready, **When** the scheduled trigger fires, **Then** it is triggered. Test: `PrerequisiteGateTests.TriggerPlanDependent_TriggersDependent_WhenStillReady`
  - **AC-012.5**: **Given** a running dependent, **When** a prerequisite completes, **Then** it is not triggered; **When** it finishes, with or without an exception, and it's ready, **Then** it is triggered again. Tests: `PrerequisiteGateTests.DispatchRecurring_DoesNotTrigger_WhenDependentIsRunning`, `PrerequisiteGateTests.DispatchRecurring_RetriggersDependent_WhenItFinishesReady`
  - **AC-012.6**: **Given** a dependent's job created by a manual trigger, **When** it is about to be enqueued, **Then** it is enqueued without the gate and consumes the prerequisites' current cycles. Test: `PrerequisiteGateTests.OnStateElection_RunsManualTrigger_WithoutGate`
  - **AC-012.7**: **Given** a trigger postponed by `MinInterval`, **When** the dashboard names it, **Then** it uses the dependent's display name followed by "(postponed trigger)", instead of the dispatcher type. Test: `ExecutionPlanHangfireTests.GetName_NamesPostponedTrigger_AfterDependent`

### Business Rules

- **RN-007** (v1.3.0): Listing and counting jobs in `PrerequisitesNotMet` returns empty or 0 for now. A list or counter maintained by the state is planned for later. A single job in that state shows its reason in the dashboard history.
- **RN-001** [CONFIRMED]: Schedules are evaluated in the resolved time zone (FR-008). The default is UTC.
- **RN-002** [CONFIRMED]: Without an explicit `Id`, renaming the class or changing its namespace changes the ID, creating a new job and orphaning the old one. `RemoveOrphanRecurringJobs` cleans it up.
- **RN-003** [CONFIRMED]: A handler that doesn't auto-run causes no storage writes on schedule, because `Cron.Never()` never fires.
- **RN-004** [CONFIRMED]: Hangfire doesn't prevent overlapping runs. `[DisableConcurrentExecution]` on the handler prevents them, and this is documented in the README.
  - *Amended by [012-interval-and-overlap](../012-interval-and-overlap/spec.md) FR-003: overlap can be turned off with the core attribute or the plan entry.*
- **RN-005** [CONFIRMED]: Auto-run is decided at registration and applies to the whole storage. Choosing the processing server is done with Hangfire queues (`[Queue]` on the handler), which the README documents.
- **RN-006** [CONFIRMED]: Queues for recurring jobs can only be set with Hangfire's `[Queue]` attribute on the handler.

### Key Entities

- **RecurringAttribute**: cron, optional `Id` and `TimeZone`.
- **RecurringJobRegistryInfo**: the registered handler's metadata (cron, `Id`, `TimeZone`, display name).
- **IRecurringTrigger**: on-demand triggers by type or job ID.
- **PrerequisitesNotMet**: the custom final state of a gated dependent's job (v1.3.0).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- None stated (the old spec had no NFRs).

## Assumptions

- Out of scope: seconds-level cron precision; per-server on/off (use queues, RN-005).
- Open questions: none.

### Breaking and behavior changes in 1.2.0

- A recurring handler without `[Recurring]` fails at `Configure`. It already failed at `Build()` before, less clearly.
- On/off applies to the whole storage (the last `Build()` wins). Before, it was decided nondeterministically by whichever server fired the tick (F-008).
- `HangfireAutoRunFilter` is obsolete and no longer registered.
- `RemoveOrphanRecurringJobs` no longer removes non-ExecutionFlow recurring jobs.
