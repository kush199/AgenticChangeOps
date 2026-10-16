# Feature Specification: Durable Storage for Change Requests

**Feature Branch**: `feature/persistence`

**Created**: 2026-10-16

**Status**: Draft

**Input**: User description: "Durable storage for change requests and everything recorded about them. Callers see the same behaviour as before; the difference is that nothing recorded is lost when the system stops and starts again. Keep change requests, audit entries, requirements drafts and design drafts in durable local storage, so that after a restart every previously recorded fact is available exactly as it was."

## Clarifications

### Session 2026-10-16

- Q: When storage cannot be opened at startup (unwritable location or unsupported layout), should the system refuse to start or start and refuse each operation needing storage? → A: Start anyway; every operation needing storage is refused with a specific error and stores nothing.
- Q: With no storage setting given, should normal (non-test) running use durable storage at the safe local default or stay in memory? → A: Durable at the safe local default; in-memory only when tests choose it.
- Q: Should HTTP reports of storage failure and lost version races reuse an existing error or get new specific ones? → A: New specific error codes in the existing error-body shape: storage unavailable (server error) and version conflict (conflict), with no internal detail.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Recorded facts survive a restart (Priority: P1)

An operator stops and restarts the system. A client application then retrieves change requests, their audit entries, requirements drafts and design drafts recorded before the restart, and gets exactly the values, statuses and order it had before.

**Why this priority**: This is the whole purpose of the slice; without it records are lost on every restart.

**Independent Test**: Record a change request, approve it, add a requirements draft and a design draft, restart, then retrieve everything and compare with what was retrieved before the restart.

**Acceptance Scenarios**:

1. **Given** change requests, audit entries, a requirements draft and a design draft recorded, **When** the system is restarted, **Then** every one is retrievable with identical identifiers, timestamps, fields and status.
2. **Given** several audit entries for one change request, **When** the system is restarted, **Then** they are returned in the order they were recorded.
3. **Given** drafts at versions 1 and 2 for a change request, **When** the system is restarted and a new draft is recorded, **Then** it receives version 3.

---

### User Story 2 - Callers see no difference (Priority: P1)

A person or client application uses every existing operation and sees the same status codes, bodies, headers and error codes as before, and records are never returned in a changed shape.

**Why this priority**: A change that alters behaviour is a failed change; co-equal with Story 1.

**Independent Test**: Run the full existing automated test suite with assertions unchanged, and compare responses of every operation before and after.

**Acceptance Scenarios**:

1. **Given** the existing automated tests, **When** they run, **Then** they pass without any change to their assertions.
2. **Given** any existing operation, success or failure, **When** it is called, **Then** status code, body, headers and error code are identical to before.

---

### User Story 3 - Writes are all-or-nothing and version numbers stay unique (Priority: P1)

Whenever the system stores a change request, an audit entry, or an agent output together with its audit entry, the stored item and its audit entry are stored together or not at all. Version numbers per change request and agent have no gaps and no duplicates, even when requests overlap.

**Why this priority**: These are existing guarantees that durability must not weaken.

**Independent Test**: Force a failure during a combined write and confirm nothing partial remains; submit overlapping requests for the same next version and confirm exactly one succeeds.

**Acceptance Scenarios**:

1. **Given** a write that fails partway, **When** records are later retrieved, **Then** neither the item nor its audit entry is present.
2. **Given** two overlapping requests that would take the same version number, **When** both run, **Then** exactly one succeeds and the other is refused cleanly.
3. **Given** many agent outputs recorded in sequence, **When** versions are listed, **Then** they run 1..N with no gaps or duplicates.

---

### User Story 4 - Safe handling of unavailable or unrecognised storage (Priority: P2)

If storage cannot be opened or written, the operation is refused with a specific error that reveals no path, connection detail or internal message, and nothing partial is stored. If storage was written with a layout this version does not understand, opening it is refused with a clear error and nothing in it is changed.

**Why this priority**: Protects data and avoids leaking internals, but only matters once Stories 1-3 work.

**Independent Test**: Point the system at an unwritable location and at storage marked with an unknown layout version; confirm the specific refusals, no leaked details, and unchanged storage.

**Acceptance Scenarios**:

1. **Given** storage that cannot be opened, **When** the system starts and an operation needs storage, **Then** the system still starts and the operation is refused with a specific error containing no path, connection detail or internal message.
2. **Given** storage recording an unsupported layout version, **When** the system opens it, **Then** opening is refused with a clear error and the storage is unchanged.
3. **Given** no location configured, **When** the system starts, **Then** a safe local default location is used.

---

### User Story 5 - Same behaviour in memory and durable storage (Priority: P2)

A developer runs one shared set of behavioural tests against both the in-memory and the durable implementations, and both pass. The in-memory implementation stays available for automated tests. Adding a new agent requires no change to the storage layer.

