# Feature Specification: Workflow Orchestration for Change Requests

**Feature Branch**: `010-workflow-orchestration`

**Created**: 2026-10-14

**Status**: Draft

**Input**: User description: "Workflow orchestration for change requests. A caller (a person or a
client application) wants to know where a change request is in its lifecycle and to move it
forward; human approvers remain the only ones who make decisions. Actions: (1) retrieve the
workflow status of a change request; (2) advance a change request, which runs the next allowed
agent step (the Requirement agent's requirements draft, or the Architect agent's design draft).
Input: the ChangeRequestId only. The workflow stage is derived entirely from facts already recorded
(status, requirements drafts, design drafts and their versions); it is never stored separately and
cannot disagree with them. The order is fixed by the system's own rules, never by a language model:
a requirements draft first; a human decision follows; a design draft only for an Approved request
that has a requirements draft. Advancing runs at most one agent step and never records, changes or
implies a human decision. A Rejected, Deferred or awaiting-decision request is not advanced past
that point. Where a step is already complete for the current inputs, advancing reports that nothing
is due rather than repeating it. Status always names the next step, if any, and the specific reason
when none can run. Status returns the stage, completed steps with versions and times, the next step
and the reason when none. Advancing returns the step's result in the same form as that agent's own
operation, plus the workflow status afterwards, and every step run is audited exactly as if the
agent's own operation had been called. Unknown or malformed IDs are refused with a specific error;
advancing with no step due is refused with the reason and changes nothing; an agent step failure
stores nothing and reports only its category; no model text, endpoint, deployment name or
credential appears in any response, log or error. Out of scope: Developer and Reviewer agents,
automatic or background running, durable storage, authentication, notifications, any user
interface, parallel steps, changes to how agents work internally. All sample data is synthetic."

## Clarifications

### Session 2026-10-14

- Q: Should advancing produce a requirements draft for a `Deferred` change request that has none
  yet? → A: Yes; the requirements draft is offered as the next step, and nothing beyond it is
  offered while the request is `Deferred`.
- Q: When an `Approved` request has a design draft based on an older requirements version, should
  advancing produce a new design draft? → A: Yes; a design draft is current only if it was based
  on the latest requirements draft, otherwise a new design version is the next step.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See Where a Change Request Is (Priority: P1)

A caller asks for the workflow status of a change request. The system answers with the current
stage, the steps already completed (each with its version and time), the next step if one is due,
and, when no step can run, the specific reason. The answer is worked out fresh from what is
already recorded about the change request; nothing about workflow is stored separately.

**Why this priority**: Knowing the lifecycle position is the foundation; advancing is only safe
and understandable on top of a trustworthy status.

**Independent Test**: Using synthetic change requests in each situation (no draft; draft and
awaiting decision; Approved with no requirements draft; Approved with requirements draft only;
Approved with a current design draft; Approved with a design draft older than the latest
requirements draft; Rejected; Deferred), request the status and confirm the stage, completed
steps, next step and reason in each case.

**Acceptance Scenarios**:

1. **Given** a change request with no drafts, **When** status is requested, **Then** the stage
   shows no steps completed and the next step is the requirements draft.
2. **Given** a change request with a requirements draft that has not been decided, **When**
   status is requested, **Then** the requirements draft is listed as completed with its version
   and time, no next step is given, and the reason states that a human decision is awaited.
3. **Given** an `Approved` change request with a requirements draft and no design draft, **When**
   status is requested, **Then** the next step is the design draft.
4. **Given** an `Approved` change request whose latest design draft was based on the latest
   requirements draft, **When** status is requested, **Then** both steps are listed as completed
   with versions and times and the reason states that nothing further is due.
5. **Given** a `Rejected` or `Deferred` change request, **When** status is requested, **Then** no
   step beyond the current point is offered and the reason names the status.
6. **Given** any change request, **When** its recorded facts change (a new draft, a decision),
   **Then** the next status request reflects the change with no separate update needed.

---

### User Story 2 - Advance a Change Request One Step (Priority: P2)

A caller asks the system to advance a change request. The system runs the single next allowed
agent step and returns that step's result in the same form as the agent's own operation, together
with the workflow status afterwards. The step is audited exactly as if the agent's own operation
had been called directly.

**Why this priority**: This is the movement the caller wants; it depends on status being right.

**Independent Test**: With a stand-in content generator, advance a fresh synthetic change request
and confirm a requirements draft is stored with its usual audit entry and the returned result and
status match; approve it by the existing approval operation, advance again, and confirm a design
draft is stored with its usual audit entry.

**Acceptance Scenarios**:

1. **Given** a change request whose next step is the requirements draft, **When** it is advanced,
   **Then** exactly one requirements draft is stored, its usual audit entry is recorded, and the
   response holds that draft in the same form as the Requirement agent's own operation plus the
   workflow status afterwards.
