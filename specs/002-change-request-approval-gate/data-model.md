# Data Model: Change Request Approval Gate

**Feature**: 002-change-request-approval-gate | **Date**: 2026-10-11

Builds on [slice 001's data model](../001-change-request-intake/data-model.md). Only new or
changed types are listed.

## Enumerations

### `ApprovalDecision` (new)

| Value | Resulting status |
|-------|------------------|
| `Approve` | `Approved` |
| `Reject` | `Rejected` |
| `Defer` | `Deferred` |

### `AuditEventType` (extended)

| Value | Meaning |
|-------|---------|
| `Submitted` | Existing (slice 001) |
| `Approved` | Approver approved the request |
| `Rejected` | Approver rejected the request |
| `Deferred` | Approver deferred the request |

## Record Types

### `ApprovalDecisionRequest` (new)

| Field | Type | Required | Max Length | Notes |
|-------|------|----------|------------|-------|
| `ChangeRequestId` | `Guid` | Yes | — | Must identify an existing request |
| `ApproverName` | `string` | Yes | 100 | Plain string; whitespace-only = missing; trimmed |
| `Decision` | `ApprovalDecision` | Yes | — | Must be a defined enum value |
| `Reason` | `string?` | `Reject`/`Defer` only | 1,000 | Optional for `Approve`; whitespace-only = missing; trimmed |

**Validation rules** (collect-all):

1. `ChangeRequestId` — request must exist.
2. `ApproverName` — not null/blank; length ≤ 100.
3. `Decision` — must be a defined `ApprovalDecision` value.
4. `Reason` — required (not null/blank) when `Decision` is `Reject` or `Defer`; length ≤ 1,000
   whenever provided.
5. Status — only evaluated when the request exists: must be `Submitted` or `Deferred`.
   `Approved`/`Rejected` → "final state" error naming the status; any other status → "not
   eligible" error naming the status.

### `ChangeRequestAuditEntry` (extended — additive)

| Field | Type | Notes |
|-------|------|-------|
| `AuditEntryId` | `Guid` | Existing |
| `ChangeRequestId` | `Guid` | Existing |
| `EventType` | `AuditEventType` | Existing; `Approved`/`Rejected`/`Deferred` for decisions |
| `OccurredAt` | `DateTime` (UTC) | Existing; decision timestamp |
| `Details` | `string?` | Existing; `null` for decisions |
| `ApproverName` | `string?` | **New**; `null` for `Submitted` entries |
| `Reason` | `string?` | **New**; empty string for an `Approve` without a reason; `null` only on `Submitted` entries |
| `StatusBefore` | `ChangeRequestStatus?` | **New**; `null` for `Submitted` entries |
| `StatusAfter` | `ChangeRequestStatus?` | **New**; `null` for `Submitted` entries |

New fields are trailing record parameters defaulting to `null`, so slice 001 call sites and
entries remain valid.

### `ChangeRequest` (unchanged shape)

Only `Status` changes value, via `with`-copy and `IChangeRequestStore.Update`. All other
fields stay as submitted.

### `DecisionResult` (new)

| State | `IsSuccess` | `ChangeRequest` | `Errors` |
|-------|-------------|-----------------|----------|
| Success | `true` | Updated `ChangeRequest` | Empty |
| Failure | `false` | `null` | ≥1 `ValidationError` |

Factories: `DecisionResult.Success(ChangeRequest)`, `DecisionResult.Failure(IReadOnlyList<ValidationError>)`.

## Store Interface Change

| Method | Signature | Notes |
|--------|-----------|-------|
| `Update` (new) | `void Update(ChangeRequest changeRequest)` | Replaces the stored record with the same `ChangeRequestId`; throws `InvalidOperationException` if absent |

## State Transitions

```
Submitted ──Approve──▶ Approved   (final)
    │ ──Reject───▶ Rejected  (final)
    └──Defer────▶ Deferred ──Approve──▶ Approved
                      │ ──Reject───▶ Rejected
                      └──Defer────▶ Deferred (new audit entry)

UnderReview: not decidable in this slice
```

A successful decision performs, in order: validate → build the updated record and audit entry
(including the new audit entry ID) → `Update` status → append audit entry → return
`DecisionResult.Success`. A failed validation performs none of these.
