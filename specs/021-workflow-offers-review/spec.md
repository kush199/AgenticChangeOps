# Feature Specification: The Workflow Status Offers The Review Once A Current Change Proposal Exists

**Feature Branch**: `feature/workflow-offers-review`

**Created**: 2026-10-28

**Status**: Draft

**Input**: User description: "The workflow status offers the review once a current change proposal exists.
Actor: a caller reading the workflow status of a change request, who may then request a review. Action:
when a change request has a current change proposal and no review of it yet, and reviewing is available,
the workflow status offers the review as the next thing a person may request; the offer is information
only. Inputs: facts already recorded (status, requirements drafts, design drafts, change proposals and
reviews with the versions each was based on, and whether reviewing is available on the server). Rules:
offered only when the change request is Approved, a current proposal exists (based on the design draft
that is based on the latest requirements draft), no review is based on that proposal, and reviewing is
available; otherwise the offer is as today (the proposal when none is current and proposals are
available at the design-complete stage, else nothing); where a current proposal exists the review
replaces the proposal as the offered step; when a review of the current proposal exists nothing further
is offered; derived from recorded facts only, never stored, never asks a model; changes neither stage,
next step, reason nor completed steps; neither manual advance nor automatic advancement runs the offered
step; implies no decision and changes no status; works whether or not reviewing is configured and
reports no offer when it is not; nothing from the model, sample codebase, endpoint, deployment name or
credential appears in any response, log or error. Outcomes: the status shows the review as its offered
step; the published description lists the new value; everything else identical and existing tests pass
unchanged except those enumerating step names or asserting the offered step for a request with a current
proposal. Failures: unknown or malformed ChangeRequestId behaves as before; outdated, older-design or
older-proposal proposals and reviews neither trigger nor suppress the offer; advancing a change request
whose only offer is the review reports nothing due and changes nothing. Out of scope: running the review
automatically or on advance, any new HTTP operation, listing proposals or reviews among completed steps,
human acceptance, changes to automatic advancement or to how reviews are produced, authentication, any
user interface. All sample data is synthetic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See The Review Offered After A Current Proposal (Priority: P1)

A caller reads the workflow status of an Approved change request that has a current change proposal, no
review of that proposal, and reviewing available on the server. The status shows the review as the
offered step, so the caller knows a review may now be requested.

**Why this priority**: This is the whole feature: the status stops pointing at a step that is already
done and points at the next one a person may request.

**Independent Test**: Record an Approved change request with requirements, design and a proposal based on
that design, and no review; read the workflow status; check the offered step is the review and every
other field is what it was.

**Acceptance Scenarios**:

1. **Given** an Approved change request with a current proposal, no review of it, and reviewing
   available, **When** the workflow status is read, **Then** the offered step is the review.
2. **Given** the same change request, **When** the status is read, **Then** the stage, next step, reason
   and completed steps are exactly what they were before this feature.
3. **Given** the status was read, **When** the change request, workflow and stored records are inspected,
   **Then** nothing has changed and no offer was stored.

---

### User Story 2 - The Offer Is Replaced Only Where Today's Rules Say So (Priority: P1)

The review replaces the proposal offer only where a current proposal exists. Where no current proposal
exists, the status behaves as today: the proposal is offered when proposals are available at the
design-complete stage, otherwise nothing is offered. Once a review of the current proposal exists,
nothing further is offered.

**Why this priority**: The change must not disturb any existing response beyond the single replaced
offer.

**Independent Test**: Read the status across change requests with no proposal, an outdated proposal, a
current proposal with and without a review, and reviewing available or not; check each offer against the
rules.

**Acceptance Scenarios**:

1. **Given** no current proposal and proposals available at the design-complete stage, **When** the status
   is read, **Then** the proposal is offered, as today.
2. **Given** no current proposal and nothing else offerable, **When** the status is read, **Then** nothing
   is offered, as today.
3. **Given** a review exists that is based on the current proposal, **When** the status is read, **Then**
   nothing further is offered.
4. **Given** reviewing is not configured, **When** the status is read for a change request with a current
   proposal, **Then** the review is not offered, the status still works, and no error appears.
5. **Given** the change request is not Approved, **When** the status is read, **Then** the review is not
   offered.

---

### User Story 3 - Outdated Records Neither Trigger Nor Suppress The Offer (Priority: P2)

A proposal based on an older design, or on a design based on an older requirements draft, is not a
current proposal and does not trigger the review offer. A review of an older proposal does not suppress
the offer for the current proposal.

**Why this priority**: Versioned artefacts are regenerated; the offer must follow the current chain only.

