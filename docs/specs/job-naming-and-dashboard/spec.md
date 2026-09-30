# Spec: Job naming and dashboard

Status: approved
Mode: to-be (v1.2.0), based on the as-is mapping (see [findings.md](findings.md)). Design in [plan.md](plan.md).
Packages: `ExecutionFlow` (`ICustomNameEvent`), `ExecutionFlow.Hangfire` (`IHangfireJobName`, `DefaultHangfireJobName`, `DashboardOptionsExtensions`, `HangfireJobInfo`)
Decision record: [ADR-0006](../adr/ADR-0006-job-naming-fallback.md) (the chain below supersedes step 4 of that ADR)

## Context
Every ExecutionFlow job runs through the same internal method (`HangfireJobDispatcher.DispatchEventAsync`/`DispatchRecurringAsync`), so Hangfire's dashboard would show the same meaningless name for all of them. ExecutionFlow resolves a meaningful name for its own jobs, and leaves other jobs with Hangfire's default names.

Related capabilities:
- [dispatching](../dispatching/spec.md): `ICustomNameEvent.CustomName` travels in the job arguments (REQ-005 there).
- [recurring-jobs](../recurring-jobs/spec.md): recurring job IDs (`IJobIdGenerator`), which is also the last naming fallback.

## Actors
| Actor | Description |
| ----- | ----------- |
| Event/handler author | Names jobs through `ICustomNameEvent`, `[DisplayName]` or `[JobDisplayName]`. |
| Dashboard operator | Reads job names in the Hangfire dashboard. |
| Consumer/dashboard host | Calls `UseExecutionFlowJobNames`. |

## User stories
- US-001: As a dashboard operator, I want each ExecutionFlow job to show a meaningful name.
- US-002: As an event author, I want to name a specific job instance (e.g. "Reminder for order 42"), from a project that only references the core.
- US-003: As a dashboard operator, I want jobs that aren't ExecutionFlow's to keep their `[JobDisplayName]`, and not be named as if they were ExecutionFlow events.
- US-004: As a handler author who knows Hangfire, I want `[JobDisplayName]` to work on my handler.

## Requirements

### REQ-001: Name resolution
Attribute placement: `[DisplayName]` goes on **classes** (the handler class, or the event class). Only Hangfire's `[JobDisplayName]` goes on a **method**: the handler's `HandleAsync`, or a native job's method.

Two classes split the rules. Each step is a `protected` method of the base, so a derived class can place its own steps between them:
- **`JobDisplayNameResolver`** (concrete, in `ExecutionFlow.Hangfire`) holds the ExecutionFlow steps, with nothing Hangfire-specific:
  - `GetConfiguredName(job)`: the `CustomName` stored in the job (event jobs), or `null`.
  - `GetHandlerDisplayName(job)`: `[DisplayName]` on the registered handler class, or `null`.
  - `GetCarriedTypeDisplayName(job)`: `[DisplayName]` on the type the job carries (the event class, or the recurring handler class), **only when the handler isn't registered on this host** (e.g. producer-only), or `null`.
  - `GetFallbackName(job)`: `IJobIdGenerator.GenerateId(type)` (see RN-006).
  - `public virtual string GetName(Job job)` = configured > handler `[DisplayName]` > carried type `[DisplayName]` > fallback.

  All steps return `null` for non-ExecutionFlow jobs, except the fallback.
- **`DefaultHangfireJobName : JobDisplayNameResolver, IHangfireJobName`** overrides `GetName` to place Hangfire's `[JobDisplayName]` **before the handler class's `[DisplayName]`**. The attribute is read from the handler's `HandleAsync` for ExecutionFlow jobs, with `{0}` = the event, and from the job method for other jobs, formatted with the job's arguments.

Full order in `DefaultHangfireJobName`: `CustomName` > `[JobDisplayName]` on `HandleAsync` > handler class `[DisplayName]` > event class `[DisplayName]` (handler not registered) > `GenerateId`.

Source: US-001, US-002, US-004

