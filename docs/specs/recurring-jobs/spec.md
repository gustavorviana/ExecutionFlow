# Spec: Recurring jobs

Status: approved
Mode: to-be (v1.2.0), based on the as-is mapping (see [findings.md](findings.md)). Design in [plan.md](plan.md).
Packages: `ExecutionFlow` (`IHandler`, `RecurringAttribute`, registration), `ExecutionFlow.Hangfire` (scheduling, triggers, orphan cleanup)

## Context
Some work runs on a schedule rather than in response to an event: nightly reports, periodic syncs, cleanups. A recurring handler declares its schedule once. ExecutionFlow registers it with Hangfire when the consumer starts, lets operators turn its schedule on or off, and lets code or the dashboard trigger it on demand.

Related capabilities:
- [handlers-and-context](../handlers-and-context/spec.md): `IHandler` and `FlowContext`.
- [setup-and-configuration](../setup-and-configuration/spec.md): `Configure`, `Scan`, and `Build`.
- [job-naming-and-dashboard](../job-naming-and-dashboard/spec.md): recurring job names ([ADR-0006](../adr/ADR-0006-job-naming-fallback.md)).
- [execution-manager](../execution-manager/spec.md): `IsRunning(Type)`, `Cancel(Type)`, `Retry(Type)`.

## Actors
| Actor | Description |
| ----- | ----------- |
| Handler author | Writes an `IHandler` class and declares its schedule. |
| Consumer app | Calls `Build()`, which registers the recurring jobs, and runs the Hangfire server. |
| Operator | Turns schedules on or off, and triggers handlers from code or the dashboard. |

## User stories
- US-001: As a handler author, I want to declare a cron schedule on a handler class, so that it runs periodically without extra wiring.
- US-002: As an operator, I want to turn a recurring handler's schedule off without removing it from the code.
- US-003: As an operator, I want to trigger a recurring handler on demand, even when its schedule is off.
- US-004: As a consumer app, I want recurring jobs that were removed from the code to be cleaned up from storage, without touching jobs that aren't mine.
- US-005: As a handler author, I want a stable job ID, so that renaming the class doesn't create a new job.
- US-006: As a handler author, I want the schedule evaluated in a given time zone.

## Requirements

### REQ-001: Declare a recurring handler
A class that implements `IHandler` (not `IHandler<TEvent>`) is a recurring handler. `[Recurring("cron")]` sets its schedule, and `[DisplayName]` sets its name.
Source: US-001

Acceptance criteria:
- AC-001.1: WHEN a class implementing `IHandler` is added or scanned, THE SYSTEM SHALL register it as a recurring handler, with the cron, `Id` and `TimeZone` from `[Recurring]` and the display name from `[DisplayName]` (defaulting to the class name). Tests: `ExecutionFlowOptionsTests.Add_RecurringHandler_Populates_RecurringHandlers`, `HangfireSetupTests.Configure_Registers_RecurringHandler`, `RecurringValidationTests.Add_CopiesIdAndTimeZoneFromAttribute_ToRegistry`
- AC-001.2: WHEN a class implements both `IHandler` and `IHandler<TEvent>`, THE SYSTEM SHALL throw `InvalidOperationException` at registration. Test: `ValidationTests.Add_Throws_WhenTypeImplementsBothIHandlerAndGenericIHandler`
- AC-001.3: WHEN a recurring handler has no `[Recurring]` (or an empty cron), THE SYSTEM SHALL throw `InvalidOperationException` at `Configure`, naming the handler and saying `[Recurring]` is required. Test: `RecurringValidationTests.Configure_Throws_WhenRecurringHandlerHasNoCron`

### REQ-002: Registration with Hangfire
On `Build()`, each recurring handler is added or updated with `RecurringJobManager.AddOrUpdate`, using its resolved ID (REQ-007), its cron (or `Cron.Never()`, see REQ-003), and its resolved time zone (REQ-008).
Source: US-001

Acceptance criteria:
- AC-002.1: WHEN `Build()` runs, THE SYSTEM SHALL add or update one Hangfire recurring job per registered recurring handler. Test: `RecurringRegistrationTests.Build_UsesFullNameAsId_WhenNoExplicitId`
- AC-002.2: `BuildDispatcherOnly()` SHALL NOT register recurring jobs (P-005). Test: `ServiceCollectionExtensionsTests.DispatcherOnly_DoesNotRegister_IRecurringTrigger` (indirect)

