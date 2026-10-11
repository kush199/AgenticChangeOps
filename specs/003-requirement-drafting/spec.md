# Feature Specification: Requirement Drafting for Change Requests

**Feature Branch**: `003-requirement-drafting`

**Created**: 2026-10-11

**Status**: Draft

**Input**: User description: "Requirement drafting for change requests. A caller asks the system
to draft structured requirements for an existing change request. The system uses a language
model to produce a requirements draft containing: a plain-language summary of the intent, a list
of functional requirements, a list of acceptance criteria, a list of assumptions, and a list of
open questions. The draft is stored as a typed record linked to the ChangeRequestId, with a
version number, the generation timestamp, and the draft status Draft, and an audit entry is
recorded. Drafting is requested explicitly on demand; it is never triggered automatically by
submission. Only change requests in status Submitted or Deferred can be drafted; requests that
are Approved or Rejected, or that do not exist, are rejected with a specific error. Requesting a
draft again creates a new version and preserves earlier versions; the latest version and the
full version history can be retrieved by ChangeRequestId. The model only generates the draft
content: what happens next is decided by the system's own rules. Drafting never changes the
change request's status and never approves, rejects or defers anything; decisions remain with
the approval gate. If the model call fails, or its output is incomplete or malformed (for
example, missing a required section or empty requirements), the system stores nothing, records
no draft, and returns a specific error, leaving the change request unchanged. The behaviour must
be fully testable without a live model. Credentials and endpoints are never stored in the
repository. Out of scope: human editing of drafts, any user interface, authentication, other
agents. All sample data is synthetic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Draft Requirements for a Change Request (Priority: P1)

A caller explicitly asks the system to draft structured requirements for an existing change
request that is awaiting a decision. The system produces a draft containing a plain-language
summary of the intent, functional requirements, acceptance criteria, assumptions, and open
questions. The draft is stored as version 1 with status `Draft`, linked to the change request,
and an audit entry records that it was generated.

**Why this priority**: This is the whole point of the slice — turning a free-text change request
into structured, reviewable requirements. Everything else supports or protects this outcome.

**Independent Test**: Submit a synthetic change request (slice 001), request a draft using a
stand-in content generator that returns a well-formed draft, then confirm the returned draft
has all five sections, version 1, status `Draft`, a generation timestamp, and that one drafting
audit entry exists.

**Acceptance Scenarios**:

1. **Given** a change request in status `Submitted`, **When** a caller requests a draft and the
   generator returns complete content, **Then** a draft is stored with version 1, status
   `Draft`, the generation timestamp, and all five sections populated.

2. **Given** a change request in status `Deferred`, **When** a caller requests a draft and the
   generator returns complete content, **Then** a draft is stored in the same way.

3. **Given** a successfully stored draft, **When** the change request is read afterwards,
   **Then** its status is unchanged.

4. **Given** a successfully stored draft, **When** the audit history of the change request is
   listed, **Then** it contains exactly one new entry for the drafting, recording the draft
   version and a timestamp, after all earlier entries.

---

### User Story 2 - Redraft Creates a New Version (Priority: P2)

A caller requests a draft again for the same change request (for example after new information
was added to the request). The system creates a new version and keeps every earlier version
intact.

**Why this priority**: Requirements evolve; losing earlier drafts would destroy the trail of
how understanding changed. It builds directly on the first story.

**Independent Test**: Request three drafts for one change request, then confirm three versions
numbered 1, 2, 3 exist and version 1 and 2 are byte-for-byte unchanged.

**Acceptance Scenarios**:

1. **Given** a change request with one stored draft, **When** a caller requests another draft,
   **Then** a version 2 draft is stored and version 1 remains unchanged.

2. **Given** a change request with N stored drafts, **When** a new draft is requested,
   **Then** the new draft has version N+1 and N+1 drafts exist in total.

3. **Given** two different change requests, **When** each is drafted, **Then** version
   numbering is independent: each starts at 1.

---

### User Story 3 - Retrieve Latest Draft and Version History (Priority: P3)

A caller retrieves the latest draft for a change request, or the full version history in
version order.

**Why this priority**: Drafts are only useful if they can be read back; this is the retrieval
contract for later slices (review, architecture).

**Independent Test**: Draft a request twice, retrieve latest (expect version 2) and history
(expect versions 1 then 2); retrieve for a request with no drafts and confirm a clear
"no drafts" outcome.

**Acceptance Scenarios**:

1. **Given** a change request with three drafts, **When** the latest is retrieved, **Then**
   version 3 is returned.

