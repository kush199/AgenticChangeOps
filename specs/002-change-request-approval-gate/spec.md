# Feature Specification: Change Request Approval Gate

**Feature Branch**: `002-change-request-approval-gate`

**Created**: 2026-10-11

**Status**: Draft

**Input**: User description: "Change Request approval gate. A human approver records a decision
on a submitted change request: Approve, Reject, or Defer. Each decision requires the
ChangeRequestId, an approver name, and a decision; a reason is mandatory for Reject and Defer
and optional for Approve. Only a request in status Submitted or Deferred can receive a
decision. A request already Approved or Rejected is final and any further decision is rejected
with a clear error. A decision updates the request status (Approved, Rejected or Deferred) and
records an audit entry containing the decision, approver name, reason, and timestamp, so the
full history of decisions is preserved in order. Invalid decisions (unknown ChangeRequestId,
missing approver, missing reason where required, request in a final state) are rejected with
specific errors and change nothing. No action downstream of approval may proceed for a request
that is not Approved: the system exposes a check that answers whether a given request is
cleared to proceed, and it returns false for every status except Approved. Approver identity is
a plain string; no authentication is in scope. This slice has no AI agents and no LLM calls.
All sample data is synthetic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Approve a Submitted Change Request (Priority: P1)

A human approver reviews a submitted change request and records an Approve decision, optionally
with a reason. The request moves to status `Approved` and an audit entry captures who decided,
what they decided, and when.

**Why this priority**: Approval is the gate that unlocks all downstream work. It is the
single most valuable outcome and the happy path of the whole slice.

**Independent Test**: Submit a request (slice 001), record an Approve decision with an approver
name, then confirm the status is `Approved` and one decision audit entry exists with the
approver, decision, and timestamp.

**Acceptance Scenarios**:

1. **Given** a request in status `Submitted`, **When** an approver records `Approve` with an
   approver name and no reason, **Then** the request status becomes `Approved` and an audit
   entry is recorded with the decision, approver name, an empty reason, and a UTC timestamp.

2. **Given** a request in status `Submitted`, **When** an approver records `Approve` with a
   reason, **Then** the status becomes `Approved` and the audit entry contains that reason.

3. **Given** a request in status `Deferred`, **When** an approver records `Approve`, **Then**
   the status becomes `Approved` and the audit history shows the earlier deferral followed by
   the approval.

---

### User Story 2 - Reject or Defer with a Reason (Priority: P2)

A human approver records a Reject or Defer decision, which requires a reason. Reject is a
final outcome; Defer parks the request so it can be decided again later.

**Why this priority**: Together with Approve, these complete the three-way decision. Mandatory
reasons give the audit trail its meaning.

**Independent Test**: Record a Reject on one submitted request and a Defer on another; confirm
statuses are `Rejected` and `Deferred` respectively and each audit entry carries the reason.

**Acceptance Scenarios**:

1. **Given** a request in status `Submitted`, **When** an approver records `Reject` with a
   reason, **Then** the status becomes `Rejected` and an audit entry records the decision,
   approver, reason, and timestamp.

2. **Given** a request in status `Submitted`, **When** an approver records `Defer` with a
   reason, **Then** the status becomes `Deferred` and an audit entry is recorded.

3. **Given** a request in status `Deferred`, **When** an approver records `Defer` again with a
   reason, **Then** the status remains `Deferred` and a second audit entry is appended.

4. **Given** a request in status `Deferred`, **When** an approver records `Reject` with a
   reason, **Then** the status becomes `Rejected`.

---

### User Story 3 - Reject Invalid Decisions Without Side Effects (Priority: P3)

A decision that breaks any rule is refused with a specific error, and neither the request
status nor the audit history changes.

**Why this priority**: The gate is only trustworthy if invalid input can never alter state.

**Independent Test**: Attempt each kind of invalid decision and confirm a specific error is
returned and the request and its audit history are unchanged.

**Acceptance Scenarios**:

1. **Given** a `ChangeRequestId` that does not exist, **When** a decision is recorded,
   **Then** a not-found error identifying the unknown ID is returned and nothing is stored.

2. **Given** a request in status `Submitted`, **When** a decision is recorded with a missing
   or whitespace-only approver name, **Then** an error naming the approver field is returned
   and status and audit history are unchanged.

3. **Given** a request in status `Submitted`, **When** `Reject` or `Defer` is recorded with a
   missing or whitespace-only reason, **Then** an error stating that a reason is required for
   that decision is returned and nothing changes.

