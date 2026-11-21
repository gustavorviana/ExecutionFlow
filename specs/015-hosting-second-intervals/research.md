# Research: Intervals of 1 second in the local host

Decisions for [spec.md](spec.md). Maintainer decisions are recorded in the spec's Clarifications; this file records the technical ones.

## R-001 Where the 1-second minimum is checked

- **Decision**: keep the check in the core, in the same two places as today, with the bound changed from 1 minute to 1 second: `HandlerBuilder.Interval(TimeSpan)` (`ArgumentOutOfRangeException`) and `ExecutionFlowOptions.ParseInterval` for `[RecurringInterval]` (`InvalidOperationException`). Exception types and messages keep their shape; only the bound and the text ("at least one second") change.
- **Rationale**: the minimum is provider-neutral (FR-003). Providers raise their own effective minimum later (Hangfire, R-004) without throwing (RN-002).
- **Alternatives**: moving validation to each provider. Rejected: it would move where and when an invalid value fails, a behavior change for no gain.

## R-002 Fractions of a second

- **Decision**: `Interval(TimeSpan)` keeps the exact `TimeSpan` in the registry (AC-003.3). The local host waits the exact value (`IClock.Delay` already takes any `TimeSpan`). Hangfire truncates to whole seconds when it computes the effective interval (R-004).
- **Rationale**: requiring whole seconds would break callers that pass fractions today (spec Clarifications).

## R-003 Local host: no scheduling change needed

- **Decision**: the local host already schedules any `TimeSpan` (`RecurringHandlerHost.NextOccurrence`) and already reads 6-field crons with `CronFormat.IncludeSeconds` (`RecurringHandlerHost.cs:47-48`). FR-001 and FR-004 need only the core bound (R-001) and tests (AC-001.x, AC-004.x).
- **Verification**: the tests run on `FakeTimeProvider`, which drives `TimeProviderClock`; SC-002 is a test that advances 100 occurrences and checks there's no drift (the cadence is computed from the start, `start + n * interval`, so it can't accumulate error).

## R-004 Hangfire: effective interval

- **Decision**: one internal function, `HangfireSetup.EffectiveIntervalOf(RecurringJobRegistryInfo)`, used by both registration (`RecurringJobResolver.ResolveCron`) and the ticker filter, so they never disagree:
  1. truncate to whole seconds;
  2. if the result is a whole number of minutes → return it unchanged (minute path, RN-001);
  3. otherwise (seconds part) → `max(value, SchedulePollingInterval)`.
- The warnings (R-006) are emitted once, at `Build`, not by this function.
- **Rationale**: a single source of truth avoids a ticker gating with the declared interval while the cron was built from the raised one.

## R-005 Hangfire: crons and tickers for the second path

- **Decision**: extend `IntervalCron`:
  - `TryGetExactCron`: the current minute/hour rules are unchanged; new rule for sub-minute intervals: `n` whole seconds with `60 % n == 0` → `*/n * * * * *` (`n = 1` → `* * * * * *`).
  - New constant `SecondTicker = "* * * * * *"`; `ToCron` returns it for an interval with a seconds part and no exact cron (e.g. 45 s, 90 s), and `Ticker` (per minute) as today for whole-minute intervals.
- `IntervalTickerFilter` stays the single filter; it passes the effective interval and a resolution (minute or second) to `IntervalTickerStore.TryStartRun`.
- `IntervalTickerStore.TryStartRun` truncates `utcNow` to the resolution: minute (as today) for the minute path, second for the second path. The stored value stays "UTC ticks of the truncated start", so a handler that switches path (AC-005.10) reads a compatible value: a minute-truncated last run compared with a second-truncated now only makes the first wait slightly longer, never shorter.
- **Rationale**: Hangfire 1.8.23 parses 6-field crons natively (its internalized Cronos 0.11.1 uses `CronFormat.IncludeSeconds`; checked in the binary), so the ticker only needs a different cron and truncation.

## R-006 Hangfire: warnings and the polling minimum

- **Decision**: `HangfireOptions.SchedulePollingInterval` (`TimeSpan`, default 15 s, `ThrowIfLocked` like the other settings, `ArgumentOutOfRangeException` below 1 s). When recurring jobs are registered (`Build`), for each handler whose own schedule is registered (`HasRegisteredSchedule`: auto-run off and `RunOnOwnSchedule` dependents included, dependents triggered by the plan excluded):
  - interval with a seconds part below the minimum → warning "interval X set, but Hangfire doesn't support less than M; using M" (AC-005.4);
  - whole-minute interval below the minimum → warning, no change (AC-005.8);
  - cron whose minimum gap is below the minimum → warning, no change (FR-002).
