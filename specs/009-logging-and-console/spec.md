# Feature Specification: Logging and console

**Feature Branch**: `main` (brownfield)

**Created**: 2026-09-30

**Status**: Implemented (v1.2.0)

**Mode**: brownfield, to-be changes implemented in v1.2.0

**Input**: Migrated from `docs/specs/logging-and-console/spec.md` (`REQ-n` → `FR-n`, `US-00n` → User Story n; `AC` and `RN` numbers kept). Findings are in [research.md](research.md).

**Packages**: `ExecutionFlow` (`IExecutionLogger`, `IExecutionLoggerFactory`, `ExecutionLoggerFactory`, `CompositeExecutionLogger`, `HandlerLogType`, `ExecutionLoggerExtensions`), `ExecutionFlow.Hangfire.Console` (`ConfigureConsole`, `ConsoleConfig`, `HangfireExecutionLogger`, progress bars), `ExecutionFlow.Extensions.Logging` (`AddMicrosoftLogging`, `MicrosoftExecutionLoggerFactory`).

**Amends**: none

## Context

Handlers log through `context.Log`, so the same code can write to several destinations: the Hangfire dashboard console and the application's `ILogger`. Logging is observability: it must never change a job's outcome.

Related: [001-handlers-and-context](../001-handlers-and-context/spec.md) (`FlowContext.Log`, `Parameters` seen by loggers, `JobId`/`AttemptNumber`).

**Actors**:

| Actor | Description |
| ----- | ----------- |
| Handler author | Writes `context.Log.Info(...)`, creates progress bars. |
| Logger author | Implements `IExecutionLoggerFactory`. |
| Dashboard operator | Reads the job console in the Hangfire dashboard. |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One logging API for every destination (Priority: P1)

As a handler author, I want one logging API that reaches every configured destination.

**Why this priority**: it is the core of the feature; every other story builds on it.

**Independent Test**: register two logger factories and check that `context.Log.Info(...)` reaches both.

**Acceptance Scenarios**: AC-001.1, AC-002.1, AC-002.3, AC-002.4, AC-003.2 (see Requirements).

---

### User Story 2 - Job log and progress in the dashboard (Priority: P2)

As a dashboard operator, I want each job's log and progress in the dashboard.

**Why this priority**: the main destination, but opt-in (`ConfigureConsole`).

**Independent Test**: configure the console and check formatting, colors and level filtering.

**Acceptance Scenarios**: AC-003.1, AC-003.3, AC-004.1.

---

### User Story 3 - A broken logger never fails the job (Priority: P1)

As a handler author, I want a broken logger never to fail my job.

**Why this priority**: logging is observability and must never change a job's outcome.

**Independent Test**: register a logger that throws and check that the handler and the other loggers keep going.

**Acceptance Scenarios**: AC-002.2.

---

### User Story 4 - Reach the application's logger (Priority: P2)

As a handler author, I want `context.Log` to reach my application's logger (Serilog, Application Insights...).

**Why this priority**: an opt-in bridge; without it, logs only reach the registered loggers.

**Independent Test**: call `AddMicrosoftLogging()` and check category, scope, template and level mapping against a fake `ILoggerFactory`.

**Acceptance Scenarios**: AC-005.1, AC-005.2, AC-005.3.

### Edge Cases

- No logger registered: `context.Log.*` writes nowhere, silently (AC-002.3).
- A logger factory throws: its logger is skipped (AC-002.2).
- The same factory registered twice: kept once (AC-003.2).
- A placeholder without a matching argument: it stays as written (AC-003.3).
- `CreateProgressBar` outside a Hangfire job: `InvalidOperationException` (AC-004.1).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 Logging API**: `IExecutionLogger.Log(HandlerLogType, message, args)` with extension methods per level (`Trace`, `Debug`, `Info`, `Warning`, `Error`, `Critical`, `Success`, including `object` and `Exception` overloads). `Success` is an ExecutionFlow-specific level. *(Story 1)*
  - **AC-001.1**: [CONFIRMED] **Given** an `IExecutionLogger`, **When** a level extension method is called (string, `object` or `Exception` overload), **Then** `Log` is called with that level and message. Tests: `ExecutionLoggerExtensionsTests` (25 tests: `Trace_String_CallsLogWithTraceLevel` ... `Critical_Exception_CallsLogWithExceptionToString`)