4. **Given** a request in status `Approved` or `Rejected`, **When** any decision is recorded,
   **Then** an error stating the request is in a final state is returned (naming the current
   status) and nothing changes.

5. **Given** a decision value that is not `Approve`, `Reject`, or `Defer`, **When** it is
   recorded, **Then** an error listing the accepted values is returned and nothing changes.

6. **Given** a decision with several problems at once (e.g., missing approver and missing
   reason), **When** it is recorded, **Then** one error per failing field is returned.

---

### User Story 4 - Check Whether a Request Is Cleared to Proceed (Priority: P4)

Any downstream consumer asks whether a given change request is cleared to proceed. The answer
is true only when the request's status is `Approved`.

**Why this priority**: This check is how the gate is enforced for future slices. It is small
but is the contract that downstream work depends on.

**Independent Test**: Ask the check for requests in each status and confirm only `Approved`
yields true.

**Acceptance Scenarios**:

1. **Given** a request in status `Approved`, **When** the clearance check is made, **Then** it
   returns true.

2. **Given** a request in status `Submitted`, `UnderReview`, `Deferred`, or `Rejected`,
   **When** the clearance check is made, **Then** it returns false.

3. **Given** an unknown `ChangeRequestId`, **When** the clearance check is made, **Then** it
   returns false (an unknown request is never cleared).

---

### User Story 5 - Review Decision History in Order (Priority: P5)

A user lists the audit history of a request and sees every decision, in the order made,
alongside the original submission entry.

**Why this priority**: Preserved history is the accountability value of the gate, but it
builds on the other stories.

**Independent Test**: Defer a request twice, then approve it; list its audit history and
confirm the submission entry followed by three decision entries in the order made.

**Acceptance Scenarios**:

1. **Given** a request that was deferred, deferred again, then approved, **When** its audit
   history is listed, **Then** the entries appear in the order recorded: `Submitted`,
   `Deferred`, `Deferred`, `Approved`, none missing or altered.

2. **Given** a rejected decision attempt, **When** the audit history is listed, **Then** it
   contains no entry for the failed attempt.

---

### Edge Cases

- Approver name containing only whitespace is treated as missing.
- Reason containing only whitespace is treated as missing where required; on Approve it is
  treated as no reason.
- Two decisions recorded in quick succession each produce their own entry; history is
  ordered by timestamp, and decisions with identical timestamps keep the order in which they
  were processed.
- A request in status `UnderReview` is not decidable by this slice (only `Submitted` or
  `Deferred` are); an attempt returns an error naming the current status as not eligible.
- Re-submitting an identical decision after a final state is still refused; no idempotent
  success is returned.
- Approver names and reasons are stored trimmed and otherwise exactly as provided.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a human approver to record a decision of `Approve`, `Reject`,
  or `Defer` against a change request identified by `ChangeRequestId`.
- **FR-002**: Each decision MUST require a `ChangeRequestId`, an approver name, and a decision;
  a missing or whitespace-only approver name MUST be rejected.
- **FR-003**: A reason MUST be required for `Reject` and `Defer`, and MUST be optional for
  `Approve`.
- **FR-004**: System MUST accept decisions only for requests in status `Submitted` or
  `Deferred`.
- **FR-005**: System MUST treat `Approved` and `Rejected` as final: any further decision on
  such a request MUST be rejected with an error that states the request is in a final state
  and names the current status.
- **FR-006**: System MUST reject a decision on a request in any other status (e.g.,
  `UnderReview`) with an error naming the current status as not eligible.
- **FR-007**: A successful decision MUST update the request status to `Approved`, `Rejected`,
  or `Deferred` to match the decision.
- **FR-008**: A successful decision MUST record exactly one audit entry containing the
  decision, approver name, reason (if any), the status before and after, and a UTC timestamp,
  linked to the `ChangeRequestId`.
- **FR-009**: Audit entries MUST be preserved in the order recorded; listing the history for a
  request MUST return the submission entry and all decision entries in that order, with none
  removed or altered.
- **FR-010**: System MUST reject invalid decisions (unknown `ChangeRequestId`, missing
  approver, missing required reason, request in a non-decidable status, unrecognised decision
  value) with specific, field- or condition-named errors, and MUST change neither request
  status nor audit history.
- **FR-011**: When a decision has more than one problem, System MUST report every failing
  field, not only the first.
