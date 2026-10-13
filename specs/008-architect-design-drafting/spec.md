# Feature Specification: Architect Design Drafting for Approved Change Requests

**Feature Branch**: `008-architect-design-drafting`

**Created**: 2026-10-13

**Status**: Draft

**Input**: User description: "Architect design drafting for approved change requests. A caller asks
the system to draft a design analysis for an existing change request. The Architect agent
produces the draft; a human reviews it. Actions: request a design draft, retrieve the latest
design draft, list the full design draft history in version order. Input: the ChangeRequestId
only; the agent works from the change request's own fields and its latest requirements draft. A
design draft may be produced only for a change request whose status is Approved, and only if it
has a requirements draft. The language model only generates content; the system's own rules
decide whether and when drafting happens and what is stored. The draft never changes the change
request's status and never approves, rejects, defers or clears anything. Requesting again creates
a new version and preserves earlier versions. The Architect agent is added as its own
self-contained location beside the Requirement agent without editing the Requirement agent, the
intake and approval capabilities, or the shared model-access code, other than to register it.
A design draft is a typed record linked to the ChangeRequestId and to the version of the
requirements draft it was based on, with a version number, generation timestamp, status Draft,
and the agent name, containing a summary of the proposed approach, affected components, impact
and risks, dependencies, rollback considerations, and open questions. An audit entry of a new
type DesignGenerated is recorded for every stored draft. HTTP operations expose the three
actions with the same error conventions as the requirements drafting operations. Failures
(unknown or malformed ID, not Approved, no requirements draft, model failure/timeout/incomplete
or malformed output) store nothing, write no audit entry, and return a specific error carrying
only a failure category; no model text, endpoint, deployment name or credential appears in any
error, log or response. Out of scope: Developer and Reviewer agents, orchestration,
authentication, durable storage, any user interface, human editing of drafts. Fully testable
without a live model; any live check is opt-in. All sample data is synthetic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Draft a Design for an Approved Change Request (Priority: P1)

A caller explicitly asks the system to draft a design analysis for a change request that has been
approved and already has a requirements draft. The Architect agent produces a design draft
containing a summary of the proposed approach, the affected components, the impact and risks,
dependencies, rollback considerations, and open questions. The draft is stored as version 1 with
status `Draft`, linked to the change request and to the version of the requirements draft it was
based on, and an audit entry of type `DesignGenerated` records that it was generated.

**Why this priority**: This is the whole point of the slice: turning an approved, specified
change into a reviewable design analysis. Everything else supports or protects this outcome.

**Independent Test**: Using synthetic data, create an `Approved` change request with a
requirements draft, request a design draft using a stand-in content generator that returns
well-formed content, then confirm the draft has all six sections, version 1, status `Draft`, a
generation timestamp, the agent name, the requirements draft version it was based on, and that
exactly one `DesignGenerated` audit entry exists.

**Acceptance Scenarios**:

1. **Given** an `Approved` change request with a requirements draft, **When** a caller requests a
   design draft and the generator returns complete content, **Then** a draft is stored with
   version 1, status `Draft`, the generation timestamp, the agent name, and all six sections
   populated.

2. **Given** a change request with several requirements draft versions, **When** a design draft
   is requested, **Then** the stored draft records the version of the latest requirements draft
   as its basis.

3. **Given** a successfully stored design draft, **When** the change request is read afterwards,
   **Then** its status is unchanged and it is not approved, rejected, deferred or cleared by the
   drafting.

4. **Given** a successfully stored design draft, **When** the audit history is listed, **Then** it
   contains exactly one new `DesignGenerated` entry, after all earlier entries, recording the
   design draft version and a timestamp.

---

### User Story 2 - Redesign Creates a New Version (Priority: P2)

A caller requests a design draft again for the same change request. The system creates a new
version and keeps every earlier version intact.

**Why this priority**: Designs evolve as requirements change; losing earlier drafts would destroy
the trail of how the design developed.

**Independent Test**: Request three design drafts for one change request, then confirm versions
1, 2, 3 exist and versions 1 and 2 are unchanged, each with its own audit entry.

