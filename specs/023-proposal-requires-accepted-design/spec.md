# Feature Specification: A Change Proposal Requires An Accepted Design

**Feature Branch**: `feature/proposal-requires-accepted-design`

**Created**: 2026-11-04

**Status**: Draft

**Input**: User description: "A change proposal can only be requested when the latest design draft has been
accepted by a person. Actor: a caller requesting a change proposal for an approved change request. Action:
when a proposal is requested, the system checks that the latest design draft has a recorded decision of
Accepted; if not, the request is refused and nothing is produced. Inputs: the ChangeRequestId and the
recorded design decisions that already exist. Rules: the new check applies after the existing eligibility
checks, in this order: the request exists and is Approved; a requirements draft exists; a design draft
based on the latest requirements draft exists; then the latest design draft must be Accepted. No decision
and Rejected are refused with different specific reasons. A decision on an older design version does not
count. The check happens before the model is reached, so a refused request makes no model call, stores
nothing and records no audit entry. If, while a proposal is being prepared, the latest design stops being
the accepted one, the proposal is refused as outdated and nothing is stored. Existing proposals are never
altered or removed by a later decision or newer design. The check reads recorded facts only, never asks a
model, never records or changes a decision or any status, and approves nothing. Outcomes: an Accepted
latest design behaves exactly as today; a refused request returns the same kind of conflict response, in
the same error format, as the other refusals of the proposal operation, naming the design decision as the
reason; the only behaviour change is that undecided or Rejected designs are now refused; every other
response, status code, body, header and error code is identical and existing tests pass with assertions
unchanged, except those that request a proposal without first accepting the design, which gain an
acceptance step in their setup. Failures: undecided, Rejected, or no-longer-accepted at storing time is
refused with a specific error, storing nothing and calling no model; all existing refusals keep their
reasons and order. Out of scope: any change to workflow status, stages, offered step or automatic
advancement; decisions on proposals or reviews; gating of reviews; changing or revoking a design decision;
any new operation; any change to how proposals are produced; authentication; any user interface. All
sample data is synthetic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Proposal Allowed Only After The Design Is Accepted (Priority: P1)

A caller requests a change proposal for an Approved change request whose latest design draft a person has
Accepted. The request proceeds exactly as it does today and a proposal is produced and stored.

**Why this priority**: This is the happy path and must remain unchanged; the gate is only worthwhile if
accepted designs flow through untouched.

**Independent Test**: Approve a change request, produce requirements and design drafts, record an Accept
decision on the latest design, request a proposal, and compare the response and stored result with the
behaviour before this feature.

**Acceptance Scenarios**:

1. **Given** an Approved change request whose latest design version is Accepted, **When** a proposal is
   requested, **Then** the response, status code, body, headers and stored records are identical to today's.
2. **Given** that proposal was produced, **When** the proposal is retrieved, **Then** it is unchanged from
   today's behaviour.

---

### User Story 2 - An Undecided Design Blocks The Proposal (Priority: P1)

A caller requests a proposal when the latest design draft has no recorded decision. The request is refused
with a reason saying the design has not been decided, and nothing happens beyond the refusal.

**Why this priority**: This is the core guarantee: no proposal is built on a design no person has judged.

**Independent Test**: Reach a latest design with no decision, request a proposal, and check the refusal
reason, that no model was called, that nothing was stored and that no audit entry was recorded.

**Acceptance Scenarios**:

1. **Given** an Approved change request whose latest design has no decision, **When** a proposal is
   requested, **Then** a conflict response is returned, in the same error format as the other refusals of
   the proposal operation, with a specific reason saying the design has not been decided.
2. **Given** that refusal, **When** records are inspected, **Then** no proposal, no audit entry and no
   other change exists, and the model was never contacted.

---

### User Story 3 - A Rejected Design Blocks The Proposal (Priority: P1)

A caller requests a proposal when the latest design draft was Rejected. The request is refused with a
different specific reason saying the design was rejected.

**Why this priority**: A rejected design must never feed a proposal, and the caller needs to know why.

**Independent Test**: Reject the latest design, request a proposal, and check the reason differs from the
undecided reason and that nothing was produced, stored or audited.

**Acceptance Scenarios**:

1. **Given** a latest design with a Rejected decision, **When** a proposal is requested, **Then** a
   conflict response is returned with a specific reason saying the design was rejected, distinct from the
   undecided reason.
2. **Given** that refusal, **When** records are inspected, **Then** nothing was stored or audited and no
   model call was made.

---

### User Story 4 - Only The Latest Design's Decision Counts (Priority: P2)

A decision recorded on an older design version is ignored. If a newer design draft exists, the proposal
depends solely on the decision about that newer draft.

**Why this priority**: Without this, an old acceptance could let a proposal through on a design nobody
has judged.

**Independent Test**: Accept a design, request a newer design draft, request a proposal, and check it is
refused as undecided; then accept the newer draft and check the proposal is allowed.

**Acceptance Scenarios**:

1. **Given** version 1 Accepted and a newer version 2 with no decision, **When** a proposal is requested,
   **Then** it is refused as not decided.
2. **Given** version 1 Rejected and version 2 Accepted, **When** a proposal is requested, **Then** it
   proceeds.

---

### User Story 5 - The Design Changing Mid-Preparation Refuses The Proposal (Priority: P2)

If, while a proposal is being prepared, the latest design stops being the accepted one (for example a
newer design draft appears), the proposal is refused as outdated and nothing is stored.

**Why this priority**: Prevents a proposal from being stored against a design that is no longer current
and accepted.

