# Implementation Plan: Recurring jobs

**Branch**: N/A (migrated) | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/recurring-jobs/plan.md` (v1.2.0, status: approved; the spec's "Decided to-be changes", items 1–7). Decisions and findings are in [research.md](research.md).

## Summary

Fixes the recurring-job defects found in the as-is mapping: F-001, F-002, F-003, F-004, F-005, F-007 and F-008. It adds a stable ID and time zones, and moves the on/off switch to the schedule itself. v1.3.0 adds Hangfire enforcement of the execution plan (FR-009..FR-012; design in [010 research.md](../010-execution-plan/research.md) R-003).

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: `Hangfire.Core` 1.8.23 (in `ExecutionFlow.Hangfire` only)

**Storage**: Hangfire job storage (recurring jobs; plan generations in v1.3.0)

**Testing**: xUnit + NSubstitute (`RecurringRegistrationTests`, `RecurringValidationTests`, `ExecutionPlanHangfireTests`, `PrerequisiteGateTests`)

**Target Platform**: any .NET runtime supporting `netstandard2.0`

**Project Type**: library

**Performance Goals**: N/A (registration runs once at startup)

**Constraints**: disabled handlers cause no storage writes on schedule (RN-003)

**Scale/Scope**: tens of recurring handlers per consumer

## Constitution Check

- I. Dependency-free core: ✅ the core only gains attribute/registry properties.
- II. Processors isolated: ✅ scheduling, validation and orphan cleanup live in `ExecutionFlow.Hangfire`.
- IV. Producer-only mode mutates no global state: ✅ `BuildDispatcherOnly()` registers no recurring jobs (AC-002.2).
- V. Minimal public surface: ⚠️ behavior changes in 1.2.0 (see Risks); `HangfireAutoRunFilter` is `[Obsolete]`, not removed.
- VII. Tested, traceable behavior: ✅ every AC names a test.

## Project Structure

### Documentation (this feature)

```text
specs/005-recurring-jobs/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `RecurringAttribute.Id` and `RecurringAttribute.TimeZone` (optional named properties). `RecurringJobRegistryInfo.Id` and `RecurringJobRegistryInfo.TimeZone` | No |
| `ExecutionFlow.Hangfire` | `HangfireOptions.RecurringTimeZone`, `SetJobTimeZone<T>` / `SetJobTimeZone(Type, string)`. `HangfireAutoRunFilter` is `[Obsolete]` and no longer registered | **Behavior**: a recurring handler without `[Recurring]` now fails at `Configure`; on/off is per storage |

```text
Src/ExecutionFlow/Attributes/RecurringAttribute.cs
Src/ExecutionFlow.Hangfire/HangfireSetup.cs            (OnConfigured, RegisterRecurring)
Src/ExecutionFlow.Hangfire/HangfireOptions.cs
Src/ExecutionFlow.Hangfire/Infrastructure/Filters/HangfireAutoRunFilter.cs  ([Obsolete])
Tests/ExecutionFlow.Hangfire.Tests/
```

## Design

- **Validation in `HangfireSetup.OnConfigured`** (which runs during `Configure`, next to the existing `SetJobAutoRun` check):
  - Every recurring handler must have a cron. Otherwise throw `InvalidOperationException` naming the handler.
  - Every time zone (global, per handler in options, on the attribute) must resolve with `TimeZoneInfo.FindSystemTimeZoneById`.
  - Recurring job IDs must be unique.
- **ID resolution:** `RecurringJobIds.Resolve(registration, generator)` = `registration.Id ?? generator.GenerateId(handlerType)`. It's used by `RegisterRecurring` and by `HangfireDispatcher.Trigger(Type)`.
- **Time zone resolution:** `SetJobTimeZone<T>` > `[Recurring(TimeZone)]` > `RecurringTimeZone` > UTC. It's passed through `RecurringJobOptions.TimeZone`.
- **On/off:** a disabled handler (per-handler setting, else global) is registered with `Cron.Never()`. `HangfireAutoRunFilter` is no longer registered, because with no automatic fires for disabled handlers there's nothing to cancel. That removes the fragile detection of manual triggers by text (F-004).
- **Orphans:** remove only recurring jobs whose `Job.Method` is `HangfireJobDispatcher.DispatchRecurringAsync` and whose ID isn't registered. Jobs that fail to load (`Job == null`) are left alone.

### Versioning impact

1.2.0 (FR-009..FR-012: 1.3.0).

### Risks

| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Apps with a recurring handler that has no `[Recurring]` | `Configure` now throws | That already failed at `Build()`, so the error just moves earlier and gets clearer |
| Consumers relying on per-consumer on/off | Behavior changes | It was nondeterministic (F-008). Release notes point to queues |
| Time zone IDs differ between Windows (.NET Framework) and IANA | Configuration error on some hosts | A clear message, documented in the README |

## Complexity Tracking

No constitution violations. The 1.2.0 behavior changes were accepted with release notes (see Risks).
