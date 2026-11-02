# Quickstart validation: Handler configuration classes

## Prerequisites

```
dotnet restore
dotnet build --no-restore
```

## Scenarios

1. **Configuration class** — `dotnet test --no-build --filter "FullyQualifiedName~HandlerConfigurationTests"`. Expect: every builder value reaches the step; inline `Add<T>()` returns the same builder; a handler configured twice throws (FR-001).
2. **Item-by-item precedence** — same filter. Expect: the builder changes only what it calls; dependencies merge and can be removed; `AllowOverlap()` cancels `[NoOverlap]` (FR-002).
3. **Builder vs options in Hangfire** — `--filter "FullyQualifiedName~HandlerConfigurationHangfireTests"`. Expect: the builder wins over `SetJobAutoRun<T>`/`SetJobTimeZone<T>` with a trace warning; an option applies when the builder doesn't set that item (AC-002.5, AC-002.6).
4. **Assembly scan** — `ApplyConfigurationsFromAssembly` applies every configuration class, skips abstract ones, throws without a parameterless constructor (FR-003).
5. **Hangfire extensions** — `handler.Queue("reports")` and `.AutomaticRetry(...)` win over the class's attributes (FR-004).
6. **Regression** — full `dotnet test`: only the two tests that asserted the `SetJobAutoRun` + plan error change (SC-003).