2. **Given** a change request with three drafts, **When** the history is retrieved, **Then**
   all three are returned in ascending version order, none missing or altered.

3. **Given** an existing change request with no drafts, **When** the latest or history is
   retrieved, **Then** the latest retrieval reports that no draft exists and the history is
   empty.

4. **Given** an unknown `ChangeRequestId`, **When** the latest or history is retrieved,
   **Then** a not-found error identifying the ID is returned.

---

### User Story 4 - Refuse Drafting for Ineligible Requests (Priority: P4)

A request to draft is refused with a specific error when the change request does not exist or is
not in a draftable status. Nothing is stored and the generator is not called.

**Why this priority**: Prevents wasted model usage and drafts for finished business.

**Independent Test**: Attempt drafting for an unknown ID, an `Approved` request, a `Rejected`
request, and an `UnderReview` request; confirm a distinct specific error each time and that no
draft or audit entry was created.

**Acceptance Scenarios**:

1. **Given** an unknown `ChangeRequestId`, **When** a draft is requested, **Then** a not-found
   error identifying the ID is returned and nothing is stored.

2. **Given** a change request in status `Approved` or `Rejected`, **When** a draft is
   requested, **Then** an error naming the current status as not draftable is returned and
   nothing is stored.

3. **Given** a change request in any status other than `Submitted` or `Deferred`, **When** a
   draft is requested, **Then** it is refused in the same way, naming the current status.

4. **Given** any refused request, **When** the generator's call count is inspected, **Then**
   the generator was never invoked.

---

### User Story 5 - Fail Safely on Bad Model Output (Priority: P5)

When the content generator fails, or returns content that is incomplete or malformed, the system
stores nothing, records no draft and no audit entry, and returns a specific error. The change
request is left exactly as it was.

**Why this priority**: Model output is unreliable by nature; the system must never persist a
half-formed draft or let a failure corrupt state.

**Independent Test**: Use a stand-in generator that fails, one that returns a missing section,
and one that returns an empty requirements list; after each, confirm a specific error and that
no draft, version number, or audit entry was created.

**Acceptance Scenarios**:

1. **Given** a draftable request, **When** the generator fails (unavailable, times out, or
   raises an error), **Then** a generation-failure error is returned and nothing is stored.

2. **Given** a draftable request, **When** the generator returns content missing a required
   section (summary, functional requirements, acceptance criteria, assumptions, or open
   questions), **Then** an error naming the missing section is returned and nothing is stored.

3. **Given** a draftable request, **When** the generator returns an empty or whitespace-only
   summary, or an empty list of functional requirements or acceptance criteria, **Then** an
   error naming the offending section is returned and nothing is stored.

4. **Given** a rejected generation attempt followed by a successful one, **When** the draft is
   stored, **Then** it receives the next version number with no gap left by the failed attempt.

---

### User Story 6 - Drafting Never Decides or Triggers Itself (Priority: P6)

Drafting is only ever performed because a caller explicitly asked for it, and its content has no
effect on approval decisions. A draft that appears to say "approve this" or "reject this" changes
nothing.

**Why this priority**: Protects the separation between content generation and decisions that
the constitution requires; it is a guard story rather than a feature story.

**Independent Test**: Submit a request and confirm no draft exists afterwards; draft it with
generator content containing decision-like wording and confirm status, clearance check, and
decision history are unaffected.

**Acceptance Scenarios**:

1. **Given** a newly submitted change request, **When** no one has requested drafting,
   **Then** no draft exists and the generator has not been called.

2. **Given** generated content that contains words such as "approved" or "reject", **When**
   the draft is stored, **Then** the change request status is unchanged and the clearance check
   still returns false.

3. **Given** a stored draft, **When** an approver decides the request through the approval gate,
   **Then** the decision rules behave exactly as before and take no input from the draft.

---

### Edge Cases

- Two draft requests for the same change request in quick succession each get their own
  distinct, consecutive version number; none is lost or duplicated.
- Generated list items that are blank or whitespace-only are dropped before storing; a list
  that contains only blank items counts as empty. Dropping blank items alone never causes a
  draft to be rejected.
- The `OpenQuestions` and `Assumptions` lists may legitimately contain zero items only when the
  generator explicitly supplies them as present-but-empty; a missing section is always an
  error. (See Assumptions for the rule applied.)
- Very long generated items are accepted up to a stated limit; content beyond the limit is
  treated as malformed rather than silently truncated.
- Generator output that is structurally valid but meaningless (for example, all items
  identical) is not detected by this slice; content quality is not judged.
