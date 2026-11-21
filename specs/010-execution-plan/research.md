# Research: Execution plan

Decisions for [spec.md](spec.md). `R-001` and `R-002` were `DEC-001`/`DEC-002` of the old plan; `R-003` was ADR-0009.

## R-001: "Enabled" lives on the entry

- **Decision**: the consumer sets `Enabled(bool)` on each entry. The plan is self-contained, and providers only read `step.Enabled`.
- **Rationale**: a disabled handler stays visible in the plan and in reports, which tells "disabled" apart from "not declared".
- **Alternatives considered**: a predicate passed to validation by each provider (the first design); rejected because each provider would re-implement it.

## R-002: The policy for unavailable prerequisites is explicit

- **Decision**: the default is `MissingPrerequisite.Throw`, so nothing stops silently. `SkipDependents` is the opt-in cascade.
- **Rationale**: a dependent that silently stops running is the failure this feature exists to prevent.
- **Alternatives considered**: skipping by default; rejected for the same reason.

## R-003: Dependencies between recurring handlers by generation (was ADR-0009, accepted 2026-09-30)

**Context**: "B depends on A" has to hold for every run of B, not only the first. Otherwise, after the first cycle B would keep running on A's stale output, or run while A is failing. The rule must mean the same thing in every execution provider. Hangfire recurring jobs, which may run on several servers, can't use `ContinueJobWith`.

**Alternatives considered**:
1. **"Has run once" flag.** Simple, but it only protects the first run.
2. **The prerequisite triggers its dependent on success.** It ignores the dependent's own schedule.
3. **Generation counter.** Every completed cycle of a handler increments its generation. The dependent remembers which generation of each prerequisite it last consumed, and runs only when every prerequisite has a newer generation.

**Decision**: option 3, with option 2 as the trigger: generations decide whether a dependent may run, and a completing prerequisite triggers the dependents that became ready. "Completed" means the handler returned without throwing, until a cycle-status API exists.

In Hangfire:
- Generations live in the job storage, in the hash `executionflow:plan:generation` (one field per recurring job ID) and the hash `executionflow:plan:consumed:<dependent job id>`. They're read and written under a distributed lock.
- A dependent has no schedule of its own (`Cron.Never()`). When a prerequisite completes, it triggers each enabled dependent whose prerequisites all have a new generation, so a chain C → B → A runs in order and a dependent of several prerequisites runs after the last one.
- The gate runs when a new job of a dependent is about to be enqueued (triggered by a prerequisite, or manually). Without a new generation of every prerequisite, the job goes to the custom final state `PrerequisitesNotMet` instead of `Enqueued`. It never takes a worker, it doesn't end as a false `Succeeded`, and it fires no hook.
- Retries aren't gated again, because their generations were already consumed when the job was created.
- Per dependent, the plan can choose `MinInterval` (a too-early trigger is postponed to the end of the interval, counted from the end of the previous run) or `RunOnOwnSchedule` (the dependent keeps its cron and each occurrence is gated). A dependent that is running is never triggered; it re-checks when it finishes.
- A manual run skips the gate and consumes the current generations. The plan recognizes its own triggers by a marker in storage, and schedule occurrences by Hangfire's enqueue reason, the only signal Hangfire 1.8 records.

**Consequences**:
- A dependent runs right after its last prerequisite completes, so no gated runs pile up in the dashboard. `PrerequisitesNotMet` only shows up for a manual trigger without new cycles, or for two prerequisites completing at the same time.
- The dashboard shows each gated run as `PrerequisitesNotMet`, with its reason. Listing and counting jobs in that state isn't available yet ([005-recurring-jobs](../005-recurring-jobs/spec.md) RN-007).
- Generations are consumed when the job is enqueued. If the dependent then fails, its next run needs a new cycle of the prerequisites.

**Related**: RN-001 of this feature; [005-recurring-jobs](../005-recurring-jobs/spec.md) FR-009..FR-011; [011-local-hosting](../011-local-hosting/spec.md) FR-005.