2. **Given** an `Approved` change request with a requirements draft and no current design draft,
   **When** it is advanced, **Then** exactly one design draft is stored, its usual audit entry is
   recorded, and the response holds that draft in the same form as the Architect agent's own
   operation plus the workflow status afterwards.
3. **Given** any successful advance, **When** the change request is read afterwards, **Then** its
   status is unchanged, and no approval, rejection, deferral or clearance was recorded or implied.
4. **Given** any successful advance, **When** the audit history is listed, **Then** it contains
   exactly the entries the agent's own operation would have written, and none other.
5. **Given** a change request, **When** it is advanced once, **Then** at most one agent step has
   run; the caller must advance again to run any further step.

---

### User Story 3 - Refuse Advancing When Nothing Is Due (Priority: P3)

Advancing a change request that has no step due, because the current inputs are already
completed or because a human decision is required, is refused with the specific reason. Nothing
is stored, nothing is audited, and no agent is invoked.

**Why this priority**: Prevents repeated model use and protects the human gate.

**Independent Test**: Advance synthetic requests that are awaiting a decision, Rejected, Deferred
with a requirements draft, and fully complete; confirm a specific reason each time, no new drafts
or audit entries, and zero generator invocations.

**Acceptance Scenarios**:

1. **Given** a change request awaiting a human decision, **When** it is advanced, **Then** it is
   refused with a reason stating the decision is awaited and nothing changes.
2. **Given** a `Rejected` or `Deferred` change request with nothing further allowed, **When** it
   is advanced, **Then** it is refused with a reason naming the status and nothing changes.
3. **Given** an `Approved` change request whose design draft is current with its inputs, **When**
   it is advanced, **Then** it is refused with a reason stating nothing is due and no new version
   is created.
4. **Given** any refused advance, **When** the generator's call count and audit history are
   inspected, **Then** no generator was invoked and no entry was written.

---

### User Story 4 - Refuse Bad Identifiers and Fail Safely (Priority: P4)

A status or advance request with a malformed or unknown `ChangeRequestId` is refused with a
specific error. If the agent step run by an advance fails (model failure, timeout, incomplete or
malformed output, not configured, or inputs changing mid-step), nothing is stored and the failure
is reported with only its category, exactly as the agent's own operation would.

**Why this priority**: Orchestration must never weaken the safety properties the agents already
have, nor leak internals.

**Independent Test**: Request status and advance for malformed and unknown IDs; advance with
stand-in generators that fail, time out, omit sections, and return text containing a fake
endpoint and credential; confirm specific errors, nothing stored, no audit entry, and no leaked
content anywhere.

**Acceptance Scenarios**:

1. **Given** a malformed or unknown `ChangeRequestId`, **When** status or advance is requested,
   **Then** a specific error is returned and nothing is stored.
2. **Given** a due step and a generator that fails, times out or returns bad output, **When** the
   change request is advanced, **Then** nothing is stored, no audit entry is written, and only a
   failure category is returned, the same as the agent's own operation.
3. **Given** any failure or refusal, **When** the response, errors and logs are inspected, **Then**
   none contain model text, an endpoint, a deployment name or a credential.
4. **Given** a failed advance, **When** status is requested, **Then** it is identical to the
   status before the attempt.

---

### Edge Cases

- A step becomes due or stops being due between a status read and an advance: advance always
  re-derives the stage at the moment it runs and acts on that, never on an earlier status.
- Two advances for the same change request in quick succession: at most one step per advance, no
  duplicated or skipped versions, and the second reports nothing due if the first completed it.
- A requirements draft is added after a design draft exists: the design draft is no longer
  current for its inputs, so a new design draft becomes the next step; the earlier one is kept.
- A change request is `Deferred` with no requirements draft: the requirements draft remains an
  allowed step under the Requirement agent's own rules; once drafted, the request is not advanced
  further while `Deferred`.
- A change request is `UnderReview` with no requirements draft: the Requirement agent will not
  draft in that status, so no step is due and the reason names the status.
- An `Approved` change request with no requirements draft: the Requirement agent will not draft
  in that status and a design draft requires one, so no step is due and the reason states that a
  requirements draft is required.
- Agent output claiming approval or instructing the next step: stored only as draft content; it
  never alters status, stage or routing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a caller to retrieve the workflow status of a change request
  by `ChangeRequestId` alone.
- **FR-002**: The system MUST allow a caller to advance a change request by `ChangeRequestId`
  alone, running the next allowed agent step.
- **FR-003**: The workflow stage MUST be derived entirely, at request time, from the change
  request's recorded status, its requirements drafts, and its design drafts with their versions;
  it MUST NOT be stored separately and MUST NOT be able to disagree with those facts.
- **FR-004**: Step order MUST be determined by the system's own deterministic rules and never by
  a language model: a requirements draft first; a human decision follows; a design draft only for
  an `Approved` change request that has a requirements draft.
- **FR-005**: Status MUST return the current stage, the completed steps each with its version and
  time, the next step if one is due, and the specific reason when none is.
