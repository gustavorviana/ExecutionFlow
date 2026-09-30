# Spec: <capability>

Status: draft | in review | approved
Mode: brownfield as-is | to-be
Packages: <ExecutionFlow | ExecutionFlow.Hangfire | ...>

## Context
<The problem this capability solves for library users, and its goal.>

## Actors
| Actor | Description |
| ----- | ----------- |
| Handler author | Writes `IHandler`/`IHandler<TEvent>` implementations. |
| Producer app | Publishes or schedules events. |
| Consumer app | Hosts the Hangfire server that runs the handlers. |
| Dashboard operator | Monitors and controls jobs through the Hangfire dashboard. |

## User stories
- US-001: As a <actor>, I want <action>, so that <benefit>.

## Requirements
### REQ-001: <title>
<Observable behavior. No implementation details.>
Source: US-001

Acceptance criteria:
- AC-001.1: WHEN <event> THE SYSTEM SHALL <response>. Test: `<TestClass.Method_Expectation_WhenCondition>`
- AC-001.2: Given <context> When <action> Then <result>. Test: none (see F-001)

## Business rules
- RN-001 [CONFIRMED] <rule>. (`Src/...cs:line`)
- RN-002 [INFERRED] <rule>. Validate: <question>.

## Edge cases and errors
- <input or state> → <expected behavior>

## Non-functional requirements
- NFR-001: <verifiable metric>

## Out of scope
- <explicitly excluded behavior>

## Open questions
- [NEEDS CLARIFICATION: ...]
