# Plan: Logging and console (v1.2.0)

Spec: [spec.md](spec.md) ("Decided to-be changes", items 1–7)
Status: approved

## Affected packages and public API
| Package | Change | Breaking? |
| ------- | ------ | --------- |
| `ExecutionFlow` | Composite isolation. `AddLogger` dedupe. New `ExecutionLoggerContext`, `IExecutionLoggerContextFactory`, `FlowContextBuilder.SetHandler(Type, Type)`, `ExecutionLoggerFactory.CreateLogger(ExecutionLoggerContext)` | No |
| `ExecutionFlow.Hangfire` | `HangfireJobDispatcher` calls `SetHandler` | No |
| `ExecutionFlow.Hangfire.Console` | `ILogger`-style templates, `ConsoleConfig.MinimumLevel`, `CreateProgressBar` uses `GetPerformContext()` | Behavior: named placeholders are now filled |
| `ExecutionFlow.Extensions.Logging` (new) | `options.AddMicrosoftLogging()`, `MicrosoftExecutionLoggerFactory` | New package |

## Design
- **Isolation:** `CompositeExecutionLogger.Log` wraps each logger in try/catch → `Trace.TraceWarning`.
- **Dedupe:** `ExecutionFlowOptions.AddLogger(Type)` ignores a type that's already registered.
- **Logger context (optional interface, no break):** `IExecutionLoggerContextFactory : IExecutionLoggerFactory` adds `CreateLogger(ExecutionLoggerContext)`. `ExecutionLoggerFactory.CreateLogger(ExecutionLoggerContext)` uses it when a factory implements it, otherwise the `FlowParameters` overload. `FlowContextBuilder` builds the context from `SetJob` and the new `SetHandler(handlerType, eventType)`. `HangfireJobDispatcher` sets both.
- **Templates:** `LogMessageTemplate.Format(message, args)` (internal to the console package) fills each `{placeholder}` (named or numeric, with optional `,alignment` and `:format`) with the argument **in order of appearance**, like `Microsoft.Extensions.Logging`. `{{`/`}}` are literal braces. With no arguments the message is written as-is. Placeholders without an argument stay as written.
- **Minimum level:** `ConsoleConfig.MinimumLevel` (default `Trace`). `Success` is compared as `Information`.
- **`ExecutionFlow.Extensions.Logging`** (netstandard2.0, depends on `ExecutionFlow` + `Microsoft.Extensions.Logging.Abstractions` >= 6.0.0):
  - `MicrosoftExecutionLoggerFactory : IExecutionLoggerContextFactory`, built with `ILoggerFactory`. The category is the handler type's full name, otherwise the event type's, otherwise `"ExecutionFlow"`.
  - The logger calls `ILogger.Log(level, message, args)` inside `BeginScope({ JobId, AttemptNumber })`, so templates stay structured.
  - Level mapping: Trace/Debug/Information/Warning/Error/Critical 1:1, and `Success` → `Information`.
  - `options.AddMicrosoftLogging()` = `AddLogger<MicrosoftExecutionLoggerFactory>()`. `ILoggerFactory` is resolved from the container (DI), or must be registered in the built-in activator.

## Decisions
### DEC-001: Optional context interface instead of changing `IExecutionLoggerFactory`
- Changing `CreateLogger(FlowParameters)` would break every custom logger. The derived interface is opt-in.

### DEC-002: Placeholders are positional by appearance (like `ILogger`)
- This keeps one message working in both destinations. `"{0} {1}"` keeps working, because the placeholders appear in order.

## Versioning impact
1.2.0 (lockstep). The new package ships at 1.2.0.
