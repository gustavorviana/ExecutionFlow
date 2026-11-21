# Data Model: ID format validation

No stored data changes. The feature adds one rule over the existing ID strings.

## ID

A string that identifies a job. Kinds:

| Kind | Source | Validated at |
|------|--------|--------------|
| Custom ID | `ICustomIdEvent.CustomId` | `Publish` / `Schedule` |
| Recurring ID | `[Recurring(Id)]`, `[RecurringInterval(Id)]`, `HandlerBuilder.Id`, or `IJobIdGenerator.GenerateId` | handler registration (explicit) / `RecurringJobResolver` (generated) |
| Query ID | argument of `IsRunning`, `IsPending`, `Cancel`, `Retry`, `Trigger` | method entry |
| Hangfire job ID | created by the storage | never created by the app; passes as a query ID |

## ID format (FR-001)

- Allowed characters: Unicode letters, combining marks and decimal digits (the identifier categories of C#, without format characters), `_`, `-`, `.`, `+`, `` ` ``, `[`, `]`, `,`, `=`, space (U+0020).
- At least one character other than the space.
- No trimming or normalization.

## Validation outcomes

| Input | Custom ID | Query / trigger ID | Recurring ID |
|-------|-----------|--------------------|--------------|
| `null` | no custom ID | `ArgumentNullException` | `null` explicit ID = use the generator |
| `""` | no custom ID | `ArgumentException` | `HandlerBuilder.Id`: `ArgumentException` |
| only spaces | `ArgumentException` | `ArgumentException` | `ArgumentException` |
| character outside the format | `ArgumentException` | `ArgumentException` | `ArgumentException` naming the handler |
| valid | accepted | accepted | accepted |
