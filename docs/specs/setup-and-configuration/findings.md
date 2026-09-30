# Findings: Setup and configuration

- F-001 bug (carried over as recurring-jobs F-009): `HangfireSetup.Build()` without a service provider reuses `JobActivator.Current` whenever it's a `FlowEngineJobActivator`, including one that belongs to **another** `HangfireSetup` (e.g. one that called `ConfigureActivator()`). The build then resolves that other setup's generators and registrations. Found through a test that only failed when run with the rest of the suite.
  - Related: REQ-002
  - Resolution: fixed (AC-002.3, AC-002.4)

- F-002 global state: each full `Build()` adds `HangfireStateFilter` and `DeduplicationCleanupFilter` to `GlobalJobFilters`, and `HandlerJobFilterProvider` to `JobFilterProviders`, and never removes them. Two full setups in one process (or in tests) accumulate filters: hooks fire twice, and both registries' filters apply.
  - Related: REQ-002, RN-002
  - Resolution: fixed (AC-002.2, RN-002: the last full build wins, scoped to Hangfire)

- F-003 inconsistency: "locked" options only guard methods. Property setters still work after `Configure`, and whether a change takes effect depends on when each option is read (live, like deduplication, or at `Build`, like recurring).
  - Related: AC-001.3
  - Resolution: fixed (AC-001.3)

- F-004 risk: with DI, `Build()` (filters, recurring jobs) only happens when something resolves the dispatcher. The hosted service forces that at startup, but apps without a generic host never register recurring jobs until the first publish. [INFERRED]
  - Related: AC-005.3
  - Resolution: fixed (AC-005.3, `StartExecutionFlow`)

- F-005 risk: registering both `AddHangfireToExecutionFlow` and `AddExecutionFlowDispatcher` makes the last registered `IEventDispatcher` win. That can be the producer-only one, silently publishing to the other storage. [INFERRED]
  - Related: AC-005.2
  - Resolution: fixed (AC-005.2)

- F-006 gap: producer-only hosts have no `IExecutionManager`, so the API side of a split API/worker architecture can't check, cancel or retry jobs through ExecutionFlow.
  - Related: REQ-003
  - Resolution: fixed (AC-003.2, AC-005.4; limits in RN-004)

- F-007 untested: the partial-assembly path of `Scan` (`ReflectionTypeLoadException` → `OnTypeLoadFailure`) has no test. Only `AssemblyTypeScanContext` itself is tested.
  - Related: AC-001.4
  - Resolution: open

- F-008 dependency floor (from the constitution's open questions): the DI package references `Microsoft.Extensions.* 10.0.*`, forcing apps on older runtimes to upgrade those abstractions. `Hangfire.Console 1.4.*` still floats.
  - Related: constitution
  - Resolution: fixed (RN-005). Verified by packing: the DI nuspec declares >= 6.0.0
