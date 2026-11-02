# Research: Setup and configuration

Decisions and brownfield findings for [spec.md](spec.md). `R-001` was `DEC-001` of the old plan; `R-002` was ADR-0005.

## R-001: The last full build wins (option A) (was DEC-001)

- **Decision**: a second full `Build()` replaces the previous full setup's global filters (RN-002).
- **Rationale**: chosen by the maintainer.
- **Alternatives considered**: "throw on the second build"; "scope filters per storage". Scoping is recorded in [constitution](../../.specify/memory/constitution.md) principle II (was ADR-0007).

## R-002: Producer-only (isolated) dispatcher (was ADR-0005, accepted retroactively 2026-09-30)

**Context**: some apps only publish jobs, and the jobs are run by another process. Sometimes those apps publish to a different storage than a Hangfire instance already running in the same process. A full `Build()` registers global filters, a filter provider, and recurring jobs (`Src/ExecutionFlow.Hangfire/HangfireSetup.cs:62-89`), which would interfere with that other instance.

**Alternatives considered**:
1. Require a separate process for each Hangfire storage.
2. **A second build path that creates only a dispatcher**, bound to an explicit `JobStorage`, with no global side effects.

**Decision**: option 2:
- `HangfireSetup.BuildDispatcherOnly(JobStorage, IServiceProvider = null)` and `BuildDispatcherOnly(IBackgroundJobClient, JobStorage, IServiceProvider = null)` (`HangfireSetup.cs:98-128`) return an `IEventDispatcher`. They require a non-null client and storage, and don't touch `GlobalJobFilters`, `JobFilterProviders`, `JobActivator.Current`, or recurring jobs. When no provider is given, they use a private `FlowEngineJobActivator` with only `IJobIdGenerator` and `IHangfireJobName` registered.
- `AddExecutionFlowDispatcher(...)` (`Src/ExecutionFlow.Hangfire.DependencyInjection/ServiceCollectionExtensions.cs:89-123`) wraps it for DI.
- A `HangfireSetup` instance builds only once. Calling `Build` or `BuildDispatcherOnly` a second time throws (`ThrowIfBuilt`).

**Consequences**:
- The producer usually doesn't know the handlers, so dashboard names and similar features fall back to event type names ([008-job-naming-and-dashboard research](../008-job-naming-and-dashboard/research.md), was ADR-0006).
- It returns `IEventDispatcher`, not `IHangfireDispatcher`, so recurring triggers aren't available in producer-only mode.

**Related**: [constitution](../../.specify/memory/constitution.md) principle IV (was P-005); features 002-setup-and-configuration, [003-dispatching](../003-dispatching/spec.md).

## Findings (brownfield, was findings.md)

- **F-001** bug (carried over as recurring-jobs F-009): `HangfireSetup.Build()` without a service provider reuses `JobActivator.Current` whenever it's a `FlowEngineJobActivator`, including one that belongs to **another** `HangfireSetup` (e.g. one that called `ConfigureActivator()`). The build then resolves that other setup's generators and registrations. Found through a test that only failed when run with the rest of the suite.
  - Related: FR-002
  - Resolution: fixed (AC-002.3, AC-002.4)

- **F-002** global state: each full `Build()` adds `HangfireStateFilter` and `DeduplicationCleanupFilter` to `GlobalJobFilters`, and `HandlerJobFilterProvider` to `JobFilterProviders`, and never removes them. Two full setups in one process (or in tests) accumulate filters: hooks fire twice, and both registries' filters apply.
  - Related: FR-002, RN-002
  - Resolution: fixed (AC-002.2, RN-002: the last full build wins, scoped to Hangfire)

- **F-003** inconsistency: "locked" options only guard methods. Property setters still work after `Configure`, and whether a change takes effect depends on when each option is read (live, like deduplication, or at `Build`, like recurring).
  - Related: AC-001.3
  - Resolution: fixed (AC-001.3)

- **F-004** risk: with DI, `Build()` (filters, recurring jobs) only happens when something resolves the dispatcher. The hosted service forces that at startup, but apps without a generic host never register recurring jobs until the first publish. [INFERRED]
  - Related: AC-005.3
  - Resolution: fixed (AC-005.3, `StartExecutionFlow`)

- **F-005** risk: registering both `AddHangfireToExecutionFlow` and `AddExecutionFlowDispatcher` makes the last registered `IEventDispatcher` win. That can be the producer-only one, silently publishing to the other storage. [INFERRED]
  - Related: AC-005.2
  - Resolution: fixed (AC-005.2)

- **F-006** gap: producer-only hosts have no `IExecutionManager`, so the API side of a split API/worker architecture can't check, cancel or retry jobs through ExecutionFlow.
  - Related: FR-003
  - Resolution: fixed (AC-003.2, AC-005.4; limits in RN-004)

- **F-007** untested: the partial-assembly path of `Scan` (`ReflectionTypeLoadException` → `OnTypeLoadFailure`) has no test. Only `AssemblyTypeScanContext` itself is tested.
  - Related: AC-001.4
  - Resolution: open

- **F-008** dependency floor (from the constitution's open questions): the DI package references `Microsoft.Extensions.* 10.0.*`, forcing apps on older runtimes to upgrade those abstractions. `Hangfire.Console 1.4.*` still floats.
  - Related: [constitution](../../.specify/memory/constitution.md) (Technical Constraints)
  - Resolution: fixed (RN-005). Verified by packing: the DI nuspec declares >= 6.0.0