- Warnings use `Trace.TraceWarning`, like the existing Hangfire configuration warnings (`HangfireSetup.cs:64,77`, 012 AC-002.6). Tests capture them with a `TraceListener`, as the 012 tests do.
- **Rationale**: same channel as the existing configuration warnings; one warning per handler (SC-003). Not at `Configure` (as first planned): `Configure` also runs before `BuildDispatcherOnly`, and producer-only mode must not warn (AC-005.11, constitution IV).

## R-007 Hangfire: minimum gap of a cron without Cronos

- **Problem**: Hangfire.Core 1.8.23 doesn't expose its cron parser (public API: only the `Cron` string helpers; Cronos is internalized). Constitution II allows Cronos only in `ExecutionFlow.Hosting`.
- **Decision**: a small internal analyzer in `ExecutionFlow.Hangfire` (`CronGap.MinimumGap(string cron)`), conservative:
  - reads only the seconds field (6-field crons; a 5-field cron has seconds = `{0}`) and the minutes field;
  - supports `*`, `n`, `a-b`, `a,b`, `*/s`, `a-b/s`, `a/s` (the only syntaxes those fields accept; `L`, `W`, `#`, `?` and names don't apply to them);
  - minimum gap = the smallest circular distance between allowed seconds when the minute field allows consecutive minutes, otherwise within the minute; for a minimum of 60 s or more, the same over minutes.
  - On anything it can't read, it returns "unknown" and no warning is emitted (Hangfire still validates the cron on `AddOrUpdate`, 005 edge case).
- It may over-report (e.g. a minute field that only allows consecutive minutes on some hours); a false warning is acceptable, a crash or a changed cron is not (RN-002).
- **Alternatives**: referencing Cronos from `ExecutionFlow.Hangfire` (needs a constitution amendment; rejected for a warning); reflecting into Hangfire's internal Cronos (fragile across versions).

## R-008 Ticker lock

- **Decision**: keep the global lock `executionflow:interval-lock` (012 R-001).
- **Rationale**: Hangfire's recurring scheduler runs each due job at most once per polling cycle, so a second-level ticker takes the lock at most once per cycle (15 s by default), not once per second. The critical section is one hash read and one write. Per-job locks can be introduced later without a spec change (pure refactor).

## R-009 Skip logs in the local host

- **Decision**: `HostingOptions.LogSkippedOccurrences` (bool, default `true`) plus the builder extension `LogSkippedOccurrences<THandler>(this HandlerBuilder<THandler>, bool = true)` storing `Hosting:LogSkippedOccurrences`, resolved by `HostingOptions.LogSkippedOccurrencesOf(Type)` like `IntervalFromEndOf`/`SkipWhenOverlappingOf`. `RunCoordinator` checks it before the two `LogInformation("ExecutionFlow: skipped ...")` calls (`RunCoordinator.cs:62,129`).
- **Rationale**: same pattern as the other per-handler hosting settings (014 R-004); the shared `ExecutionFlow.Hosting` log category makes category filtering too coarse (spec Clarifications).

## R-010 Hangfire's scheduler polling (verified 2026-11-20, T005)

- **Result**: confirmed by inspecting `Hangfire.Core.dll` 1.8.23 (`netstandard2.0`) by reflection: `RecurringJobScheduler` has a `TimeSpan pollingDelay` constructor parameter (field `_pollingDelay`); `BackgroundJobServer.GetRequiredProcesses` is the only method that constructs it, and it reads `BackgroundJobServerOptions.SchedulePollingInterval`; a new `BackgroundJobServerOptions` has `SchedulePollingInterval = 00:00:15`. So the 15 s default holds. The misfire behavior (an overdue occurrence fires once) wasn't inspected; it doesn't affect the spec's behavior.

- Assumption: Hangfire 1.8's `RecurringJobScheduler` checks due jobs once per `BackgroundJobServerOptions.SchedulePollingInterval` (15 s by default) and, with the default misfire handling, fires an overdue job once. This is from Hangfire's documentation, not from reading its code.
- **Verification**: a task reads Hangfire 1.8.23's `RecurringJobScheduler` (decompiled or source) before implementing R-006; if the polling source differs, the default of `SchedulePollingInterval` and the docs (FR-006) are adjusted. The spec's behavior (minimum = configured setting) doesn't depend on it.
