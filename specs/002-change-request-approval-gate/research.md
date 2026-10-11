# Research: Change Request Approval Gate

**Feature**: 002-change-request-approval-gate | **Date**: 2026-10-11

No `NEEDS CLARIFICATION` items remained after the spec clarification session. The decisions
below record the design choices made against the existing slice 001 code.

## D1: Where the gate lives

- **Decision**: New `IApprovalService` / `ApprovalService` in the existing
  `AgenticChangeOps.Intake` project.
- **Rationale**: Shares store, clock, ID generator, and records. Leaves `IChangeRequestService`
  untouched, so slice 001's contract and tests are not at risk.
- **Alternatives**: (a) add methods to `IChangeRequestService` — mixes intake and approval and
  widens a stable contract; (b) new assembly — reference plumbing with no isolation benefit.

## D2: Representing decision audit data

- **Decision**: Add nullable `ApproverName`, `Reason`, `StatusBefore`, `StatusAfter` to
  `ChangeRequestAuditEntry` as trailing optional record parameters; add `Approved`, `Rejected`,
  `Deferred` to `AuditEventType`.
- **Rationale**: One typed record, one ordered history (FR-009). Defaults of `null` keep the
  existing `new ChangeRequestAuditEntry(...)` call in `ChangeRequestService.Submit` compiling
  and keep submission entries semantically unchanged.
- **Alternatives**: separate `ApprovalAuditEntry` type (two lists to merge); encode in
  `Details` (untyped blob, violates Principle IV).

## D3: Status mutation

- **Decision**: `ChangeRequest` stays an immutable record; the service creates
  `request with { Status = ... }` and calls a new `IChangeRequestStore.Update`. `Update` throws
  `InvalidOperationException` if the ID is unknown.
- **Rationale**: Minimal store surface; keeps records immutable; unknown-ID is already handled
  by the service before reaching the store.
- **Alternatives**: mutable `Status` setter (loses immutability guarantee from slice 001's
  data model); delete-and-resave (breaks audit/ID invariants).

## D4: Transition rules

- **Decision**: Static table in the validator/service:

  | Current status | Decidable? | Approve → | Reject → | Defer → |
  |---|---|---|---|---|
  | `Submitted` | yes | `Approved` | `Rejected` | `Deferred` |
  | `Deferred` | yes | `Approved` | `Rejected` | `Deferred` |
  | `Approved`, `Rejected` | no — final | — | — | — |
  | `UnderReview` | no — not eligible | — | — | — |

- **Rationale**: Straight from FR-004 to FR-006; deterministic per Principle II.
- **Alternatives**: none worth recording; a generic state-machine library would violate
  Principle VII.

## D5: Validation shape

- **Decision**: Reuse `ValidationError(FieldName, Message)`. Collect-all for input fields
  (`ApproverName`, `Decision`, `Reason`), plus `ChangeRequestId` (not found) and `Status`
  (final / not eligible). Status eligibility is checked only when the request exists.
- **Rationale**: Matches slice 001 conventions and FR-010/FR-011. Reuse satisfies
  Principle VII (second caller of an existing type).
- **Alternatives**: error-code enum — useful later for UIs, but speculative now.

## D6: Clearance check semantics

- **Decision**: `bool IsClearedToProceed(Guid)` returns `true` iff the request exists and its
  status is `Approved`. Unknown ID → `false` (clarified 2026-10-11).
- **Rationale**: Fail-closed; cannot throw or be mistaken for "go".

## D7: Ordering of audit entries

- **Decision**: Keep the existing store ordering (`OrderBy(OccurredAt)`, a stable sort, so
  ties keep insertion order). No change to slice 001 behavior, contract, or tests.
- **Rationale**: Slice 001's approved spec (FR-016, US4 AS3, SC-005) and its test
  `AuditTests.GetAuditHistory_MultipleEntriesInjectedDirectly_ReturnedInAscendingOrder`
  require ascending chronological order. Decisions are appended in processing order and a
  production clock is monotonic, so chronological order equals recording order; same-timestamp
  decisions keep processing order via the stable sort. A backwards-stepping clock is out of
  scope (see spec Assumptions).
- **Alternatives**: return raw insertion order — equals chronological order under a monotonic
  clock but would break the slice 001 test above and contradict its spec, for no
  production benefit.

## D8: Length limits

- **Decision**: `ApproverName` ≤ 100, `Reason` ≤ 1,000, both validated after trimming.
- **Rationale**: Matches `Requester`'s limit; reason sized for a short paragraph.