- **FR-002 Pluggable loggers**: `options.AddLogger<TFactory>()` registers an `IExecutionLoggerFactory`. Each execution gets a `CompositeExecutionLogger` over every non-null logger the factories create from the execution's `FlowParameters`. A factory type registered twice is kept once (F-003). A factory implementing `IExecutionLoggerContextFactory` receives an `ExecutionLoggerContext` (parameters, `JobId`, `AttemptNumber`, handler type, event type) instead. *(Stories 1, 3)*
  - **AC-002.1**: [CONFIRMED] **Given** factories registered with `AddLogger`, **When** an execution's logger is created, **Then** invalid types are rejected and the composite dispatches to every non-null logger. Tests: `ValidationTests.AddLogger_Throws_ForNullType`, `AddLogger_Throws_ForTypeThatDoesNotImplementIExecutionLoggerFactory`, `AddLogger_Adds_ValidFactoryType`, `ExecutionLoggerFactory_Throws_ForNullFactories`, `ExecutionLoggerFactory_CreatesLogger_WithEmptyFactories`, `CompositeExecutionLogger_DispatchesToAllLoggers`, `CompositeExecutionLogger_SkipsNullLoggers`, `FlowEngineJobActivatorTests.RegisterLoggerFactory_CreatesExecutionLoggerFactory`, `FlowContextBuilderTests.Build_Generic_CreatesLogger`
  - **AC-002.2**: **Given** a logger that throws, **When** the handler logs, **Then** the system reports it with `Trace.TraceWarning` and keeps calling the remaining loggers; the handler never sees the exception. **Given** a factory that throws, **When** loggers are created, **Then** its logger is skipped. (F-001) Tests: `ExecutionLoggerIsolationTests.ThrowingLogger_DoesNotStopOtherLoggers_NorThrowToTheHandler`, `ThrowingFactory_IsSkipped_WhenCreatingLoggers`
  - **AC-002.3**: **Given** no logger registered, **When** `context.Log.*` is called, **Then** it writes nowhere, silently; `AddMicrosoftLogging()` is the opt-in route to `ILogger` (F-002). Test: `ValidationTests.ExecutionLoggerFactory_CreatesLogger_WithEmptyFactories`
  - **AC-002.4**: **Given** a factory that implements `IExecutionLoggerContextFactory`, **When** loggers are created, **Then** the system passes it the job and handler facts; a plain factory keeps receiving `FlowParameters`. Tests: `ExecutionLoggerIsolationTests.ContextFactory_ReceivesJobAndHandlerFacts`, `PlainFactory_KeepsReceivingParameters`

- **FR-003 Hangfire console**: `options.ConfigureConsole()` / `ConfigureConsole(Action<ConsoleConfig>)` registers `ConsoleConfig` and the console logger factory. The logger writes `[LEVEL] message` in the level's color (customizable with `SetColor`) to the job's Hangfire console. Messages are formatted like `ILogger` templates: each `{placeholder}`, named or numeric, takes the argument in the same position, `,alignment` and `:format` apply, `{{`/`}}` are literal braces, a missing argument keeps the placeholder, and a message without arguments is written as-is (F-004). A custom `Formatter` replaces the whole format. `ConsoleConfig.MinimumLevel` (default `Trace`) filters lines, with `Success` counted as `Information` (F-005). Hangfire itself must have `GlobalConfiguration.UseConsole()`, which `ConfigureConsole` doesn't do; both READMEs say so (F-006). *(Stories 1, 2)*
  - **AC-003.1**: [CONFIRMED] **Given** `ConfigureConsole`, with or without a configuration action, **When** it is called, **Then** `ConsoleConfig` and the logger factory are registered, and colors and formatter behave as configured. Tests: `ConsoleConfigTests.GetColor_ReturnsDefaultColor_ForEachLogType`, `SetColor_OverridesDefaultColor`, `GetColor_ReturnsWhite_ForUnmappedLogType`, `Formatter_IsNullByDefault`, `Formatter_CanBeSet`, `HangfireOptionsExtensionsTests.ConfigureConsole_AddsLoggerFactory`, `ConfigureConsole_AddsOption`, `ConfigureConsole_WithAction_AppliesConfiguration`, `ConfigureConsole_WithAction_Throws_ForNullAction`, `ConfigureConsole_ReturnsOptions_ForChaining`
  - **AC-003.2**: **Given** `ConfigureConsole()` (or any `AddLogger<T>`) is called twice, **When** the options are used, **Then** the factory is registered once. (F-003) Test: `ExecutionLoggerIsolationTests.AddLogger_IgnoresTheSameFactoryRegisteredTwice`
  - **AC-003.3**: **Given** console messages with templates, arguments and levels, **When** they are written, **Then** formatting and level filtering follow FR-003. (F-004, F-005, F-007) Tests: `ConsoleFormattingTests.FormatMessage_FillsNamedPlaceholders_ByPosition`, `FormatMessage_KeepsNumericPlaceholdersWorking`, `FormatMessage_AppliesFormatAndAlignment`, `FormatMessage_TreatsDoubledBracesAsLiterals`, `FormatMessage_KeepsPlaceholder_WhenArgumentIsMissing`, `FormatMessage_WithoutArguments_WritesMessageAsIs`, `FormatMessage_UsesCustomFormatter_WhenSet`, `IsEnabled_RespectsMinimumLevel_WithSuccessAsInformation`, `MinimumLevel_DefaultsToTrace`

