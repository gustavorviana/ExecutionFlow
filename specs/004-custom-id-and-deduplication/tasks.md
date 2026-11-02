---
description: "Tasks for the deduplication redesign v1.2.0 (migrated; all complete)"
---

# Tasks: Deduplication redesign (v1.2.0)

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

**Tests**: mandatory (constitution VII). Each task is done when the tests of the acceptance scenarios it covers pass.

**Migration note**: `TASK-00n` → `T00n`, same numbers.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Foundational

- [x] T001 [P] [US5] Move `DeduplicationBehavior` to `ExecutionFlow.Abstractions` and add `DeduplicationAttribute` (FR-006; depends on: none; done when the solution builds, and `DeduplicationAttributeTests` pass)
- [x] T002 [P] [US3] `DeduplicationStore`: read (checking the job's state), reserve, release (FR-007, FR-008; depends on: none; done when `DeduplicationStoreTests` pass)

## Phase 2: User Stories 3, 4, 5 - Deduplicated publishing (Priority: P1/P2)

- [x] T003 [US3] Dispatcher uses the lock and the reservation key, the per-event behavior, and the timeout options (FR-004, FR-005, FR-006, FR-007, FR-009; depends on T001, T002; done when the dedup tests in `DispatcherTests` pass, including Scheduled counting as active and the lock timeout)
- [x] T004 [US3] `DeduplicationCleanupFilter`, registered by `Build()` (FR-008; depends on T002; done when `DeduplicationCleanupFilterTests` pass)

## Phase 3: User Story 1 - Lookups by custom ID (Priority: P1)

- [x] T005 [US1] Execution manager uses the key as a fast path (FR-003; depends on T002; done when the existing `ExecutionManagerTests` pass, and the key fast-path tests pass)

## Phase 4: User Story 2 - Custom ID during execution (Priority: P3)

- [x] T006 [P] [US2] `FlowContext.SetCustomId` becomes `[Obsolete]` (FR-002; depends on: none; done when it builds with no new warnings in `Src/` (internal use is suppressed))

## Phase 5: Polish & Cross-Cutting

- [x] T007 README, spec and finding status (all requirements; depends on T001..T006; done when the README documents the attribute, the options, the `CancellationToken` note and the upgrade notes, the spec is `approved`, and every AC names an existing test)
