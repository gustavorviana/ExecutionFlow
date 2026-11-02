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

- **Decision** (revised for FR-006): `ExecutionPlan` records the `MissingPrerequisite` policy of `Build`. At `Configure`, each step's enabled state is re-resolved as `ConfiguredEnabled ?? global default` and the availability pass runs again with that policy. In the user plan, `ConfiguredEnabled` comes from the user's builder only (the options layer never counts); in the automatic plan it comes from the `GetPlanOf` builder, which is the options layer.
- **Rationale**: the global default (`GlobalRecurringAutoRun`) and, in the automatic plan, `GetPlanOf().Enabled(false)` are only known at `Configure`; a prerequisite they turn off must still follow Throw/SkipDependents.
- **Alternatives considered**: evaluating at `Build` (options don't exist yet); dropping re-resolution (the global default would be ignored by plan handlers).

## R-004: Provider settings as named settings

- **Decision**: `HandlerBuilder.HasSetting(string key, object value)` (public, documented for provider authors) and `ExecutionPlanStep.Settings`; provider extensions use keys prefixed with the provider name (e.g. `Hangfire:Queue`).
- **Rationale**: the core can't reference Hangfire (constitution I); EF uses annotations for the same reason.
- **Alternatives considered**: `InternalsVisibleTo` from the core to Hangfire (couples the packages and blocks third-party providers).

## R-005: No Hangfire "timeout" extension

- **Finding**: Hangfire 1.8 has no execution-timeout filter. `LatencyTimeoutAttribute` deletes a job that waited in the queue longer than a limit; it doesn't stop a running job. The README example `[Timeout("00:10:00")]` (section "Hangfire Native Attributes") refers to an attribute that doesn't exist in Hangfire 1.8.
- **Options**: (a) drop "timeout" from the Hangfire extensions (queue and retries only) and fix the README; (b) map it to `LatencyTimeoutAttribute` and name it accordingly (`DeleteIfWaitingLongerThan`); (c) implement an execution timeout that cancels the handler's `CancellationToken` after the limit (new behavior, needs its own requirement).
- **Decision** (maintainer, 2026-11-02): option (a). The Hangfire extensions are queue and retries only; the README example `[Timeout("00:10:00")]` is removed.

## R-006: One warning rule for the options layer

- **Decision**: a handler in the user plan that also has `GetPlanOf` values (directly or through the obsolete `SetJobAutoRun`/`SetJobTimeZone`) gets one trace warning naming it and the ignored layer, whatever items the user plan sets. It replaces the per-item warning of AC-002.5.
- **Rationale**: with FR-006 the options layer is dropped as a whole, so warning only for overridden items would hide the dropped ones.
- **Alternatives considered**: warning per overridden item (misses items the user plan doesn't set but still drops).

## R-007: `GetPlanOf` for an unregistered handler

- **Decision**: `GetPlanOf<T>()` doesn't register `T`. At `Configure`, a `GetPlanOf` for a type that isn't a registered recurring handler throws naming it.
- **Rationale**: it keeps today's behavior of `SetJobAutoRun`/`SetJobTimeZone` ("references type … which is not registered"), which now route to `GetPlanOf`, and registration stays in `Scan`/`Add`.
- **Alternatives considered**: `GetPlanOf` registering the handler (a second way to register, against the maintainer's "no duplicate ways" goal).

## R-008: `GetPlanOf(Type)` overload

- **Decision**: besides `GetPlanOf<T>()`, a public `GetPlanOf(Type)` returning `HandlerBuilder`, mirroring `ExecutionPlanner.Add(Type)`.
- **Rationale**: the obsolete `SetJobAutoRun(Type, bool)`/`SetJobTimeZone(Type, string)` live in `ExecutionFlow.Hangfire` and must call it; the core has no `InternalsVisibleTo` for Hangfire.
- **Alternatives considered**: `InternalsVisibleTo` from the core (couples the packages).

## R-009: Global defaults stay provider options

- **Decision**: the core resolves each handler's enabled state and time zone, asking the provider for its global defaults through protected virtuals (`IsRecurringHandlerEnabledByDefault()`, `DefaultRecurringTimeZone`), so `GlobalRecurringAutoRun`/`RecurringTimeZone` stay where they are.
- **Rationale**: one resolution in the core keeps Hangfire and the local host identical (011); the global options are provider-level and already public.
- **Alternatives considered**: moving the globals to the core (a public API move not requested).
