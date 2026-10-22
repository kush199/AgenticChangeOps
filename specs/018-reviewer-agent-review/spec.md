# Feature Specification: Reviewer Agent Reviews A Change Proposal

**Feature Branch**: `feature/reviewer-agent-review`

**Created**: 2026-10-22

**Status**: Draft

**Input**: User description: "A Reviewer agent that produces a verified, advisory review of a change
proposal, as stored content only. Actors: a caller asking for a review of the latest change proposal of
an approved change request; the Reviewer agent drafts the findings and a person reads them. Actions:
produce a review; retrieve the latest review and the full review history through the system's internal
service (no HTTP operation in this slice). Input is the ChangeRequestId only. A review is produced only
for an Approved change request with a requirements draft, a design draft based on the latest
requirements draft and a change proposal based on that design draft. The review has a system-checked
part (every proposed edit's quoted text re-checked against the current sample codebase, recorded as
not performed if the sample is unavailable) and a model-generated part (findings on acceptance
criteria, consistency with the design and unmentioned risks), each finding carrying a category, an
advisory severity, a plain statement and at least one reference to an existing requirements, design or
proposal item; any unresolvable reference rejects the whole review. Severity is advisory only; a review
never changes any status or decides anything. The Reviewer agent uses no tools. Out of scope: HTTP
operations, workflow/status/automatic-advancement changes, tools for the Reviewer, using the
constitution's principles as review input, human acceptance, decisions based on a review, applying or
running anything, authentication, any user interface, changing other agents. All sample data is
synthetic."

## Clarifications

### Session 2026-10-22

- Q: What counts as a referenceable item that a finding can point to? → A: A specific entry in a list, identified by document kind (requirements, design or proposal), list name and the entry's position or id; a whole document or free text is not a valid reference.
- Q: Should the Reviewer agent be shown the system's per-edit quote-check result? → A: Yes, as read-only input computed before the model call; the stored result is still the system's own and the agent cannot alter it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Produce A Verified Advisory Review (Priority: P1)

A caller asks for a review of the latest change proposal of an approved change request. The system
works from the change request, its latest requirements draft, the design draft based on it and the
latest proposal based on that design. It runs its own re-check of every proposed edit's quoted text
against the current sample codebase, then has the Reviewer agent draft findings. The stored review holds
the system-checked result, the findings (each with category, advisory severity, plain statement and
references) and open questions.

**Why this priority**: This is the whole feature: a stored, checked, readable review that a person can
use before deciding anything.

**Independent Test**: With a scripted model, request a review for an Approved change request with a full
chain of requirements, design and proposal; check the stored review has the system-checked result, the
findings with resolvable references, open questions, version number, generation time, status Draft and
agent name, and that the change request status is unchanged.

**Acceptance Scenarios**:

1. **Given** an Approved change request with a requirements draft, a design draft based on the latest
   requirements draft and a proposal based on that design draft, **When** a review is requested,
   **Then** a review is stored, linked to the ChangeRequestId and to the versions of the requirements
   draft, design draft and proposal it was based on, with version number, generation time, status
   Draft and agent name.
2. **Given** the stored review, **When** inspected, **Then** its system-checked result lists, for every
   proposed edit, whether its quoted text was found in the current sample, and the agent had no ability
   to alter that result.
3. **Given** the stored review, **When** inspected, **Then** every finding has a category, an advisory
   severity, a plain-language statement and at least one reference to an item that exists in the
   requirements, design or proposal.
4. **Given** the stored review, **When** the change request is inspected, **Then** its status is
   unchanged and nothing was approved, rejected or cleared.
5. **Given** the stored review, **When** the audit trail is read, **Then** it holds one
   `ReviewGenerated` entry for it, with no model text or file content.
6. **Given** the review, **When** its classification is inspected, **Then** it is non-consequential
   content and severity has no effect on any system behaviour.

---

### User Story 2 - Retrieve The Latest Review And Its History (Priority: P1)

A caller retrieves the latest review of a change request, or the full review history in version order,
through the system's internal service. Requesting again creates a new version and earlier versions are
preserved.

**Why this priority**: A review that cannot be read back or that overwrites earlier ones is of no use.

**Independent Test**: Request two reviews for one change request; check latest returns version 2 and
history returns versions 1 and 2 in order with version 1 unchanged; check a change request with no review
yields the same "none yet" outcome style as proposals.

**Acceptance Scenarios**:

1. **Given** two stored reviews, **When** the latest is retrieved, **Then** version 2 is returned.
2. **Given** two stored reviews, **When** the history is listed, **Then** both are returned in ascending
   version order with version 1 unchanged.
3. **Given** a change request with no review, **When** the latest is retrieved, **Then** a clear "no
   review" outcome is returned, matching the proposal convention.
4. **Given** an unknown or malformed ChangeRequestId, **When** either retrieval is called, **Then** it is
   refused with the same kind of specific error as for proposals.

---

### User Story 3 - Refusals Store Nothing (Priority: P1)

A review is refused, with a specific error and nothing stored, when the ChangeRequestId is unknown or
malformed, the change request is not Approved, there is no requirements draft, there is no design draft
or only one not based on the latest requirements draft, or there is no proposal or only one not based on
the latest design draft.

