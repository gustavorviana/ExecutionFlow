# Findings: Execution manager

- F-001 bug: a Scheduled job can't be cancelled. `Cancel(string)` (through `FindHangfireJobId`) only searches Processing and Enqueued, even for an exact Hangfire job ID. The reservation-key fast path also only accepts Processing or Enqueued. So a `Schedule(event, TimeSpan.FromDays(3))` can't be cancelled through `IExecutionManager`, and cancelling a scheduled job is the most common reason to cancel.
  - Related: AC-002.3
  - Resolution: fixed (AC-002.3..AC-002.5)

- F-002 risk: `GetJobs` materializes every job in the state (`.ToList()`) and reads one job parameter per job. For Succeeded or Deleted, Hangfire keeps jobs for a day by default, so a busy app can load thousands of jobs, plus as many extra storage reads, in one call.
  - Related: AC-004.2, NFR-001
  - Resolution: fixed (AC-004.2, lazy `GetJobs`)

- F-003 gap: `JobState` has no Scheduled, so scheduled jobs can't be listed or counted.
  - Related: AC-005.2
  - Resolution: fixed (`JobState.Scheduled`, AC-004.1, AC-005.1)

- F-004 bug: for recurring jobs, `JobInfo.EventTypeName` is `"DispatchRecurringAsync"`, the dispatcher method name (`BuildJobInfo` falls back to `invocationData.Method` when the method isn't generic). The handler type isn't exposed.
  - Related: AC-004.4
  - Resolution: fixed (AC-004.4)

- F-005 surprising: `GetJobs` returns every job in the state, including non-ExecutionFlow jobs. [INFERRED from the code: there's no filter on the job method]
  - Related: AC-004.3
  - Resolution: fixed (AC-004.3)

- F-006 inconsistency: `Cancel` returns `void` while `Retry` returns `bool`, so the caller can't tell whether anything was cancelled.
  - Related: RN-003
  - Resolution: fixed (`Cancel` returns `bool`, REQ-002)

- F-007 risk: `StateChangedAt = new DateTimeOffset(timestamp)` treats `Kind = Unspecified` as local time. A spike shows Hangfire's `JobHelper.DeserializeNullableDateTime` returns `Kind = Utc` (fine), but `Unspecified` gives a -03:00 offset on this host. Storages that read the timestamp straight from a database column may return `Unspecified`.
  - Related: AC-004.5
  - Resolution: fixed (AC-004.5)
