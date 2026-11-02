# Research: Handler configuration classes

Product decisions are in [spec.md](spec.md) Clarifications (maintainer, 2026-11-02). These are the design decisions.

## R-001: Explicit values per item on the step

- **Decision**: the builder records only what is called; the built `ExecutionPlanStep` exposes each explicit value as nullable (or "not set"), and providers resolve each item independently.
- **Rationale**: EF semantics (RN-001): the configuration changes only what it sets; attributes and options keep the rest.
- **Alternatives considered**: copying attribute values into the step at `Build` (loses the source, so a later option couldn't win over an attribute and lose to the builder).

## R-002: Overlap as three states

- **Decision**: overlap on the builder is "not set" (attribute decides), `NoOverlap(timeout)`, or `AllowOverlap()`; the step's `OverlapTimeout` becomes the resolved value (null = overlap allowed), computed from builder > attribute.
- **Rationale**: `AllowOverlap()` must be able to cancel an attribute's `[NoOverlap]`.
- **Alternatives considered**: a nullable timeout only (can't tell "allowed explicitly" from "not set").

## R-003: Availability re-evaluated at configuration

- **Decision**: `ExecutionPlan` records the `MissingPrerequisite` policy of `Build`. When a provider registers the plan, it computes each step's enabled state (builder `Enabled` ?? `SetJobAutoRun<T>` ?? global) and re-runs the planner's availability pass with that policy.
- **Rationale**: with "builder wins, option otherwise" (AC-002.6), an option can turn off a prerequisite after `Build`; the plan's Throw/SkipDependents must still apply to it.
- **Alternatives considered**: only the builder's `Enabled` counts in the plan (contradicts AC-002.6); evaluating at `Build` (options may not exist yet).

## R-004: Provider settings as named settings

- **Decision**: `HandlerBuilder.HasSetting(string key, object value)` (public, documented for provider authors) and `ExecutionPlanStep.Settings`; provider extensions use keys prefixed with the provider name (e.g. `Hangfire:Queue`).
- **Rationale**: the core can't reference Hangfire (constitution I); EF uses annotations for the same reason.
- **Alternatives considered**: `InternalsVisibleTo` from the core to Hangfire (couples the packages and blocks third-party providers).

## R-005: No Hangfire "timeout" extension

- **Finding**: Hangfire 1.8 has no execution-timeout filter. `LatencyTimeoutAttribute` deletes a job that waited in the queue longer than a limit; it doesn't stop a running job. The README example `[Timeout("00:10:00")]` (section "Hangfire Native Attributes") refers to an attribute that doesn't exist in Hangfire 1.8.
- **Options**: (a) drop "timeout" from the Hangfire extensions (queue and retries only) and fix the README; (b) map it to `LatencyTimeoutAttribute` and name it accordingly (`DeleteIfWaitingLongerThan`); (c) implement an execution timeout that cancels the handler's `CancellationToken` after the limit (new behavior, needs its own requirement).
- **Decision** (maintainer, 2026-11-02): option (a). The Hangfire extensions are queue and retries only; the README example `[Timeout("00:10:00")]` is removed.