**Independent Test**: Record a proposal, then a newer requirements draft and design; confirm the review
is not offered for the old proposal. Record a current proposal after a review of an older proposal;
confirm the review is offered.

**Acceptance Scenarios**:

1. **Given** the only proposal is based on an older design, **When** the status is read, **Then** the
   review is not offered.
2. **Given** a review exists only for an older proposal and a newer current proposal exists, **When** the
   status is read, **Then** the review is offered.

---

### User Story 4 - Advancing Never Runs The Offered Review (Priority: P2)

Asking the workflow to advance a change request whose only offer is the review reports that nothing is
due and changes nothing. Automatic advancement likewise never runs the review.

**Why this priority**: The offer is information only; a review is requested by a person.

**Independent Test**: With a change request whose only offer is the review, request an advance and check
the "nothing due" outcome and that no review was produced.

**Acceptance Scenarios**:

1. **Given** the only offer is the review, **When** the workflow is asked to advance, **Then** it reports
   that nothing is due and no record changes.
2. **Given** automatic advancement is enabled, **When** a change request reaches a current proposal,
   **Then** no review is produced.

---

### Edge Cases

- An unknown or malformed ChangeRequestId behaves exactly as before.
- A review based on the current proposal exists alongside older reviews: nothing further is offered.
- A change request that is not Approved but has a current proposal: no review offer.
- Reviewing availability changes between reads: each read reflects availability at that moment; nothing
  is remembered.
- No model, sample codebase, endpoint, deployment name or credential appears in any response, log or
  error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The workflow status MUST offer the review if and only if the change request is Approved, a
  current proposal exists, no review is based on that proposal, and reviewing is available.
- **FR-002**: A current proposal MUST mean a proposal based on the design draft that is based on the
  latest requirements draft.
- **FR-003**: Where no current proposal exists, the offer MUST be as before this feature: the proposal
  when proposals are available at the design-complete stage, otherwise nothing.
- **FR-004**: Where a current proposal exists, the review MUST replace the proposal as the offered step.
- **FR-005**: When a review based on the current proposal exists, the status MUST offer nothing further.
- **FR-006**: The offer MUST be derived from recorded facts on each read, never stored, and never ask a
  model.
- **FR-007**: The offer MUST NOT change the stage, next step, reason or completed steps, and MUST NOT
  change any status or imply any decision.
- **FR-008**: Neither the manual advance nor automatic advancement MUST ever run the offered review;
  advancing when the review is the only offer MUST report that nothing is due and change nothing.
- **FR-009**: The workflow status MUST keep working when reviewing is not configured, and MUST report no
  review offer in that case.
- **FR-010**: Proposals or reviews that are outdated, based on an older design, or of an older proposal
  MUST NOT trigger or suppress the offer.
- **FR-011**: An unknown or malformed ChangeRequestId MUST behave exactly as before.
- **FR-012**: The published description of the interface MUST list the review as a possible offered step.
- **FR-013**: Apart from the new value and the replaced offer, every existing response, status code,
  body, header and error code MUST be identical, and every existing automated test MUST pass with
  assertions unchanged, except those enumerating the possible step names or asserting the offered step
  for a request with a current proposal.
- **FR-014**: Nothing from the model, sample codebase, endpoint, deployment name or credential MUST
  appear in any response, log or error.

### Key Entities

- **Workflow status**: the read-only view of a change request's stage, next step, reason, completed
  steps and offered step.
- **Offered step**: information-only pointer to what a person may request next; one of proposal, review,
  or none.
- **Current proposal**: a change proposal based on the design draft based on the latest requirements
  draft.
- **Review**: an advisory record based on a specific proposal version.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of tested cases where all four conditions hold, the status shows the review as its
  offered step.
- **SC-002**: In 100% of tested cases where any condition fails, the offer equals what it was before this
  feature (except where a current proposal exists, where the review replaces the proposal).
- **SC-003**: Reading the status any number of times leaves every stored record unchanged and produces no
  review.
- **SC-004**: Advancing a change request whose only offer is the review reports nothing due in every
  tested case.
- **SC-005**: All pre-existing automated tests pass with assertions unchanged except those enumerating
  step names or asserting the offered step for a request with a current proposal.
- **SC-006**: The published interface description lists the review as a possible offered step.

## Assumptions

- "Offered step" is the existing information-only field of the workflow status; this feature adds one
  possible value and changes when an existing value is shown.
- Whether reviewing is available is already known to the server in the same way proposal availability is.
- "Approved" and the version-basing of drafts, designs, proposals and reviews are as already recorded.
- Outside the listed out-of-scope items, no behaviour of the existing HTTP operations changes.
