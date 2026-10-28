# Feature Specification: HTTP Operations To Retrieve The Latest Review And The Review History

**Feature Branch**: `feature/reviewer-retrieval-operations`

**Created**: 2026-10-26

**Status**: Draft

**Input**: User description: "HTTP operations to retrieve the latest review and the review history of a
change request. Actor: a caller of the HTTP interface reading back reviews that already exist. Action:
(1) retrieve the latest review; (2) list the full review history in version order. Both expose the
existing review capability and add no rule of their own. Input: the ChangeRequestId in the address, no
body. Rules: same conventions as the existing retrieval operations for drafts, designs and proposals;
malformed identifier is invalid; unknown identifier is not found; latest on a change request with no
review is a distinct 'no review' outcome; history on a change request with no review is an empty list;
reviews in version order, oldest first for the history, each exactly as stored; read-only; work whether
or not reviewing is configured; nothing from the model, sample codebase, endpoint, deployment name or
credential appears in any response, log or error. Outcomes: JSON; latest returns the highest version;
history returns every review with version 1 unchanged after later versions exist; the response to a
successful review request now carries a link to the latest-review operation (the only change to an
existing response); the published description lists both operations with their possible responses;
everything else identical and existing tests pass unchanged except those enumerating operations, the
published description, or the response to a successful review request. Out of scope: changes to how
reviews are requested or produced, workflow/status/automatic-advancement changes, human acceptance,
filtering or paging, authentication, any user interface. All sample data is synthetic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Retrieve The Latest Review (Priority: P1)

A caller asks for the latest review of a change request, with the ChangeRequestId in the address and no
body. The response is the stored review with the highest version, as JSON, exactly as stored.

**Why this priority**: Reading back the current review is the core need; without it a review requested
over HTTP cannot be seen over HTTP.

**Independent Test**: Store two reviews for a change request; request the latest; check the body equals
the stored version 2 review and nothing changed.

**Acceptance Scenarios**:

1. **Given** a change request with reviews at versions 1 and 2, **When** the latest is requested, **Then**
   a success response returns the version 2 review as JSON, exactly as stored.
2. **Given** a change request with one review, **When** the latest is requested, **Then** that review is
   returned.
3. **Given** reviewing is not configured on the server, **When** the latest is requested for a change
   request with a stored review, **Then** the review is still returned.
4. **Given** any successful retrieval, **When** the change request, workflow and stored reviews are
   inspected, **Then** nothing has changed.

---

### User Story 2 - List The Review History (Priority: P1)

A caller asks for the full review history of a change request. The response lists every stored review,
oldest first, each exactly as stored. Version 1 is unchanged after later versions exist.

**Why this priority**: Reviews are versioned and advisory; seeing how they evolved is the other half of
reading them back.

**Independent Test**: Store three reviews; request the history; check three reviews in order 1, 2, 3 and
that version 1 equals what was first stored.

**Acceptance Scenarios**:

1. **Given** reviews at versions 1, 2 and 3, **When** the history is requested, **Then** all three are
   returned in version order, oldest first, each exactly as stored.
2. **Given** later versions exist, **When** the history is requested, **Then** version 1 is identical to
   when it was first stored.
3. **Given** an existing change request with no review, **When** the history is requested, **Then** an
   empty list is returned with a success response.
4. **Given** reviewing is not configured, **When** the history is requested, **Then** the stored reviews
   are still returned.

---

### User Story 3 - Refusals Follow The Existing Conventions (Priority: P1)

A malformed identifier is refused as invalid. An unknown identifier is reported as not found. For the
latest operation only, a change request that exists but has no review is reported as a distinct "no
review" outcome. Each returns its documented status and error code, in the shared error shape, with no
stack trace or internal detail.

**Why this priority**: Callers must tell "bad request", "no such change request" and "nothing reviewed
yet" apart, as they can for drafts, designs and proposals.

**Independent Test**: Request both operations with a malformed id, an unknown id, and an existing change
request with no review; check each status and error code against the documented ones.

**Acceptance Scenarios**:

1. **Given** a malformed identifier, **When** either operation is requested, **Then** it is refused as
   invalid.
2. **Given** a well-formed identifier of no change request, **When** either operation is requested,
   **Then** it is reported as not found.
3. **Given** an existing change request with no review, **When** the latest is requested, **Then** the
   distinct "no review" outcome is returned, different from not found.
4. **Given** any refusal, **When** the response and logs are inspected, **Then** they hold no stack trace,
   internal detail, model text, sample code, endpoint, deployment name or credential.

---

### User Story 4 - A Successful Review Request Links To The Latest Review (Priority: P2)

The response to a successful review request now carries a link to the latest-review operation, as the
corresponding responses for drafts, designs and proposals do. This is the only change to an existing
response.

**Why this priority**: It makes the review operations discoverable in the same way as the other agents'
operations, but depends on the retrieval operation existing.