**Why this priority**: Keeps the two implementations honest and the design extensible.

**Independent Test**: Run the shared behavioural tests against each implementation; record an output from a new, test-only agent without modifying storage.

**Acceptance Scenarios**:

1. **Given** the shared behavioural tests, **When** run against each implementation, **Then** all pass for both.
2. **Given** an agent the storage has never seen, **When** its output and audit entry are recorded, **Then** it is stored and retrieved with no storage change.

---

### Edge Cases

- Restart immediately after a write completes: the write must be present.
- Stop mid-write: the item and its audit entry are both absent, never one without the other.
- Empty storage on first start: created with the current layout version and no records.
- Records with identical timestamps: audit order still follows recording order.
- Overlapping requests for different change requests or different agents: independent version sequences, neither blocked incorrectly.
- Storage location moved or removed between runs: treated as unavailable or empty per the failure rules, never silently half-read.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST keep change requests, audit entries, requirements drafts and design drafts in durable local storage that survives stopping and starting the system.
- **FR-002**: After a restart, every previously recorded item MUST be retrievable with identical identifiers, timestamps, field values, status and order.
- **FR-003**: A stored change request, audit entry, or agent output and its audit entry MUST be stored together or not at all.
- **FR-004**: A failed write MUST leave nothing partial in storage.
- **FR-005**: Version numbers per change request and agent MUST have no gaps and no duplicates, including when requests overlap.
- **FR-006**: When overlapping requests would take the same version number, exactly one MUST succeed and the others MUST be refused cleanly.
- **FR-007**: Audit entries MUST be returned in the order they were recorded.
- **FR-008**: The storage location MUST be a run-time setting with a safe local default, normal running with no setting MUST use durable storage at that default, and the storage file MUST never be committed to the repository.
- **FR-009**: Storage MUST record the version of its layout.
- **FR-010**: Opening storage written with an unsupported layout version MUST be refused with a clear error and MUST change nothing.
- **FR-011**: If storage cannot be opened or written, the operation MUST be refused with a specific error that contains no path, connection detail or internal message. The system MUST still start in this case; each operation needing storage is refused, and operations not needing storage keep working.
- **FR-012**: Records MUST be returned in exactly the shape they had before; no record shape changes.
- **FR-013**: Every HTTP operation MUST return the same status codes, bodies, headers and error codes as before, except that two new failure outcomes are added in the existing error-body shape and carry no internal detail: storage unavailable (server error) and version conflict (conflict).
- **FR-014**: The in-memory implementation MUST remain available for automated tests.
- **FR-015**: The same behavioural tests MUST run against both implementations and pass for both.
- **FR-016**: The storage layer MUST NOT depend on any particular agent; adding an agent MUST require no storage change.
- **FR-017**: All sample and test data MUST be synthetic.

### Key Entities

- **Change request**: A requested change with identifier, timestamps, fields and status.
- **Audit entry**: An ordered record of something that happened to a change request, stored together with the item it describes.
- **Agent output**: A versioned output (e.g. requirements draft, design draft) belonging to a change request and an agent, with a version unique within that pair.
- **Storage layout version**: A marker recording which layout the stored data uses.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a restart, 100% of change requests, audit entries, requirements drafts and design drafts recorded earlier are retrievable with values, status and order identical to before.
- **SC-002**: 100% of existing automated tests pass with assertions unchanged.
- **SC-003**: 100% of existing HTTP operations return identical status codes, bodies, headers and error codes compared with pre-change recordings.
- **SC-004**: In a test of at least 50 overlapping requests competing for the same next version, exactly one succeeds per version and the resulting versions run 1..N with no gaps or duplicates.
- **SC-005**: In every injected write failure, 0 partial records remain.
- **SC-006**: 100% of storage-failure and unsupported-layout refusals contain no file path, connection detail or internal message.
- **SC-007**: The shared behavioural tests pass for both in-memory and durable implementations.
- **SC-008**: A new agent's output can be stored and retrieved with 0 changes to the storage layer.

## Assumptions

- A single running copy of the system uses the storage at a time.
- A safe local default location is inside a user- or project-local, untracked folder.
- Existing records already carry all information needed; no record shape changes.
- Unsupported-layout and unavailable-storage errors reuse the existing error-reporting style.
- Audit entries are returned in timestamp order, entries with equal timestamps in the order they were recorded. This is the existing behaviour of the in-memory store and the durable store matches it; it equals recording order whenever the clock does not move backwards.

## Out of Scope

Multiple running copies sharing storage, a network or cloud database, backup and restore tooling, migration of data from earlier layouts, encryption at rest, retention or deletion of records, background processing, authentication, any user interface, and changes to the shape of any record.
