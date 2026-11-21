# Implementation Plan: ID format validation

**Branch**: `development` | **Date**: 2026-11-20 | **Spec**: [spec.md](spec.md)

**Input**: [spec.md](spec.md). Decisions in [research.md](research.md).

## Summary

One internal validator in the core (`IdFormat`, R-001) checks the ID format of FR-001 with a per-character test (R-002) and builds a one-line, escaped error message (R-003). It's called at every entry point before any storage access (R-004): custom IDs in `HangfireDispatcher.Dispatch`, query IDs in the four `HangfireExecutionManager` string methods and `Trigger(string)`, explicit recurring IDs in the core when a handler is registered (attribute or `HandlerBuilder.Id`), and generated recurring IDs in `RecurringJobResolver`. No public API is added; the behavior change is breaking, confirmed by the maintainer, and ships in 2.0.0 (R-007).

## Technical Context

**Language/Version**: C# on `netstandard2.0`; tests on .NET 10

**Primary Dependencies**: core: none; Hangfire: `Hangfire.Core` 1.8.23

**Storage**: N/A (no stored data changes)

**Testing**: xUnit + NSubstitute; existing `DispatcherTests`, `ExecutionManagerTests`, `ExecutionFlowOptionsTests`, `HandlerConfigurationHangfireTests`; new `IdFormatTests` (core)

**Target Platform**: any `netstandard2.0` runtime

**Project Type**: library (NuGet packages, lockstep version)

**Performance Goals**: validation is O(length of the ID), no allocation on the valid path

**Constraints**: core stays dependency-free; no new public API; checks happen before any storage access (SC-004)

**Scale/Scope**: every ID entry point of the library (7 methods, 3 recurring ID sources)

## Constitution Check

*Pre-design and post-design: same result.*

- I. Dependency-free core: ✅ `IdFormat` uses only `System`.
- II. Processors isolated: ✅ the rule lives in the core; Hangfire-only concepts (`IJobIdGenerator`, storage queries) call it from `ExecutionFlow.Hangfire`.
- III. Optional integrations: ✅ no new package.
- IV. Producer-only mode: ✅ `Publish`/`Schedule` validation touches no global state.
- V. Minimal public surface / breaking changes: ✅ no new public API (`IdFormat` is internal, R-001). Documented behavior changes incompatibly ([contracts/public-api.md](contracts/public-api.md)): the maintainer confirmed it, recorded in the spec's Clarifications, and it ships in a major version, 2.0.0 (R-007).
- VI. Dispatch doesn't block: ✅ a CPU-only check before the existing lock.
- VII. Tested, traceable: ✅ every AC names a test; the new test class is `IdFormatTests`.

## Project Structure

### Documentation (this feature)

```text
specs/016-id-format-validation/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/public-api.md
├── checklists/requirements.md
└── tasks.md            # /speckit-tasks
```

### Source Code (repository root)

| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | new internal `IdFormat`; `InternalsVisibleTo` for `ExecutionFlow.Hangfire`; checks in `ExecutionFlowOptions` (attribute IDs) and `HandlerBuilder.Id`; XML docs | Yes (confirmed) |
| `ExecutionFlow.Hangfire` | checks in `HangfireDispatcher.Dispatch` and `Trigger(string)`, `HangfireExecutionManager` string methods, `RecurringJobResolver.Resolve`; XML docs | Yes (confirmed) |
| `ExecutionFlow.Hosting` | none (explicit IDs are checked in the core) | — |

```text
Src/ExecutionFlow/Abstractions/IdFormat.cs                      (new)
Src/ExecutionFlow/ExecutionFlow.csproj                          (InternalsVisibleTo)
Src/ExecutionFlow/ExecutionFlowOptions.cs                       (attribute IDs)
Src/ExecutionFlow/Abstractions/HandlerBuilder.cs                (Id)
Src/ExecutionFlow/Abstractions/ICustomIdEvent.cs, Attributes/RecurringAttribute.cs, Attributes/RecurringIntervalAttribute.cs  (XML docs)
Src/ExecutionFlow.Hangfire/Infrastructure/HangfireDispatcher.cs
Src/ExecutionFlow.Hangfire/Infrastructure/HangfireExecutionManager.cs
Src/ExecutionFlow.Hangfire/Infrastructure/RecurringJobResolver.cs
Src/ExecutionFlow.Hangfire/IJobIdGenerator.cs, IRecurringTrigger.cs  (XML docs)
README.md                                                       (ID format, breaking changes)

Tests/ExecutionFlow.Tests/Abstractions/IdFormatTests.cs         (new)
Tests/ExecutionFlow.Tests/ExecutionFlowOptionsTests.cs
Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/DispatcherTests.cs
Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/ExecutionManagerTests.cs
Tests/ExecutionFlow.Hangfire.Tests/HangfireValidationTests.cs   (Trigger("") now throws ArgumentException)
Tests/ExecutionFlow.Hangfire.Tests/HandlerConfigurationHangfireTests.cs  (generated IDs)
```

**Structure Decision**: existing layout; one new internal file in the core and one new test class.
