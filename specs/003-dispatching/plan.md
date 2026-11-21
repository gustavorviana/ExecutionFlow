# Implementation Plan: Dispatching v1.2.0

**Branch**: N/A (migrated) | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/dispatching/plan.md` (status: approved). Decisions are in [research.md](research.md).

## Summary

Fixes the defects found while mapping dispatching ([research.md](research.md), Findings), and documents the handler routing rule. Implements AC-001.3, AC-002.2, AC-003.2, AC-004.3, FR-007 and FR-008, and adds tests for criteria that had none.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: Hangfire.Core 1.8 (in `ExecutionFlow.Hangfire` only)

**Storage**: Hangfire job storage

**Testing**: xUnit + NSubstitute (`DispatcherTests`, `HangfireJobDispatcherTests`, `HandlerJobFilterProviderTests`, `JobParametersTests`)

**Target Platform**: any .NET runtime supporting `netstandard2.0`

**Project Type**: library

**Performance Goals**: none stated (SC-001)

**Constraints**: `Publish`/`Schedule` stay synchronous (SC-002)

**Scale/Scope**: one dispatch pipeline shared by `Publish` and both `Schedule` overloads

## Constitution Check

- I. Dependency-free core: ✅ the core only gets XML doc changes.
- II. Processors isolated: ✅ all Hangfire changes stay in `ExecutionFlow.Hangfire`.
- IV. Producer-only mode mutates no global state: ✅ the new option only affects `HandlerJobFilterProvider`, registered by the full build.
- V. Minimal public surface: ✅ one new public option; `JobParameters` is internal.
- VI. Dispatch doesn't block: ✅ no new waits.
- VII. Tested, traceable behavior: ✅ every AC names a test.

## Project Structure

### Documentation (this feature)

```text
specs/003-dispatching/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | XML docs on `IEventDispatcher` and `PublishResult` | No |
| `ExecutionFlow.Hangfire` | `HangfireDispatcher` pipeline, `HangfireExecutionManager` null guards, `HandlerJobFilterProvider`, new `HangfireOptions.RetryUnregisteredEventJobs` | No API break. Behavior changes for null events, empty `CustomId`, and retries of jobs without a handler |

```text
Src/ExecutionFlow.Hangfire/Infrastructure/
├── HangfireDispatcher.cs
├── HangfireJobDispatcher.cs
├── HangfireExecutionManager.cs
├── HandlerJobFilterProvider.cs
└── JobParameters.cs
```

## Design

- `HangfireDispatcher.Publish` and both `Schedule` overloads delegate to one private `Dispatch<TEvent>(TEvent, IState)`. The steps are: null guard, dedup (only with a non-empty custom ID), then job creation.
- Job creation: when the client implements `IBackgroundJobClientV2`, the job is built with `Job.FromExpression` and created with `Create(job, state, parameters)`, which stores the `CustomId` parameter in the same storage transaction. Other clients use `Create(job, state)` and then `SetJobParameter`.
- `JobParameters` (internal) is the single place that reads and writes the `CustomId` parameter, so the encoding stays consistent and legacy values stay readable.
- `HandlerJobFilterProvider` adds `AutomaticRetryAttribute { Attempts = 0 }` for ExecutionFlow event jobs whose handler isn't registered on this host, unless `RetryUnregisteredEventJobs` is set.

## Versioning impact

Minor: 1.2.0 (lockstep, see constitution [Versioning and Release](../../.specify/memory/constitution.md)).

## Risks

| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Parameter encoding differs between V2 and `SetJobParameter`, which would break lookups of jobs already in storage | `IsRunning`/`Cancel` by custom ID miss old jobs | A spike (T004), and `JobParameters.ReadCustomId` accepting both formats |
| Users relying on retries for jobs with no handler | Jobs fail sooner | `RetryUnregisteredEventJobs = true`, and release notes |
| Custom `IBackgroundJobClient` without V2 | Not atomic | Fallback path, documented in AC-007.2 |

## Complexity Tracking

No constitution violations.
