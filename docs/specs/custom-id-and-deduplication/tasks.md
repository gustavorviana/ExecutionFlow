# Tasks: Deduplication redesign (v1.2.0)

Plan: [plan.md](plan.md)

- [x] TASK-001: Move `DeduplicationBehavior` to `ExecutionFlow.Abstractions` and add `DeduplicationAttribute`
  - Requirements: REQ-006
  - Depends on: —
  - Done when: the solution builds, and `DeduplicationAttributeTests` pass

- [x] TASK-002: `DeduplicationStore`: read (checking the job's state), reserve, release
  - Requirements: REQ-007, REQ-008
  - Depends on: —
  - Done when: `DeduplicationStoreTests` pass

- [x] TASK-003: Dispatcher uses the lock and the reservation key, the per-event behavior, and the timeout options
  - Requirements: REQ-004, REQ-005, REQ-006, REQ-007, REQ-009
  - Depends on: TASK-001, TASK-002
  - Done when: the dedup tests in `DispatcherTests` pass, including Scheduled counting as active and the lock timeout

- [x] TASK-004: `DeduplicationCleanupFilter`, registered by `Build()`
  - Requirements: REQ-008
  - Depends on: TASK-002
  - Done when: `DeduplicationCleanupFilterTests` pass

- [x] TASK-005: Execution manager uses the key as a fast path
  - Requirements: REQ-003
  - Depends on: TASK-002
  - Done when: the existing `ExecutionManagerTests` pass, and the key fast-path tests pass

- [x] TASK-006: `FlowContext.SetCustomId` becomes `[Obsolete]`
  - Requirements: REQ-002
  - Depends on: —
  - Done when: it builds with no new warnings in `Src/` (internal use is suppressed)

- [x] TASK-007: README, spec and finding status
  - Requirements: all
  - Depends on: TASK-001..TASK-006
  - Done when: the README documents the attribute, the options, the `CancellationToken` note and the upgrade notes, the spec is `approved`, and every AC names an existing test