- **FR-012**: System MUST expose a clearance check that, given a `ChangeRequestId`, returns
  true only when the request's status is `Approved`, and false for every other status and for
  an unknown `ChangeRequestId` (never a not-found error or exception, so callers cannot
  mistake "unknown" for "cleared").
- **FR-013**: Approver identity MUST be accepted as a plain string; no authentication or
  identity verification is performed in this slice.
- **FR-014**: System MUST NOT invoke any AI agent or LLM service in this slice, and no
  decision or status transition may be derived from model output.
- **FR-015**: Approver name MUST NOT exceed 100 characters and reason MUST NOT exceed 1,000
  characters (after trimming); values exceeding them MUST be rejected with an error naming
  the field and the limit.

### Key Entities

- **ApprovalDecision**: The input of a decision attempt. Attributes: `ChangeRequestId`,
  `ApproverName` (plain string), `Decision` (`Approve`, `Reject`, `Defer`), `Reason`
  (optional text, mandatory for `Reject` and `Defer`).

- **ChangeRequest** (existing, slice 001): Its `Status` is now updated by this slice to
  `Approved`, `Rejected`, or `Deferred`. No other attributes change.

- **ChangeRequestAuditEntry** (existing, slice 001): Extended with decision events. A decision
  entry carries the event type (`Approved`, `Rejected`, or `Deferred`), approver name, reason,
  status before, status after, and UTC timestamp, in addition to the `ChangeRequestId`.

- **DecisionResult**: The outcome of a decision attempt: either the updated `ChangeRequest`
  (on success) or a list of specific errors (on failure). Never both.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A valid decision completes and returns the updated request in under 500
  milliseconds under normal operating conditions.
- **SC-002**: 100% of invalid decisions return at least one error that names the specific
  failing field or condition; none return a generic "invalid" message.
- **SC-003**: After any rejected decision attempt, the request status and audit history are
  identical to their state before the attempt (zero partial changes).
- **SC-004**: The clearance check returns true for exactly one of the five request statuses
  (`Approved`) and false for the other four and for unknown IDs.
- **SC-005**: A request decided N times has exactly N decision audit entries plus the
  submission entry, in the order the decisions were made.
- **SC-006**: Once a request is `Approved` or `Rejected`, 100% of subsequent decision attempts
  are refused and the request never leaves that status.
- **SC-007**: 100% of deterministic paths in this slice (validation, eligibility, status
  transition, audit recording, clearance check) are covered by automated tests that run
  without any live external service or LLM call.

## Assumptions

- This slice builds on slice 001 (Change Request intake): requests, their statuses, and the
  audit history already exist, and this slice only adds decision recording and the clearance
  check.
- The interaction surface is the same in-process .NET library as slice 001; no HTTP, MCP, or
  CLI layer is in scope.
- Persistence remains the in-memory store from slice 001; concurrent access and durable
  storage remain out of scope.
- Approver name is limited to 100 characters (matching `Requester`) and reason to 1,000
  characters; values are trimmed before validation.
- The decision's `Reason` is stored as provided (trimmed); an omitted optional reason on
  `Approve` is stored as empty.
- The `UnderReview` status exists in the lifecycle enum but nothing in this slice or slice 001
  moves a request into it; it is therefore not decidable and not cleared.
- The artefact being approved is the change request record. It has no separate version number
  and is immutable after intake except for `Status`; the artefact version at time of approval
  is therefore identified by the `ChangeRequestId` plus `SubmittedAt` (unchanged since intake)
  and the recorded `StatusBefore`/`StatusAfter`. This interpretation of Constitution
  Principle III is recorded in plan.md.
- Any approver may decide any request; role-based permissions and "approver must differ from
  requester" rules are out of scope with authentication.
- A deferred request has no expiry or reminder; it simply remains decidable.
- The system clock is monotonic (never steps backward) within a process, so chronological
  order equals the order decisions were recorded; slice 001's audit ordering (ascending
  timestamp, ties by insertion order) is reused unchanged. Clock-skew handling is out of scope.
- All sample change requests, approver names, reasons, and fixtures MUST be fully synthetic —
  no real people, organisations, or systems.

## Clarifications

### Session 2026-10-11

- Q: When the clearance check is asked about a `ChangeRequestId` that does not exist, should it return `false` or a not-found error? → A: Return `false`, the same as any non-Approved status.
