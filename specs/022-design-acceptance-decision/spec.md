# Feature Specification: A Person Accepts Or Rejects A Design Draft

**Feature Branch**: `feature/design-acceptance-decision`

**Created**: 2026-11-02

**Status**: Draft

**Input**: User description: "A person accepts or rejects a design draft, and the decision is recorded and
visible. Actor: a human approver reviewing the design draft of an approved change request, acting through
the HTTP interface. Action: record a decision on one specific version of a change request's design draft:
Accept or Reject, with the approver's name and, for Reject, a reason; the decision is then visible when the
design is retrieved. Inputs: ChangeRequestId, design version, decision, approver name, reason (required for
Reject, optional for Accept). Rules: only the latest design version can be decided (older is refused as
outdated); each version can be decided once and the decision is final; approver name required and reason
required for Reject, with the same trimming and length rules as the existing approval decision; an
unrecognised decision value is reported with the accepted values and without echoing the input; a rejected
design does not stop a new design draft being requested later, which becomes a new version awaiting its own
decision; a decision never changes the change request's status, approves nothing else and triggers no other
step; recording is the human action itself and is never automated; decisions are stored as typed records
tied to the design version and never edited or deleted; overlapping decisions on the same version produce
exactly one decision and one refusal; nothing from the model, sample codebase, endpoint, deployment name or
credential appears in any response, log or error. Outcomes: each design version shows its decision state
(none yet, Accepted, Rejected, with approver, reason and time) when the latest design and the design
history are retrieved, as an additional member absent while no decision exists; a new-type audit entry is
recorded for every decision; the published description lists the new operation; everything else identical
and existing tests pass unchanged except those enumerating operations, audit event types or the published
description; workflow status, proposal operations and automatic advancement unchanged. Failures: unknown or
malformed ChangeRequestId or design version, a version that is not the latest, a version already decided,
missing approver, missing reason for Reject, or invalid decision value is refused with a specific error and
stores nothing. Out of scope: requiring an accepted design before a proposal can be requested, any change
to the workflow or automatic advancement, decisions on proposals or reviews, changing or revoking a
decision, authentication of the approver, any user interface. All sample data is synthetic."

## Clarifications

### Session 2026-11-02

- Q: Can a design draft built on an older requirements draft be decided when no newer design exists? → A: No; it is refused as outdated, the same as an older design version.
- Q: Should a decision be refused when the change request is not Approved? → A: Yes; refused with a specific error and nothing stored.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Accept The Latest Design Draft (Priority: P1)

An approver reviewing the latest design draft of an Approved change request records an Accept decision
with their name (and optionally a reason). The decision is stored against that design version and appears
when the design is retrieved.

**Why this priority**: This is the core value: a person's judgement on a design is captured and visible.

**Independent Test**: Record an Approved change request with a design draft; record an Accept decision on
its latest version; retrieve the latest design and the design history and check the decision, approver,
reason and time are shown; check a new-type audit entry exists and the change request status is unchanged.

**Acceptance Scenarios**:

1. **Given** an Approved change request whose latest design version has no decision, **When** an approver
   records Accept with a name and no reason, **Then** the decision is stored against that version and
   success is reported.
2. **Given** that decision exists, **When** the latest design is retrieved, **Then** it shows the decision
   as Accepted with approver, reason (if any) and time.
3. **Given** that decision exists, **When** the design history is retrieved, **Then** the decided version
   shows the same decision and undecided versions show no decision member.
4. **Given** the decision was recorded, **When** the change request is inspected, **Then** its status,
   workflow status and every other record are unchanged apart from the decision and its audit entry.

---

### User Story 2 - Reject A Design Draft With A Reason (Priority: P1)

An approver records a Reject decision with their name and a reason. The rejection is final for that
version, is visible on retrieval, and does not prevent a new design draft being requested later.

**Why this priority**: Rejection is the other half of the decision, and its reason is what makes it useful.

**Independent Test**: Reject the latest design with a reason; check it shows as Rejected; request a new
design draft and check it is a new version with no decision.

