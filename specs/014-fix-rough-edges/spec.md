# Feature Specification: Fix Three Rough Edges From Manual Testing

**Feature Branch**: `fix/rough-edges-from-manual-testing`

**Created**: 2026-10-17

**Status**: Draft

**Input**: User description: "Correct three rough edges found during manual testing, and change
nothing else. Actors: a caller of the HTTP interface, and a human approver using the approval
operation. (1) Make validation messages for unrecognised priority, change type and decision values
clear: name the field, list the accepted values, never show an internal placeholder or number, never
echo the caller's text. (2) Remove the dead end in which an Approved change request with no
requirements draft can never move forward: it may receive a requirements draft (the only case in
which an Approved request may receive one), the workflow treats that draft as the due step so
automatic and manual advancement complete it, and approval itself is unchanged. (3) For the
requirements and design drafting operations, refuse a malformed identifier as invalid, then report
an unknown identifier as not found, and only then report that drafting is not configured. Every
other response, status code, body, header and error code is identical to before. Out of scope: the
approval rules or human gate, Developer or Reviewer agents, any new operation or setting,
authentication, any user interface, changes to how agents generate content. All sample data is
synthetic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Clear Messages For Unrecognised Values (Priority: P1)

A caller submits a request whose priority, change type or decision is not one of the accepted
values. The error names the field and lists the accepted values, so the caller can correct the
request without guessing. The message never contains an internal placeholder or a number standing
in for a value, and never repeats the caller's own text.

**Why this priority**: It affects every caller who makes a typo and is the most visible rough edge.

**Independent Test**: Submit one unrecognised value for each of the three fields and check each error
names that field and lists exactly the accepted values.

**Acceptance Scenarios**:

1. **Given** a request carrying an unrecognised priority, **When** it is submitted, **Then** the
   error names the priority field and lists the accepted priorities, with no placeholder, number or
   echo of the supplied text.
2. **Given** an unrecognised change type, **When** submitted, **Then** the same holds for the change
   type field.
3. **Given** an unrecognised decision on the approval operation, **When** submitted, **Then** the
   same holds for the decision field, and no decision is recorded.
4. **Given** a supplied value that contains hostile or very long text, **When** it is rejected,
   **Then** none of that text appears in the response or logs.

---

### User Story 2 - Approved Request Without A Requirements Draft Can Move Forward (Priority: P1)

A change request was approved before any requirements draft existed. Today it can never receive
one, so nothing further can happen to it. After this change it may receive a requirements draft, and
the workflow treats that draft as the next due step. Automatic advancement (when switched on) and a
manual advance both complete it, after which the request continues as any approved request with a
draft does.

**Why this priority**: It removes a permanent dead end.

**Independent Test**: Approve a request that has no requirements draft, request a draft, confirm it
is accepted, the workflow status shows it as the due step beforehand, and a manual advance (and
automatic advancement) completes it.

**Acceptance Scenarios**:

1. **Given** an Approved request with no requirements draft, **When** a requirements draft is
   requested, **Then** it is accepted and recorded.
2. **Given** that request, **When** its workflow status is read, **Then** the requirements draft is
   shown as the due step.
3. **Given** that request, **When** a manual advance runs or an automatic check occurs, **Then** the
   requirements draft is produced and the next due step follows the normal rules.
4. **Given** an Approved request that already has a requirements draft, **When** another is
   requested, **Then** it is refused exactly as before.
5. **Given** a request awaiting a decision, Rejected or Deferred, **When** a requirements draft is
   requested, **Then** the outcome is unchanged from before.
6. **Given** a request with no requirements draft, **When** approval is submitted, **Then** it is
   accepted exactly as before.

---

### User Story 3 - Unknown Identifiers Report Not Found Consistently (Priority: P2)

For the requirements and design drafting operations, checks run in a fixed order: a malformed
identifier is refused as invalid; then an unknown identifier is reported as not found; only then is
the "drafting is not configured" condition reported. An unknown identifier therefore reports not
found whether or not drafting is configured.