**Independent Test**: Request a review; check the link points at the latest-review operation for that
change request and that the status, body and other headers are as before.

**Acceptance Scenarios**:

1. **Given** a successful review request, **When** the response is inspected, **Then** it carries a link
   to the latest-review operation of that change request.
2. **Given** that response, **When** compared to before this feature, **Then** only the link differs.

---

### User Story 5 - The Interface Description Lists Them, Everything Else Is Unchanged (Priority: P2)

The published description of the interface lists both new operations with their possible responses.
Every other response, status code, body, header and error code is identical to before.

**Why this priority**: Callers rely on the published description, and existing behaviour must not drift.

**Independent Test**: Read the published description and check both operations and every documented
response are present; run the existing automated tests and check they pass with assertions unchanged
except those enumerating operations, the published description, or the response to a successful review
request.

**Acceptance Scenarios**:

1. **Given** the published description, **When** read, **Then** both operations appear with their success,
   invalid, not found and (latest only) no-review responses.
2. **Given** the existing automated tests, **When** run, **Then** all pass, with only the permitted
   assertions changed.

---

### Edge Cases

- A request body sent to either operation: ignored; the operations take no input but the address.
- Reviews stored for a change request alongside drafts, designs and proposals: only reviews are returned.
- Storage unavailable when reading: reported with the same shape as for the other retrieval operations; no
  detail leaks.
- Two reads while a new review is being stored: each sees a consistent set of whole reviews, never a
  partial one.
- Reviewing not configured: has no effect on either operation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST offer an HTTP operation returning the latest review of a change request, and
  another listing its full review history, each taking the ChangeRequestId in the address and no body.
- **FR-002**: Both operations MUST expose the existing review capability and MUST add no rule of their own.
- **FR-003**: Both operations MUST follow the conventions of the existing retrieval operations for drafts,
  designs and proposals (JSON naming, string enums, shared error body, identifier handling).
- **FR-004**: A malformed identifier MUST be refused as invalid; an unknown identifier MUST be reported as
  not found.
- **FR-005**: The latest operation MUST report a change request that exists but has no review as a
  distinct "no review" outcome.
- **FR-006**: The history operation MUST return an empty list for a change request that exists but has no
  review.
- **FR-007**: The latest operation MUST return the review with the highest version; the history operation
  MUST return every review, oldest first.
- **FR-008**: Each review MUST be returned exactly as stored; version 1 MUST be unchanged after later
  versions exist.
- **FR-009**: Both operations MUST be read-only and MUST change nothing.
- **FR-010**: Both operations MUST work whether or not reviewing is configured on the server.
- **FR-011**: No response, log or error MAY contain model text, sample codebase content, the endpoint, the
  deployment name or any credential, nor any stack trace or internal detail.
- **FR-012**: A successful retrieval MUST return JSON.
- **FR-013**: The response to a successful review request MUST carry a link to the latest-review
  operation; this MUST be the only change to an existing response.
- **FR-014**: The published description of the interface MUST list both operations with their possible
  responses.
- **FR-015**: Every existing response, status code, body, header and error code, apart from the link in
  FR-013 and the additions in FR-014, MUST remain identical, and every existing automated test MUST pass
  with assertions unchanged except those enumerating the operations, the published description, or the
  response to a successful review request.

### Key Entities

- **Review**: The existing stored, versioned, advisory review of a change proposal; returned unchanged.
- **Review History**: The ordered set of all reviews of one change request, oldest first; possibly empty.
- **Error Report**: The shared error shape with a code and fixed message, used for invalid, not found and
  no-review outcomes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a change request with N stored reviews, 100% of latest requests return the review with
  version N, and 100% of history requests return exactly N reviews in ascending version order.
- **SC-002**: 100% of returned reviews are identical to what was stored, including version 1 after later
  versions exist.
- **SC-003**: 100% of tested malformed, unknown and no-review cases return their documented status and
  error code with 0 stack traces or internal details.
- **SC-004**: With reviewing not configured, 100% of tested retrievals behave identically to the
  configured case.
- **SC-005**: Across all tested responses, logs and errors, 0 contain model text, sample code, endpoint,
  deployment name or credential.
- **SC-006**: 0 stored reviews, change request statuses or workflow states differ after any number of
  retrievals.
- **SC-007**: 100% of existing automated tests pass, with changes limited to assertions that enumerate
  operations, the published description, or the response to a successful review request.
- **SC-008**: The published description lists both operations and every documented response.

## Assumptions

- The review capability, its storage and its internal retrieval already exist from the Reviewer slices and
  are not changed.
- "Same conventions" means the proposal retrieval operations' route style, error body and code pattern;
  exact routes, status codes and error code names are fixed at planning, mirroring the proposal
  operations, with a distinct code for "no review".
- The link on the review-request response mirrors the link the proposal-request response carries to its
  latest-proposal operation.
- The history is returned whole, without filtering or paging.
- Authentication is absent, as for every existing operation.
- All sample data is synthetic.
