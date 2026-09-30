# Findings: Handlers and context

- F-001 bug: `FlowParameters` stores items in a case-insensitive dictionary (`StringComparer.OrdinalIgnoreCase`) but keeps read-only keys in a `HashSet<string>` with the default, case-sensitive comparer (`Src/ExecutionFlow/Abstractions/FlowParameters.cs`). `Parameters["performcontext"] = null` overwrites the read-only `"PerformContext"`, and `Remove("customname")` removes the read-only `"CustomName"`.
  - Related: AC-003.2
  - Resolution: fixed (AC-003.2)

- F-002 coupling: the context exposes no job ID or attempt number. Handlers that need them (logging, correlation, idempotency) read Hangfire's `PerformContext` from `Parameters["PerformContext"]` and cast it, tying handler code to Hangfire (against ADR-0007).
  - Related: REQ-002
  - Resolution: fixed (AC-002.2, AC-002.3)

- F-003 usability: the infrastructure keys (`"PerformContext"`, `"CustomName"`) are internal constants (`ContextConsts`), so handlers must type magic strings.
  - Related: AC-003.3
  - Resolution: resolved without public keys (maintainer's decision): `GetPerformContext()` and `ICustomNameEvent` cover the access, and the keys stay internal (AC-003.3)

- F-004 docs: the README shows handlers setting `CorrelationId`/`LogType` without saying that parameters live only for that execution and mainly serve the logger.
  - Related: REQ-003
  - Resolution: fixed (README)
