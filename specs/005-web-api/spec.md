# Feature Specification: HTTP API for Change Requests

**Feature Branch**: `feature/web-api`

**Created**: 2026-10-12

**Status**: Draft

**Input**: User description: "HTTP API for change requests. A user interacts with the system over HTTP using JSON. Operations: (1) submit a change request; (2) retrieve a change request by its ChangeRequestId; (3) list its audit history in order; (4) record an approval decision (Approve, Reject or Defer) with approver name and reason; (5) ask whether a change request is cleared to proceed; (6) request a requirements draft for a change request; (7) retrieve the latest requirements draft and the full draft history. Each operation exposes exactly the behaviour of the existing intake, approval and drafting capabilities; the API adds no business rules of its own. Validation failures are returned as structured responses naming each failing field, with a client-error status. An unknown or malformed ChangeRequestId returns a not-found or invalid-identifier response. A decision or draft request that the business rules refuse (for example a request already in a final state) returns a conflict-style response with the specific reason. Draft generation failures (model unreachable, timed out, malformed output) return a distinct service-failure response carrying only the failure category, never the model's text, the endpoint, or any configuration value. An unexpected failure returns a generic error that never leaks internal details, stack traces or secrets. Drafting uses the real model-backed generator when its configuration is present; when it is absent the API still starts and every operation except drafting works, and drafting reports clearly that it is not configured. No endpoint, deployment name or credential appears in any response, log or the API's published description. Every response is JSON. The API publishes a machine-readable description of itself and a browsable page so a person can try each operation by hand. The API listens only on the local machine by default, because authentication is out of scope. Data is held in memory for the lifetime of the process. The automated test suite exercises the API without any real model call. Out of scope: authentication, authorization, persistent storage, a user interface, new agents, cross-origin browser access. All sample data is synthetic."

## Clarifications

### Session 2026-10-12

- Q: Should retrieving drafts be one operation returning latest plus history, or two separate retrievals? → A: Two separate retrievals (latest draft; full history), together counted as operation 7.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Submit and Retrieve a Change Request (Priority: P1)

A caller submits a change request as JSON and receives back the stored request with its
`ChangeRequestId`. The caller can later retrieve that request by identifier and list its audit
history in the order recorded. Submission and retrieval behave exactly as the intake capability
(slice 001) defines.

**Why this priority**: Every other operation depends on a change request existing. This story
alone delivers a usable intake service over HTTP.

**Independent Test**: Start the API with no model configuration, submit a synthetic change
request, then retrieve it and list its audit history; confirm the content matches what the
intake capability would return for the same input.

**Acceptance Scenarios**:

1. **Given** a valid synthetic change request body, **When** it is submitted, **Then** the
   response is JSON, indicates creation, and contains the stored request including its
   `ChangeRequestId` and initial status.
2. **Given** a stored change request, **When** it is retrieved by `ChangeRequestId`, **Then** the
   response is JSON containing the same request.
3. **Given** a stored change request with audit entries, **When** its audit history is listed,
   **Then** the entries are returned in the order they were recorded.
4. **Given** a submission with one or more invalid fields, **When** it is submitted, **Then** the
   response has a client-error status and names every failing field, and nothing is stored.
5. **Given** an identifier that is well-formed but unknown, **When** retrieval or history is
   requested, **Then** the response is a not-found response.
6. **Given** an identifier that is not a valid identifier at all, **When** retrieval or history is
   requested, **Then** the response is an invalid-identifier response, distinct from not-found.

---

### User Story 2 - Record a Decision and Check Clearance (Priority: P2)

A caller records an approval decision (Approve, Reject or Defer) with approver name and reason
for a change request, and can ask whether a change request is cleared to proceed. Decision and
clearance behave exactly as the approval gate capability (slice 002) defines.

**Why this priority**: The human approval gate is the core control of the system; exposing it is
the next most valuable step after intake.

**Independent Test**: Submit a synthetic change request, check clearance (not cleared), record
an Approve decision, check clearance again (cleared), then attempt a second decision and confirm
a conflict-style refusal with the specific reason.

**Acceptance Scenarios**:

1. **Given** a submitted change request, **When** a valid decision is recorded, **Then** the
   response is JSON and reflects the resulting status and the audit entry recorded by the
   approval gate.
2. **Given** a decision with missing or invalid fields (for example Reject without a reason),
   **When** it is sent, **Then** the response has a client-error status and names every failing
   field, and the change request is unchanged.
3. **Given** a change request already in a final state, **When** another decision is sent,
   **Then** the response is a conflict-style response carrying the specific reason given by the
   approval gate, and nothing changes.
4. **Given** an unknown or malformed `ChangeRequestId`, **When** a decision or clearance check is
   requested, **Then** the response is not-found or invalid-identifier respectively.
5. **Given** any change request, **When** clearance is asked, **Then** the response states
   whether it is cleared to proceed, exactly as the approval gate decides.

---

### User Story 3 - Request and Retrieve Requirements Drafts (Priority: P3)

