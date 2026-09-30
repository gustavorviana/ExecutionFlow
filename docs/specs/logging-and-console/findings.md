# Findings: Logging and console

- F-001 risk: `CompositeExecutionLogger.Log` calls each logger without a try/catch (`Src/ExecutionFlow/Abstractions/CompositeExecutionLogger.cs`). A logger that throws, e.g. a Hangfire.Console write failure or a buggy custom logger, throws out of `context.Log.Info(...)` inside the handler and fails the job. The remaining loggers are skipped. Same principle as lifecycle hooks: observability must never break execution.
  - Related: AC-002.2
  - Resolution: fixed in v1.2.0 (item 1)

- F-002 gap: there's no bridge to `Microsoft.Extensions.Logging`. `context.Log` only reaches loggers registered with `AddLogger` (today, only the Hangfire console), so logs silently go nowhere without `ConfigureConsole()` and never reach the application's logger.
  - Related: AC-002.3
  - Resolution: fixed in v1.2.0 (item 2: new package `ExecutionFlow.Extensions.Logging`)

- F-003 bug: `AddLogger` accepts the same factory type more than once, so calling `ConfigureConsole()` twice writes every line twice.
  - Related: AC-003.2
  - Resolution: fixed in v1.2.0 (item 3)

- F-004 inconsistency: `ConsoleConfig.FormatMessage` uses `string.Format`. A message written in `ILogger` style (`"Order {OrderId}"`) throws a `FormatException`, which is swallowed, so the raw template is written without values.
  - Related: REQ-003
  - Resolution: fixed in v1.2.0 (item 4)

- F-005 gap: the console has no minimum level. Trace and Debug always go to the dashboard.
  - Related: REQ-003
  - Resolution: fixed in v1.2.0 (item 5)

- F-006 docs: Hangfire.Console needs `GlobalConfiguration.UseConsole()` in the Hangfire configuration. `ConfigureConsole()` doesn't call it, and without it nothing shows up. [INFERRED from Hangfire.Console's documented behavior]
  - Related: REQ-003
  - Resolution: fixed in v1.2.0 (item 6)

- F-007 untested: `HangfireExecutionLogger`, `ConsoleConfig.FormatMessage`, the logger factory and progress bars have no tests. `CreateProgressBar` duplicates the `PerformContext` lookup instead of using `GetPerformContext()`.
  - Related: AC-003.3, AC-004.1
  - Resolution: fixed in v1.2.0 (item 7)
