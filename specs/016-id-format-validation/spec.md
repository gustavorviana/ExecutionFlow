# Feature Specification: ID format validation

**Feature Branch**: `development`

**Created**: 2026-11-20

**Status**: Draft

**Input**: User description: "Define a pattern where IDs and custom IDs can only contain letters and digits, no spaces or special characters (allow `_` and `-`); anything else must throw. Allow `.` and `+` so a custom ID can hold a full class name, and the characters of a generic type's full name. Validate custom IDs, recurring IDs and the IDs passed to query methods."

**Packages**: `ExecutionFlow`, `ExecutionFlow.Hangfire`, `ExecutionFlow.Hosting`

**Amends**:
- [004-custom-id-and-deduplication](../004-custom-id-and-deduplication/spec.md) FR-001 (a custom ID must match the ID format), FR-003 (the IDs given to `IsRunning`/`IsPending`/`Cancel`/`Retry` must match the ID format).
- [005-recurring-jobs](../005-recurring-jobs/spec.md) FR-004 (`Trigger(string)` validates its ID), FR-007 (a recurring job ID must match the ID format).
- [007-execution-manager](../007-execution-manager/spec.md) FR-001, FR-002, FR-003 (the ID parameter must match the ID format).

## Context

IDs given by the application reach the library's diagnostic messages without any check. Two code scanning alerts ("log entries created from user input") show the path: an ID read from an HTTP request goes to `IExecutionManager.Cancel(id)` or into an event's custom ID, and ends in a trace warning written by the library. An ID with a line break can forge lines in that trace.

Today any string is accepted as a custom ID, a recurring ID or a query ID. This feature defines one ID format for all of them and rejects anything outside it, as early as possible: when the event is published, when recurring jobs are registered, and when a query method is called.

