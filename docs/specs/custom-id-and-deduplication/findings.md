# Findings: Custom ID and deduplication

- F-001 possible bug: deduplication only considers Processing and Enqueued jobs (`Src/ExecutionFlow.Hangfire/Infrastructure/HangfireExecutionManager.cs`, `IsRunning`/`IsPending`). Delayed or scheduled publishes, and failed jobs waiting for an automatic retry (Hangfire moves them to Scheduled), aren't seen. So publishing the same custom ID again creates a duplicate.
  - Related: RN-003, AC-003.2
  - Resolution: fixed (REQ-004, REQ-007, RN-003)

- F-002 rule: `ReplaceExisting` on a Processing job only marks it Deleted. The running handler keeps going unless it observes the `CancellationToken`, so the old and new job can run at the same time.
  - Related: AC-005.2
  - Resolution: accepted as rule (AC-005.2), documented in the README

- F-003 limitation: deduplication checks and then creates, without any atomic primitive (`HangfireDispatcher.CheckDeduplication` followed by `CreateJob`). Two producers publishing the same custom ID at the same time both create a job.
  - Related: RN-005
  - Resolution: fixed (REQ-007, RN-005); the crash window is documented

- F-004 bug (regression in unpublished v1.2.0): `FlowContext.SetCustomId(null)` stored the JSON literal `null`, which `JobParameters.Decode` returned as the string `"null"`. So the job looked like it had the custom ID "null".
  - Related: AC-002.3
  - Resolution: fixed. `Decode` maps the literal `null` to no custom ID. Tests: `JobParametersTests.WriteCustomId_Null_ReadsBackAsNull`, `HangfireJobDispatcherTests.WithoutDI_DispatchEventAsync_ClearsCustomId_WhenHandlerSetsNull`

- F-005 design debt (not market-grade): lookups by custom ID and deduplication page through the Monitoring API, which Hangfire provides for the dashboard, and read one job parameter per active job (`InfraUtils.ReadAll`, page size 10). That's O(active jobs) storage calls per publish with deduplication on. Tools that guarantee uniqueness (Sidekiq unique jobs, Temporal workflow IDs, Service Bus and SQS FIFO duplicate detection) use an atomic key lookup instead.
  - Related: RN-006, NFR-001
  - Resolution: fixed for publishing (REQ-007); execution manager lookups use the key first and fall back to the scan (RN-006)

- F-006 untested: nothing tests that `SetCustomId(id)` with a non-null value overwrites the stored job parameter, and that lookups see the new value (RN-004). `ContextTests.SetCustomId_Stores_Value_Readable_Via_CustomId` only checks the in-memory `FlowContext.CustomId`.
  - Related: AC-002.2, RN-004
  - Resolution: open

- F-007 untested: with several jobs sharing one custom ID, nothing tests which one `Cancel`/`Retry` acts on (AC-003.4). `Publish_ReplaceExisting_CancelsAndEnqueuesNew` doesn't check that the old job was deleted (AC-005.1).
  - Related: AC-003.4, AC-005.1
  - Resolution: partly fixed. The deletion is now asserted (`Publish_ReplaceExisting_CancelsAndEnqueuesNew`, `Publish_ReplaceExisting_DeletesReservedJob`). The first-match order of the scan (AC-003.4) is still untested
