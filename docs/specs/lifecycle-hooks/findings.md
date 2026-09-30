# Findings: Lifecycle hooks

- F-001 bug: `Duration` is wrong on every host outside UTC. Hangfire stores `StartedAt` in UTC (`2026-09-30T19:00:19.8482526Z`). `HangfireStateFilter.GetDuration` parses it with `DateTime.TryParse`, which converts it to local time (`Kind = Local`), and subtracts it from `DateTime.UtcNow`. On a UTC-3 host, a 5-second job reported `03:00:05`. Confirmed with a spike on 2026-09-30.
  - Related: AC-003.2
  - Resolution: fixed (AC-003.2)

- F-002 bug: `OnFailed` fires on every failed attempt, including attempts that will be retried. `HangfireStateFilter` is registered with order -1, and Hangfire's `AutomaticRetryAttribute` has order 20, so our election filter sees the candidate `FailedState` before the retry filter replaces it with `ScheduledState` (orders confirmed with a spike). A job that fails 3 times and then succeeds fires `OnFailed` 3 times. The README's example sends an admin alert from `OnFailed`.
  - Related: AC-002.4, RN-004
  - Resolution: fixed (AC-002.0, AC-002.4..AC-002.6; filter order 100 and `TraversedStates`)

- F-003 risk: hook calls aren't wrapped in try/catch (`HangfireStateFilter.OnStateElection`). A hook that throws, for example because the `Publish` in the README example fails when the database is down, propagates into Hangfire's state election.
  - Related: RN-002
  - Resolution: fixed (REQ-004)

- F-004 surprising: `OnEnqueued` for a publish runs in the producer process, and never runs for producer-only hosts, which register no filters.
  - Related: RN-001
  - Resolution: accepted as rule (RN-001), documented in the README

- F-005 gap: scheduled jobs fire no hook when they're created. The first hook is `OnEnqueued`, when they become due.
  - Related: RN-003
  - Resolution: accepted as rule (RN-003), documented in the README

- F-006 misleading contract: the README says "All events include `Duration`", but it's always zero in `OnEnqueued`, `OnProcessing`, `OnCancelled`, and retries seen from Scheduled.
  - Related: AC-003.3
  - Resolution: fixed (REQ-003, AC-003.3)

- F-007 untested: nothing tests that `Duration` has the right value on a non-UTC host (F-001), or that `OnFailed` doesn't fire when a retry follows (F-002). The existing tests call the filter with a candidate state directly, without Hangfire's retry filter in the pipeline.
  - Related: AC-002.4, AC-003.2
  - Resolution: fixed. The state filter tests now run the real `AutomaticRetryAttribute` first. The duration test only catches a regression on a host outside UTC (CI runs in UTC)