**Acceptance Scenarios**:

1. **Given** a change request with one stored design draft, **When** another is requested,
   **Then** version 2 is stored and version 1 remains unchanged.

2. **Given** a change request with N stored design drafts, **When** a new one is requested,
   **Then** the new draft has version N+1 and N+1 drafts exist in total.

3. **Given** two different change requests, **When** each is designed, **Then** version
   numbering is independent: each starts at 1.

4. **Given** a new requirements draft was added after the first design draft, **When** a design
   draft is requested again, **Then** the new design draft is based on the new requirements
   version while the earlier design draft still records its original basis.

---

### User Story 3 - Retrieve Latest Design Draft and Version History (Priority: P3)

A caller retrieves the latest design draft for a change request, or the full design draft history
in version order.

**Why this priority**: Drafts are only useful if a human reviewer (or a later slice) can read them
back.

**Independent Test**: Design a request twice; retrieve latest (expect version 2) and history
(expect versions 1 then 2); retrieve for a request with no design drafts and confirm a clear
"no design draft" outcome.

**Acceptance Scenarios**:

1. **Given** a change request with three design drafts, **When** the latest is retrieved, **Then**
   version 3 is returned.

2. **Given** a change request with three design drafts, **When** the history is retrieved,
   **Then** all three are returned in ascending version order, none missing or altered.

3. **Given** an existing change request with no design drafts, **When** the latest or history is
   retrieved, **Then** the latest retrieval reports that no design draft exists and the history is
   empty.

4. **Given** an unknown or malformed `ChangeRequestId`, **When** the latest or history is
   retrieved, **Then** a specific error is returned in the same way as for requirements drafts.

---

### User Story 4 - Refuse Ineligible Requests (Priority: P4)

A design request is refused with a specific error when the identifier is malformed or unknown,
the change request is not `Approved`, or the change request has no requirements draft. Nothing is
stored, no audit entry is written, and the content generator is not called.

**Why this priority**: Prevents wasted model usage and designs for work that is not cleared to
proceed or has no specified requirements.

**Independent Test**: Attempt design drafting for a malformed ID, an unknown ID, and change
requests in `Submitted`, `UnderReview`, `Deferred` and `Rejected` status, plus an `Approved`
request with no requirements draft; confirm a distinct specific error each time and that no draft
or audit entry exists.

**Acceptance Scenarios**:

1. **Given** a malformed `ChangeRequestId`, **When** a design draft is requested, **Then** a
   malformed-identifier error is returned and nothing is stored.

2. **Given** an unknown `ChangeRequestId`, **When** a design draft is requested, **Then** a
   not-found error identifying the ID is returned and nothing is stored.

3. **Given** a change request in any status other than `Approved`, **When** a design draft is
   requested, **Then** an error naming the current status as not eligible is returned and nothing
   is stored.

4. **Given** an `Approved` change request with no requirements draft, **When** a design draft is
   requested, **Then** a specific "requirements draft required" error is returned and nothing is
   stored.

5. **Given** any refused request, **When** the generator's call count is inspected, **Then** the
   generator was never invoked.

6. **Given** a design request in progress and a new requirements draft stored before the design
   is saved, **When** the generator returns, **Then** the request is refused with a specific
   error, nothing is stored, and no audit entry is written.

---

### User Story 5 - Fail Safely on Model Problems (Priority: P5)

When the content generator fails, times out, or returns content that is incomplete or malformed,
the system stores nothing, writes no audit entry, and returns a specific error carrying only a
failure category. The change request and all earlier drafts are unchanged.

**Why this priority**: Model output is untrusted; bad output must never become a stored record,
and internals must never leak.

**Independent Test**: Use stand-in generators that fail, time out, omit a section, return an
empty section, and return unparseable content (including text containing a fake endpoint and
credential); confirm each yields a category-only error, nothing stored, no audit entry, and no
model text or configuration values in the error, logs or response.

**Acceptance Scenarios**:

1. **Given** an eligible request and a generator that fails or times out, **When** a design draft
   is requested, **Then** a category-only error is returned and nothing is stored or audited.

