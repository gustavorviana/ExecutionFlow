# Research: Dependencies by attribute

Product decisions are in [spec.md](spec.md) Clarifications (maintainer, 2026-11-02). These are the design decisions.

## R-001: The automatic plan is formed in the core, at Configure

- **Decision**: `ExecutionFlowSetup.Configure` forms the automatic plan after the user's callback and before the options lock, by feeding every registered recurring handler to an `ExecutionPlanner` and building it with `AutomaticPlanMissingPrerequisite`. The enabled state is asked from the provider through a protected virtual on the options.
- **Rationale**: one implementation for every provider (Hangfire and the local host), reusing the planner's ordering and validation (spec RN-002); configuration errors surface at startup like an explicit plan's.
- **Alternatives considered**: forming it in each provider's `OnConfigured` (duplicated per provider, and the options are already locked); forming it lazily at `Build` (errors surface later than for an explicit plan).

## R-002: Entry run mode replaces the attribute's run mode as a whole

- **Decision**: if the entry sets `RunMode(...)`, the attribute's `[DependentRun]` is ignored entirely; otherwise the attribute's mode applies. Confirmed by the maintainer (2026-11-02), together with a single mode in both places.
- **Rationale**: the run mode is one choice among three (010 FR-007). Merging field by field could combine the entry's `RunOnOwnSchedule` with the attribute's `MinInterval` into an invalid mode the user never wrote.
- **Alternatives considered**: field-by-field merge (rejected for the reason above).

## R-003: The automatic plan bypasses the explicit plan's SetJobAutoRun conflict

- **Decision**: the automatic plan is stored through an internal path that doesn't call `OnPlanRegistering`, so Hangfire's "plan + `SetJobAutoRun` → throw" rule (005 FR-009) doesn't apply; instead `SetJobAutoRun`/`GlobalRecurringAutoRun` are the input of each step's `Enabled`.
- **Rationale**: in the automatic plan, `SetJobAutoRun` is the only on/off switch (spec Clarifications).
- **Alternatives considered**: calling `UsePlan` internally (would throw on any `SetJobAutoRun`).

## R-004: `[DependentRun].MinInterval` only requires a positive value

- **Decision** (confirmed by the maintainer, 2026-11-02): same `[d.]hh:mm:ss` parser as `[RecurringInterval]`, but the value only has to be positive, not at least one minute.
- **Rationale**: it mirrors `ExecutionPlanEntry.MinInterval`, which accepts any positive `TimeSpan` (010 AC-007.2); the one-minute minimum belongs to schedules, not to the dependent's spacing.
- **Alternatives considered**: a one-minute minimum (would make the attribute stricter than the fluent method).
