# Feature Specification: Change Request Intake

**Feature Branch**: `001-change-request-intake`

**Created**: 2026-10-10

**Status**: Draft

**Input**: User description: "Change Request intake. A user submits a software change request
(title, description, requester, affected system, priority, change type such as feature, bugfix
or config change). The system validates it, assigns a unique ChangeRequestId, persists it as a
typed record with a timestamp and an initial status of Submitted, and records an audit entry
for the submission. Invalid requests are rejected with clear, specific validation errors and
nothing is persisted. A submitted request can be retrieved by its ChangeRequestId, and the
full audit history for it can be listed in order. This slice has no AI agents and no LLM calls.
All sample data is synthetic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Submit Valid Change Request (Priority: P1)

A user provides all required fields for a change request. The system validates the input,
assigns a unique identifier, stores the record, creates an audit trail entry, and returns
the assigned identifier to the caller.

**Why this priority**: This is the core intake action. Without it, no other behaviour in
this slice is reachable. It is the single most valuable outcome to demonstrate.

**Independent Test**: Can be fully tested by submitting a well-formed request and confirming
a ChangeRequestId is returned, the record is retrievable, and one audit entry exists.

**Acceptance Scenarios**:

1. **Given** a complete, valid change request payload (all fields populated within allowed
   values), **When** the user submits it, **Then** the system returns the full persisted
   `ChangeRequest` record (including the newly assigned `ChangeRequestId`, `SubmittedAt`
   timestamp, and status `Submitted`), and one audit entry of type `Submitted` is recorded.

2. **Given** a valid payload where all optional whitespace is trimmed, **When** submitted,
   **Then** the stored values reflect trimmed input and the record is persisted normally.

---

### User Story 2 - Reject Invalid Change Request (Priority: P2)

A user submits a change request that fails one or more validation rules. The system rejects
the submission, returns specific per-field error messages, and stores nothing.

**Why this priority**: Data quality and auditability depend on the store containing only
valid records. Rejection behaviour is a non-negotiable correctness boundary.

**Independent Test**: Can be tested by submitting requests with various invalid fields and
confirming the error messages name the specific failing fields and no record is persisted.

**Acceptance Scenarios**:

1. **Given** a payload with a missing required field (e.g., empty title), **When** submitted,
   **Then** the system returns a validation error that names the field (`Title`) and explains
   why it failed (e.g., "Title is required"), and no record is stored.

2. **Given** a payload with an unrecognised priority value, **When** submitted, **Then** the
   system returns a validation error identifying the `Priority` field and listing accepted
   values, and no record is stored.

3. **Given** a payload with multiple invalid fields simultaneously, **When** submitted,
   **Then** the system returns one error message per failing field (not just the first), and
   no record is stored.

4. **Given** a payload with a field exceeding its maximum allowed length, **When** submitted,
   **Then** the system returns a validation error naming the field and stating the limit, and
   no record is stored.

---

### User Story 3 - Retrieve Change Request by ID (Priority: P3)

A user provides a ChangeRequestId previously returned by the system. The system returns the
full record with all original fields and the current status.

**Why this priority**: Retrieval is required to confirm intake succeeded and to support
downstream consumers reading the persisted record. It depends on Story 1.

**Independent Test**: Can be tested immediately after a successful submission by fetching
the returned ChangeRequestId and confirming all fields match the submitted values.

**Acceptance Scenarios**:

1. **Given** a ChangeRequestId that was returned by a prior successful submission, **When**
   the user retrieves it, **Then** the system returns the complete record with all original
   field values, the assigned timestamp, and status `Submitted`.

2. **Given** a ChangeRequestId that does not exist in the store, **When** the user attempts
   retrieval, **Then** the system returns a structured not-found response (no exception
   leaked to caller) identifying the unknown ID.

---

### User Story 4 - List Audit History for a Change Request (Priority: P4)

A user provides a ChangeRequestId and receives the complete chronological list of audit
entries recorded for that change request.

