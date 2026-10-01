# Tasks: Local hosting (v1.3.0)

Plan: [plan.md](plan.md)

- [ ] TASK-001: Create the package project, add the `IClock` seam with the netstandard2.0 polyfill, and add the project to the solution
  - Requirements: REQ-009, NFR-002
  - Depends on: execution-plan/TASK-003, recurring-jobs/TASK-009
  - Done when: it builds for both TFMs

- [ ] TASK-002: `DependencyGate`
  - Requirements: REQ-005
  - Depends on: TASK-001
  - Done when: `DependencyGateTests` pass

- [ ] TASK-003: `ConcurrencyLimiter`, `HostingOptions` and `HandlerExecutor`
  - Requirements: REQ-002, REQ-003, REQ-006
  - Depends on: TASK-001
  - Done when: `HostingOptionsTests` and `ConcurrencyLimiterTests` pass

- [ ] TASK-004: `RecurringHandlerHost<T>`
  - Requirements: REQ-003, REQ-004, REQ-005
  - Depends on: TASK-002, TASK-003
  - Done when: `RecurringHandlerHostTests` pass

- [ ] TASK-005: `ExecutionPlanRunner` and `RunOnceHostedService`
  - Requirements: REQ-007
  - Depends on: TASK-003
  - Done when: `ExecutionPlanRunnerTests` and `RunOnceHostedServiceTests` pass

- [ ] TASK-006: `AddExecutionFlowHosting` and `StartupReportHostedService`
  - Requirements: REQ-001, REQ-002, REQ-008
  - Depends on: TASK-004, TASK-005
  - Done when: `ServiceCollectionExtensionsTests` and `StartupReportHostedServiceTests` pass

- [ ] TASK-007: Example, READMEs and publish workflow
  - Requirements: —
  - Depends on: TASK-006
  - Done when: the example runs in scheduled and run-once modes
