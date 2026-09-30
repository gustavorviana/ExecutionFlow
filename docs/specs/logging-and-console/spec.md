# Spec: Logging and console

Status: approved (v1.2.0)
Mode: brownfield, to-be changes implemented in v1.2.0
Packages: `ExecutionFlow` (`IExecutionLogger`, `IExecutionLoggerFactory`, `ExecutionLoggerFactory`, `CompositeExecutionLogger`, `HandlerLogType`, `ExecutionLoggerExtensions`), `ExecutionFlow.Hangfire.Console` (`ConfigureConsole`, `ConsoleConfig`, `HangfireExecutionLogger`, progress bars), `ExecutionFlow.Extensions.Logging` (`AddMicrosoftLogging`, `MicrosoftExecutionLoggerFactory`).

## Context
Handlers log through `context.Log`, so the same code can write to several destinations: the Hangfire dashboard console and the application's `ILogger`. Logging is observability: it must never change a job's outcome.

Related: [handlers-and-context](../handlers-and-context/spec.md) (`FlowContext.Log`, `Parameters` seen by loggers, `JobId`/`AttemptNumber`).

## Actors
| Actor | Description |
| ----- | ----------- |
| Handler author | Writes `context.Log.Info(...)`, creates progress bars. |
| Logger author | Implements `IExecutionLoggerFactory`. |
| Dashboard operator | Reads the job console in the Hangfire dashboard. |

## User stories
- US-001: As a handler author, I want one logging API that reaches every configured destination.
- US-002: As a dashboard operator, I want each job's log and progress in the dashboard.
- US-003: As a handler author, I want a broken logger never to fail my job.
- US-004: As a handler author, I want `context.Log` to reach my application's logger (Serilog, Application Insights...).

## Requirements

### REQ-001: Logging API
`IExecutionLogger.Log(HandlerLogType, message, args)` with extension methods per level (`Trace`, `Debug`, `Info`, `Warning`, `Error`, `Critical`, `Success`, including `object` and `Exception` overloads). `Success` is an ExecutionFlow-specific level.
Source: US-001

Acceptance criteria:
- AC-001.1: [CONFIRMED] Tests: `ExecutionLoggerExtensionsTests` (25 tests: `Trace_String_CallsLogWithTraceLevel` ... `Critical_Exception_CallsLogWithExceptionToString`)

### REQ-002: Pluggable loggers
`options.AddLogger<TFactory>()` registers an `IExecutionLoggerFactory`. Each execution gets a `CompositeExecutionLogger` over every non-null logger the factories create from the execution's `FlowParameters`. A factory type registered twice is kept once (F-003). A factory implementing `IExecutionLoggerContextFactory` receives an `ExecutionLoggerContext` (parameters, `JobId`, `AttemptNumber`, handler type, event type) instead.
Source: US-001

Acceptance criteria:
- AC-002.1: [CONFIRMED] Tests: `ValidationTests.AddLogger_Throws_ForNullType`, `AddLogger_Throws_ForTypeThatDoesNotImplementIExecutionLoggerFactory`, `AddLogger_Adds_ValidFactoryType`, `ExecutionLoggerFactory_Throws_ForNullFactories`, `ExecutionLoggerFactory_CreatesLogger_WithEmptyFactories`, `CompositeExecutionLogger_DispatchesToAllLoggers`, `CompositeExecutionLogger_SkipsNullLoggers`, `FlowEngineJobActivatorTests.RegisterLoggerFactory_CreatesExecutionLoggerFactory`, `FlowContextBuilderTests.Build_Generic_CreatesLogger`
- AC-002.2: WHEN a logger throws, THE SYSTEM reports it with `Trace.TraceWarning` and keeps calling the remaining loggers; the handler never sees the exception. WHEN a factory throws, its logger is skipped. (F-001) Tests: `ExecutionLoggerIsolationTests.ThrowingLogger_DoesNotStopOtherLoggers_NorThrowToTheHandler`, `ThrowingFactory_IsSkipped_WhenCreatingLoggers`
- AC-002.3: WHEN no logger is registered, `context.Log.*` writes nowhere, silently; `AddMicrosoftLogging()` is the opt-in route to `ILogger` (F-002). Test: `ValidationTests.ExecutionLoggerFactory_CreatesLogger_WithEmptyFactories`
- AC-002.4: WHEN a factory implements `IExecutionLoggerContextFactory`, THE SYSTEM passes it the job and handler facts; a plain factory keeps receiving `FlowParameters`. Tests: `ExecutionLoggerIsolationTests.ContextFactory_ReceivesJobAndHandlerFacts`, `PlainFactory_KeepsReceivingParameters`

### REQ-003: Hangfire console
`options.ConfigureConsole()` / `ConfigureConsole(Action<ConsoleConfig>)` registers `ConsoleConfig` and the console logger factory. The logger writes `[LEVEL] message` in the level's color (customizable with `SetColor`) to the job's Hangfire console. Messages are formatted like `ILogger` templates: each `{placeholder}`, named or numeric, takes the argument in the same position, `,alignment` and `:format` apply, `{{`/`}}` are literal braces, a missing argument keeps the placeholder, and a message without arguments is written as-is (F-004). A custom `Formatter` replaces the whole format. `ConsoleConfig.MinimumLevel` (default `Trace`) filters lines, with `Success` counted as `Information` (F-005). Hangfire itself must have `GlobalConfiguration.UseConsole()`, which `ConfigureConsole` doesn't do; both READMEs say so (F-006).
Source: US-002

