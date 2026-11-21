# Implementation Plan: Handlers and context

**Branch**: `main` (brownfield capability) | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Migrated from `docs/specs/handlers-and-context/plan.md` (status: approved; covers the spec's to-be changes 1–4). Decisions and findings are in [research.md](research.md).

## Summary

Give handlers processor-neutral job facts (`JobId`, `AttemptNumber`) through `FlowContextBuilder.SetJob`, typed Hangfire access through `GetPerformContext()`, and protect read-only parameters in any casing.

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: none in the core; Hangfire in `ExecutionFlow.Hangfire`

**Storage**: N/A

**Testing**: xUnit + NSubstitute (`FlowContextBuilderTests`, `FlowParametersTests`, `HangfireJobDispatcherTests`)

**Target Platform**: any .NET runtime supporting `netstandard2.0`

**Project Type**: library

**Performance Goals**: N/A

**Constraints**: handlers must not need Hangfire types to read job facts

**Scale/Scope**: one context per execution

## Constitution Check

- I. Dependency-free core: ✅ job facts are plain properties on the core context.
- II. Processors isolated: ✅ `GetPerformContext()` lives in the Hangfire package; handlers no longer cast `PerformContext` (F-002).
- V. Minimal public surface: ✅ infrastructure keys stay internal; only additions are public.
- VII. Tested, traceable behavior: ✅ every AC names its tests, except AC-003.3 (API-shape decision, see F-003).

## Project Structure

### Documentation (this feature)

```text
specs/001-handlers-and-context/
├── spec.md
├── plan.md
├── research.md
└── tasks.md
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | `FlowContext.JobId`, `FlowContext.AttemptNumber`, `FlowContextBuilder.SetJob(string, int)` | No (additions). Behavior: case-variant writes to read-only keys now throw |
| `ExecutionFlow.Hangfire` | `FlowContextExtensions.GetPerformContext()`, and `HangfireJobDispatcher` sets the job facts | No |

```text
Src/ExecutionFlow/Abstractions/ (IHandler.cs, FlowContext, FlowContextBuilder, FlowParameters.cs)
Src/ExecutionFlow.Hangfire/ (HangfireJobDispatcher, FlowContextExtensions)
Tests/ExecutionFlow.Tests/ and Tests/ExecutionFlow.Hangfire.Tests/
```

## Design

- `FlowParameters._readOnlyKeys` uses `StringComparer.OrdinalIgnoreCase`, like `_items`.
- `FlowContextBuilder.SetJob(jobId, attemptNumber)` stores the processor's job facts. `Build` passes them to the context through internal setters. Defaults: `JobId = null`, `AttemptNumber = 1`.
- `HangfireJobDispatcher.CreateContextBuilder` calls `SetJob(performContext.BackgroundJob.Id, RetryCount + 1)`. `RetryCount` is read like the state filter reads it (raw parameter, `int.TryParse`, 0 on failure). A null `PerformContext` leaves the defaults.
- Keys: no infrastructure key is public (maintainer's decision). They stay internal in `ContextConsts`: `GetPerformContext()` covers Hangfire access, and the custom name is on the event (`ICustomNameEvent`).
- `FlowContextExtensions.GetPerformContext(this FlowContext)` returns the `PerformContext` or `null`.

Versioning impact: 1.2.0 (minor; see constitution "Versioning and Release").

## Complexity Tracking

No constitution violations.
