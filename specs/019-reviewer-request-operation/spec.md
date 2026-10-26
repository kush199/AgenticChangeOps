# Feature Specification: HTTP Operation To Request A Review

**Feature Branch**: `feature/reviewer-request-operation`

**Created**: 2026-10-26

**Status**: Draft

**Input**: User description: "An HTTP operation to request a review of a change proposal. Actor: a caller of
the HTTP interface requesting a review for an existing change request. Action: request a review; the
operation exposes the existing review capability and adds no rule of its own. Input: the ChangeRequestId
in the address, no request body. Rules: same conventions as the existing design and proposal operations;
checks in order: malformed identifier is invalid, unknown identifier is not found, reviewing not
configured is a distinct 'not configured' condition, only then does the review capability decide
eligibility, outdated inputs and everything else. Ineligible or outdated is a conflict with the specific
reason; model unreachable, timeout, unusable output or rejected findings is a distinct service failure
carrying only the failure category. Nothing from the model, sample codebase, endpoint, deployment name or
credential appears in any response, log or error. Never changes a status and never approves, rejects,
defers or clears anything. Outcomes: success returns the stored review as JSON with a created status and
no retrieval link; the published interface description lists the operation; everything else is unchanged.
Failures store nothing; overlapping requests for one change request produce versions with no gap and no
duplicate. Out of scope: retrieving the latest review or history, workflow/status/automatic-advancement
changes, human acceptance, changes to how reviews are produced, authentication, any user interface. All
sample data is synthetic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Request A Review Over HTTP (Priority: P1)

A caller sends a review request for an existing change request, with the ChangeRequestId in the address
and no body. The existing review capability produces and stores a review, and the caller receives that
stored review as JSON with a created status. The response carries no link to a retrieval operation.

**Why this priority**: This is the whole feature: making the existing review capability reachable over
HTTP, like designs and proposals.

**Independent Test**: With a scripted model, request a review for an eligible change request; check the
created status, that the body equals the stored review, that no retrieval link is present, and that the
change request status is unchanged.

**Acceptance Scenarios**:

1. **Given** an eligible change request and reviewing configured, **When** a review is requested, **Then**
   the response has a created status and a JSON body holding the stored review (its version, the versions
   it was based on, generation time, status, agent name, system-checked result, findings and open
   questions).
2. **Given** that response, **When** its headers are inspected, **Then** there is no link to a retrieval
   operation.
3. **Given** a successful request, **When** the change request and workflow are inspected, **Then** no
   status changed and nothing was approved, rejected, deferred or cleared.
4. **Given** two successful requests in turn, **When** the responses are compared, **Then** versions are 1
   and 2 and version 1 is preserved.

---

### User Story 2 - Checks Happen In A Fixed Order (Priority: P1)

The operation checks, in order: the identifier is well formed (else refused as invalid); the change
request exists (else not found); reviewing is configured on the server (else a distinct "not configured"
condition); only then does the review capability decide eligibility, outdated inputs and everything else.

**Why this priority**: A predictable order makes each error unambiguous and keeps the model unreachable
when an earlier check fails.

**Independent Test**: Combine failing conditions (malformed id on an unconfigured server, unknown id on an
unconfigured server, an ineligible request on an unconfigured server) and check the earliest check wins
each time and the model is never called.

**Acceptance Scenarios**:

1. **Given** a malformed identifier, **When** requested, **Then** it is refused as invalid, whatever the
   configuration.
2. **Given** a well-formed identifier of no change request, **When** requested, **Then** it is reported as
   not found, even if reviewing is not configured.
3. **Given** an existing change request and reviewing not configured, **When** requested, **Then** the
   distinct "not configured" condition is reported, even if the change request is ineligible.
4. **Given** reviewing configured, **When** requested, **Then** eligibility and outdated-input decisions
   come from the review capability alone, with no rule added by this operation.

---

### User Story 3 - Refusals And Failures Are Distinct And Store Nothing (Priority: P1)

A request the review capability refuses because the change request is not eligible, or its inputs are
outdated, is reported as a conflict naming the specific reason. A review that fails because the model is
unreachable, times out or returns unusable output, or whose findings are rejected, is reported as a
distinct service failure carrying only the failure category. Each returns its documented status and error
code and stores nothing.

**Why this priority**: Callers must tell "fix your request" from "try again later", without any leak.

**Independent Test**: Provoke each refusal and failure with a scripted model; check status, error code and
specific reason or category, that nothing was stored, and that no response, log or error holds model text,
sample file content, endpoint, deployment name or credential.

**Acceptance Scenarios**:

1. **Given** an ineligible change request, **When** requested, **Then** a conflict is returned naming the
   specific reason and nothing is stored.
2. **Given** a design or proposal that is outdated, **When** requested, **Then** a conflict is returned
   naming that reason and nothing is stored.
3. **Given** a model that is unreachable, times out or returns unusable output, **When** requested,
   **Then** a service failure is returned carrying only the failure category and nothing is stored.
4. **Given** findings that are rejected, **When** requested, **Then** a service failure distinct from the
   other failures is returned carrying only the category and nothing is stored.
5. **Given** any refusal or failure, **When** the response, logs and errors are inspected, **Then** they
   contain no model text, sample code, endpoint, deployment name or credential.

---

### User Story 4 - Overlapping Requests Keep Versions Whole (Priority: P2)