- **FR-006**: Status MUST always name the next step when one can run, and a specific reason
  (awaiting decision, rejected, deferred, requirements draft required, or nothing further due)
  when none can.
- **FR-007**: Advancing MUST run at most one agent step per request, and MUST re-derive the stage
  at the moment it runs.
- **FR-008**: Advancing MUST NOT record, change or imply a human decision, and MUST NOT change the
  change request's status.
- **FR-009**: A change request that is `Rejected`, `Deferred`, or awaiting a decision MUST NOT be
  advanced past that point. A `Deferred` request with no requirements draft is the one exception
  to refusing outright: its requirements draft is offered as the next step, and no later step is
  offered while it remains `Deferred`.
- **FR-010**: Where a step is already complete for the current inputs, advancing MUST refuse with
  the reason that nothing is due and MUST NOT repeat the step; a design draft is current only if
  it was based on the latest requirements draft.
- **FR-011**: A successful advance MUST return the step's result in the same form as that agent's
  own operation, together with the workflow status afterwards.
- **FR-012**: Every agent step run by advancing MUST be recorded in the audit trail exactly as if
  the agent's own operation had been called, and no additional entries MUST be written by
  orchestration itself.
- **FR-013**: Status and advance MUST refuse a malformed or unknown `ChangeRequestId` with a
  distinct specific error.
- **FR-014**: An advance with no step due MUST be refused with the specific reason, store
  nothing, write no audit entry, and invoke no agent.
- **FR-015**: If the agent step fails, the system MUST store nothing and report the failure with
  only its category, as for the agent's own operation.
- **FR-016**: No model text, endpoint, deployment name or credential MUST appear in any response,
  log or error.
- **FR-017**: The feature MUST NOT change how the Requirement and Architect agents work
  internally, and MUST NOT include the Developer or Reviewer agents, automatic or background
  running, durable storage, authentication, notifications, any user interface, or parallel steps.
- **FR-018**: All behaviour MUST be verifiable without a live model; any live check MUST be
  opt-in and never run by default, and all sample data MUST be synthetic.

### Key Entities

- **Workflow Status**: A read-only view of one change request's lifecycle position. Attributes:
  change request identifier, current stage, completed steps, next step (if any), reason (when no
  step is due). Derived at request time; never persisted.
- **Completed Step**: One finished agent step: which step it was, the draft version, and the time
  it was generated.
- **Workflow Stage**: Where the change request is in the fixed order (for example, awaiting a
  requirements draft, awaiting a decision, ready for design, design complete, closed by decision).
- **Advance Result**: The result of the one step run, in the producing agent's own form, plus the
  Workflow Status afterwards.
- **Change Request, Requirements Draft, Design Draft, Audit Entry**: Existing entities; only read
  by status, and written only by the agents' own operations when invoked by an advance.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every tested combination of change request status and draft state, status
  names either one next step or a specific reason, and never both or neither.
- **SC-002**: Starting from a new synthetic change request, a caller can reach a stored
  requirements draft and (after an approval recorded by a human) a stored design draft using only
  status and advance, with exactly one audit entry per step run and none added by orchestration.
- **SC-003**: In 100% of tested refusal situations (malformed ID, unknown ID, awaiting decision,
  Rejected, Deferred, nothing due), zero drafts, zero audit entries and zero agent invocations
  result, with a distinct specific reason.
- **SC-004**: In 100% of tested step-failure situations, zero drafts and zero audit entries are
  written and the error carries only a failure category.
- **SC-005**: Across all tested outcomes, no response, error or log contains model text, an
  endpoint, a deployment name or a credential.
- **SC-006**: Across all tested advances, the change request status is identical before and
  after, and no decision of any kind is recorded.
- **SC-007**: For the same change request state, an advance returns a draft identical in form to
  the one the agent's own operation returns, and the full automated suite passes with no live
  model or network and every existing test still passing.

## Assumptions

- "Awaiting a decision" means a change request in status `Submitted` or `UnderReview`.
- The Requirement agent keeps its own eligibility rules (it drafts only for `Submitted` or
  `Deferred` requests); orchestration offers its step only when that agent would accept the
  request, and otherwise names the status as the reason.
- A requirements draft counts as completed once any version exists; further requirements versions
  are produced through the agent's own operation, not by advancing, since a human decision should
  follow the draft that exists.
- A design draft is "current" when it was based on the latest requirements draft version; if a
  newer requirements draft exists, a new design draft is the next step.
- Storage stays in-memory and non-durable, consistent with earlier slices.
- Error conventions, identifier validation and failure categories are taken from the existing
  agent operations; orchestration adds only its own distinct reasons for no step due.
- Status and advance are exposed the way existing operations are, with consistent conventions;
  the exact surface is a planning decision.
- Developer and Reviewer steps will extend the workflow in later slices; their needs do not shape
  this slice.
