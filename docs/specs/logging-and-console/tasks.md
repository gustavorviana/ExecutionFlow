# Tasks: Logging and console (v1.2.0)

Plan: [plan.md](plan.md)

- [x] TASK-001: Isolate loggers in `CompositeExecutionLogger`, and dedupe `AddLogger`
  - Requirements: AC-002.2, AC-003.2
  - Done when: throwing-logger and duplicate-registration tests pass

- [x] TASK-002: `ExecutionLoggerContext`, `IExecutionLoggerContextFactory`, `FlowContextBuilder.SetHandler`; the dispatcher sets it
  - Requirements: item 2
  - Done when: a context factory receives the job and handler facts

- [x] TASK-003: Console: `ILogger`-style templates, `MinimumLevel`, `CreateProgressBar` via `GetPerformContext()`, and tests (F-007)
  - Requirements: REQ-003, REQ-004
  - Done when: formatting, level and console-logger tests pass

- [x] TASK-004: New package `ExecutionFlow.Extensions.Logging` with its test project, in the solution
  - Requirements: item 2
  - Done when: the bridge tests pass and `dotnet pack` includes it

- [x] TASK-005: READMEs (root, Console, new package), spec, findings
  - Done when: the spec is `approved` and every AC names an existing test
