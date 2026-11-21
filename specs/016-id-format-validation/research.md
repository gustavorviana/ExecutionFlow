# Research: ID format validation

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## R-001 One validator in the core

- **Decision**: an internal static class `IdFormat` in `ExecutionFlow` (`Src/ExecutionFlow/Abstractions/IdFormat.cs`) with `IsValid(string)` and `Validate(string id, string paramName)` / `ValidateRecurring(Type handlerType, string id)`. `ExecutionFlow.Hangfire` gets access through a new `InternalsVisibleTo` (the core already exposes internals to `ExecutionFlow.Hosting` and the tests).
- **Rationale**: FR-001 is one rule for every ID; one place keeps the character set and the message identical at every entry point. Internal keeps the public surface minimal (principle V).
- **Alternatives**: a public `IdFormat` (rejected: new public API nobody asked for; can be made public later without a break); a validator per package (rejected: the rule would drift); a `Regex` (rejected: a simple per-character check is clearer, allocation-free and has no regex timeout concerns on `netstandard2.0`).

## R-002 Character check

- **Decision**: walk the ID by code point (`char.IsSurrogatePair` → read both `char`s) and allow a code point when `CharUnicodeInfo.GetUnicodeCategory(id, index)` is a letter (`UppercaseLetter`, `LowercaseLetter`, `TitlecaseLetter`, `ModifierLetter`, `OtherLetter`, `LetterNumber`), a combining mark (`NonSpacingMark`, `SpacingCombiningMark`) or `DecimalDigitNumber`, or when it's one of `_ - . + ` [ ] , =` or the space (U+0020 only). Everything else is rejected, notably `Control`, `Format` (U+202E, U+200B...), `LineSeparator`, `ParagraphSeparator`, `Surrogate` (lone halves) and other whitespace. The ID must also contain at least one non-space character.
- **Rationale**: these are the categories C# accepts in identifiers, minus `Format` (`ConnectorPunctuation` is covered by `_`), so every type full name without format characters is a valid default ID. `Format` characters are excluded because they're invisible and can disguise what a log line shows. `CharUnicodeInfo.GetUnicodeCategory(string, int)` exists on `netstandard2.0` and handles surrogate pairs.
- **Alternatives**: ASCII only (rejected by the maintainer: a handler named `Criação` would fail with its default ID); `char.IsLetterOrDigit` per `char` (rejected: misses combining marks and supplementary-plane letters); Unicode only for recurring IDs (rejected: two rules).

## R-003 Error message

- **Decision**: `ArgumentException(message, paramName)` where the message is `The ID '<escaped>' is invalid. IDs can only contain letters, digits, spaces and _ - . + ` [ ] , =` (for recurring IDs: `The recurring job ID '<escaped>' of handler '<FullName>' is invalid. ...`). Escaping: `\r`, `\n`, `\t`, `\0`, `\\` stay readable as `\r`, `\n`, `\t`, `\0`, `\\`; any other `Control`, `Format`, `LineSeparator`, `ParagraphSeparator` or lone `Surrogate` char becomes `\uXXXX`. Other characters are kept. `null` → `ArgumentNullException(paramName)`.
- **Rationale**: AC-001.3: the message is on one line and can't forge log lines itself; the escaped form shows exactly what was wrong.
- **Alternatives**: omitting the ID from the message (rejected: hard to debug); truncating long IDs (not needed: no length limit, spec Assumptions).

## R-004 Where each ID is validated

| ID | Entry point | Place | Spec |
|----|-------------|-------|------|
| Custom ID | `Publish`, `Schedule` (both overloads) | `HangfireDispatcher.Dispatch`, right after `JobParameters.TryGetCustomId`, before `CreateJob`/`DispatchDeduplicated` | FR-002 |
| Query ID | `IsRunning(string)`, `IsPending(string)`, `Cancel(string)`, `Retry(string)` | first line of each method in `HangfireExecutionManager` | FR-003 |
| Trigger ID | `IRecurringTrigger.Trigger(string)` | `HangfireDispatcher.Trigger(string)`, replacing the current `IsNullOrEmpty` check (it already throws `ArgumentNullException` for `null`/empty) | FR-003 |
| Explicit recurring ID | `[Recurring(Id)]`, `[RecurringInterval(Id)]` | `ExecutionFlowOptions` where the registration is created (attribute) | FR-004 |
| Explicit recurring ID | `HandlerBuilder.Id(string)` | `HandlerBuilder.Id`, replacing its `IsNullOrWhiteSpace` check | FR-004 |
| Generated recurring ID | `IJobIdGenerator.GenerateId` | `RecurringJobResolver.Resolve` (Hangfire), so every caller (registration, `Trigger(Type)`, `Retry(Type)`) sees the same check | FR-004 |

- **Rationale**: explicit IDs are core concepts and are checked in the core, so the check runs in every provider, including the local host, which doesn't use recurring IDs at runtime. `IJobIdGenerator` only exists in Hangfire, so generated IDs are checked there. Every check runs before any storage access (SC-004).
- **Consequence for the spec**: AC-004.4 is covered by a core test (`ExecutionFlowOptionsTests.AddHandler_Throws_WhenRecurringIntervalIdIsInvalid`) instead of a local-host test: the local host never sees the ID, the core rejects it while the handler is registered. Test names in the spec are aligned with the existing test classes (`DispatcherTests`, `ExecutionManagerTests`).

## R-005 Null and empty IDs

- **Decision**: custom ID `null`/empty → no custom ID, unchanged (`TryGetCustomId` already returns `false`). Custom ID only spaces → invalid. Query/trigger ID `null` → `ArgumentNullException`; empty or only spaces → `ArgumentException`. `HandlerBuilder.Id(null)` keeps throwing `ArgumentException` today; it becomes `ArgumentNullException` (a subclass, so existing `catch (ArgumentException)` keeps working).
- **Rationale**: spec Clarifications (2026-11-20).

## R-006 Trace warnings

- **Decision**: no escaping is added to the existing `Trace.TraceWarning` calls.
- **Rationale**: every caller-provided ID that reaches them now passed FR-001 (no control characters); IDs read back from the storage were validated when written or are Hangfire job IDs (spec Assumptions). Adding a second mechanism would duplicate the rule.
- **Follow-up**: if code scanning still reports the trace sinks after this feature (it may not see the validator as a sanitizer), the alerts are dismissed with a link to this decision.

## R-007 Breaking change

- **Decision**: ships in **2.0.0** (the unreleased 1.2.0 is renumbered), with the README "Breaking changes" list updated; no option to turn the check off.
- **Rationale**: confirmed by the maintainer (spec Clarifications); principle V requires a major bump.
- **Alternatives**: shipping in 1.2.0 (rejected: violates principle V); an opt-in option in 1.x (rejected: two behaviors to document and test); amending the constitution to allow breaking changes in a minor (rejected by the maintainer).
- **Out of scope**: the version bump itself (`Src/Directory.Build.props`, "1.2.0" references) is done separately from this feature.