The format keeps every ID the library generates today valid: type full names (namespaces with `.`, nested types with `+`, generic types with `` ` ``, `[`, `]`, `,`, `=` and spaces, accented and non-Latin letters), Hangfire job IDs (digits in SQL Server, GUIDs in Redis) and custom IDs such as `order-42`.

## Clarifications

### Session 2026-11-20

- Q: Which IDs are validated? → A: Custom IDs (`ICustomIdEvent.CustomId`), recurring job IDs (`[Recurring(Id)]`, `[RecurringInterval(Id)]` and the `IJobIdGenerator` result) and the IDs passed to query and control methods (`IsRunning`, `IsPending`, `Cancel`, `Retry`, `Trigger`) (maintainer, 2026-11-20).
- Q: Which characters are allowed? → A: Letters `A`–`Z` and `a`–`z`, digits `0`–`9`, `_`, `-`, `.` and `+`, so a custom ID can hold a full class name (nested classes use `+`). The characters of a generic type's full name are allowed too: `` ` ``, `[`, `]`, `,`, `=` and the space (maintainer, 2026-11-20). *(Letters and digits were later extended beyond ASCII, see below.)*
- Q: What happens with an invalid ID in a query method? → A: It throws `ArgumentException`, the same as publishing; it doesn't return `false` (maintainer, 2026-11-20).
- Q: Is this a breaking change, and in which version? → A: Yes: an ID that was accepted before (e.g. with `:`, `/` or a line break) now throws. The maintainer confirmed it (constitution principle V) (maintainer, 2026-11-20).
- Q: A `null`, empty or whitespace-only ID in `IsRunning`, `IsPending`, `Cancel`, `Retry` or `Trigger`: throw or keep returning `false`? → A: Throw: `ArgumentNullException` for `null`, `ArgumentException` for empty or whitespace-only. Also a breaking change, accepted by the maintainer (maintainer, 2026-11-20).
- Q: Is the space allowed in every ID, or only in recurring IDs built from generic types? → A: In every ID: one rule for all IDs. A space can't forge log lines; line breaks stay rejected. Leading and trailing spaces are allowed and kept as is (maintainer, 2026-11-20).
- Q: Letters and digits outside ASCII (e.g. `Criação`, `注文`)? → A: Allowed. C# accepts any Unicode letter in a type name, so a default recurring ID can contain them; the rule accepts the letters, combining marks and digits C# accepts in identifiers. Control characters, invisible format characters (e.g. U+202E, U+200B) and Unicode line separators stay rejected: they're what can forge or disguise log lines (maintainer, 2026-11-20).
- Q: Which version ships this breaking change? → A: 2.0.0. Principle V requires a major bump; the unreleased 1.2.0 is renumbered 2.0.0, together with its other breaking changes. Amending the constitution to allow breaking changes in a minor was rejected (maintainer, 2026-11-20).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Custom IDs with a safe format (Priority: P1)

As an application developer who publishes events with a custom ID built from request data, I want the library to reject a custom ID outside the ID format when I publish, so that a malicious value never reaches the job storage or the library's diagnostic messages.

**Why this priority**: custom IDs are the most common path from user input into the library, and one of the two code scanning alerts.

**Independent Test**: publish an event whose custom ID contains a line break and check that `Publish` throws `ArgumentException` and creates no job.

**Acceptance Scenarios**: AC-002.1..AC-002.4.

---

### User Story 2 - Query and control methods with a safe format (Priority: P1)

As an application developer who exposes job queries in an API (e.g. `GET /jobs/{id}`), I want `IsRunning`, `IsPending`, `Cancel`, `Retry` and `Trigger` to reject an ID outside the ID format, so that a request can't write forged lines into the library's diagnostic messages.

**Why this priority**: the other code scanning alert; the ID goes from the URL to a trace warning.

**Independent Test**: call `Cancel("42\r\nfake line")` and check it throws `ArgumentException` without reading the storage.

**Acceptance Scenarios**: AC-003.1..AC-003.4.

---

### User Story 3 - Recurring IDs with a safe format (Priority: P2)

As a handler author, I want a recurring job ID outside the ID format to fail when recurring jobs are registered, naming the handler, so that a bad ID is found at startup instead of failing later in a query or a trigger.

**Why this priority**: recurring IDs come from the developer, not from requests, so the risk is lower; validating them keeps one rule for every ID and avoids registering a job that `Trigger(string)` could never reach.

**Independent Test**: declare `[Recurring("* * * * *", Id = "sync:daily")]` and check that registration throws `ArgumentException` naming the handler and the ID.

**Acceptance Scenarios**: AC-004.1..AC-004.4.

### Edge Cases

- A custom ID that is `null` or empty still means "no custom ID" (004 FR-001): it's not validated and doesn't throw. A custom ID made only of spaces is invalid and throws, so every job with a custom ID can be queried by it.
- A query ID that is `null`, empty or only spaces throws (FR-003). *(Was: returns `false`, and `Trigger` did nothing.)*
- An ID made only of allowed characters but with leading or trailing spaces (e.g. `" order-42"`) is valid as is: the library doesn't trim IDs.
- Letters and digits outside ASCII are valid (e.g. `Criação`, `注文`, Arabic-Indic digits), including letters outside the Basic Multilingual Plane (encoded as two `char`s). Invisible format characters are not, even though they can appear in C# identifiers: a type whose full name has one needs an explicit valid ID.
- A custom ID changed through the obsolete `FlowContext<TEvent>.SetCustomId` isn't used by deduplication (004 FR-002) and isn't validated.
- Jobs created before this version with a custom ID outside the format stay in the storage; querying them with that ID now throws, and they can still be reached by their Hangfire job ID.
- The error message shows the invalid ID with control characters escaped (e.g. `\r`, `\n`, `\t`, `\u0000`), so the exception text itself can't forge log lines.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 ID format**: a valid ID is a string with at least one character other than the space, whose characters are all in this set: Unicode letters (including accented letters such as `ç`, `ã`, `é` and non-Latin scripts), combining marks, Unicode digits, and `_`, `-`, `.`, `+`, `` ` ``, `[`, `]`, `,`, `=` and the space (U+0020). Any other character makes the ID invalid, in particular control characters, invisible format characters (such as bidirectional overrides and zero-width characters), Unicode line and paragraph separators, other whitespace and other punctuation or symbols. An invalid ID throws `ArgumentException` whose `ParamName` is the parameter that carried the ID and whose message contains the invalid ID with control characters escaped and the list of allowed characters. *(Stories 1–3)*
  - **AC-001.1**: **Given** the IDs `order-42`, `ExecutionFlow.Examples.Handlers.Recurring.DataSyncHandler`, `Outer+Inner`, `` Ns.Handler`1[[System.String, System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea5e798]] ``, `12345`, `3f2504e0-4f89-11d3-9a0c-0305e82c3301`, `Pedidos.Criação`, `pedido-ação-42` and `注文`, **Then** all are valid. Test: `IdFormatTests.IsValid_ReturnsTrue_WhenIdHasOnlyAllowedCharacters`
  - **AC-001.2**: **Given** IDs containing `\r`, `\n`, `\t`, `\0`, `:`, `/`, `\`, `"`, `'`, `{`, `}`, `<`, `>`, `;`, `@`, `#`, U+202E (right-to-left override), U+200B (zero-width space), U+2028 (line separator), U+00A0 (no-break space), **Then** each is invalid. Test: `IdFormatTests.IsValid_ReturnsFalse_WhenIdHasCharacterOutsideFormat`
  - **AC-001.3**: **Given** an invalid ID `"a\r\nb"`, **When** it's rejected, **Then** the exception message shows `a\r\nb` escaped, on one line, and `ParamName` names the parameter. Test: `IdFormatTests.Validate_EscapesControlCharactersInMessage_WhenIdIsInvalid`
- **FR-002 Custom ID at publish time**: `Publish` and `Schedule` validate a non-empty custom ID (`ICustomIdEvent.CustomId`) with FR-001 before any storage access. An invalid custom ID throws and no job is created. A `null` or empty custom ID means "no custom ID", as before. *(Story 1)*
  - *Amends [004](../004-custom-id-and-deduplication/spec.md) FR-001.*
  - **AC-002.1**: **Given** an event with custom ID `order-42`, **When** it's published, **Then** the job is created with that custom ID, as before. Test: `DispatcherTests.Publish_CreatesJob_WhenCustomIdIsValid`
  - **AC-002.2**: **Given** an event with custom ID `"order-42\r\nfake"`, **When** `Publish` is called, **Then** it throws `ArgumentException` with `ParamName` `event` and no job is created. Test: `DispatcherTests.Publish_Throws_WhenCustomIdIsInvalid`
  - **AC-002.3**: **Given** the same event, **When** `Schedule` is called, **Then** it throws `ArgumentException` and no job is created. Test: `DispatcherTests.Schedule_Throws_WhenCustomIdIsInvalid`
  - **AC-002.4**: **Given** an event with a `null` or empty custom ID, **When** it's published, **Then** the job is created without a custom ID, as before. Test: `DispatcherTests.Publish_CreatesJobWithoutCustomId_WhenCustomIdIsNullOrEmpty`
- **FR-003 Query and control IDs**: `IExecutionManager.IsRunning(string)`, `IsPending(string)`, `Cancel(string)`, `Retry(string)` and `IRecurringTrigger.Trigger(string)` validate their ID with FR-001 before any storage access. An invalid ID throws `ArgumentException`; a `null` ID throws `ArgumentNullException`, and an empty or whitespace-only ID throws `ArgumentException` (was: returns `false`). The overloads that take a `Type` are unchanged (the ID they resolve is a recurring ID, FR-004). *(Story 2)*
  - *Amends [004](../004-custom-id-and-deduplication/spec.md) FR-003, [005](../005-recurring-jobs/spec.md) FR-004 and [007](../007-execution-manager/spec.md) FR-001, FR-002, FR-003.*
  - **AC-003.1**: **Given** a Hangfire job ID (`12345`) or a custom ID (`order-42`), **When** any of these methods is called, **Then** it behaves as before. Test: `ExecutionManagerTests.QueryMethods_BehaveAsBefore_WhenIdIsValid`
  - **AC-003.2**: **Given** the ID `"42\r\nfake"`, **When** `IsRunning`, `IsPending`, `Cancel` or `Retry` is called, **Then** it throws `ArgumentException` and the storage isn't read. Test: `ExecutionManagerTests.QueryMethods_Throw_WhenIdIsInvalid`
  - **AC-003.3**: **Given** a `null` ID, **When** any of these methods is called, **Then** it throws `ArgumentNullException`; **Given** an empty or whitespace-only ID, **Then** it throws `ArgumentException`. No storage access happens. Test: `ExecutionManagerTests.QueryMethods_Throw_WhenIdIsNullOrWhiteSpace`
  - **AC-003.4**: **Given** the ID `"sync:daily"`, **When** `Trigger(string)` is called, **Then** it throws `ArgumentException` and no job is triggered. Test: `DispatcherTests.Trigger_Throws_WhenIdIsInvalid`
- **FR-004 Recurring job IDs**: when recurring jobs are registered, every resolved recurring ID (`[Recurring(Id)]`, `[RecurringInterval(Id)]`, or the `IJobIdGenerator` result when no explicit ID is set) is validated with FR-001, in every provider. An invalid ID throws `ArgumentException` whose message names the handler type and the ID. The default ID (the handler's full name) is always valid. *(Story 3)*
  - *Amends [005](../005-recurring-jobs/spec.md) FR-007.*
  - **AC-004.1**: **Given** a handler without an explicit ID, **When** recurring jobs are registered, **Then** its ID is its full name, as before, including nested (`+`) and generic types. Test: `HandlerConfigurationHangfireTests.Build_AcceptsDefaultId_WhenHandlerIsNestedOrGeneric`
  - **AC-004.2**: **Given** `[Recurring("* * * * *", Id = "sync:daily")]` or `HandlerBuilder.Id("sync:daily")`, **When** the handler is registered, **Then** it throws `ArgumentException` naming the handler and the ID. Test: `ExecutionFlowOptionsTests.AddHandler_Throws_WhenRecurringIdIsInvalid`
  - **AC-004.3**: **Given** an `IJobIdGenerator` that returns `"a/b"`, **When** recurring jobs are registered, **Then** it throws `ArgumentException` naming the handler and the ID. Test: `HandlerConfigurationHangfireTests.Build_Throws_WhenGeneratedIdIsInvalid`
  - **AC-004.4**: **Given** `[RecurringInterval("00:00:05", Id = "sync daily\n")]`, **When** the handler is registered (before any provider, so also in the local host), **Then** it throws `ArgumentException` naming the handler and the ID. Test: `ExecutionFlowOptionsTests.AddHandler_Throws_WhenRecurringIntervalIdIsInvalid`
- **FR-005 Documentation**: the README and the XML docs of `ICustomIdEvent.CustomId`, `[Recurring]`, `[RecurringInterval]`, `IJobIdGenerator`, `IExecutionManager` and `IRecurringTrigger` state the ID format, that an invalid ID throws `ArgumentException`, and where it's checked. The 2.0.0 breaking changes list mentions it. Covered by a documentation task (no test).

### Key Entities

- **ID**: a string that identifies a job: a custom ID given by an event, a recurring job ID, or a Hangfire job ID. All of them follow the ID format (FR-001).
- **ID format**: the set of allowed characters, shared by every ID and checked in one place.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: no ID with a character outside the format reaches a diagnostic message of the library: every caller-provided ID is validated at its entry point (FR-002..FR-004). Code scanning alerts that remain on those messages are dismissed citing [research.md](research.md) R-006.
- **SC-002**: 100% of the invalid IDs listed in AC-001.2 are rejected at every entry point (publish, schedule, the five query and control methods, recurring registration).
- **SC-003**: 100% of the IDs generated by the library today (type full names, Hangfire job IDs) and the custom IDs used in the examples keep working, with no change in the examples.
- **SC-004**: an invalid ID is reported before any storage access: no job, reservation key or storage read happens for it.

## Assumptions

- Applications that use characters outside the format in custom IDs (e.g. `tenant:42`) must change them to a valid separator (e.g. `tenant-42` or `tenant.42`) when they upgrade to 2.0.0. The breaking changes list says so.
- No maximum length is defined; the storage's own limits still apply.
- Trace warnings that write IDs need no escaping once every ID that reaches them has passed FR-001; IDs read back from the storage were validated when they were written, or are Hangfire job IDs.
- The validation is case-sensitive only in the sense that both cases are allowed; it doesn't normalize or trim IDs.