Acceptance criteria:
- AC-003.1: [CONFIRMED] Tests: `ConsoleConfigTests.GetColor_ReturnsDefaultColor_ForEachLogType`, `SetColor_OverridesDefaultColor`, `GetColor_ReturnsWhite_ForUnmappedLogType`, `Formatter_IsNullByDefault`, `Formatter_CanBeSet`, `HangfireOptionsExtensionsTests.ConfigureConsole_AddsLoggerFactory`, `ConfigureConsole_AddsOption`, `ConfigureConsole_WithAction_AppliesConfiguration`, `ConfigureConsole_WithAction_Throws_ForNullAction`, `ConfigureConsole_ReturnsOptions_ForChaining`
- AC-003.2: WHEN `ConfigureConsole()` (or any `AddLogger<T>`) is called twice, the factory is registered once. (F-003) Test: `ExecutionLoggerIsolationTests.AddLogger_IgnoresTheSameFactoryRegisteredTwice`
- AC-003.3: Formatting and level filtering. (F-004, F-005, F-007) Tests: `ConsoleFormattingTests.FormatMessage_FillsNamedPlaceholders_ByPosition`, `FormatMessage_KeepsNumericPlaceholdersWorking`, `FormatMessage_AppliesFormatAndAlignment`, `FormatMessage_TreatsDoubledBracesAsLiterals`, `FormatMessage_KeepsPlaceholder_WhenArgumentIsMissing`, `FormatMessage_WithoutArguments_WritesMessageAsIs`, `FormatMessage_UsesCustomFormatter_WhenSet`, `IsEnabled_RespectsMinimumLevel_WithSuccessAsInformation`, `MinimumLevel_DefaultsToTrace`

### REQ-004: Progress bars
`context.CreateProgressBar(title?)` returns an `ExecutionProgressBar` (`SetValue(percentage)`, `SetValue(current, total)`, `Complete()`). It throws `InvalidOperationException` outside a Hangfire job. It reads the `PerformContext` with `GetPerformContext()`.
Source: US-002

Acceptance criteria:
- AC-004.1: WHEN called outside a Hangfire job, THE SYSTEM throws `InvalidOperationException`. Test: `ConsoleFormattingTests.CreateProgressBar_Throws_OutsideAHangfireJob`

### REQ-005: Microsoft.Extensions.Logging bridge
`options.AddMicrosoftLogging()` (package `ExecutionFlow.Extensions.Logging`, depends on the core and `Microsoft.Extensions.Logging.Abstractions` >= 6.0.0 only, so any processor can use it, ADR-0007) sends `context.Log` to `ILogger`. The category is the handler type's full name, else the event type's, else `ExecutionFlow`. Each entry runs inside a scope with `JobId` and `AttemptNumber`. The message and arguments are passed as a template, so named properties survive. Levels map one to one, `Success` to `Information` (RN-002). Disabled levels are skipped. Opt-in (P-004).
Source: US-004

Acceptance criteria:
- AC-005.1: Category. Tests: `MicrosoftExecutionLoggerFactoryTests.CreateLogger_UsesHandlerTypeAsCategory_WhenHandlerIsKnown`, `CreateLogger_UsesEventTypeAsCategory_WhenHandlerIsUnknown`, `CreateLogger_UsesDefaultCategory_WhenNoTypeIsKnown`
- AC-005.2: Template, scope and levels. Tests: `MicrosoftExecutionLoggerFactoryTests.Log_KeepsTemplatePropertiesAndFormatsMessage_WhenArgsAreGiven`, `Log_WrapsEntryInJobScope_WhenLogging`, `Log_MapsLevel_WhenLogging`, `Log_SkipsEntry_WhenLevelIsDisabled`
- AC-005.3: Registered once. Test: `MicrosoftExecutionLoggerFactoryTests.AddMicrosoftLogging_RegistersFactoryOnce_WhenCalledTwice`

## Business rules
- RN-001 [CONFIRMED] Loggers see the execution's `FlowParameters` (the same instance the handler uses).
- RN-002 [CONFIRMED] `Success` has no equivalent in `Microsoft.Extensions.Logging`.

## Out of scope
- Log persistence and retention (the destination's job).
- Structured logging inside the Hangfire console (the console is plain text).

## Decided to-be changes (implemented in v1.2.0)
Decided with the maintainer on 2026-09-30.

1. **Isolate each logger** (F-001): `CompositeExecutionLogger` calls each logger in its own try/catch. A failure goes to `Trace.TraceWarning`, and the handler and the other loggers keep going.
2. **New package `ExecutionFlow.Extensions.Logging`** (F-002): a bridge from `context.Log` to the application's `ILogger`. `options.AddMicrosoftLogging()` registers a logger factory that resolves `ILoggerFactory`. The category is the handler type (or the event type when the handler is unknown), and `JobId`/`AttemptNumber` go into a logging scope. `Success` maps to `Information`, and the message template and arguments are passed as-is, so structured logging works. The package depends only on the core and `Microsoft.Extensions.Logging.Abstractions` >= 6.0, so it works with any processor (ADR-0007). Opt-in (P-004).
3. **The same logger factory registered twice is kept once** (F-003): `AddLogger` ignores a type that's already registered, so `ConfigureConsole()` twice doesn't duplicate lines.
4. **Console formats like `ILogger` templates** (F-004): each `{placeholder}`, named or numeric, is filled with the argument in the same position, and `{{`/`}}` are literal braces. The same message works in the console and in `ILogger`.
5. **Minimum level for the console** (F-005): `ConsoleConfig.MinimumLevel`, default `Trace` (everything, as today). `Success` counts as `Information` for filtering.
6. **Document `GlobalConfiguration.UseConsole()`** (F-006) in both READMEs.
7. **Tests** for the console logger, formatting and progress bars (F-007). `CreateProgressBar` uses `GetPerformContext()`.

## Open questions
- None.
