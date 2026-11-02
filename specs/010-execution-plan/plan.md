# Implementation Plan: Execution plan

**Branch**: `feat/execution-plan-dependencies` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/execution-plan/plan.md`. Decisions are in [research.md](research.md).

## Summary

A dependency-free declaration in the core: `ExecutionPlanner` collects entries, `Build(policy)` validates them (cycles, availability, run modes) and returns an immutable, topologically ordered `ExecutionPlan` that every execution provider reads.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: none (core package)

**Storage**: N/A

**Testing**: xUnit + NSubstitute (`ExecutionPlannerTests`)

**Target Platform**: any .NET runtime supporting `netstandard2.0`

**Project Type**: library

**Performance Goals**: N/A (runs once at startup)

**Constraints**: no host, container or storage needed to build a plan (SC-001)

**Scale/Scope**: tens of handlers per plan

## Constitution Check

- I. Dependency-free core: ✅ only core types.
- II. Processors isolated: ✅ the plan is processor-neutral; providers enforce it.
- V. Minimal public surface: ✅ new public types only, no breaking change.
- VII. Tested, traceable behavior: ✅ every AC names an `ExecutionPlannerTests` test.

## Project Structure

### Documentation (this feature)

```text
specs/010-execution-plan/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | New: `ExecutionPlanner`, `ExecutionPlanEntry`, `ExecutionPlan`, `ExecutionPlanStep`, `MissingPrerequisite`, `DependsOnAttribute` | No |

```text
Src/ExecutionFlow/
├── Abstractions/ExecutionPlanner.cs, ExecutionPlanEntry.cs, ExecutionPlan.cs, ExecutionPlanStep.cs, MissingPrerequisite.cs
└── Attributes/DependsOnAttribute.cs
Tests/ExecutionFlow.Tests/Abstractions/ExecutionPlannerTests.cs
```

## Design

- `ExecutionPlanner` keeps its entries in declaration order, keyed by `Type` with the core's `TypeEqualityComparer`.
- `Add<T>()` reads `[DependsOn]` and returns the entry. `DependsOn`, `Enabled` and `DisplayName` are fluent setters on the entry.
- `Build(policy)` freezes the planner, then works in four passes:
  1. Detect cycles with a DFS that marks nodes `Visiting` or `Done` and keeps the path for the error message. Only declared prerequisites are followed.
  2. Order the handlers with a post-order DFS over the declaration order.
  3. Walk that order and resolve availability. A step is available when it's enabled and every prerequisite is declared and available. Because of the order, prerequisites are resolved first, so the cascade is a single pass.
  4. Return an immutable `ExecutionPlan` of `ExecutionPlanStep`s.

## Complexity Tracking

No constitution violations.