**Independent Test**: Start preparing a proposal for an Accepted design, introduce a newer design before
storing, and check the outcome is an outdated refusal with nothing stored.

**Acceptance Scenarios**:

1. **Given** a proposal being prepared against an Accepted latest design, **When** a newer design draft
   becomes the latest before the proposal is stored, **Then** the proposal is refused as outdated and
   nothing is stored.
2. **Given** proposals already stored, **When** a later decision or newer design arrives, **Then** the
   existing proposals are neither altered nor removed.

---

### User Story 6 - Existing Refusals Keep Their Reasons And Order (Priority: P2)

All existing eligibility checks behave as before and run first, in order: the request exists and is
Approved; a requirements draft exists; a design draft based on the latest requirements draft exists. Only
then is the design decision checked.

**Why this priority**: Callers and tests depend on current refusal reasons; the new check must not mask
or reorder them.

**Independent Test**: For each existing refusal condition, also leave the design undecided where
applicable, and check the existing reason is the one reported.

**Acceptance Scenarios**:

1. **Given** a change request that is unknown or not Approved, **When** a proposal is requested, **Then**
   the existing refusal is returned, unchanged.
2. **Given** no requirements draft, or no design draft based on the latest requirements draft, **When** a
   proposal is requested, **Then** the existing refusal is returned, not the design-decision refusal.

---

### Edge Cases

- Latest design is Accepted but an older version was Rejected: the proposal proceeds.
- Latest design is Rejected and an older version was Accepted: refused as rejected.
- A new design draft is requested after a Rejected one: the new version is undecided, so proposals are
  refused as not decided until it is Accepted.
- Two overlapping events (a proposal request and a new design draft): either the proposal is stored
  against the still-accepted latest design, or it is refused as outdated; never stored against a design
  that is not the accepted latest.
- A change request that already has proposals stored when its design later becomes outdated: existing
  proposals stay as they are.
- Refusals never echo caller input, model output, endpoints, deployment names, sample code or credentials.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When a proposal is requested, the system MUST check that the latest design draft has a
  recorded decision of Accepted before any further work is done.
- **FR-002**: The check MUST run after the existing eligibility checks and in this order: the change
  request exists and is Approved; a requirements draft exists; a design draft based on the latest
  requirements draft exists; the latest design draft is Accepted.
- **FR-003**: A latest design draft with no recorded decision MUST cause the request to be refused with a
  specific reason saying it has not been decided.
- **FR-004**: A latest design draft whose decision is Rejected MUST cause the request to be refused with a
  specific reason, different from FR-003, saying it was rejected.
- **FR-005**: A decision recorded on a design version other than the latest MUST NOT count towards
  eligibility.
- **FR-006**: A refused request MUST make no model call, store nothing and record no audit entry.
- **FR-007**: If, while a proposal is being prepared, the latest design stops being the accepted one, the
  proposal MUST be refused as outdated and nothing MUST be stored.
- **FR-008**: Existing stored proposals MUST NOT be altered or removed by a later decision or a newer
  design.
- **FR-009**: The check MUST rely on recorded facts only and MUST NOT consult a model.
- **FR-010**: The check MUST NOT record or change any design decision, change any status, or approve
  anything.
- **FR-011**: A refusal under this feature MUST use the same kind of conflict response and the same error
  format as the other refusals of the proposal operation, and MUST name the design decision as the reason.
- **FR-012**: A proposal request whose latest design is Accepted MUST behave exactly as it does today.
- **FR-013**: Apart from FR-003 and FR-004 refusals, every existing response, status code, body, header
  and error code MUST be identical to before, and all existing refusals MUST keep their reasons and order.
- **FR-014**: Nothing from the model, sample codebase, endpoint, deployment name or credential MUST appear
  in any response, log or error produced by this feature.
- **FR-015**: Every existing automated test MUST pass with assertions unchanged; only tests that request a
  proposal without first accepting the design MAY change, and only by gaining an acceptance step in their
  setup.

### Key Entities

- **Change Request**: The request under consideration; must be Approved for a proposal to be requested.
- **Design Draft (version)**: A numbered draft; only the latest version is relevant to the gate.
- **Design Decision**: A person's recorded Accepted or Rejected judgement on one design version, already
  existing; read-only here.
- **Change Proposal**: The output of the proposal operation; produced only when the latest design is
  Accepted and never altered by later decisions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of proposal requests whose latest design is undecided or Rejected are refused, each
  with a reason that distinguishes the two cases.
- **SC-002**: 0 model calls, 0 stored records and 0 audit entries result from any refused request.
- **SC-003**: 100% of proposal requests whose latest design is Accepted produce results identical to
  today's.
- **SC-004**: 0 stored proposals are ever stored against a design that was not the accepted latest at the
  moment of storing, including under overlapping requests.
- **SC-005**: All pre-existing automated tests pass, with assertion changes in none of them; the only
  edits are added acceptance steps in the setup of tests that request a proposal.
- **SC-006**: Existing refusal reasons are returned unchanged for 100% of the existing ineligible
  scenarios, in the existing order.

## Assumptions

- The design decision record and its retrieval already exist (slice 022); this feature only reads them.
- "Latest design draft" means the highest design version, as already used by the proposal operation.
- A design built on an older requirements draft is handled by the existing checks and keeps its current
  outcome; this feature adds no new treatment for it.
- The outdated-at-storing-time refusal reuses the existing way the proposal operation refuses stale
  inputs.
- Reviews are not gated by this feature; they keep their current eligibility rules.
- All sample data is synthetic.