2. **Given** a generator output missing a required section or with an empty required section,
   **When** a design draft is requested, **Then** it is treated as incomplete: nothing is stored
   or audited and a category-only error is returned.

3. **Given** a generator output that cannot be interpreted, **When** a design draft is requested,
   **Then** it is treated as malformed in the same way.

4. **Given** any failure, **When** the error, logs and response are inspected, **Then** none
   contain model text, endpoint, deployment name or credential.

5. **Given** model settings are not configured, **When** a design draft is requested, **Then**
   the request is refused with a specific "not configured" error and nothing is stored or
   audited.

---

### User Story 6 - Expose the Actions Over HTTP (Priority: P6)

The three actions are available as HTTP operations whose success and error conventions match
those of the requirements drafting operations.

**Why this priority**: Callers reach the system over HTTP; consistency lets existing callers and
tests treat both kinds of draft alike.

**Independent Test**: Exercise request, latest and history operations in-process against synthetic
data and compare status codes and error shapes with the requirements drafting operations for each
equivalent situation.

**Acceptance Scenarios**:

1. **Given** an eligible change request, **When** a design draft is requested over HTTP, **Then**
   the response conveys the stored draft using the same success conventions as requirements
   drafting.

2. **Given** each refusal and failure situation, **When** the operation is called over HTTP,
   **Then** the response uses the same error conventions as the equivalent requirements drafting
   situation, with a specific error for each distinct cause.

3. **Given** a no-draft or unknown-ID retrieval, **When** called over HTTP, **Then** the outcome
   matches the requirements drafting conventions.

### Edge Cases

- A status other than `Approved` at request time is always refused, whatever it was earlier.
- A requirements draft exists in several versions: the latest is always the basis.
- Two design requests for the same change request in quick succession: each produces a distinct,
  consecutively numbered version with no duplicates or gaps.
- Generator output containing instructions or claims of approval: it is stored only as draft
  content; it never alters status, approval, rejection, deferral or clearance.
- Generator output with extra, unrecognised sections: required sections must still all be present
  and valid; unrecognised content is not stored.
- Very long section content: handled consistently with requirements drafting limits.
- A well-formed but unknown identifier versus a malformed identifier: distinct errors.
- The requirements draft changes (a newer version is stored) while a design draft is being
  generated: the request is refused with a "requirements draft changed" error and nothing is
  stored, so a design is never linked to a requirements version it was not based on.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a caller to request a design draft for a change request by
  `ChangeRequestId` alone.
- **FR-002**: The system MUST produce a design draft only when the change request's status is
  `Approved` and it has at least one requirements draft.
- **FR-003**: The system MUST base each design draft on the change request's own fields and its
  latest requirements draft, and record the version of that requirements draft on the design
  draft.
- **FR-004**: The system MUST store each design draft as a typed record containing: the
  `ChangeRequestId`, the requirements draft version it was based on, a version number, a
  generation timestamp, status `Draft`, the agent name, and the six content sections (summary of
  the proposed approach, affected components, impact and risks, dependencies, rollback
  considerations, open questions).
- **FR-005**: The system MUST record an audit entry of new type `DesignGenerated` for every stored
  design draft, recording the design draft version and a timestamp, and MUST record none for
  refused or failed requests.
- **FR-006**: The system MUST NOT change the change request's status, and MUST NOT approve,
  reject, defer or clear anything, as a result of drafting; generator content has no effect on
  system decisions.
- **FR-007**: The system MUST create a new version on every successful request, numbered
  consecutively per change request from 1, and MUST preserve all earlier versions unchanged.
- **FR-008**: The system MUST allow a caller to retrieve the latest design draft and the full
  design draft history in ascending version order for a change request.
- **FR-009**: The system MUST report a clear "no design draft" outcome for the latest retrieval
  and an empty history for an existing change request with none.
- **FR-010**: The system MUST refuse requests with a malformed or unknown `ChangeRequestId`, a
  change request that is not `Approved`, a change request with no requirements draft, or one
  whose requirements draft changed while the design draft was being generated, each with a
  distinct specific error, storing nothing and (except for the changed-draft case, which is
  detected after generation) not invoking the generator.