**Acceptance Scenarios**:

1. **Given** an undecided latest design version, **When** an approver records Reject with a name and a
   reason, **Then** the decision is stored and retrieval shows Rejected with approver, reason and time.
2. **Given** a rejected design, **When** a new design draft is requested, **Then** it is created as a new
   version awaiting its own decision, and the earlier version keeps its Rejected decision.
3. **Given** a Reject with no reason (or a whitespace-only reason), **When** it is submitted, **Then** it
   is refused with an error saying a reason is required for Reject, and nothing is stored.

---

### User Story 3 - Decisions Are Final, Current And Exclusive (Priority: P1)

A decision can only be made on the latest design version, only once per version, and never edited or
deleted. When two decisions on the same version overlap, exactly one is recorded and the other refused.

**Why this priority**: Without finality and currency the recorded decisions cannot be trusted.

**Independent Test**: Decide a version; attempt a second decision on it; attempt a decision on an older
version after a newer one exists; submit two simultaneous decisions on one version.

**Acceptance Scenarios**:

1. **Given** a version already Accepted or Rejected, **When** another decision is submitted for it,
   **Then** it is refused as already decided, and the stored decision is unchanged.
2. **Given** a newer design version exists, **When** a decision is submitted for an older version, **Then**
   it is refused as outdated and nothing is stored.
3. **Given** two overlapping decisions on the same undecided version, **When** both are processed,
   **Then** exactly one decision is stored and exactly one request is refused.

---

### User Story 4 - Invalid Requests Are Refused Without Side Effects (Priority: P2)

Requests with an unknown or malformed ChangeRequestId or design version, a missing approver, a missing
reason for Reject, or an unrecognised decision value are refused with a specific error and store nothing.

**Why this priority**: Clear refusals keep the record clean and tell the caller what to fix.

**Independent Test**: Submit each invalid request and check the specific error and that no decision or
audit entry was stored.

**Acceptance Scenarios**:

1. **Given** an unrecognised decision value, **When** it is submitted, **Then** the error lists the
   accepted values and does not echo what the caller sent.
2. **Given** a missing or whitespace-only approver name, **When** it is submitted, **Then** an error
   naming the approver is returned and nothing is stored.
3. **Given** an unknown or malformed ChangeRequestId, or a design version that is malformed or does not
   exist, **When** it is submitted, **Then** a specific error is returned and nothing is stored.
4. **Given** an approver name or reason longer than the existing approval decision allows, **When** it is
   submitted, **Then** it is refused under the same rule, and surrounding whitespace is trimmed as there.

---

### User Story 5 - Every Decision Is Audited And Nothing Else Moves (Priority: P2)

Every decision records a new-type audit entry with the decision, approver, reason, time and design
version. The decision approves nothing else and starts no other step.

**Why this priority**: Auditability and the guarantee that a human decision is not an automated trigger.

**Independent Test**: Record a decision; inspect the audit entries, the workflow status, proposals and
automatic advancement behaviour.

**Acceptance Scenarios**:

1. **Given** a recorded decision, **When** audit entries are read, **Then** an entry of the new type holds
   the decision, approver, reason, time and design version.
2. **Given** a recorded decision, **When** the workflow status is read and automatic advancement runs,
   **Then** the results are identical to those without a decision, and no proposal or review is produced.

---

### Edge Cases

- A decision on a version of a change request that has no design draft is refused as an unknown version.
- A decision on a change request that is not Approved is refused with a specific error and stores nothing.
- Decision values are matched as the existing approval decision matches them; the refusal never echoes
  the supplied value.
- A latest design based on an older requirements draft is refused as outdated, exactly as an older design
  version is; nothing is stored.
- A rejected latest design stays the latest version until a new design draft is requested.
- Retrieval of designs with no decision is identical to before (no decision member).
- A decision refused for any reason leaves the stored decision (if any) and audit entries untouched.
- No model, sample codebase, endpoint, deployment name or credential appears in any response, log or error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A caller MUST be able to record an Accept or Reject decision on one specific design version of
  a change request, supplying the ChangeRequestId, design version, decision, approver name and optional
  reason.
