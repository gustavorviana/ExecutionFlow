# Tasks: Handlers and context (v1.2.0)

Plan: [plan.md](plan.md)

- [x] TASK-001: Case-insensitive read-only keys
  - Requirements: AC-003.2
  - Done when: tests for case-variant overwrite and removal throw

- [x] TASK-002: `FlowContext.JobId`/`AttemptNumber` and `FlowContextBuilder.SetJob`, set by `HangfireJobDispatcher`
  - Requirements: REQ-002
  - Done when: builder tests and dispatcher tests (first attempt, retry) pass

- [x] TASK-003: Public keys and `GetPerformContext()`
  - Requirements: AC-003.3
  - Done when: extension tests pass and `ContextConsts` uses the public keys

- [x] TASK-004: README, spec, findings
  - Done when: the spec is `approved` and every AC names an existing test
