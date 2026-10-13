# Feature Specification: Organise Agent Code by Agent

**Feature Branch**: `feature/organise-by-agent`

**Created**: 2026-10-13

**Status**: Draft

**Input**: User description: "Organise agent code by agent, with no change in behaviour. A developer working on the system reorganises the existing Requirement agent so that everything specific to that agent is in one place, and everything shared by all language-model-backed agents is in a separate agent-neutral place. Callers and users of the system see no difference."

## Clarifications

### Session 2026-10-13

- Q: Should the change-request store and audit trail stop referring to the requirement draft type entirely, or may they keep one generic, agent-neutral place for agent output? → A: Intake keeps one generic, agent-neutral "agent output" extension point (existing audit event names and values unchanged); the draft record and its rules move to the agent location.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find everything about the Requirement agent in one place (Priority: P1)

A developer opens the system to change how the Requirement agent drafts requirements. Today its pieces are spread across the intake capability and a model-integration area that is named after intake. After this slice, the agent's record types, validation, service, interface, prompt, response parsing, generator and tests all live in a single location named after the agent.

**Why this priority**: This is the core of the slice. Without a single agent-owned location, nothing else (a second agent, clean dependencies) is possible.

**Independent Test**: Can be fully tested by listing every file that is specific to the Requirement agent and confirming each is under the one agent-named location, and that no agent-specific file remains elsewhere.

**Acceptance Scenarios**:

1. **Given** the reorganised system, **When** a developer lists the files that are specific to the Requirement agent (record types, validation, service, interface, prompt, response parsing, generator, tests), **Then** every one of them is inside the single location named after that agent.
2. **Given** the reorganised system, **When** a developer searches outside that location, **Then** no Requirement-agent-specific type, prompt or test is found there, except references that consume the agent through its interface.

---

### User Story 2 - Reuse model access without touching any agent (Priority: P1)

A developer needs the language-model access seam, its Azure implementation, run-time settings and failure mapping. These are separated into an agent-neutral location that names no agent and refers to no agent.

**Why this priority**: Co-equal with Story 1; shared model access is what a second agent would reuse, and mixing it with agent code would defeat the reorganisation.

**Independent Test**: Can be fully tested by inspecting the agent-neutral location for any mention of, or dependency on, the Requirement agent (names, types, prompts, strings).

**Acceptance Scenarios**:

1. **Given** the reorganised system, **When** a developer inspects the agent-neutral location, **Then** it contains the model-access seam, its Azure implementation, run-time settings and failure mapping, and nothing else specific to any agent.
2. **Given** the agent-neutral location, **When** it is checked for references to the Requirement agent, **Then** none are found, and it builds without the agent location present.

---

### User Story 3 - Intake and approval stand independent of agents (Priority: P2)

A developer working on change-request intake or approval can build and test those capabilities without any agent being present.

**Why this priority**: Protects the dependency direction (agents depend on intake, never the reverse) but is secondary to establishing the two locations.

**Independent Test**: Can be fully tested by building and running the intake and approval tests with the agent location and the agent-neutral location excluded.

**Acceptance Scenarios**:

1. **Given** the reorganised system, **When** intake and approval are built without any agent location, **Then** they build and their tests pass.
2. **Given** the intake and approval code, **When** it is checked for references to the Requirement agent, **Then** none are found.

---

### User Story 4 - Add a second agent by adding one location (Priority: P2)

A developer later adds another agent. They create one new location beside the Requirement agent's, and neither the Requirement agent nor the agent-neutral code needs editing.

**Why this priority**: Proves the structure is repeatable, which is the purpose of the reorganisation, but no second agent is delivered in this slice.

**Independent Test**: Can be tested by a dry-run review: walking through the files a hypothetical second agent would add and confirming that none requires a change to the first agent's location or the agent-neutral location.

**Acceptance Scenarios**:

1. **Given** the reorganised system, **When** a developer follows the documented structure to add an agent, **Then** the steps involve adding one new location and its registration only, with no edit to the Requirement agent location or the agent-neutral location.

---

### User Story 5 - Callers see no difference (Priority: P1)

Anyone calling the HTTP API, and anyone running the existing tests, observes exactly the behaviour they saw before the move.

**Why this priority**: "No change in behaviour" is the defining constraint of the slice; any difference makes the slice incomplete.

**Independent Test**: Can be fully tested by running the full existing test suite and comparing recorded API responses and the published API description before and after.

**Acceptance Scenarios**:

1. **Given** the reorganised system, **When** the full existing automated test suite runs, **Then** every test passes with its assertions unchanged (only references to moved types differ).
2. **Given** the recorded responses of every HTTP operation before the move, **When** the same requests are made after the move, **Then** status codes, bodies, headers and error codes are identical.
3. **Given** the published API description before and after, **When** they are compared, **Then** they are identical except for differences that the move unavoidably causes, each of which is listed in the slice's documentation.