**Why this priority**: Audit traceability is a constitution requirement. After intake, the
history must contain exactly the submission entry. This story validates the audit trail
foundation for all future lifecycle events.

**Independent Test**: Can be tested after a successful submission by listing audit entries
for the returned ID and confirming exactly one entry of type `Submitted` exists.

**Acceptance Scenarios**:

1. **Given** a ChangeRequestId from a successful submission, **When** the user lists its
   audit history, **Then** the system returns exactly one entry with event type `Submitted`,
   the submission timestamp, and the ChangeRequestId.

2. **Given** a ChangeRequestId that does not exist, **When** the user lists its audit history,
   **Then** the system returns a structured not-found response identifying the unknown ID.

3. **Given** a ChangeRequestId with multiple audit entries (future lifecycle events), **When**
   listed, **Then** entries are returned in strictly ascending chronological order with no
   entries missing.

---

### Edge Cases

- What happens when a requester name contains only whitespace? (Treated as empty → validation
  error for `Requester` field.)
- What happens when a title is exactly at the maximum allowed length? (Accepted — boundary
  value is valid.)
- What happens when a title exceeds the maximum length by one character? (Rejected with a
  length error.)
- What happens when the same ChangeRequestId is used for two retrieval calls? (Both return
  the same record — idempotent read.)
