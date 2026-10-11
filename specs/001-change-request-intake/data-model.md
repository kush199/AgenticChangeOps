# Data Model: Change Request Intake

**Feature**: 001-change-request-intake | **Date**: 2026-10-11

## Enumerations

### `Priority`

| Value | Meaning |
|-------|---------|
| `Low` | Minimal urgency; can be scheduled at convenience |
| `Medium` | Normal urgency; scheduled in the next cycle |
| `High` | Elevated urgency; requires prompt attention |
| `Critical` | Maximum urgency; requires immediate triage |

### `ChangeType`

| Value | Meaning |
|-------|---------|
| `Feature` | New capability or enhancement |
| `Bugfix` | Correction of a defect |
| `ConfigChange` | Change to configuration without code modification |

### `ChangeRequestStatus`

Full lifecycle enum. This slice sets only `Submitted`; transitions to other values are
managed by downstream slices.

| Value | Meaning |
|-------|---------|
| `Submitted` | Intake recorded; awaiting triage |
| `UnderReview` | Being analysed by the Architect or Developer agent |
| `Approved` | Approved for implementation |
| `Rejected` | Declined; reason captured in audit history |
| `Deferred` | Postponed; may be reconsidered in a future cycle |

### `AuditEventType`

| Value | Meaning |
|-------|---------|
| `Submitted` | Change request received and persisted (this slice) |

*Additional event types (e.g., `StatusChanged`, `Approved`, `Rejected`) are out of scope
for this slice and will be added in future slices.*

---

## Record Types

### `SubmissionRequest`

Represents the caller-supplied data for a new change request submission.

| Field | Type | Required | Max Length | Notes |
|-------|------|----------|------------|-------|
| `Title` | `string` | Yes | 200 | Whitespace-only treated as empty |
| `Description` | `string` | Yes | 2,000 | Whitespace-only treated as empty |
| `Requester` | `string` | Yes | 100 | Whitespace-only treated as empty |
| `AffectedSystem` | `string` | Yes | 200 | Whitespace-only treated as empty |
| `Priority` | `Priority` | Yes | — | Must be a defined enum value |
| `ChangeType` | `ChangeType` | Yes | — | Must be a defined enum value |

**Validation rules** (all run before rejection; collect-all semantics):
1. `Title` — not null/blank; length ≤ 200
2. `Description` — not null/blank; length ≤ 2,000
3. `Requester` — not null/blank; length ≤ 100
4. `AffectedSystem` — not null/blank; length ≤ 200
5. `Priority` — must be a defined `Priority` enum value
6. `ChangeType` — must be a defined `ChangeType` enum value

---

### `ChangeRequest`

The persisted record for a submitted change request.

| Field | Type | Assigned by | Notes |
|-------|------|-------------|-------|
| `ChangeRequestId` | `Guid` | System (`IIdGenerator`) | Globally unique; never supplied by caller |
| `Title` | `string` | Caller (trimmed) | |
| `Description` | `string` | Caller (trimmed) | |
| `Requester` | `string` | Caller (trimmed) | |
| `AffectedSystem` | `string` | Caller (trimmed) | |
| `Priority` | `Priority` | Caller | |
| `ChangeType` | `ChangeType` | Caller | |
| `Status` | `ChangeRequestStatus` | System | Always `Submitted` at intake |
| `SubmittedAt` | `DateTime` (UTC) | System (`ISystemClock`) | Immutable after creation |

**Identity**: `ChangeRequestId` (GUID) — primary lookup key.
**Immutability**: All fields are immutable after creation in this slice.

---

### `ChangeRequestAuditEntry`

Records a single lifecycle event for a change request.

| Field | Type | Assigned by | Notes |
|-------|------|-------------|-------|
| `AuditEntryId` | `Guid` | System (`IIdGenerator`) | Globally unique |
| `ChangeRequestId` | `Guid` | System | FK to `ChangeRequest` |
| `EventType` | `AuditEventType` | System | Always `Submitted` in this slice |
| `OccurredAt` | `DateTime` (UTC) | System (`ISystemClock`) | Same value as `ChangeRequest.SubmittedAt` at intake |
| `Details` | `string?` | System | Optional descriptive text; may be null |

**Ordering**: Audit entries for a given `ChangeRequestId` are returned sorted by
`OccurredAt` ascending. Ties broken by insertion order.

---

### `ValidationError`

Represents a single field-level validation failure.

| Field | Type | Notes |
|-------|------|-------|
| `FieldName` | `string` | Name of the failing field (e.g., `"Title"`) |
| `Message` | `string` | Human-readable explanation (e.g., `"Title is required"`) |

---

### `SubmissionResult`

The outcome of a `Submit` call. Exactly one of the two states holds:

| State | Condition | `IsSuccess` | `ChangeRequest` | `Errors` |
|-------|-----------|-------------|-----------------|---------|
| Success | Validation passed | `true` | Full `ChangeRequest` record | Empty list |
| Failure | Validation failed | `false` | `null` | ≥1 `ValidationError` items |

Factory methods:
- `SubmissionResult.Success(ChangeRequest changeRequest)`
- `SubmissionResult.Failure(IReadOnlyList<ValidationError> errors)`

---

## Infrastructure Interfaces

### `ISystemClock`

| Member | Signature | Notes |
|--------|-----------|-------|
| `UtcNow` | `DateTime UtcNow { get; }` | Returns current UTC time |

Production: `SystemClock` — delegates to `DateTime.UtcNow`.
Test fake: `FakeSystemClock` — returns a configurable `DateTime` value.

### `IIdGenerator`

| Member | Signature | Notes |
|--------|-----------|-------|
| `NewId` | `Guid NewId()` | Returns a new unique identifier |

Production: `GuidIdGenerator` — delegates to `Guid.NewGuid()`.
Test fake: `FakeIdGenerator` — returns a queue of pre-configured `Guid` values.

---

## Store Interface

### `IChangeRequestStore`

| Method | Signature | Notes |
|--------|-----------|-------|
| `Save` | `void Save(ChangeRequest changeRequest)` | Persists a new record; throws if ID already exists |
| `FindById` | `ChangeRequest? FindById(Guid id)` | Returns null if not found |
| `SaveAuditEntry` | `void SaveAuditEntry(ChangeRequestAuditEntry entry)` | Appends to audit log |
| `GetAuditEntries` | `IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId)` | Returns empty list if ChangeRequestId unknown |

Implementation: `InMemoryChangeRequestStore` — backed by two `Dictionary` fields.

---

## State Transition Diagram (this slice)

```
[New Submission]
      │
      ▼
  Validate
      │
 ┌────┴────┐
 │         │
Fail      Pass
 │         │
 ▼         ▼
Return   Assign ChangeRequestId + SubmittedAt
Errors   Persist ChangeRequest (Status = Submitted)
         Record AuditEntry (EventType = Submitted)
         Return SubmissionResult.Success(changeRequest)
```

State transitions beyond `Submitted` are out of scope for this slice.