Two review requests for the same change request that overlap in time produce distinct versions with no
gap and no duplicate.

**Why this priority**: HTTP makes simultaneous callers possible; version integrity must hold.

**Independent Test**: Fire overlapping requests for one change request; check the stored versions are
consecutive starting from the first and none repeats.

**Acceptance Scenarios**:

1. **Given** N overlapping successful requests, **When** all finish, **Then** the stored versions are
   exactly 1..N with no repeat.
2. **Given** an overlapping request that is refused or fails, **When** all finish, **Then** it leaves no
   gap in the versions.

---

### User Story 5 - The Interface Description Lists It, Everything Else Is Unchanged (Priority: P2)

The published description of the interface lists the new operation with its possible responses. Every
other response, status code, body, header and error code is identical to before.

**Why this priority**: Callers rely on the published description, and existing behaviour must not drift.

**Independent Test**: Read the published description and check the new operation and each documented
response are present; run the existing automated tests and check they pass with assertions unchanged
except those that enumerate operations or the published description.

**Acceptance Scenarios**:

1. **Given** the published description, **When** read, **Then** the new operation appears with the created
   response and every refusal and failure response above.
2. **Given** the existing automated tests, **When** run, **Then** all pass, with only
   operation-enumerating or description-enumerating assertions changed.

---

### Edge Cases

- A review request carrying a body: the body is ignored; the operation takes no input but the address.
- Reviewing not configured while the identifier is malformed or unknown: the earlier check wins.
- Storage unavailable or a version conflict during storing: reported with the same shapes as for designs
  and proposals, nothing stored.
- Repeated requests for an unchanged change request: each creates a new version.
- The sample codebase unavailable: handled by the existing review capability (check recorded as not
  performed); the operation adds no rule.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST offer an HTTP operation to request a review for a change request, taking the
  ChangeRequestId in the address and no body.
- **FR-002**: The operation MUST expose the existing review capability and MUST add no eligibility,
  freshness or other rule of its own.
- **FR-003**: The operation MUST follow the conventions of the existing design and proposal operations
  (JSON naming, string enums, shared error body, identifier handling).
- **FR-004**: The operation MUST refuse a malformed identifier as invalid before any other check.
- **FR-005**: The operation MUST report an unknown identifier as not found, before the configuration check.
- **FR-006**: The operation MUST report reviewing not configured on the server as a distinct "not
  configured" condition, after the not-found check and before any review decision.
- **FR-007**: Only after those checks MUST the review capability decide eligibility, outdated inputs and
  everything else.
- **FR-008**: A refusal by the review capability for ineligibility or outdated inputs MUST be reported as a
  conflict carrying the specific reason.
- **FR-009**: A model that is unreachable, times out or returns unusable output, and findings that are
  rejected, MUST each be reported as a service failure, distinct from the conflict, carrying only the
  failure category.
- **FR-010**: No response, log or error MAY contain model text, sample codebase content, the endpoint, the
  deployment name or any credential.
- **FR-011**: The operation MUST NOT change any change request status and MUST NOT approve, reject, defer
  or clear anything.
- **FR-012**: A successful request MUST return the stored review as JSON with a created status and MUST NOT
  include a link to a retrieval operation.
- **FR-013**: Every refusal and failure MUST return its documented status and error code and MUST store
  nothing.
- **FR-014**: Overlapping review requests for the same change request MUST produce versions with no gap and
  no duplicate.
- **FR-015**: The published description of the interface MUST list the operation with each possible
  response.
- **FR-016**: Every existing response, status code, body, header and error code MUST remain identical, and
  every existing automated test MUST pass with its assertions unchanged except those enumerating the
  operations or the published description.

### Key Entities

- **Review**: The existing stored, versioned, advisory review of a change proposal; returned unchanged by
  this operation.
- **Review Request**: A caller's request identified only by the ChangeRequestId; carries no body and
  creates at most one new review version.
- **Error Report**: The shared error shape with a code, fixed message, optional fields naming the failing
  rule, and, for service failures, only a failure category.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For an eligible change request, 100% of review requests return the stored review with a
  created status and no retrieval link.
- **SC-002**: Across every combination of failing conditions tested, the earliest check in the fixed order
  decides the response in 100% of cases, and the model is reached in 0% of cases where an earlier check
  failed.
- **SC-003**: 100% of tested refusals and failures return their documented status and error code and leave
  0 stored reviews and 0 audit entries behind.
- **SC-004**: Across all tested responses, logs and errors, 0 contain model text, sample code, endpoint,
  deployment name or credential.
- **SC-005**: For N simultaneous successful requests, stored versions are exactly 1..N in every run
  tested.
- **SC-006**: 100% of existing automated tests pass, with changes limited to assertions that enumerate
  operations or the published description.
- **SC-007**: The published description lists the new operation and every documented response.

## Assumptions

- The review capability, its eligibility rules, failure categories and storage already exist from the
  Reviewer slice and are not changed.
- "Same conventions" means the proposal operations' route style, error body and code pattern; the exact
  status codes and error code names are fixed at planning, mirroring the proposal operations.
- The "not configured" condition uses a distinct error code for reviewing, as the proposal operation does
  for proposals.
- Retrieval of reviews stays on the internal service only; HTTP retrieval is a later slice.
- Authentication is absent, as for every existing operation.
- All sample data is synthetic.