- **FR-002**: Only the latest design version of a change request MUST be decidable, and only while it is
  based on the latest requirements draft; a decision on an older version, or on a design based on an older
  requirements draft, MUST be refused as outdated.
- **FR-003**: Each design version MUST accept at most one decision; Accepted and Rejected MUST be final,
  and a further decision on that version MUST be refused as already decided.
- **FR-004**: The approver name MUST be required, and a reason MUST be required for Reject (optional for
  Accept), using the same trimming and length rules as the existing approval decision.
- **FR-005**: An unrecognised decision value MUST be refused with an error listing the accepted values and
  MUST NOT echo the supplied value.
- **FR-006**: A rejected design MUST NOT prevent a new design draft being requested; the new draft MUST be
  a new version awaiting its own decision.
- **FR-007**: Recording a decision MUST NOT change the change request's status, approve anything else, or
  trigger any other step, and MUST NOT be performed automatically in any way.
- **FR-008**: Decisions MUST be stored as typed records tied to the design version they refer to, and MUST
  never be edited or deleted.
- **FR-009**: When overlapping decisions target the same version, exactly one MUST be stored and exactly
  one MUST be refused.
- **FR-010**: Retrieving the latest design and the design history MUST show each version's decision state
  (Accepted or Rejected, with approver, reason and time) as an additional member that is absent while no
  decision exists.
- **FR-011**: Every recorded decision MUST produce an audit entry of a new type holding the decision,
  approver, reason, time and design version.
- **FR-012**: An unknown or malformed ChangeRequestId or design version, a non-latest version, an already
  decided version, a missing approver, a missing reason for Reject, an invalid decision value, or a change
  request that is not Approved MUST each be refused with a specific error and store nothing.
- **FR-013**: The published description of the interface MUST list the new operation.
- **FR-014**: Apart from the new operation, the additional decision member and the new audit entry type,
  every existing response, status code, body, header and error code MUST be identical, and every existing
  automated test MUST pass with assertions unchanged, except those enumerating operations, audit event
  types or the published description.
- **FR-015**: The workflow status, the proposal operations and automatic advancement MUST be unchanged.
- **FR-016**: Nothing from the model, sample codebase, endpoint, deployment name or credential MUST appear
  in any response, log or error.

### Key Entities

- **Design decision**: a record of Accept or Reject tied to one design version, with approver, optional
  reason (required for Reject) and time; immutable once stored.
- **Design draft version**: a numbered version of a change request's design draft; shows its decision when
  one exists.
- **Decision audit entry**: an audit record of a new type for each design decision, holding decision,
  approver, reason, time and design version.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of tested cases a valid decision on the latest undecided version is stored and shown
  on retrieval of both the latest design and the design history.
- **SC-002**: In 100% of tested invalid or refused requests nothing is stored and the error is specific to
  the cause.
- **SC-003**: In every overlapping-decision test exactly one decision is stored and exactly one request is
  refused.
- **SC-004**: After any decision, the change request status, workflow status and automatic advancement
  results are identical to those without it.
- **SC-005**: Every stored decision has exactly one matching audit entry; refused requests have none.
- **SC-006**: All pre-existing automated tests pass with assertions unchanged except those enumerating
  operations, audit event types or the published description.
- **SC-007**: The published interface description lists the new operation.

## Assumptions

- "Approved change request" means a change request with status Approved, as already recorded; a decision on
  any other status is refused (confirmed in clarification).
- "Same trimming and length rules" refers to those applied to approver name and reason in the existing
  change request approval decision.
- The decision values are Accept and Reject, matched as the existing approval decision matches its values.
- Design versions are the existing numbered versions of design drafts; "latest" is the highest number.
- Durable storage and the audit log already exist and are extended, not replaced.
- Outside the listed out-of-scope items, no behaviour of existing HTTP operations changes.