**Why this priority**: Eligibility is the system's own rule; the model must never be reached when the
rules say no.

**Independent Test**: For each condition, set up the state, request a review, and check the specific
distinct error, that the model was not called, and that no review or audit entry was stored.

**Acceptance Scenarios**:

1. **Given** a change request that is not Approved, **When** a review is requested, **Then** it is refused
   with a specific error and nothing is stored.
2. **Given** an Approved change request missing a requirements draft, a design draft or a proposal,
   **When** requested, **Then** each case is refused with its own specific error and nothing is stored.
3. **Given** a design draft not based on the latest requirements draft, or a proposal not based on the
   latest design draft, **When** requested, **Then** it is refused as outdated with a specific error and
   nothing is stored.

---

### User Story 4 - A Bad Or Failed Review Is Rejected Whole (Priority: P1)

If any finding's reference points to nothing, the whole review is rejected and nothing is stored. If the
model call fails, times out, or returns incomplete or malformed output, or the review exceeds the maximum
number of findings or maximum size, nothing is stored and the caller gets a specific error carrying only a
failure category.

**Why this priority**: References are what make findings checkable; partial acceptance would defeat them.

**Independent Test**: With a scripted model, return one dangling reference among valid findings, then too
many findings, an oversized review, and each model failure mode; check each is rejected and nothing is
stored.

**Acceptance Scenarios**:

1. **Given** several findings of which one references a non-existent item, **When** checked, **Then** the
   whole review is rejected and nothing is stored.
2. **Given** a finding with no reference, **When** checked, **Then** the whole review is rejected.
3. **Given** a review over the maximum findings or size, **When** checked, **Then** it is rejected.
4. **Given** a model failure, timeout, or incomplete or malformed output, **When** the request ends,
   **Then** nothing is stored and the error carries only a failure category.
5. **Given** any failure, **When** responses and logs are inspected, **Then** they contain no model text,
   file content, endpoint, deployment name or credential.

---

### User Story 5 - The Sample Check Degrades Gracefully, And Stale Inputs Are Refused (Priority: P2)

If the sample codebase is unavailable, the review continues and records the system-checked part as not
performed. If the requirements draft, design draft or proposal stop being the latest while a review is
being prepared, the review is refused as outdated and nothing is stored.

**Why this priority**: Keeps the review useful without the sample and prevents reviews of superseded
content.

**Independent Test**: Make the sample unavailable and check a review is stored with "not performed"; make
a newer proposal appear mid-request and check the review is refused as outdated with nothing stored.

**Acceptance Scenarios**:

1. **Given** the sample codebase is unavailable, **When** a review is requested, **Then** a review is
   stored whose system-checked result states the check was not performed.
2. **Given** a newer requirements draft, design draft or proposal is saved while a review is prepared,
   **When** storage is attempted, **Then** the review is refused as outdated and nothing is stored.

---

### User Story 6 - Nothing Else Changes (Priority: P2)

With the Reviewer agent not used, every existing response, status code, body and header is identical to
before, and every existing automated test passes with its assertions unchanged. The work is delivered in
several small reviewable steps, each leaving the system working with every test passing, the first adding
only logic that nothing yet calls.

**Why this priority**: Protects the existing system and keeps the change reviewable.

**Independent Test**: Run the full existing suite unchanged after each step; compare existing responses
before and after.

**Acceptance Scenarios**:

1. **Given** the Reviewer agent is not used, **When** any existing operation is called, **Then** its
   response is identical to before.
2. **Given** the first delivery step, **When** the system runs, **Then** no existing behaviour has changed
   because nothing yet calls the new logic.

### Edge Cases

- A proposal contains no edits: the system-checked result records zero edits checked and the review
  proceeds.
- The model returns zero findings: accepted if otherwise well-formed (a review may find nothing).
- Two review requests for one change request arrive at once: each gets a distinct, consecutive version.
- A quoted text that matched when the proposal was stored no longer matches the current sample: recorded
  as not matched in the system-checked result; the review continues.
- A finding references an item of a superseded draft or proposal version: treated as a reference to
  nothing and rejected.
- The model attempts to state a system-checked result of its own: ignored; only the system's own result is
  stored.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept a ChangeRequestId only as input for a review request.
- **FR-002**: The system MUST produce a review only for an Approved change request that has a
  requirements draft, a design draft based on the latest requirements draft, and a proposal based on that
  design draft's latest version; otherwise it MUST refuse with a specific, distinct reason and store
  nothing.
- **FR-003**: The system MUST refuse an unknown or malformed ChangeRequestId with a specific error and
  store nothing. At the internal service "malformed" means an empty identifier; a malformed text
  identifier can only arrive over HTTP, which is a later slice.
- **FR-004**: The system MUST re-check, without any model, every proposed edit's quoted text against the
  current sample codebase, and record the per-edit result in the review; the agent MUST NOT be able to
  alter it.
- **FR-004a** (added by clarification, between FR-004 and FR-005): The system MUST compute the quote-check result before the model call and give it to the
  Reviewer agent as read-only input; the stored result MUST be the system's own regardless of what the
  agent returns.
