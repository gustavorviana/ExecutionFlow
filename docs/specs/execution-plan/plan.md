# Plan: Execution plan (v1.3.0)

Spec: [spec.md](spec.md)
Status: draft

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | New: `ExecutionPlanner`, `ExecutionPlanEntry`, `ExecutionPlan`, `ExecutionPlanStep`, `MissingPrerequisite`, `DependsOnAttribute` | No |

## Design
- `ExecutionPlanner` keeps its entries in declaration order, keyed by `Type` with the core's `TypeEqualityComparer`.
- `Add<T>()` reads `[DependsOn]` and returns the entry. `DependsOn`, `Enabled` and `DisplayName` are fluent setters on the entry.
- `Build(policy)` freezes the planner, then works in four passes:
  1. Detect cycles with a DFS that marks nodes `Visiting` or `Done` and keeps the path for the error message. Only declared prerequisites are followed.
  2. Order the handlers with a post-order DFS over the declaration order.
  3. Walk that order and resolve availability. A step is available when it's enabled and every prerequisite is declared and available. Because of the order, prerequisites are resolved first, so the cascade is a single pass.
  4. Return an immutable `ExecutionPlan` of `ExecutionPlanStep`s.

## Decisions
### DEC-001: "Enabled" lives on the entry
- Context: this was first designed as a predicate passed to validation by each provider.
- Decision: the consumer sets `Enabled(bool)` on each entry. The plan is self-contained, and providers only read `step.Enabled`.
- Consequences: a disabled handler stays visible in the plan and in reports, which tells "disabled" apart from "not declared".

### DEC-002: The policy for unavailable prerequisites is explicit
- Decision: the default is `Throw`, so nothing stops silently. `SkipDependents` is the opt-in cascade.