**Why this priority**: It removes an inconsistency but affects fewer callers.

**Independent Test**: With drafting unconfigured, call both operations with a malformed, an unknown
and a known identifier and check the three outcomes.

**Acceptance Scenarios**:

1. **Given** drafting is not configured, **When** an unknown identifier is used, **Then** the
   response is the same not-found response as when drafting is configured.
2. **Given** drafting is not configured, **When** a malformed identifier is used, **Then** it is
   refused as invalid, as before.
3. **Given** drafting is not configured, **When** a known identifier is used, **Then** the
   "not configured" condition is reported, as before.
4. **Given** drafting is configured, **When** any identifier is used, **Then** responses are
   unchanged.

---

### Edge Cases

- A value differing from an accepted one only by letter case or surrounding spaces: handled exactly
  as today; only the message for values already rejected changes.
- A missing (as opposed to unrecognised) value: status code, error code and field are unchanged. The
  system does not distinguish a missing value from an unrecognised one when validating, so its
  message follows the same new wording (field named, accepted values listed, no placeholder).
- An Approved request whose requirements draft is being produced concurrently: the existing rule of
  one step at a time per request applies unchanged.
- A model failure while producing the newly permitted draft: the existing failure responses apply
  unchanged, and no model text, endpoint, deployment name or credential appears.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: For an unrecognised priority, change type or decision, the error MUST name the field
  and list the accepted values.
- **FR-002**: Those errors MUST NOT contain an internal placeholder or number, and MUST NOT echo the
  caller's text.
- **FR-003**: An Approved request with no requirements draft MUST be able to receive one; this is
  the only case in which an Approved request may receive one.
- **FR-004**: An Approved request that already has a requirements draft MUST still be refused one.
- **FR-005**: The workflow status MUST show the requirements draft as the due step for an Approved
  request without one, and manual and automatic advancement MUST complete it.
- **FR-006**: Approval MUST remain unchanged, including not being refused when no draft exists.
- **FR-007**: For the requirements and design drafting operations, checks MUST run in the order:
  malformed identifier (invalid), unknown identifier (not found), drafting not configured.
- **FR-008**: Every response other than those changed by FR-001/002, FR-003/005 and FR-007 MUST be
  identical in status code, body, header and error code.
- **FR-009**: No model text, endpoint, deployment name or credential MUST appear in any response,
  log or error.
- **FR-010**: The spec and the change summary MUST each list the three response changes, and recorded
  response baselines MUST change only for them.
- **FR-011**: Every existing automated test MUST pass with assertions unchanged, except those that
  assert one of the three listed changes.

### Key Entities

- **Change request**: Has a priority, change type and status (including Approved); may or may not
  have a requirements draft.
- **Requirements draft**: Draft content for a change request; its presence decides whether it is the
  due step.
- **Workflow status**: The read-only view of which step, if any, is due.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For each of the three fields, 100% of unrecognised values yield an error naming the
  field and listing all accepted values, with zero placeholders, numbers or echoed text.
- **SC-002**: An Approved request without a requirements draft reaches a recorded draft through one
  manual advance, and also through automatic advancement, in 100% of tries.
- **SC-003**: With drafting unconfigured, 100% of unknown identifiers report not found on both
  drafting operations.
- **SC-004**: A comparison of recorded response baselines before and after shows differences only in
  the three listed changes; zero other differences.
- **SC-005**: All existing automated tests pass, with assertion changes limited to those asserting
  the three listed changes.

## Assumptions

- The accepted values for each field are those the system already accepts; none are added or removed.
- "Not found" uses the status code and error shape the system already uses for unknown identifiers
  when drafting is configured.
- Which existing tests assert the three changes is determined at planning time.
- Out of scope: approval rules and human gate, Developer and Reviewer agents, new operations or
  settings, authentication, user interface, changes to how agents generate content.
