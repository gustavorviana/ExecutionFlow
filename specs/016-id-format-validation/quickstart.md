# Quickstart: validating ID format validation

## Automated

```
dotnet build
dotnet test --no-build --filter "FullyQualifiedName~IdFormatTests|FullyQualifiedName~DispatcherTests|FullyQualifiedName~ExecutionManagerTests|FullyQualifiedName~ExecutionFlowOptionsTests|FullyQualifiedName~HandlerConfigurationHangfireTests|FullyQualifiedName~HangfireValidationTests"
dotnet test --no-build
```

Expected: every test named in the spec's acceptance scenarios passes, and the full suite stays green.

## Manual (Consumer example, needs SQL Server via Aspire)

Run the Aspire AppHost, then against the Consumer API:

| Request | Expected |
|---------|----------|
| `POST /orders/42` | 202, job created with custom ID `order-42` (unchanged) |
| `POST /orders/42%0D%0Afake` | 500 with `ArgumentException` (custom ID `order-42\r\nfake` rejected); no job in the dashboard |
| `GET /jobs/order-42` | `{ running, pending }` as before |
| `DELETE /jobs/a%3Ab` | 500 with `ArgumentException` for `a:b`; no storage access |

The examples need no change: their custom IDs and recurring IDs already match the format (SC-003).

## Code scanning

After the merge, the "log entries created from user input" alerts for `HangfireDispatcher` and `HangfireExecutionManager` should be closed. If they stay open, dismiss them citing [research.md](research.md) R-006.