Acceptance criteria:
- AC-001.1: `JobDisplayNameResolver` precedence. Tests: `JobDisplayNameResolverTests.GetName_UsesCustomName_First`, `GetName_UsesHandlerDisplayName_WhenNoCustomName`, `GetName_UsesEventDisplayName_WhenHandlerNotRegistered`, `GetName_UsesRecurringHandlerDisplayName_WhenNotRegistered`, `GetName_IgnoresEventDisplayName_WhenHandlerRegistered`, `DefaultHangfireJobNameTests.GetName_ReturnsDisplayName_ForRegisteredEventHandler`, `GetName_ReturnsDisplayName_ForRegisteredRecurringHandler`
- AC-001.2: An empty `CustomName` is ignored. Tests: `JobDisplayNameResolverTests.GetName_IgnoresEmptyCustomName`, `HangfireJobInfoTests.EventJobInfo_CustomJobName_Null_WhenEmpty`, `EventJobInfo_CustomJobName_Null_WhenNotProvided`
- AC-001.3: `JobDisplayNameResolver` SHALL NOT read Hangfire's `[JobDisplayName]`. Test: `JobDisplayNameResolverTests.GetName_IgnoresHangfireJobDisplayName`
- AC-001.4: WHEN no name is found, `JobDisplayNameResolver.GetName` SHALL fall back to `GenerateId`. Tests: `JobDisplayNameResolverTests.GetName_FallsBackToIdGenerator_ForRegisteredHandler`, `GetName_FallsBackToIdGenerator_ForEventType_WhenHandlerNotRegistered`, `GetName_FallsBackToIdGenerator_ForRecurringHandler_WhenNotRegistered`, `GetName_NativeJob_FallsBackToIdGenerator_WithJobType`
- AC-001.5: WHEN the handler's `HandleAsync` has `[JobDisplayName("... {0}")]` and there's no `CustomName`, `DefaultHangfireJobName` SHALL use it with `{0}` = the event (`ToString()`). Test: `DefaultHangfireJobNameTests.GetName_UsesHandlerJobDisplayName_WithEventAsPlaceholder`
- AC-001.6: `CustomName` SHALL win over `[JobDisplayName]`, and `[JobDisplayName]` SHALL win over the handler class's `[DisplayName]`. Tests: `DefaultHangfireJobNameTests.GetName_CustomName_WinsOverJobDisplayName`, `GetName_JobDisplayName_WinsOverHandlerDisplayName`, `GetName_UsesHandlerDisplayName_WhenNoJobDisplayName`
- AC-001.7: WHEN `[JobDisplayName]` has placeholders other than `{0}`, or the job is recurring (no event), THE SYSTEM SHALL use the text as written. Test: `DefaultHangfireJobNameTests.GetName_JobDisplayNameWithUnknownPlaceholder_UsesTextAsIs`

### REQ-002: Per-instance name
An event implementing `ICustomNameEvent` (in the core, namespace `ExecutionFlow.Abstractions`) names its own job. An events project needs only the core package.
Source: US-002

Acceptance criteria:
- AC-002.1: Tests: `HangfireJobInfoTests.EventJobInfo_CustomJobName_Extracted_WhenProvided`, `DispatcherTests.Publish_StoresCustomNameInJobArgs_WhenEventImplementsICustomNameEvent`
- AC-002.2: `ICustomNameEvent` SHALL live in the core package. It's verified by the build: `Examples/ExecutionFlow.Examples.Shared` uses it and references only `ExecutionFlow`.

### REQ-003: Other jobs
`HangfireJobInfo.Create` returns `null` for non-ExecutionFlow jobs. Their name is `[JobDisplayName]` on the job method, formatted with the job's arguments (the text as written if formatting fails), otherwise `IJobIdGenerator.GenerateId(job.Type)` (the job's type, e.g. the interface in `Enqueue<IService>(x => x.Run())`). When the dashboard has no `IHangfireJobName` (REQ-004), Hangfire's own default (`Type.Method`) is used.
Source: US-003