A caller requests a requirements draft for a change request, and can retrieve the latest draft
and the full draft history. Drafting, versioning and validation behave exactly as the drafting
capability (slices 003 and 004) defines, including use of the real model-backed generator when
it is configured.

**Why this priority**: Drafting is the first agent-assisted capability and depends on intake;
it is valuable but not required for the approval workflow to function.

**Independent Test**: With a stand-in generator, submit a synthetic change request, request a
draft, retrieve the latest draft and the history, and request a second draft to confirm versions
accumulate; then confirm each refusal and failure case returns its specified response.

**Acceptance Scenarios**:

1. **Given** a draftable change request and a working generator, **When** a draft is requested,
   **Then** the response is JSON containing the stored draft with its version.
2. **Given** a change request with drafts, **When** the latest draft is retrieved and, separately,
   the full history is retrieved, **Then** the first returns the highest version and the second
   returns every version, in version order.
3. **Given** a change request the business rules refuse to draft for (for example one in a state
   where drafting is not allowed), **When** a draft is requested, **Then** the response is a
   conflict-style response carrying the specific reason, and nothing is stored.
4. **Given** a generator failure (unreachable, timed out, response too large, malformed output),
   **When** a draft is requested, **Then** the response is a distinct service-failure response
   carrying only the failure category, and no draft, version or audit entry is created.
5. **Given** no model configuration, **When** a draft is requested, **Then** the response clearly
   states that drafting is not configured, while all other operations continue to work.
6. **Given** a change request with no drafts, **When** the latest draft is retrieved, **Then** the
   response reports that no draft exists (distinct from an unknown change request), and the full
   history is empty.

---

### User Story 4 - Safe, Self-Describing, Local-Only Service (Priority: P4)

A person starting the API can discover and try every operation by hand through a published
machine-readable description and a browsable page. Every response is JSON, unexpected failures
reveal nothing internal, no endpoint, deployment name or credential ever appears in any
response, log or the published description, and the service only accepts connections from the
local machine by default.

**Why this priority**: These are cross-cutting guarantees that constrain how every other story
is delivered; they are a guard rather than new capability.

**Independent Test**: Retrieve the published description and confirm it lists all seven
operations and contains no configuration value; force an unexpected failure with a stand-in
component and confirm the generic error; inspect responses and logs from the whole automated
suite for any configured value.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** the machine-readable description is requested, **Then**
   it describes all seven operations, their inputs and their documented responses.
2. **Given** the API is running, **When** the browsable page is opened, **Then** a person can
   issue each operation by hand.
3. **Given** an unexpected failure inside any operation, **When** the response is inspected,
   **Then** it is a generic JSON error with no stack trace, internal message, path or secret.
4. **Given** model configuration is present, **When** all responses, logs and the published
   description are inspected, **Then** none contains the endpoint, deployment name or any
   credential.
5. **Given** default settings, **When** the API starts, **Then** it listens only on the local
   machine.
6. **Given** a request with an unsupported route, method or non-JSON body, **When** it is sent,
   **Then** the response is still JSON and uses a client-error status.

---

### Edge Cases

- A request body that is not valid JSON, is empty, or has the wrong shape yields a client-error
  response in the same structured format as validation failures; it is never an unexpected error.
- A submission or decision with several problems reports every failing field in one response.
- A decision body naming a decision value outside Approve, Reject or Defer is a validation
  failure naming that field.
- Two requests arrive at once for the same change request (for example two decisions); the
  business rules decide the outcome, so exactly one succeeds and the other receives a
  conflict-style response.
- A draft request for a change request whose generation previously failed is treated as any new
  request; failures leave no trace, so there is nothing to recover from.
- A very large request body is refused with a client-error response rather than processed.
- Model configuration is present but invalid (missing, blank or placeholder value): the API
  reports drafting as not configured, naming no value, and continues to serve all other
  operations.
- The failure category reported for draft failures never includes text returned by the model,
  the endpoint, or any configuration value.
- Restarting the process loses all data; this is expected and not an error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose, over HTTP using JSON, exactly these seven operations: submit a
  change request; retrieve a change request by `ChangeRequestId`; list its audit history in
  recorded order; record an approval decision (Approve, Reject or Defer) with approver name and
  reason; check whether a change request is cleared to proceed; request a requirements draft;
  retrieve requirements drafts. Operation 7 is exposed as two separate retrievals, one for the
  latest draft and one for the full ordered draft history, mirroring the two reads of slice 003;
  no combined retrieval is provided.
- **FR-002**: Each operation MUST delegate to the existing intake, approval gate and drafting
  capabilities and expose exactly their behaviour. The API MUST NOT add, change or bypass any
  business rule, validation rule, state transition, or audit behaviour.
- **FR-003**: Every response, including errors, unsupported routes and unsupported methods,
  MUST be JSON.
- **FR-004**: Validation failures MUST return a client-error status and a structured body that
  names every failing field with a reason, as reported by the underlying capability.
