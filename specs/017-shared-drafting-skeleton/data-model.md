# Data Model: Shared Drafting Skeleton

No stored record, enum value or serialized shape changes (FR-017). The types below are in-memory only and live in `AgenticChangeOps.Drafting`.

## DraftingEngine<TRecord, TBasis, TGenerated, TContent, TRejection>

Holds the store and clock the agent already uses, and one `IDraftingAgent`. The id generator is passed only to the shared tool-call helper by each agent's save delegate; the engine takes none. Operations:

| Operation | Returns |
|---|---|
| `RunAsync(id, cancellationToken, save)` | `DraftingOutcome<TRecord, TRejection>` |
| `GetLatest(id)` | `QueryResult<TRecord?>` (existing Intake type) |
| `GetHistory(id)` | `QueryResult<IReadOnlyList<TRecord>>` |

`save` is the per-call save delegate (see below); it carries anything call-specific, such as the automatic marker, so the engine never sees it.

## IDraftingAgent<TRecord, TBasis, TGenerated, TContent, TRejection>

| Member | Purpose |
|---|---|
| `bool PropagatesCancellation` | True only for the Developer: caller-requested cancellation is rethrown instead of reported. |
| `(TBasis? Basis, ValidationError? Error) CheckEligibility(Guid id, ChangeRequest? request)` | The agent's own rules, order and messages. Called before generation and again inside the locked step. |
| `Task<TGenerated?> GenerateAsync(ChangeRequest request, TBasis basis, CancellationToken ct)` | Calls the agent's own generator. |
| `ReviewOutcome<TContent, TRejection> Review(TGenerated? generated)` | The agent's validation, and for the Developer its quote check. |
| `ValidationError? CheckStillCurrent(Guid changeRequestId, TBasis usedForGeneration, TBasis current)` | The agent's own currency check; the Requirement agent returns null. |
| `int CountExisting(Guid id)` | Reads the agent's own collection for the next version. |
| `IReadOnlyList<TRecord> ReadAll(Guid id)` | Reads the agent's own collection (used by both reads). |

## DraftingOutcome<TRecord, TRejection>

Success with a record; or failure with errors and an optional `GenerationFailureCategory`; or rejected with a typed `TRejection`. Agents without a rejection use a one-value placeholder enum owned by the Drafting project.

## ReviewOutcome<TContent, TRejection>

`Valid(content)`, `Invalid(errors)` (the engine reports `MalformedOutput`), or `Rejected(reason)`.

## NoBasis and DraftingErrors

`NoBasis` is the basis type of an agent generated from the change request alone (a sealed class with one instance); it lets such an agent use the engine without inventing a basis. `DraftingErrors.NotFound(id)` is the not-found error (field `ChangeRequestId`, message `No change request with id <id> exists.`) that every agent's eligibility check and the engine's reads share.

## Save delegate

`(ChangeRequest current, TBasis basis, TContent content, int version, DateTime generatedAt) -> TRecord`. Runs inside the lock; builds the agent's record and audit entries, writes tool-call entries, and calls the agent's own atomic store save so the record and its main audit entry are stored together or not at all.

## ToolCallAuditEntries (only if T018 finds the two copies identical)

`static ChangeRequestAuditEntry Create(string agentName, IIdGenerator ids, Guid changeRequestId, ChangeRequestStatus status, ToolCallRecord call, DateTime generatedAt)`.

- **Target:** control characters removed and cut to 120 characters.
- **Timing:** `OccurredAt` is the call's start, never later than `generatedAt`.
- **Elapsed:** `ElapsedMilliseconds` clamped to 0 through `int.MaxValue`.
- **Fields:** event type `ToolCalled`, no details, status before equals status after, outcome as text.

The loop over a content's tool calls stays in each agent's save delegate (the Architect tolerates a null list, the Developer does not).

## What each agent keeps (and the engine never sees)

Records, audit event types, enum values, inputs and bases, prompts, generators, validators, verifier, result types, automatic marker, tool-call mapping (see research R10).