Acceptance criteria:
- AC-003.1: Tests: `DefaultHangfireJobNameTests.GetName_NativeJob_FallsBackToIdGenerator`, `GetName_GenericNativeJob_IsNotTreatedAsExecutionFlowEvent`, `GetName_NativeJob_UsesHangfireJobDisplayName_WithArguments`, `HangfireValidationTests.DefaultHangfireJobName_Throws_ForNullIdGenerator`, `ServiceCollectionExtensionsTests.BothExtensions_ConsumerRegistryWins_ForJobNames`

### REQ-004: Dashboard integration
`DashboardOptions.UseExecutionFlowJobNames(IHangfireJobName)` and `UseExecutionFlowJobNames(IServiceProvider)` set `DisplayNameFunc`. The provider overload resolves lazily. If no `IHangfireJobName` is registered, every job keeps its default name, with no exception and no warning.
Source: US-001

Acceptance criteria:
- AC-004.1: Tests: `DashboardOptionsExtensionsTests.UseExecutionFlowJobNames_WithInstance_SetsDisplayNameFunc`, `UseExecutionFlowJobNames_WithServiceProvider_ResolvesLazily`, `UseExecutionFlowJobNames_NullOptions_Throws`
- AC-004.2: Test: `DashboardOptionsExtensionsTests.UseExecutionFlowJobNames_WithServiceProvider_FallsBackToDefaultName_WhenNotRegistered`

## Business rules
- RN-001 [CONFIRMED] ExecutionFlow jobs are recognized by the dispatcher method (`HangfireJobDispatcher.DispatchEventAsync`/`DispatchRecurringAsync`), not by method shape.
- RN-002 [CONFIRMED] Hangfire's `[JobDisplayName]` only applies to methods, so on handlers it's read from `HandleAsync` (the method that runs the job), not from the class.
- RN-003 [CONFIRMED] The naming strategy can be replaced with `HangfireOptions.SetJobName<T>()`.
- RN-004 [CONFIRMED] On a producer-only host, handlers aren't registered, so names come from steps 4–5 (the event type).
- RN-005 [CONFIRMED] Step 5 (`GenerateId`) only applies when no name is defined anywhere. With the default generator, that name is the full type name, the same value used as the recurring job ID.
- RN-006 [CONFIRMED] There is a single fallback for every job, ExecutionFlow or not: `GenerateId(type)`, where the type is the registered handler type, otherwise the type the job carries (the event type for event jobs, the handler type for recurring jobs), otherwise `job.Type`. (`JobDisplayNameResolver.GetName`)
- RN-007 [CONFIRMED] The ordering rules live in one place, `JobDisplayNameResolver.GetConfiguredName`, which is protected and not virtual. A replacement `IHangfireJobName` can derive from `JobDisplayNameResolver` to reuse the rules and choose its own fallback.

## Out of scope
- Localizing names.
- Names outside the dashboard (logging uses its own naming).

## Breaking and behavior changes in 1.2.0
- `ICustomNameEvent` moved from `ExecutionFlow.Hangfire` to `ExecutionFlow.Abstractions`.
- `HangfireJobInfo.Create` returns `null` for non-ExecutionFlow jobs.
- Non-ExecutionFlow jobs are no longer named after a type argument (generic jobs), and their `[JobDisplayName]` is honored.
- Names that nothing defines now come from `IJobIdGenerator` (the full type name by default) instead of the class name. Native jobs without `[JobDisplayName]` show `GenerateId(job.Type)` instead of Hangfire's `Type.Method`.
- A missing `IHangfireJobName` no longer breaks the dashboard.
- New public base class `JobDisplayNameResolver`. `DefaultHangfireJobName` derives from it.
- Removed from `HangfireJobInfo`, with the naming rules now in `JobDisplayNameResolver`: `GetExpectedName(IExecutionFlowRegistry)`, `GetExpectedName(IJobRegistryInfo)` and the protected `GetTypeDisplayName(Type)`.
- `[JobDisplayName]` on a handler now comes after every configured name, including the event's `[DisplayName]`.

## Open questions
- None. `{0}` in `[JobDisplayName]` = the event (plan DEC-001).
