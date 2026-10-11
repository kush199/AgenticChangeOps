# Contract: IApprovalService

**Feature**: 002-change-request-approval-gate | **Date**: 2026-10-11
**Type**: In-process .NET library interface

## Interface: `IApprovalService`

### `RecordDecision`

```
DecisionResult RecordDecision(ApprovalDecisionRequest request)
```

**Purpose**: Record a human approver's decision on a change request.

**Pre-conditions**: `request` is not null.

**Post-conditions (success path)**:
- The request's `Status` is `Approved`, `Rejected`, or `Deferred` per the decision.
- Exactly one `ChangeRequestAuditEntry` is appended with: `EventType` matching the outcome,
  `ApproverName`, `Reason`, `StatusBefore`, `StatusAfter`, and `OccurredAt` (UTC).
- Returns `DecisionResult` with `IsSuccess = true` and the updated `ChangeRequest`.

**Post-conditions (failure path)**:
- Returns `DecisionResult` with `IsSuccess = false` and one `ValidationError` per failing
  field or condition.
- Request status and audit history are unchanged.

**Errors** (`ValidationError.FieldName` → condition):

| FieldName | Condition | Example message |
|-----------|-----------|-----------------|
| `ChangeRequestId` | No such request | `"Change request '<id>' was not found."` |
| `ApproverName` | Missing / blank | `"ApproverName is required."` |
| `ApproverName` | Over 100 chars | `"ApproverName must not exceed 100 characters."` |
| `Decision` | Not a defined value | `"Decision '<v>' is not valid. Accepted values: Approve, Reject, Defer."` |
| `Reason` | Missing for Reject/Defer | `"Reason is required when the decision is Reject."` |
| `Reason` | Over 1,000 chars | `"Reason must not exceed 1000 characters."` |
| `Status` | `Approved`/`Rejected` | `"Change request is already Approved and in a final state; no further decisions are allowed."` |
| `Status` | Any other non-decidable status | `"Change request in status 'UnderReview' is not eligible for a decision."` |

All applicable errors are returned together. `Status` is only reported when the request
exists.

---

### `IsClearedToProceed`

```
bool IsClearedToProceed(Guid changeRequestId)
```

**Purpose**: Answer whether downstream work may proceed for a request.

**Returns**: `true` only if the request exists and its status is `Approved`; `false` for
`Submitted`, `UnderReview`, `Rejected`, `Deferred`, and for an unknown `changeRequestId`.

**Side effects**: None. Never throws for an unknown ID.

---

## Invariants

1. A request's status changes only through a successful `RecordDecision` (after intake).
2. `Approved` and `Rejected` are terminal: no successful decision can follow them.
3. Each successful decision produces exactly one audit entry; a failed decision produces none.
4. `DecisionResult.IsSuccess = true` ⇔ `Errors` empty and `ChangeRequest` non-null.
5. `IsClearedToProceed` is `true` for exactly one status (`Approved`).

## Dependency Injection Contract

`ApprovalService` is constructed with:

| Dependency | Interface | Notes |
|------------|-----------|-------|
| Store | `IChangeRequestStore` | Same instance as `ChangeRequestService` |
| Clock | `ISystemClock` | `FakeSystemClock` in tests |
| ID generator | `IIdGenerator` | `FakeIdGenerator` in tests (audit entry IDs) |
| Validator | `ApprovalDecisionValidator` | Stateless |

No LLM client or other external dependency is permitted (Principle II, FR-014).