- What happens when a ChangeRequestId is syntactically malformed (not a valid GUID format)?
  (System returns a structured invalid-ID response without querying the store.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept a change request submission containing: `Title`, `Description`,
  `Requester`, `AffectedSystem`, `Priority`, and `ChangeType`.
- **FR-002**: System MUST treat `Title`, `Description`, `Requester`, `AffectedSystem`,
  `Priority`, and `ChangeType` as required fields; any missing or blank value MUST cause
  rejection.
- **FR-003**: System MUST validate that `Priority` is one of the accepted values:
  `Low`, `Medium`, `High`, `Critical`.
- **FR-004**: System MUST validate that `ChangeType` is one of the accepted values:
  `Feature`, `Bugfix`, `ConfigChange`.
- **FR-005**: System MUST validate that `Title` does not exceed 200 characters.
- **FR-006**: System MUST validate that `Description` does not exceed 2,000 characters.
- **FR-007**: System MUST validate that `AffectedSystem` does not exceed 200 characters.
- **FR-008**: System MUST validate that `Requester` does not exceed 100 characters.
- **FR-009**: System MUST assign a system-generated, globally unique `ChangeRequestId` to
  every valid submission.
- **FR-010**: System MUST persist a typed change request record containing all submitted
  fields, the assigned `ChangeRequestId`, the submission timestamp (UTC), and an initial
  status of `Submitted`.
- **FR-011**: System MUST record one audit entry for every successful submission, linked to
  the `ChangeRequestId`, with event type `Submitted` and the submission timestamp.
- **FR-012**: System MUST reject invalid submissions with a structured response containing
  at least one specific, field-named error message per failing field.
- **FR-013**: System MUST NOT persist any record or audit entry when a submission is rejected.
- **FR-014**: System MUST allow retrieval of a persisted change request by its
  `ChangeRequestId`, returning the full typed record.
- **FR-015**: System MUST return a structured not-found response (no unhandled exception)
  when retrieval or audit listing is attempted with an unknown `ChangeRequestId`.
- **FR-016**: System MUST allow listing all audit entries for a change request by its
  `ChangeRequestId`, returned in ascending chronological order.
- **FR-017**: System MUST NOT invoke any AI agent or LLM service in this slice.

### Key Entities

- **ChangeRequest**: Represents a submitted change request.
  Attributes: `ChangeRequestId` (unique identifier), `Title`, `Description`, `Requester`,
  `AffectedSystem`, `Priority` (enum), `ChangeType` (enum), `Status` (enum), `SubmittedAt`
  (UTC timestamp).
  Valid `Status` values (full lifecycle enum): `Submitted`, `UnderReview`, `Approved`,
  `Rejected`, `Deferred`. This slice sets only `Submitted`; transitions to other states
  are managed by downstream slices.

- **ChangeRequestAuditEntry**: Records a lifecycle event for a change request.
  Attributes: `AuditEntryId` (unique identifier), `ChangeRequestId`, `EventType` (enum),
  `OccurredAt` (UTC timestamp), `Details` (optional descriptive text).

- **SubmissionResult**: The outcome of a submission attempt.
  Contains either the full `ChangeRequest` record (on success — including the system-assigned
  `ChangeRequestId`, `SubmittedAt` timestamp, and `Status`) or a list of field-named
  validation errors (on failure). Never both simultaneously.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A valid change request submission completes and returns a `ChangeRequestId`
  in under 500 milliseconds under normal operating conditions.
- **SC-002**: Every rejected submission returns at least one error message that names the
  specific failing field — zero cases of generic "invalid input" without field identification.
- **SC-003**: No data is stored when a submission fails validation: a retrieval attempt
  using any identifier constructed during a failed submission returns not-found.
- **SC-004**: A retrieved change request contains all fields exactly as submitted, with no
  data loss or silent truncation.
- **SC-005**: The audit history for any submitted change request contains entries in strict
  chronological order with no gaps; after intake, exactly one entry of type `Submitted`
  exists.
- **SC-006**: 100% of deterministic logic paths (validation, ID assignment, persistence,
  audit recording, retrieval) are covered by automated tests that run without any live
  external service or LLM call.

## Assumptions

- Requester identity is provided as a plain string in the submission payload; no
  authentication, session management, or identity verification is in scope for this slice.
- `ChangeRequestId` is a GUID (globally unique identifier) generated by the system at
  submission time; callers do not supply their own IDs.
- Priority enum values are exactly: `Low`, `Medium`, `High`, `Critical`.
- ChangeType enum values are exactly: `Feature`, `Bugfix`, `ConfigChange`.
- Persistence for this slice uses an in-memory store (scoped to the running process);
  durable, out-of-process storage is deferred to a future slice.
- All timestamps are stored and returned in UTC.
- All sample change requests, test fixtures, and demonstration data used in development,
  tests, and documentation MUST be fully synthetic — no real project names, requester names,
  or system names.
- The audit log in this slice records only the `Submitted` event; additional lifecycle
  events (e.g., `Approved`, `Rejected`) are out of scope for this slice.
- Concurrent access to the in-memory store is out of scope for this slice; single-threaded
  sequential access is assumed. Thread-safety requirements are deferred to the durable
  storage slice.
- The `ChangeRequestAuditEntry` record is the sole observability signal for this slice.
  Separate structured logging, distributed tracing, and metrics are out of scope; they
  may be introduced in a dedicated cross-cutting concerns slice.
- The interaction surface for this slice is an **in-process .NET library**: callers invoke
  typed service methods directly (no HTTP, MCP, or CLI layer in scope for this slice).
  Future slices may wrap this service in a REST endpoint or MCP tool.

## Clarifications

### Session 2026-10-11

- Q: What is the interaction surface through which callers submit and retrieve change requests? → A: In-process .NET library — callers invoke typed service methods directly.
- Q: Beyond `Submitted`, what are all the valid status values a `ChangeRequest` can reach across its full lifecycle? → A: `Submitted`, `UnderReview`, `Approved`, `Rejected`, `Deferred`.
- Q: When a submission succeeds, what does the service method return — just the ChangeRequestId or the full ChangeRequest record? → A: Full `ChangeRequest` record (all fields + assigned ID + timestamp + status).
- Q: Should the intake service produce any structured log or trace entry per submission attempt, or is the audit record the sole observability signal? → A: Audit record only — no separate log/trace output required in this slice.
- Q: Should the in-process store handle concurrent submissions safely, or is single-threaded sequential access assumed for this slice? → A: Out of scope — single-threaded sequential access assumed for this slice.