---

### Edge Cases

- A type is shared by intake and the agent today (for example the persisted draft record, the draft-generated audit event and the store operations that save and read drafts). The slice must decide where it lives without changing its persisted shape or any serialized value.
- A moved type changes namespace: any public contract that exposes its name (for example in the API description) must be identified and listed.
- A type is used by both the Requirement agent and the agent-neutral code: it must be placed on the neutral side with no agent-specific content, or split so that the agent-specific part stays with the agent.
- Live model checks that are skipped without credentials must remain skipped in the same circumstances and must still reference only moved types.
- Test helpers (fakes, fixtures) used by both agent tests and intake tests must not force intake tests to depend on the agent location.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Requirement agent's record types, validation, service, interface, prompt, response parsing, generator and tests MUST all be located in a single location named after the agent.
- **FR-002**: The language-model access seam, its Azure implementation, run-time settings and failure mapping MUST be located in a separate location whose name does not include any agent.
- **FR-003**: The agent-neutral location MUST NOT refer to any specific agent, by type, name, prompt text or configuration key.
- **FR-004**: The intake and approval capabilities MUST NOT depend on any agent.
- **FR-005**: The agent location MAY depend on intake and on the agent-neutral location; the agent-neutral location MUST NOT depend on the agent location.
- **FR-006**: The structure MUST allow a second agent to be added by adding one location beside the first, without editing the first agent's location or the agent-neutral location.
- **FR-007**: Every existing automated test MUST pass with its assertions unchanged; only references to moved types (namespaces, project references) MAY change.
- **FR-008**: Every HTTP operation MUST return the same status codes, bodies, headers and error codes as before the move.
- **FR-009**: The published API description MUST be unchanged except where the move unavoidably affects it; each such difference MUST be listed.
- **FR-010**: No type MUST be renamed or removed unless it is listed with the reason.
- **FR-011**: No new configuration setting, dependency or capability MUST be introduced.
- **FR-012**: The persisted and serialized shape of every existing record and enumeration value MUST be unchanged.
- **FR-013**: The README and project documentation MUST describe the new structure, including where an agent's code lives, where shared model access lives, and how to add a further agent.
- **FR-014**: Intake MUST expose one generic, agent-neutral extension point for storing an agent's output together with its audit entry (saved together or not at all, versions returned in ascending order). It MUST NOT name the requirement draft type, and the Requirement agent MUST use it to persist its drafts. The existing audit event names and values, including the draft-generated event, MUST be unchanged.
- **FR-015**: The requirement draft record and its status enumeration MUST move to the Requirement agent location with unchanged members and values.

### Key Entities

- **Agent location**: The single named place holding everything specific to one agent. Initially only the Requirement agent has one.
- **Agent-neutral model access**: The shared seam, Azure implementation, run-time settings and failure mapping used by any language-model-backed agent.
- **Intake and approval capabilities**: Change-request submission, retrieval, audit and the human approval gate; independent of any agent.
- **Requirement draft records**: The agent's output records, whose persisted shape and audit linkage must be preserved exactly.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the existing automated tests pass after the move, with zero modified assertions.
- **SC-002**: 100% of HTTP operations produce identical status codes, bodies, headers and error codes compared with recordings made before the move.
- **SC-003**: The published API description differs from the pre-move version only in explicitly listed items (target: zero differences, or every difference listed with its reason).
- **SC-004**: A developer can enumerate every Requirement-agent file by inspecting one location, with zero agent-specific files found elsewhere.
- **SC-005**: Zero references to any specific agent are found in the agent-neutral location or in intake and approval.
- **SC-006**: A reviewer walking through the steps to add a second agent identifies zero required edits to the first agent's location or the agent-neutral location.
- **SC-007**: Zero new configuration settings, dependencies or capabilities are introduced, and every renamed or removed type (target: none) is listed with its reason.
- **SC-008**: The README and project documentation describe the new structure and a reader can locate the agent location and the shared location from them alone.

## Assumptions

- "Location" means a distinct, named unit of the solution (for example a separate project and matching test project); the exact form is decided in planning.
- The only agent delivered so far is the Requirement agent, so the structure is validated by that one agent plus a documented walk-through for a second; no second agent is built.
- Existing behaviour, including error codes and the "drafting not configured" state, is the baseline; recordings of current responses are taken before moving code.
- Persisted record shapes are in-memory only today, so there is no stored data to migrate, but serialized shapes still must not change.
- The constitution's Simplicity principle (shared helpers only when used by two callers) is respected because this slice relocates existing code and introduces no new abstraction; the shared model-access seam is existing code kept as-is.
- Moving types between namespaces or projects is not a rename; only a change to a type's own name counts as a rename for FR-010.
- The Architect agent, new behaviour, orchestration, persistence, authentication, user interface and technology stack changes are out of scope.