- A change request that moves to `Approved` or `Rejected` after drafts exist keeps all its
  drafts retrievable; only creation of new drafts is refused.
- A failure while recording the audit entry must not leave a stored draft without its audit
  entry, or an audit entry without its draft.
- The change request's own text fields are passed to the generator as-is; the request is not
  modified by drafting.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow a caller to explicitly request a requirements draft for an
  existing change request identified by `ChangeRequestId`.
- **FR-002**: System MUST NOT start drafting automatically as a result of submitting a change
  request or of any other event; drafting occurs only on an explicit request.
- **FR-003**: System MUST accept drafting requests only for change requests in status
  `Submitted` or `Deferred`.
- **FR-004**: System MUST refuse a drafting request for an unknown `ChangeRequestId` with a
  not-found error that identifies the ID.
- **FR-005**: System MUST refuse a drafting request for a change request in any other status
  (including `Approved`, `Rejected`, and `UnderReview`) with an error that names the current
  status as not draftable.
- **FR-006**: System MUST obtain draft content from a language-model content generator that
  receives the change request's content and returns the five draft sections: a plain-language
  summary of the intent, functional requirements, acceptance criteria, assumptions, and open
  questions.
- **FR-007**: System MUST validate generated content before storing it: the summary MUST be
  non-empty; functional requirements and acceptance criteria MUST each contain at least one
  non-blank item; assumptions and open questions MUST be present (they may be empty lists);
  and no section may be absent. Blank or whitespace-only items MUST be dropped before these
  checks are applied.
- **FR-008**: System MUST store a valid draft as a typed record containing a draft
  identifier, the `ChangeRequestId`, a version number, the generation timestamp (UTC), the
  draft status `Draft`, the producing agent's name, and the five content sections.
- **FR-009**: System MUST assign version numbers per change request, starting at 1 and
  increasing by 1 for each stored draft, with no gaps and no reuse.
- **FR-010**: System MUST preserve every earlier version when a new draft is created; stored
  drafts are never modified or removed by this slice.
- **FR-011**: System MUST record exactly one audit entry for each stored draft, linked to the
  `ChangeRequestId`, identifying the draft version and generation timestamp, and ordered
  after earlier audit entries.
- **FR-012**: System MUST allow retrieval of the latest draft for a `ChangeRequestId`, and of
  the full version history in ascending version order.
- **FR-013**: Retrieval for an unknown `ChangeRequestId` MUST return a not-found error;
  retrieval of the latest draft for an existing request with no drafts MUST report that no
  draft exists, and the history MUST be empty.
- **FR-014**: Drafting MUST NOT change the change request's status or any of its other
  attributes, and MUST NOT approve, reject, or defer anything; decisions remain exclusively
  with the approval gate.
- **FR-015**: System MUST NOT use any generated content, or the existence of a draft, to
  decide routing, status transitions, eligibility, or clearance; those remain determined by
  the system's own deterministic rules.
- **FR-016**: When the content generator fails (unavailable, times out, or raises an error),
  System MUST store no draft, create no audit entry, consume no version number, and return a
  generation-failure error distinct from validation errors.
- **FR-017**: When generated content is incomplete or malformed, System MUST store no draft,
  create no audit entry, consume no version number, and return an error naming the specific
  section or condition that failed; if more than one section fails, every failing section
  MUST be reported.
- **FR-018**: A drafting attempt that fails for any reason MUST leave the change request,
  its audit history, and all previously stored drafts identical to their state before the
  attempt.
- **FR-019**: System MUST NOT invoke the content generator for a request that is refused for
  eligibility reasons (unknown ID or non-draftable status).
- **FR-020**: Content generation MUST be hidden behind an abstraction so that all drafting
  behaviour is verifiable using stand-in generators, with no live model call, network access,
  or credential needed to run the automated tests.
- **FR-021**: System MUST NOT store credentials, keys, tokens, or service endpoints in the
  repository; any model configuration MUST come from the environment at run time.
- **FR-022**: The summary and each list item MUST NOT exceed 2,000 characters after
  trimming, and each list MUST NOT exceed 50 items after blank items have been dropped;
  content exceeding these limits MUST be treated as malformed and rejected as in FR-017, never
  truncated.

### Key Entities

- **RequirementsDraft**: A versioned requirements draft for one change request. Attributes:
  `DraftId`, `ChangeRequestId`, `Version` (integer, starts at 1), `GeneratedAt` (UTC), `Status`
  (always `Draft` in this slice), `AgentName` (the producing agent), `Summary` (plain-language
  intent), `FunctionalRequirements` (list),
  `AcceptanceCriteria` (list), `Assumptions` (list), `OpenQuestions` (list).

