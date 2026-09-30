# Specifications

ExecutionFlow follows **Spec-Driven Development (SDD)**. The specs in this folder are the source of truth for the library's behavior. Design, tasks and code are derived from them.

## Process

1. **Spec** (`spec.md`) describes *what* the capability does and *why*: requirements, acceptance criteria, business rules, edge cases, and out of scope. It says nothing about implementation.
2. **Plan** (`plan.md`) describes *how*: design, contracts, and decisions (`DEC-xxx`, promoted to an ADR when they are architectural).
3. **Tasks** (`tasks.md`) are small, ordered units of work. Each is linked to requirements and has a done criterion.
4. **Code and tests** implement the tasks. Every acceptance criterion is covered by at least one test.

**Change rule:** when behavior changes, update the spec first, then the plan and tasks, then the code. A PR that changes behavior without touching the spec is incomplete.

## Layout

```
docs/specs/
  constitution.md        # non-negotiable principles for the whole project
  _templates/            # starting points for every artifact
  adr/                   # Architecture Decision Records (ADR-NNNN-title.md)
  <capability>/
    spec.md              # required
    findings.md          # brownfield only: inconsistencies, debt, bug-or-rule doubts
    plan.md, tasks.md    # only when a change is being made
```

## IDs

| Prefix | Meaning | Scope |
| ------ | ------- | ----- |
| `REQ-001` | Requirement | per capability |
| `AC-001.1` | Acceptance criterion of REQ-001 | per capability |
| `RN-001` | Business rule | per capability |
| `NFR-001` | Non-functional requirement | per capability, or global in the constitution |
| `US-001` | User story | per capability |
| `DEC-001` | Design decision in a plan | per capability |
| `TASK-001` | Task | per capability |
| `F-001` | Finding (brownfield) | per capability |
| `P-001` | Constitution principle | global |
| `ADR-0001` | Architecture decision record | global |

When you reference an ID from another capability, qualify it with the folder name, e.g. `dispatching/REQ-003`.

**Never renumber IDs.** When an item is removed, mark it `~~REQ-004~~ (deprecated: <reason>, <date>)` and leave it in place.

## Brownfield evidence tags

As-is specs tag each rule by how certain it is:

- `[CONFIRMED]`: seen in the code or a test, with a cited source (`Src/...cs:line` or a test name), or confirmed by the maintainer.
- `[INFERRED]`: deduced, and needs validation.
- `[UNKNOWN]`: a gap.

Open ambiguities are written inline as `[NEEDS CLARIFICATION: ...]`.

## Status lifecycle

`draft` → `in review` → `approved` → (`superseded by <link>`)

## Capabilities

| Capability | Status |
| ---------- | ------ |
| [dispatching](dispatching/spec.md) | approved (v1.2.0) |
| [custom-id-and-deduplication](custom-id-and-deduplication/spec.md) | approved (v1.2.0) |
| [recurring-jobs](recurring-jobs/spec.md) | approved (v1.2.0) |
| [lifecycle-hooks](lifecycle-hooks/spec.md) | approved (v1.2.0) |
| [execution-manager](execution-manager/spec.md) | approved (v1.2.0) |
| [job-naming-and-dashboard](job-naming-and-dashboard/spec.md) | approved (v1.2.0) |
| [setup-and-configuration](setup-and-configuration/spec.md) | not started |
| [handlers-and-context](handlers-and-context/spec.md) | not started |
| [logging-and-console](logging-and-console/spec.md) | not started |

See also: [constitution](constitution.md) and [ADRs](adr/).