### REQ-003: Schedule on or off
`HangfireOptions.GlobalRecurringAutoRun` (default `true`) and `SetJobAutoRun<T>(bool)` decide whether a handler runs on its schedule. A per-handler setting wins over the global one. A handler that doesn't auto-run is registered with `Cron.Never()`: it stays visible in the dashboard and runs only when triggered. This applies to the whole storage, and the last `Build()` wins.
Source: US-002

Acceptance criteria:
- AC-003.1: WHEN auto-run is off for a handler (per-handler, or global with no per-handler value), THE SYSTEM SHALL register it with `Cron.Never()`. Tests: `RecurringRegistrationTests.Build_RegistersCronNever_WhenAutoRunIsOff`, `Build_RegistersCronNever_WhenGlobalAutoRunIsOff`
- AC-003.2: WHEN the per-handler value is `true` and the global is `false`, THE SYSTEM SHALL register the handler's cron. Test: `RecurringRegistrationTests.Build_PerHandlerAutoRun_OverridesGlobal`
- AC-003.3: WHEN `SetJobAutoRun` references a type that isn't a registered recurring handler, THE SYSTEM SHALL throw at `Configure`. Test: `HangfireOptionsTests.SetJobAutoRun_InvalidHandler_Throws_OnConfigure`
- AC-003.4: `Build()` SHALL NOT register the obsolete `HangfireAutoRunFilter`. Test: `RecurringRegistrationTests.Build_DoesNotRegisterAutoRunFilter`

### REQ-004: Manual trigger
`IRecurringTrigger.Trigger(Type)` and `Trigger(string jobId)` run a recurring job now, whether or not it auto-runs.
Source: US-003

Acceptance criteria:
- AC-004.1: WHEN `Trigger(Type)` is called with a registered recurring handler, THE SYSTEM SHALL trigger the job with the handler's resolved ID (REQ-007). WHEN the type is null or not registered, it SHALL throw. Tests: `RecurringRegistrationTests.Trigger_UsesExplicitId`, `HangfireValidationTests.HangfireDispatcher_Trigger_Throws_ForNullType`, `HangfireDispatcher_Trigger_Throws_ForUnregisteredType`
- AC-004.2: WHEN `Trigger(string)` is called with a null or empty ID, THE SYSTEM SHALL throw `ArgumentNullException`. Any other ID is passed to Hangfire unchecked. Test: `HangfireValidationTests.HangfireDispatcher_TriggerById_Throws_ForNullOrEmptyId`
- ~~AC-004.3~~ (deprecated: manual triggers are no longer detected by text, because disabled handlers never fire automatically. See REQ-003 and F-004. 2026-09-30)

### REQ-005: Retries for recurring jobs
With `HangfireOptions.DisableRecurringRetries` (default `true`), recurring jobs get `AutomaticRetry(Attempts = 0)`, unless the handler declares its own `[AutomaticRetry]`.
Source: US-001

Acceptance criteria:
- AC-005.1: Tests: `HandlerJobFilterProviderTests.DisableRecurringRetries_DefaultsToTrue`, `RecurringJob_GetsRetryDisabled_WhenDisableRecurringRetriesIsTrue`, `RecurringJob_DoesNotGetRetryDisabled_WhenDisableRecurringRetriesIsFalse`, `RecurringJob_WithCustomRetryAttribute_IsNotOverridden`, `EventJob_IsNotAffected_ByDisableRecurringRetries`

### REQ-006: Orphan cleanup
With `HangfireOptions.RemoveOrphanRecurringJobs` (default `false`), `Build()` removes ExecutionFlow recurring jobs (method `HangfireJobDispatcher.DispatchRecurringAsync`) whose ID isn't registered.
Source: US-004

Acceptance criteria:
- AC-006.1: WHEN the option is on, THE SYSTEM SHALL remove only ExecutionFlow recurring jobs that aren't registered. It SHALL leave native Hangfire recurring jobs, other applications' jobs, and jobs that fail to load untouched. Test: `RecurringRegistrationTests.Build_RemovesOnlyExecutionFlowOrphans`
- AC-006.2: WHEN the option is off (default), THE SYSTEM SHALL remove nothing. Tests: `RecurringRegistrationTests.Build_KeepsOrphans_WhenOptionIsOff`, `HangfireOptionsTests.RemoveOrphanRecurringJobs_DefaultsToFalse`

### REQ-007: Recurring job ID
The ID is `[Recurring(Id = "...")]` if set, otherwise `IJobIdGenerator.GenerateId(handlerType)` (by default `handlerType.FullName`). `IJobIdGenerator` is only used for recurring jobs. One resolver serves both registration and `Trigger(Type)`.
Source: US-005