- **FR-005**: If the sample codebase is unavailable, the system MUST record the check as not performed
  and continue the review.
- **FR-006**: The Reviewer agent MUST generate findings about whether the proposal meets the acceptance
  criteria, is consistent with the design, and leaves risks unmentioned, plus open questions.
- **FR-007**: Each finding MUST have a category, an advisory severity, a plain-language statement and at
  least one reference to an item that exists in the requirements, design or proposal the review is based
  on. A reference MUST name the document kind (requirements, design or proposal), the list within it and
  the entry's position or id; a reference to a whole document, or free text, is not valid.
- **FR-008**: The system MUST reject the whole review, storing nothing, if any finding's reference points
  to nothing or any finding has no reference.
- **FR-009**: The system MUST enforce a maximum number of findings and a maximum review size, rejecting the
  whole review when exceeded.
- **FR-010**: The Reviewer agent MUST use no tools and read nothing itself; the system's own re-check MUST
  be the only place the sample codebase is read.
- **FR-011**: Severity MUST be advisory: nothing in the system acts on it. A review MUST NOT change any
  status, approve, reject or clear anything, and MUST be classified as non-consequential content.
- **FR-012**: Eligibility and every decision MUST remain the system's own rules; the model only generates
  content.
- **FR-013**: Each stored review MUST be a typed record linked to the ChangeRequestId and to the versions
  of the requirements draft, design draft and proposal it was based on, with version number, generation
  time, status Draft and the agent name, containing the system-checked result, the findings with
  references, and open questions.
- **FR-014**: Requesting a review again MUST create a new version and preserve earlier ones.
- **FR-015**: Just before storing, the system MUST re-check that the requirements draft, design draft and
  proposal are still the latest; if not, it MUST refuse as outdated and store nothing.
- **FR-016**: The system MUST record an audit entry of a new type `ReviewGenerated` for every stored
  review.
- **FR-017**: If the model call fails, times out, or returns incomplete or malformed output, the system
  MUST store nothing and return a specific error carrying only a failure category.
- **FR-018**: No model text, file content, endpoint, deployment name or credential MUST appear in any
  error, log, audit entry or response.
- **FR-019**: The system MUST provide, through its internal service, retrieval of the latest review and of
  the full review history in ascending version order; no HTTP operation is added in this slice.
- **FR-020**: With the Reviewer agent not used, every existing response, status code, body and header MUST
  be identical to before, and every existing automated test MUST pass with its assertions unchanged.
- **FR-021**: The work MUST be delivered in several small reviewable steps, each leaving the system working
  with every test passing, the first adding only logic that nothing yet calls.

### Key Entities

- **Review**: A stored, versioned, Draft, non-consequential record for one change request, linked to the
  requirements draft, design draft and proposal versions it was based on; holds the system-checked result,
  findings and open questions, generation time and agent name.
- **System-Checked Result**: The system's own per-edit record of whether each quoted text was found in the
  current sample, or that the check was not performed.
- **Finding**: A category, advisory severity, plain statement and one or more references.
- **Reference**: A pointer to one entry in a list of the requirements draft, design draft or proposal the
  review is based on, naming the document kind, the list and the entry's position or id.
- **Open Question**: A question for a person, raised by the review.
- **ReviewGenerated Audit Entry**: The audit record created for every stored review.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For an eligible change request, 100% of requested reviews with well-formed output are stored
  with all required parts and with every finding carrying at least one resolvable reference.
- **SC-002**: 100% of reviews containing even one unresolvable reference, or exceeding a limit, are
  rejected with nothing stored.
- **SC-003**: Each of the refusal conditions (unknown/malformed id, not Approved, and each missing or
  outdated input) yields its own specific error in 100% of cases, with zero reviews or audit entries stored
  and zero model calls.
- **SC-004**: 100% of failure responses, logs and audit entries are free of model text, file content,
  endpoints, deployment names and credentials.
- **SC-005**: After any number of reviews, the change request status, and all drafts and proposals, are
  identical to before; zero decisions are made from a review.
- **SC-006**: Requesting N reviews yields N preserved versions, retrievable in order, with earlier ones
  unchanged.
- **SC-007**: With the Reviewer not used, the full pre-existing automated test suite passes with no
  assertion changed after every delivery step.

## Assumptions

- The Approved status, latest-version rules, outdated checks, and "none yet"/error conventions follow the
  existing requirement, design and proposal behaviour.
- The re-check of quoted text uses the same exact-match rule the proposal check already uses, applied to
  the current sample; the Reviewer agent is not told the sample's contents.
- Finding categories and severity levels are small fixed sets chosen at planning; the maximum findings and
  size are configured limits with defaults chosen at planning.
- A review with zero findings is valid; a review is not required to find faults.
- Reviews are stored with the existing durable storage and audit mechanisms.
- The model is reached through the same adapter pattern as the other agents, and a live check is not part
  of this slice's automated tests.
- No HTTP operation, user interface, authentication or human acceptance step is added; those follow in
  later slices.
- All sample data is synthetic.
