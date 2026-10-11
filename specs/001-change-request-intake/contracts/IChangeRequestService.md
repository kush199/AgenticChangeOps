# Contract: IChangeRequestService

**Feature**: 001-change-request-intake | **Date**: 2026-10-11
**Type**: In-process .NET library interface

This document defines the public service contract that callers depend on.
Implementation details (class names, namespaces, internal helpers) are not part of
this contract.

---

## Interface: `IChangeRequestService`

### `Submit`

```
SubmissionResult Submit(SubmissionRequest request)
```

**Purpose**: Validate and persist a new change request.

**Pre-conditions**:
- `request` is not null.

**Post-conditions (success path)**:
- A `ChangeRequest` with a new unique `ChangeRequestId` and status `Submitted` is persisted.
- One `ChangeRequestAuditEntry` with `EventType = Submitted` is persisted, linked to the
  same `ChangeRequestId`.
- Returns `SubmissionResult` where `IsSuccess = true` and `ChangeRequest` is the full
  persisted record.
- No previously stored records are modified.

**Post-conditions (failure path)**:
- `SubmissionResult` where `IsSuccess = false` and `Errors` contains ≥1 `ValidationError`
  items, one per failing field.
- Nothing is persisted; the store is unchanged.

**Validation rules enforced** (all run before returning; collect-all semantics):

| Field | Rules |
|-------|-------|
| `Title` | Required (not null/blank); max 200 characters |
| `Description` | Required (not null/blank); max 2,000 characters |
| `Requester` | Required (not null/blank); max 100 characters |
| `AffectedSystem` | Required (not null/blank); max 200 characters |
| `Priority` | Required; must be a defined `Priority` enum value |
| `ChangeType` | Required; must be a defined `ChangeType` enum value |

**Error message format**: Each `ValidationError.Message` names the field and the specific
rule violated. Examples:
- `"Title is required."`
- `"Title must not exceed 200 characters."`
- `"Priority 'Urgent' is not a valid value. Accepted values: Low, Medium, High, Critical."`

---

### `GetById`

```
ChangeRequest? GetById(Guid changeRequestId)
```

**Purpose**: Retrieve a persisted change request by its identifier.

**Pre-conditions**:
- `changeRequestId` is a valid (non-empty) GUID.

**Returns**:
- The full `ChangeRequest` record if found.
- `null` if no record with that `changeRequestId` exists.

**Side effects**: None. Idempotent.

**Note on malformed IDs**: A `Guid.Empty` or default `Guid` is a valid GUID value; the store
will return `null` for it (same as any unknown ID). Syntactically malformed strings should
not reach this method — callers are responsible for parsing string input to `Guid` before
calling (parse failure is a caller concern, not a service concern).

---

### `GetAuditHistory`

```
IReadOnlyList<ChangeRequestAuditEntry>? GetAuditHistory(Guid changeRequestId)
```

**Purpose**: Retrieve the full chronological audit trail for a change request.

**Pre-conditions**:
- `changeRequestId` is a valid (non-empty) GUID.

**Returns**:
- An ordered (ascending by `OccurredAt`) read-only list of all audit entries for the given
  `changeRequestId` if the change request exists.
- `null` if no change request with that `changeRequestId` exists.

**Ordering guarantee**: Entries are returned in strictly ascending `OccurredAt` order.
Ties (same timestamp) are broken by insertion order.

**Side effects**: None. Idempotent.

---

## Invariants

1. A `ChangeRequest` is only ever persisted by a successful `Submit` call.
2. Exactly one `ChangeRequestAuditEntry` of type `Submitted` exists per persisted
   `ChangeRequest` after intake.
3. `ChangeRequest.Status` is always `Submitted` for records created by this slice.
4. `SubmissionResult.IsSuccess` and the presence of `Errors` are mutually exclusive:
   if `IsSuccess = true` then `Errors` is empty; if `IsSuccess = false` then
   `ChangeRequest` is null and `Errors` is non-empty.

---

## Dependency Injection Contract

`IChangeRequestService` is constructed with:

| Dependency | Interface | Notes |
|------------|-----------|-------|
| Store | `IChangeRequestStore` | Injected; in tests use the real in-memory store |
| Clock | `ISystemClock` | Injected; in tests use `FakeSystemClock` |
| ID generator | `IIdGenerator` | Injected; in tests use `FakeIdGenerator` |

No other external dependencies are permitted in the production implementation of this
interface (Principle II — no LLM calls; Principle VII — simplicity).
