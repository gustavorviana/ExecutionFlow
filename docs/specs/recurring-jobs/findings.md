# Findings: Recurring jobs

- F-001 bug: a recurring handler without `[Recurring]` makes `Build()` fail with Hangfire's `ArgumentNullException ("cronExpression")` (`Src/ExecutionFlow.Hangfire/HangfireSetup.cs`, `RegisterRecurring`, passing a null cron to `AddOrUpdate`). The XML docs on `RecurringJobRegistryInfo.Cron` say null means "triggered manually only". Confirmed with a spike on 2026-09-30.
  - Related: AC-001.3
  - Resolution: fixed (AC-001.3)

- F-002 dangerous: `RemoveOrphanRecurringJobs` removes every recurring job in the storage that this setup didn't register (`HangfireSetup.RegisterRecurring`, the loop over `connection.GetRecurringJobs()`). That includes native Hangfire recurring jobs, and jobs from other applications that share the storage.
  - Related: AC-006.1
  - Resolution: fixed (AC-006.1)

- F-003 debt: the recurring job ID is `handlerType.FullName` (`DefaultRecurringServiceIdGenerator`). Renaming or moving the class leaves the old job orphaned. It keeps firing and fails at execution until someone removes it.
  - Related: RN-002
  - Resolution: fixed (REQ-007)

- F-004 fragile, possibly already broken: manual-trigger detection compares the state's `Reason`, which is free text and not a Hangfire contract, with `"Triggered using recurring job manager"` and `"Triggered via Dashboard UI"` (`Src/ExecutionFlow.Hangfire/Infrastructure/Filters/HangfireAutoRunFilter.cs`). A search of `Hangfire.Core` 1.8.23 finds the first string but **not** the second (it has `"Triggered by "` instead). If the dashboard's "Trigger now" reaches the filter with a different reason, a handler with auto-run off is cancelled silently. [INFERRED] This needs a check with a real dashboard.
  - Related: AC-004.3
  - Resolution: fixed by removal. `HangfireAutoRunFilter` is no longer registered, because disabled handlers use `Cron.Never()` and never fire automatically (plan DEC-001)

- F-005 missing feature: recurring schedules are always UTC, and there's no time zone option (`RegisterRecurring` calls `AddOrUpdate` without options).
  - Related: RN-001
  - Resolution: fixed (REQ-008)

- F-006 untested: nothing tests that `Build()` actually registers the recurring jobs with Hangfire (AC-002.1), or the orphan cleanup (AC-006.1).
  - Related: AC-002.1, AC-006.1
  - Resolution: fixed (`RecurringRegistrationTests`)

- F-007 cost: a handler with auto-run off still fires on every schedule tick, creating a job that is cancelled and expires after 1 second. With frequent crons or many disabled handlers, that's constant storage writes and dashboard noise.
  - Related: RN-003
  - Resolution: fixed (AC-003.1, RN-003)

- F-008 nondeterministic: the auto-run filter runs on the server that fires the schedule tick, not on the one that processes the job. With consumers configured differently on one storage, a tick may run or be cancelled depending on which server's scheduler picked it up.
  - Related: RN-005
  - Resolution: fixed (REQ-003, RN-005)

- F-009 global state (belongs to `setup-and-configuration`): `HangfireSetup.Build()` without a service provider reuses `JobActivator.Current` when it's a `FlowEngineJobActivator`, even if that activator belongs to **another** `HangfireSetup` (for example one that called `ConfigureActivator()`). The build then uses the other setup's generators and registrations. Found through a flaky test on 2026-09-30.
  - Related: `setup-and-configuration`
  - Resolution: open (to be mapped in the `setup-and-configuration` spec)
