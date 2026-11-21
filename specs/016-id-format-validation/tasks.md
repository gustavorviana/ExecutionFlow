---
description: "Tasks for ID format validation"
---

# Tasks: ID format validation

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md)

**Tests**: mandatory (constitution VII). Write each task's tests first; a task is done when the tests of the acceptance scenarios it covers pass, together with the existing suite (`dotnet test --no-build`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task).
- **[Story]**: US1 = custom IDs (P1), US2 = query and control IDs (P1), US3 = recurring IDs (P2).

## Phase 1: Setup

- [X] T001 Add `<InternalsVisibleTo Include="ExecutionFlow.Hangfire" />` next to the existing entries in `Src/ExecutionFlow/ExecutionFlow.csproj` (research R-001).

## Phase 2: Foundational (blocks every story)

- [X] T002 Create the internal static class `IdFormat` in `Src/ExecutionFlow/Abstractions/IdFormat.cs` (namespace `ExecutionFlow.Abstractions`, block-scoped, XML docs) with: `bool IsValid(string id)`: `true` when `id` is not null, has at least one character other than the space, and every code point (walk with `char.IsSurrogatePair`, read the category with `CharUnicodeInfo.GetUnicodeCategory(id, index)`) is a letter (`UppercaseLetter`, `LowercaseLetter`, `TitlecaseLetter`, `ModifierLetter`, `OtherLetter`, `LetterNumber`), a combining mark (`NonSpacingMark`, `SpacingCombiningMark`), a `DecimalDigitNumber`, or one of `_ - . + ` [ ] , =` or `' '` (U+0020 only); reject everything else, including `Format` characters (R-002). `void Validate(string id, string paramName)`: `null` → `ArgumentNullException(paramName)`; invalid → `ArgumentException("The ID '<escaped>' is invalid. IDs can only contain letters, digits, spaces and _ - . + ` [ ] , =", paramName)`. `void ValidateRecurring(Type handlerType, string id, string paramName)`: invalid → `ArgumentException("The recurring job ID '<escaped>' of handler '<handlerType.FullName>' is invalid. ...same list...", paramName)`. `string Escape(string)`: `\r`→`\r`, `\n`→`\n`, `\t`→`\t`, `\0`→`\0`, `\`→`\\`, any other `Control`, `Format`, `LineSeparator`, `ParagraphSeparator` or lone `Surrogate` char → `\uXXXX`; everything else kept (R-003). No allocation when the ID is valid. Tests in new `Tests/ExecutionFlow.Tests/Abstractions/IdFormatTests.cs`: `IsValid_ReturnsTrue_WhenIdHasOnlyAllowedCharacters` (Theory with `order-42`, `ExecutionFlow.Examples.Handlers.Recurring.DataSyncHandler`, `Outer+Inner`, the generic full name of AC-001.1, `12345`, `3f2504e0-4f89-11d3-9a0c-0305e82c3301`, `" order-42 "`, `Pedidos.Criação`, `pedido-ação-42`, `注文`, a letter outside the BMP such as `𠀀`), `IsValid_ReturnsFalse_WhenIdHasCharacterOutsideFormat` (Theory with each character of AC-001.2, plus a lone surrogate `"\uD800"`, `""`, `"   "`, `null`), `Validate_EscapesControlCharactersInMessage_WhenIdIsInvalid` (`"a\r\nb"` → message contains `a\r\nb` literally, no CR/LF in the message, `ParamName` set), `Validate_ThrowsArgumentNull_WhenIdIsNull` (FR-001, AC-001.1..AC-001.3).

**Checkpoint**: the rule exists and is tested; nothing calls it yet.

## Phase 3: User Story 1 - Custom IDs with a safe format (Priority: P1) 🎯 MVP

**Goal**: `Publish` and `Schedule` reject a custom ID outside the format before any storage access.

**Independent Test**: publishing an event whose custom ID contains a line break throws `ArgumentException` and creates no job.

- [X] T003 [US1] In `Src/ExecutionFlow.Hangfire/Infrastructure/HangfireDispatcher.cs` `Dispatch<TEvent>`, right after `JobParameters.TryGetCustomId(@event, out var customId)` returns `true` and before `GetDeduplicationBehavior`/`CreateJob`/`DispatchDeduplicated`, call `IdFormat.Validate(customId, "event")` (paramName `event`, the parameter that carried it). `null`/empty custom ID keeps meaning "no custom ID" (`TryGetCustomId` already returns `false`). Add `<exception cref="ArgumentException">` to `Publish` and both `Schedule` overloads in the class and in `Src/ExecutionFlow/Abstractions/IEventDispatcher.cs` ("the event's custom ID doesn't match the ID format"). Tests in `Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/DispatcherTests.cs`: `Publish_CreatesJob_WhenCustomIdIsValid`, `Publish_Throws_WhenCustomIdIsInvalid` (`"order-42\r\nfake"`; assert `ParamName == "event"`, `IBackgroundJobClient` received no `Create` and the storage connection was not opened), `Schedule_Throws_WhenCustomIdIsInvalid` (both overloads), `Publish_CreatesJobWithoutCustomId_WhenCustomIdIsNullOrEmpty`, plus `Publish_Throws_WhenCustomIdIsOnlySpaces` (FR-002, AC-002.1..AC-002.4, SC-004).

**Checkpoint**: US1 complete.

## Phase 4: User Story 2 - Query and control IDs with a safe format (Priority: P1)

**Goal**: `IsRunning`, `IsPending`, `Cancel`, `Retry` and `Trigger(string)` reject an invalid, `null`, empty or whitespace-only ID before any storage access.

**Independent Test**: `Cancel("42\r\nfake")` throws `ArgumentException` without opening a storage connection.

- [X] T004 [P] [US2] In `Src/ExecutionFlow.Hangfire/Infrastructure/HangfireExecutionManager.cs`, make the first statement of `IsRunning(string jobId)`, `IsPending(string jobId)`, `Cancel(string jobId)` and `Retry(string jobId)` `IdFormat.Validate(jobId, nameof(jobId))`, replacing the `string.IsNullOrEmpty(jobId)` → `return false` checks of those methods (keep the private helpers' own null checks: they also serve the `Type` overloads). Update the XML docs of those four methods here and in `Src/ExecutionFlow/Abstractions/IExecutionManager.cs`: `<exception cref="ArgumentNullException">` for `null`, `<exception cref="ArgumentException">` for empty, whitespace-only or outside the ID format. Tests in `Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/ExecutionManagerTests.cs`: `QueryMethods_BehaveAsBefore_WhenIdIsValid` (Theory over the four methods with Hangfire ID `12345` and custom ID `order-42`), `QueryMethods_Throw_WhenIdIsInvalid` (Theory over the four methods with `"42\r\nfake"` and `"a:b"`; assert `JobStorage.GetConnection` and `GetMonitoringApi` were not called), `QueryMethods_Throw_WhenIdIsNullOrWhiteSpace` (`null` → `ArgumentNullException`; `""`, `"   "` → `ArgumentException`). Update any existing test that asserted `false` for a `null`/empty ID to assert the exception (FR-003, AC-003.1..AC-003.3).
- [X] T005 [P] [US2] In `Src/ExecutionFlow.Hangfire/Infrastructure/HangfireDispatcher.cs` `Trigger(string jobId)`, replace `if (string.IsNullOrEmpty(jobId)) throw new ArgumentNullException(...)` with `IdFormat.Validate(jobId, nameof(jobId))`. Note: empty now throws `ArgumentException`, not `ArgumentNullException`; update the XML docs here and in `Src/ExecutionFlow.Hangfire/IRecurringTrigger.cs`. Tests in `Tests/ExecutionFlow.Hangfire.Tests/Infrastructure/DispatcherTests.cs`: `Trigger_Throws_WhenIdIsInvalid` (`"sync:daily"`; `IRecurringJobManager.Trigger` not called), `Trigger_ThrowsArgumentNull_WhenIdIsNull`, `Trigger_Throws_WhenIdIsEmptyOrWhiteSpace`; update `Tests/ExecutionFlow.Hangfire.Tests/HangfireValidationTests.cs` line 221, which expects `ArgumentNullException` for `Trigger("")`, to expect `ArgumentException` (FR-003, AC-003.4).

**Checkpoint**: US2 complete; the two code scanning paths (custom ID, query ID) are closed.

## Phase 5: User Story 3 - Recurring IDs with a safe format (Priority: P2)

**Goal**: a recurring ID outside the format fails at registration, naming the handler.

**Independent Test**: `[Recurring("* * * * *", Id = "sync:daily")]` throws `ArgumentException` naming the handler and the ID when the handler is registered.

- [X] T006 [P] [US3] Explicit IDs in the core: in `Src/ExecutionFlow/ExecutionFlowOptions.cs`, where the `RecurringJobRegistryInfo` is created from the attributes (the `id: recurringAttr?.Id ?? intervalAttr?.Id` path), when that ID is not `null` call `IdFormat.ValidateRecurring(handlerType, id, "Id")`; in `Src/ExecutionFlow/Abstractions/HandlerBuilder.cs` `Id(string id)`, replace the `IsNullOrWhiteSpace` check with `null` → `ArgumentNullException(nameof(id))` and `IdFormat.ValidateRecurring(HandlerType, id, nameof(id))`. Update the XML docs of `HandlerBuilder.Id`, `Src/ExecutionFlow/Attributes/RecurringAttribute.cs` `Id` and `Src/ExecutionFlow/Attributes/RecurringIntervalAttribute.cs` `Id` with the format and the exception. Tests in `Tests/ExecutionFlow.Tests/ExecutionFlowOptionsTests.cs`: `AddHandler_Throws_WhenRecurringIdIsInvalid` (`[Recurring(..., Id = "sync:daily")]` and `HandlerBuilder.Id("sync:daily")`; message contains the handler's full name and the ID), `AddHandler_Throws_WhenRecurringIntervalIdIsInvalid` (`[RecurringInterval("00:00:05", Id = "sync daily\n")]`; message shows `\n` escaped) (FR-004, AC-004.2, AC-004.4).
- [X] T007 [P] [US3] Generated IDs in Hangfire: in `Src/ExecutionFlow.Hangfire/Infrastructure/RecurringJobResolver.cs` `Resolve`, when `registration.Id` is `null`, validate the `idGenerator.GenerateId(registration.HandlerType)` result with `IdFormat.ValidateRecurring(registration.HandlerType, id, "jobIdGenerator")` before returning it. Update the XML docs of `Src/ExecutionFlow.Hangfire/IJobIdGenerator.cs` `GenerateId` (the result must match the ID format). Tests in `Tests/ExecutionFlow.Hangfire.Tests/HandlerConfigurationHangfireTests.cs`: `Build_AcceptsDefaultId_WhenHandlerIsNestedOrGeneric` (a nested recurring handler `Outer+Inner`, a closed generic recurring handler and a recurring handler named with accents, e.g. `CriaçãoHandler`, register with their full name as ID), `Build_Throws_WhenGeneratedIdIsInvalid` (an `IJobIdGenerator` returning `"a/b"`; message names the handler and the ID) (FR-004, AC-004.1, AC-004.3).

**Checkpoint**: US3 complete; every ID entry point validates.

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T008 [P] Document the ID format in `README.md`: a short "ID format" subsection near custom IDs (allowed characters, accented and non-Latin letters included, at least one non-space character, where it's checked, that an invalid ID throws `ArgumentException`), a note in the recurring `Id` and `IExecutionManager` sections, and entries in the 2.0.0 "Breaking changes" list (the README had no such list: a new "Upgrading to 2.0" section was added, also listing the earlier 1.2.0 breaking changes; the version renumbering itself is out of scope, research R-007): custom/recurring/query IDs must match the format; `IsRunning`/`IsPending`/`Cancel`/`Retry` throw for `null`/empty instead of returning `false`; `Trigger("")` throws `ArgumentException` instead of `ArgumentNullException`. Also add the XML doc sentence to `Src/ExecutionFlow/Abstractions/ICustomIdEvent.cs` `CustomId` (FR-005).
- [X] T009 Run `dotnet build` and `dotnet test --no-build`; check that the examples run unchanged (their IDs already match the format, SC-003) by following [quickstart.md](quickstart.md); set the spec `**Status**` to `Implemented (unreleased)`.

## Dependencies & Execution Order

- T001 → T002 → (T003, T004, T005, T006, T007) → T008 → T009.
- US1 (T003), US2 (T004, T005) and US3 (T006, T007) are independent of each other once T002 is done.
- T003 and T005 both edit `HangfireDispatcher.cs` and `Infrastructure/DispatcherTests.cs`: run them one after the other.

## Parallel Example

After T002:

```text
T003 [US1] HangfireDispatcher.Dispatch           (then T005 in the same file)
T004 [US2] HangfireExecutionManager
T006 [US3] ExecutionFlowOptions + HandlerBuilder
T007 [US3] RecurringJobResolver
```

## Implementation Strategy

- **MVP**: T001–T003 (custom IDs), the most common path from request data into the library.
- **Then**: T004–T005 close the second code scanning path; T006–T007 complete the rule for recurring IDs.
- **Release**: everything ships together in 2.0.0 (breaking change confirmed, major bump per principle V); don't publish with only part of the entry points validated, so the README's single rule stays true.

## Traceability

| Spec item | Tasks |
|-----------|-------|
| FR-001 / AC-001.1..3 | T002 |
| FR-002 / AC-002.1..4 | T003 |
| FR-003 / AC-003.1..3 | T004 |
| FR-003 / AC-003.4 | T005 |
| FR-004 / AC-004.2, AC-004.4 | T006 |
| FR-004 / AC-004.1, AC-004.3 | T007 |
| FR-005 | T003–T008 (XML docs), T008 (README) |
| SC-001 | T003–T005, T009 (code scanning after merge) |
| SC-002 | T002–T007 |
| SC-003 | T007, T009 |
| SC-004 | T003, T004, T005 |

## Phase 7: No leading or trailing space (clarification 2026-11-21)

- [X] T010 Reject IDs that start or end with a space (FR-001): in `Src/ExecutionFlow/Abstractions/IdFormat.cs` make `IsValid` return `false` when `id[0] == ' '` or `id[id.Length - 1] == ' '` (this also covers empty-after-trim and only-spaces IDs); update `AllowedDescription` to end with `, and can't start or end with a space` and the XML docs that say "with at least one character other than a space" (`IdFormat`, `ICustomIdEvent`, `IJobIdGenerator`, `HandlerBuilder.Id`, `RecurringAttribute.Id`, `RecurringIntervalAttribute.Id`) to "that doesn't start or end with a space"; README "ID format" section: replace "at least one character other than a space" with "no space at the start or end". Tests in `Tests/ExecutionFlow.Tests/Abstractions/IdFormatTests.cs`: move `" order-42 "` from `IsValid_ReturnsTrue_...` to `IsValid_ReturnsFalse_...` and add `" order-42"`, `"order-42 "` (AC-001.1, AC-001.2); keep `"Ns.Handler`1[[System.String, ...]]"` (inner spaces) valid. Run build and tests.