- **FR-011**: The system MUST validate generator output for completeness and well-formedness
  (every required section present and non-empty) before storing; on failure, timeout, incomplete
  or malformed output it MUST store nothing, write no audit entry, and return a specific error
  carrying only a failure category. When model settings are not configured it MUST refuse the
  request with a specific "not configured" error and store nothing.
- **FR-012**: The system MUST NOT include model text, endpoints, deployment names or credentials
  in any error, log or response.
- **FR-013**: The system MUST expose request, latest and history as HTTP operations using the same
  success and error conventions as the requirements drafting operations.
- **FR-014**: The Architect capability MUST be self-contained in its own location beside the
  Requirement agent, and MUST NOT require changes to the Requirement agent, the intake and
  approval capabilities, or the shared model-access code other than registration.
- **FR-015**: All behaviour MUST be verifiable without a live model; any live check MUST be
  opt-in and never run by default, and all sample data MUST be synthetic.
- **FR-016**: The system MUST NOT provide human editing of drafts, user interface,
  authentication, durable storage, orchestration between agents, or Developer and Reviewer agent
  behaviour.

### Key Entities

- **Design Draft**: A reviewable design analysis for one change request. Attributes: change
  request identifier, requirements draft version it was based on, version number, generation
  timestamp, status (`Draft`), agent name, and six content sections. Immutable once stored.
- **Design Content Sections**: Summary of the proposed approach; affected components; impact and
  risks; dependencies; rollback considerations; open questions.
- **Change Request**: Existing entity; its status gates drafting and is never changed by it.
- **Requirements Draft**: Existing entity; the latest version is the input basis and is referenced
  by version from the design draft.
- **Audit Entry (`DesignGenerated`)**: New audit entry type recording that a design draft version
  was stored for a change request.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For an eligible synthetic change request, a caller receives a stored, complete
  design draft with all six sections in a single request, and the audit history shows exactly one
  new `DesignGenerated` entry.
- **SC-002**: In 100% of tested ineligible situations (malformed ID, unknown ID, each non-Approved
  status, no requirements draft), the request is refused with a distinct specific error and zero
  drafts, zero audit entries, and zero generator invocations result.
- **SC-003**: In 100% of tested model failure situations (failure, timeout, missing section, empty
  section, unparseable output), zero drafts and zero audit entries are written and the error
  carries only a failure category.
- **SC-004**: Across all tested outcomes, no error, log or response contains the model text, a
  configured endpoint, deployment name or credential.
- **SC-005**: After N successful requests for one change request, N versions exist numbered 1..N
  with all earlier versions unchanged, and the change request's status is identical to before.
- **SC-006**: The full automated test suite for this feature passes with no live model and no
  network access, and every existing test of earlier slices still passes. No existing assertion
  changes except two reviewed ones: the API description test's list of routes gains the three new
  operations, and the recorded API description gains the new operations, schemas and audit event
  value. Nothing else existing may change.
- **SC-007**: Each of the three HTTP operations returns outcomes consistent with the equivalent
  requirements drafting operations in every tested situation.

## Assumptions

- "Cleared to proceed" is exactly status `Approved`; no other status or flag is considered.
- The requirements draft used is the latest version at the time of the request; it is not required
  to have been reviewed or edited by a human.
- The Architect agent reuses the existing shared model-access capability through registration
  only, following the same pattern as the Requirement agent.
- Error conventions, identifier validation rules and content length limits are taken from the
  requirements drafting operations.
- Audit entries follow the existing audit model; `DesignGenerated` is an additional type
  alongside the existing ones.
- Storage remains in-memory and non-durable, consistent with earlier slices.
- Retrieval operations are read-only and do not create audit entries.
- Concurrency handling matches requirements drafting: simultaneous requests yield distinct,
  consecutive versions.
- Later agents (Developer, Reviewer) and orchestration will consume design drafts in future
  slices; their needs do not shape this slice.