- **FR-004 Progress bars**: `context.CreateProgressBar(title?)` returns an `ExecutionProgressBar` (`SetValue(percentage)`, `SetValue(current, total)`, `Complete()`). It throws `InvalidOperationException` outside a Hangfire job. It reads the `PerformContext` with `GetPerformContext()`. *(Story 2)*
  - **AC-004.1**: **Given** code running outside a Hangfire job, **When** `CreateProgressBar` is called, **Then** the system throws `InvalidOperationException`. Test: `ConsoleFormattingTests.CreateProgressBar_Throws_OutsideAHangfireJob`

- **FR-005 Microsoft.Extensions.Logging bridge**: `options.AddMicrosoftLogging()` (package `ExecutionFlow.Extensions.Logging`, depends on the core and `Microsoft.Extensions.Logging.Abstractions` >= 6.0.0 only, so any processor can use it, [constitution](../../.specify/memory/constitution.md) principle II) sends `context.Log` to `ILogger`. The category is the handler type's full name, else the event type's, else `ExecutionFlow`. Each entry runs inside a scope with `JobId` and `AttemptNumber`. The message and arguments are passed as a template, so named properties survive. Levels map one to one, `Success` to `Information` (RN-002). Disabled levels are skipped. Opt-in (constitution principle III). *(Story 4)*
  - **AC-005.1**: **Given** a known handler type, only an event type, or neither, **When** a logger is created, **Then** its category is the handler type, the event type, or `ExecutionFlow`. Tests: `MicrosoftExecutionLoggerFactoryTests.CreateLogger_UsesHandlerTypeAsCategory_WhenHandlerIsKnown`, `CreateLogger_UsesEventTypeAsCategory_WhenHandlerIsUnknown`, `CreateLogger_UsesDefaultCategory_WhenNoTypeIsKnown`
  - **AC-005.2**: **Given** a bridged logger, **When** an entry is logged, **Then** template properties are kept, the entry is wrapped in the job scope, the level is mapped, and disabled levels are skipped. Tests: `MicrosoftExecutionLoggerFactoryTests.Log_KeepsTemplatePropertiesAndFormatsMessage_WhenArgsAreGiven`, `Log_WrapsEntryInJobScope_WhenLogging`, `Log_MapsLevel_WhenLogging`, `Log_SkipsEntry_WhenLevelIsDisabled`
  - **AC-005.3**: **Given** `AddMicrosoftLogging()` is called twice, **When** the options are used, **Then** the factory is registered once. Test: `MicrosoftExecutionLoggerFactoryTests.AddMicrosoftLogging_RegistersFactoryOnce_WhenCalledTwice`

### Business Rules

- **RN-001** [CONFIRMED]: Loggers see the execution's `FlowParameters` (the same instance the handler uses).
- **RN-002** [CONFIRMED]: `Success` has no equivalent in `Microsoft.Extensions.Logging`.

### Key Entities

- **IExecutionLogger / IExecutionLoggerFactory**: the logging API and its pluggable factories.
- **CompositeExecutionLogger**: sends each entry to every logger, isolating failures.
- **ExecutionLoggerContext / IExecutionLoggerContextFactory**: opt-in job and handler facts for a factory.
- **ConsoleConfig**: colors, formatter and minimum level of the Hangfire console.
- **MicrosoftExecutionLoggerFactory**: the bridge to `ILogger`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- The old spec had no NFRs, so there are no `SC` items. The measurable outcome is the Context rule "logging never changes a job's outcome", covered by AC-002.2.

## Assumptions

- Out of scope: log persistence and retention (the destination's job); structured logging inside the Hangfire console (the console is plain text).
- Open questions: none.

### Decided to-be changes (implemented in v1.2.0)

Decided with the maintainer on 2026-09-30.

1. **Isolate each logger** (F-001): `CompositeExecutionLogger` calls each logger in its own try/catch. A failure goes to `Trace.TraceWarning`, and the handler and the other loggers keep going.
2. **New package `ExecutionFlow.Extensions.Logging`** (F-002): a bridge from `context.Log` to the application's `ILogger`. `options.AddMicrosoftLogging()` registers a logger factory that resolves `ILoggerFactory`. The category is the handler type (or the event type when the handler is unknown), and `JobId`/`AttemptNumber` go into a logging scope. `Success` maps to `Information`, and the message template and arguments are passed as-is, so structured logging works. The package depends only on the core and `Microsoft.Extensions.Logging.Abstractions` >= 6.0, so it works with any processor (constitution principle II). Opt-in (constitution principle III).
3. **The same logger factory registered twice is kept once** (F-003): `AddLogger` ignores a type that's already registered, so `ConfigureConsole()` twice doesn't duplicate lines.
4. **Console formats like `ILogger` templates** (F-004): each `{placeholder}`, named or numeric, is filled with the argument in the same position, and `{{`/`}}` are literal braces. The same message works in the console and in `ILogger`.
5. **Minimum level for the console** (F-005): `ConsoleConfig.MinimumLevel`, default `Trace` (everything, as before). `Success` counts as `Information` for filtering.
6. **Document `GlobalConfiguration.UseConsole()`** (F-006) in both READMEs.
7. **Tests** for the console logger, formatting and progress bars (F-007). `CreateProgressBar` uses `GetPerformContext()`.