- **FR-005**: A well-formed but unknown `ChangeRequestId` MUST return a not-found response; a
  `ChangeRequestId` that is not a valid identifier MUST return an invalid-identifier
  response that is distinguishable from not-found.
- **FR-006**: A decision or draft request refused by the business rules (for example the request
  is already in a final state, or is not in a state that permits drafting) MUST return a
  conflict-style response that carries the specific reason supplied by the capability.
- **FR-007**: A draft generation failure (model unreachable or authentication unavailable, timed
  out, response too large, malformed output) MUST return a service-failure response that is
  distinct from client errors, conflicts and unexpected errors, and carries only the failure
  category. It MUST NOT include the prompt, model text, endpoint, deployment name, identity
  detail or any configuration value.
- **FR-008**: An unexpected failure MUST return a generic error response with no internal
  detail, stack trace, file path, exception text or secret, and MUST NOT terminate the service.
- **FR-009**: When model configuration is present and valid, drafting MUST use the real
  model-backed generator. When it is absent or invalid, the API MUST still start, every
  operation except drafting MUST work, and a draft request MUST return a clear "drafting is not
  configured" response that names no value.
- **FR-010**: No model endpoint, deployment name, credential, prompt or model output MUST appear
  in any response, any log, or the published description of the API.
- **FR-011**: The API MUST publish a machine-readable description of itself covering all seven
  operations, their inputs, and their documented success and error responses, and a browsable
  page from which a person can try each operation by hand.
- **FR-012**: By default the API MUST accept connections from the local machine only. Exposing
  it more widely MUST require an explicit operator setting.
- **FR-013**: All data MUST be held in memory for the lifetime of the process; the API MUST NOT
  write change requests, audit entries or drafts to any persistent store.
- **FR-014**: The API MUST NOT implement authentication or authorization, and MUST NOT enable
  cross-origin browser access.
- **FR-015**: The API MUST NOT introduce any new agent, user interface, or new business capability.
- **FR-016**: Request bodies MUST be bounded in size; an oversized body MUST be refused with a
  client-error response.
- **FR-017**: The automated test suite MUST exercise every operation and every error class
  through the HTTP interface using a stand-in generator, MUST NOT call a real model, and MUST
  NOT require network access, credentials or model configuration.
- **FR-018**: All sample data, fixtures and examples in the published description MUST be
  fully synthetic, and no credential MUST be stored in the repository.

### Key Entities

- **Change Request** (existing, slices 001/002): The submitted request, its status and its
  audit history; submitted, retrieved and decided upon through the API.
- **Audit Entry** (existing): An ordered record of something that happened to a change request.
- **Approval Decision** (existing, slice 002): Approve, Reject or Defer, with approver name and
  reason.
- **Requirements Draft** (existing, slices 003/004): A versioned draft with summary, functional
  requirements, acceptance criteria, assumptions and open questions.
- **Error Response**: The JSON body for every non-success outcome, with a category
  (validation, invalid identifier, not found, conflict, drafting not configured, generation
  failure, unexpected), a human-readable message, and, where relevant, the failing fields or the
  failure category. Never contains internal or configuration detail.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person can submit a change request and see its stored details, history and
  clearance state using only the browsable page, in under 5 minutes, on first use.
- **SC-002**: 100% of the seven operations return results identical in content to the
  corresponding existing capability for the same input.
- **SC-003**: 100% of validation failures name every failing field in a single response.
- **SC-004**: 100% of responses across the automated suite, including every error class, are
  JSON.
- **SC-005**: 0 occurrences of an endpoint, deployment name, credential, prompt, model output,
  stack trace or internal path in any response, log line or published description across the
  automated suite.
- **SC-006**: With no model configuration, 100% of non-drafting operations succeed and 100% of
  draft requests report that drafting is not configured.
- **SC-007**: The full automated suite passes with no network, no credentials and no model
  configuration, and makes zero real model calls.
- **SC-008**: Each simple operation (submit, retrieve, history, decision, clearance, draft
  retrieval) responds within 1 second under single-user local use.

## Assumptions

- This slice adds only a delivery layer; all business rules, validation, versioning and audit
  behaviour are reused unchanged from slices 001–004.
- The API is single-operator and local; no authentication means it is unsuitable for exposure
  beyond the local machine, which is why local-only listening is the default.
- Drafting configuration is the run-time configuration defined in slice 004; its absence is a
  supported state for the API, not a startup error (the generator's own fail-fast rule in
  slice 004 is preserved by not creating the generator when configuration is absent).
- Specific response status codes, route names and body field names are planning decisions; the
  spec fixes only the categories and their distinguishability.
- The web framework and the format of the machine-readable description are planning decisions.
- Semantic Kernel and orchestration across agents remain deferred; this slice calls the existing
  drafting capability only.
- Concurrent requests are supported; consistency under concurrency is provided by the existing
  capabilities, and the API does not add its own locking rules.
- Approver identity is accepted as a plain name string, as in slice 002.