- **DraftContent**: The five content sections as returned by the content generator, before
  validation and before being wrapped in a `RequirementsDraft`. Carries no identity, version,
  or timestamp.

- **DraftingResult**: The outcome of a drafting attempt: either the stored `RequirementsDraft`
  (on success) or a list of specific errors (on failure). Never both.

- **ChangeRequest** (existing, slice 001/002): Read by this slice and never modified by it.

- **ChangeRequestAuditEntry** (existing, slices 001/002): Extended with a drafting event that
  records the draft version, the agent name, and the generation timestamp.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a draftable request and well-formed generated content, 100% of attempts
  result in a stored draft containing all five sections, with version, timestamp, and status
  `Draft`.
- **SC-002**: After N successful drafts for one request, exactly N versions exist, numbered
  1 to N without gaps, and every earlier version is identical to when it was first stored.
- **SC-003**: 100% of drafting attempts for unknown, `Approved`, `Rejected`, or `UnderReview`
  requests are refused with an error naming the specific cause, and the content generator is
  invoked zero times for them.
- **SC-004**: 100% of failed or invalid generations (generator failure, missing section, empty
  required list, blank summary) leave zero new drafts, zero new audit entries, and an
  unchanged change request, and each returns an error naming the specific cause.
- **SC-005**: Across every drafting scenario, the change request's status is identical before
  and after, and the clearance check result is unchanged.
- **SC-006**: Submitting a change request results in zero drafts and zero generator calls
  until a caller explicitly asks for drafting.
- **SC-007**: The full set of drafting scenarios runs to completion in the automated test
  suite with no live model, network, or credentials.
- **SC-008**: A repository search finds no credentials, keys, or endpoints associated with
  this feature.

## Assumptions

- This slice builds on slices 001 (intake) and 002 (approval gate): change requests, their
  statuses, the audit history, and the clearance check already exist and are only read or
  appended to here.
- The interaction surface is the same in-process .NET library as the earlier slices; no HTTP,
  MCP, or command-line layer is in scope.
- Persistence remains the in-memory store from the earlier slices; durable storage and
  concurrent access beyond the in-process guarantee in the Edge Cases remain out of scope.
- The drafting capability is the first behaviour of the Requirement/Product Agent named in the
  constitution; no other agent is introduced or called.
- The only draft status in this slice is `Draft`; later statuses (for example, reviewed or
  accepted) belong to future slices, and any such addition is an additive change to the typed
  record.
- Adding a drafting event to the audit history is an additive, non-breaking change under
  Constitution Principle IV; this is to be recorded in the plan's Complexity Tracking.
- The caller identity is not recorded for drafting; authentication is out of scope, and the
  audit entry therefore carries the agent name rather than a person.
- Drafting is classified as **non-consequential** for the purposes of Constitution
  Principle III: it changes no status, commits nothing to any downstream system, triggers no
  external action, and cannot advance a request past any checkpoint. A draft is a proposal for
  later human review, so no approval is required to create one. This classification is to be
  reviewed at the compliance check.
- The draft's input payload is the linked change request, identified by `ChangeRequestId`. A
  change request is immutable after intake except for its `Status`, and `Status` does not
  contribute to the generated content, so the content inputs are fully reconstructible from
  the request and the draft's `GeneratedAt`; no separate input snapshot is stored. The output
  payload is the five content sections. This interpretation of Constitution Principle IV is
  recorded in plan.md.
- Time limits on the model call are applied by the generator implementation, not by this
  slice. The service treats any timeout or cancellation raised by the generator as a generator
  failure (FR-016).
- The content generator receives the change request's title, description, and any other
  descriptive attributes already captured at intake; no additional context is supplied.
- Assumptions and open questions are allowed to be empty lists, because a request may be
  genuinely clear; functional requirements and acceptance criteria and the summary may not be
  empty, because a draft without them has no value.
- Items are trimmed before validation; blank items are dropped, and a list left with no valid
  item counts as empty.
- No retry of a failed generation is performed automatically; the caller may simply request
  drafting again.
- A drafting request is an in-process call that completes or fails as a whole; partial results
  are never visible to other callers.
- All sample change requests, drafts, generator outputs, and fixtures MUST be fully synthetic —
  no real people, organisations, or systems.

## Clarifications

### Session 2026-10-11

- Q: When a generated list contains some blank items alongside valid ones, should blanks be dropped or the whole draft rejected? → A: Drop blank items and store the draft if each required list still has at least one valid item.