Acceptance criteria:
- AC-007.1: WHEN `Id` is set, THE SYSTEM SHALL use it, even when a custom `IJobIdGenerator` is configured. Test: `RecurringRegistrationTests.Build_UsesExplicitId_OverIdGenerator`
- AC-007.2: WHEN `Id` isn't set, THE SYSTEM SHALL use the configured generator. Tests: `RecurringRegistrationTests.Build_UsesIdGenerator_WhenNoExplicitId`, `Build_UsesFullNameAsId_WhenNoExplicitId`, `DefaultRecurringServiceIdGeneratorTests.GenerateId_ReturnsFullName`
- AC-007.3: WHEN two handlers resolve to the same ID, THE SYSTEM SHALL throw `InvalidOperationException` at `Build()`. Test: `RecurringRegistrationTests.Build_Throws_WhenTwoHandlersResolveToTheSameId`

### REQ-008: Time zone
The cron is evaluated in the resolved time zone. Precedence: `HangfireOptions.SetJobTimeZone<T>` > `[Recurring(TimeZone = "...")]` > `HangfireOptions.RecurringTimeZone` > UTC. IDs are validated at `Configure` with `TimeZoneInfo.FindSystemTimeZoneById`.
Source: US-006

Acceptance criteria:
- AC-008.1: Precedence. Tests: `RecurringRegistrationTests.Build_UsesUtc_ByDefault`, `Build_UsesGlobalTimeZone_WhenNothingMoreSpecific`, `Build_UsesAttributeTimeZone_OverGlobal`, `Build_UsesOptionTimeZone_OverAttribute`
- AC-008.2: WHEN a configured time zone ID doesn't exist on the host, THE SYSTEM SHALL throw `InvalidOperationException` at `Configure`, naming the ID. Tests: `RecurringValidationTests.Configure_Throws_WhenGlobalTimeZoneIsUnknown`, `Configure_Throws_WhenAttributeTimeZoneIsUnknown`, `Configure_Throws_WhenOptionTimeZoneIsUnknown`, `Configure_Accepts_ValidTimeZones`
- AC-008.3: WHEN `SetJobTimeZone` references a type that isn't a registered recurring handler, THE SYSTEM SHALL throw at `Configure`. Test: `RecurringValidationTests.Configure_Throws_WhenSetJobTimeZoneReferencesUnregisteredHandler`

## Business rules
- RN-001 [CONFIRMED] Schedules are evaluated in the resolved time zone (REQ-008). The default is UTC.
- RN-002 [CONFIRMED] Without an explicit `Id`, renaming the class or changing its namespace changes the ID, creating a new job and orphaning the old one. `RemoveOrphanRecurringJobs` cleans it up.
- RN-003 [CONFIRMED] A handler that doesn't auto-run causes no storage writes on schedule, because `Cron.Never()` never fires.
- RN-004 [CONFIRMED] Hangfire doesn't prevent overlapping runs. `[DisableConcurrentExecution]` on the handler prevents them, and this is documented in the README.
- RN-005 [CONFIRMED] Auto-run is decided at registration and applies to the whole storage. Choosing the processing server is done with Hangfire queues (`[Queue]` on the handler), which the README documents.
- RN-006 [CONFIRMED] Queues for recurring jobs can only be set with Hangfire's `[Queue]` attribute on the handler.

## Edge cases and errors
- An invalid cron expression: Hangfire throws when `Build()` runs. [INFERRED] Not tested.
- Two consumers with different handler sets on one storage: each `Build()` adds or updates its own jobs. With `RemoveOrphanRecurringJobs`, each removes the other's ExecutionFlow jobs, but never non-ExecutionFlow jobs.
- A time zone ID that's valid on one host but not another (Windows IDs vs IANA on .NET Framework): `Configure` fails on that host.

## Non-functional requirements
- None stated.

## Out of scope
- Seconds-level cron precision.
- Per-server on/off (use queues, RN-005).

## Breaking and behavior changes in 1.2.0
- A recurring handler without `[Recurring]` fails at `Configure`. It already failed at `Build()` before, less clearly.
- On/off applies to the whole storage (the last `Build()` wins). Before, it was decided nondeterministically by whichever server fired the tick (F-008).
- `HangfireAutoRunFilter` is obsolete and no longer registered.
- `RemoveOrphanRecurringJobs` no longer removes non-ExecutionFlow recurring jobs.

## Open questions
- None.
