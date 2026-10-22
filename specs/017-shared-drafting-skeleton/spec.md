# Feature Specification: Shared Drafting Skeleton

**Feature Branch**: `feature/shared-drafting-skeleton`

**Created**: 2026-10-22

**Status**: Draft

**Input**: User description: "Extract the drafting-service skeleton shared by the three agents (Requirement, Architect, Developer), with no change in behaviour. A developer adding or maintaining agents removes the duplication between the three drafting services by moving the parts that are identical in purpose and behaviour into one agent-neutral place, so that each agent's service keeps only what is specific to it. Callers and users of the system see no difference."

## Clarifications

### Session 2026-10-22

- Q: Where should the shared skeleton live? → A: A new small project that references Intake; each of the three agents adds one project reference to it. This is the single explicit exception to "no new dependency".
- Q (planning): The three agent structure tests assert each agent references exactly Intake and ModelAccess; how to handle the new reference? → A: Edit the three expected lists to add the shared project; record it as the one exception to "assertions unchanged" (FR-011).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Maintain the common drafting steps in exactly one place (Priority: P1)

A developer needs to change something all three agents do identically when they produce an output for a change request: look up the request, check it is eligible, ask the generator for content, turn a generator failure into a category and nothing else, validate the content, then, as one uninterrupted step, re-check eligibility, assign the next version and store the output together with its audit entry. They also need to change how the latest output and the full history are read. Today each edit must be made three times and kept in step. After this slice the order of these steps, and the guarantees they carry, exist once in an agent-neutral place, and each agent's service holds only what is specific to it.

**Why this priority**: This is the purpose of the slice. Without it nothing else (a fourth agent, lower duplication) is achieved.

**Independent Test**: List each common step and guarantee named in the rules; confirm each is implemented in exactly one place, used by all three agents, with no second copy in any agent.

**Acceptance Scenarios**:

1. **Given** the three agents after the slice, **When** a developer searches for the sequence lookup, eligibility, generate, failure translation, validate, then locked re-check, version and store, **Then** it exists once and all three services use it.
2. **Given** the three agents after the slice, **When** a developer searches for reading the latest output and the full history, **Then** one implementation serves all three.
3. **Given** the three services after the slice, **When** each is inspected, **Then** each still holds its own eligibility rules, inputs and basis, currency checks, content validation, record and audit event type, prompt, and verification (including the Developer agent's quote check).
4. **Given** the three services, **When** an intentional difference between them exists (for example in how cancellation is handled, which extra audit entries are written, or which agents accept an automatic marker), **Then** it remains visible in that agent's own code and is not merged away.

---

### User Story 2 - Callers and users see no difference (Priority: P1)

A caller of any drafting operation (requirements, design, proposal; produce, latest, history) gets exactly what they got before. Status codes, bodies, headers and error codes are identical for every success and every failure category, and no model text, endpoint, deployment name or credential appears in any response, log or error. Overlapping requests still never take the same version, and an output and its audit entry are still stored together or not at all.

**Why this priority**: Co-equal with Story 1. A refactor that changes behaviour has failed.

**Independent Test**: Run the complete existing automated suite with assertions unchanged, and compare recorded responses for every operation and failure category taken before the change against the same operations after it.

**Acceptance Scenarios**:

1. **Given** the system after the slice, **When** the full existing automated suite runs, **Then** every test that passed before passes, with its assertions unchanged (only references to moved types may differ).
2. **Given** recorded responses for every drafting operation (success, every failure category, not-configured, ineligible, outdated basis, unknown id), **When** the same operations are repeated after the slice, **Then** status codes, bodies, headers and error codes are identical.
3. **Given** any generator failure, **When** the system responds, logs or raises an error, **Then** only the failure category (or exception type name where that is today's behaviour) appears, never model text, endpoint, deployment name or credential.
4. **Given** overlapping requests for the same change request, **When** they complete, **Then** no two outputs share a version, and no output exists without its audit entry or the reverse.

---

### User Story 3 - A fourth agent adds only itself (Priority: P2)

A developer adding a further agent supplies only its own eligibility rules, inputs and basis, currency checks, validation, record and audit event type, prompt and verification. They do not edit the shared skeleton or any existing agent.

**Why this priority**: It is the payoff and the proof that the shared code is genuinely agent-neutral, but no fourth agent is delivered here.

**Independent Test**: A throwaway stand-in agent in a test uses the skeleton with its own rules and needs no change to shared code or to any agent; a structural test shows the shared code names no agent and adds no dependency between agents.

**Acceptance Scenarios**:

1. **Given** the shared skeleton, **When** a stand-in agent is written, **Then** it works without any edit to shared code or to the three existing agents.
2. **Given** the shared code, **When** it is inspected, **Then** it refers to no specific agent, no stored record shape, and no intake or approval capability beyond what every agent already uses.
3. **Given** the project dependencies, **When** compared before and after, **Then** no agent depends on another agent, the only new reference is each agent's reference to the new shared project, and that project references Intake but no agent.

---

### User Story 4 - Know what was deliberately not shared, and how much duplication went away (Priority: P2)

A maintainer reads a register of code that looks alike but differs in behaviour, each entry with its reason, including a re-examination of every entry the earlier shared-plumbing slice left separate (each is resolved by this slice or kept with an updated reason). They also read duplicated-code figures for the three agents, measured with the earlier slice's script extended to three agents, before and after.

**Why this priority**: It keeps the extraction honest and proves it paid off, but it depends on the extraction.

**Independent Test**: Check that every earlier register entry has a current status and reason, that every new similar-but-different pair has an entry, and that both measured figures are reported.

**Acceptance Scenarios**:

1. **Given** the earlier register, **When** this slice completes, **Then** each earlier entry is marked resolved or kept with an updated reason.
2. **Given** two pieces of code that look alike but differ in behaviour, **When** the slice completes, **Then** they remain separate and the reason is recorded.
3. **Given** the measuring script, **When** run before and after, **Then** both three-agent duplication figures are reported.
4. **Given** the extraction, **When** any one agent's service would be harder to read than before, **Then** that cost is recorded and the user decides.

---

### User Story 5 - The shared skeleton is proven once (Priority: P3)

Each behaviour of the shared skeleton has its own test that proves it once, not once per agent. Existing agent tests keep proving that each agent feeds the skeleton correctly.

**Why this priority**: Supports maintainability but not user-visible behaviour.

**Independent Test**: Map each shared behaviour to exactly one skeleton test.

**Acceptance Scenarios**:

1. **Given** the skeleton's tests, **When** reviewed against the list of shared behaviours, **Then** each behaviour has exactly one test that proves it.

### Edge Cases

- Cancellation: today the Developer service rethrows cancellation from the generator while the other two services report it as a generic generator failure. This difference must be kept exactly, visibly, and not merged.
- Eligibility order differs per agent (for example, the Developer only consults its basis source after the status passes); each agent keeps its own order.
- The Developer service writes one extra audit entry per tool call before its own entry; the Requirement and Architect services write none. The extra entries must stay inside the same uninterrupted store step.
- Only Requirement and Architect take an automatic marker; the Developer does not. Marker handling stays per agent.
- The Architect and Developer re-check, inside the uninterrupted step, that the basis they used is still current; the Requirement agent has no such check. Each stays as is.
- A generator returning no content, or content that fails validation, must produce the same failure category and message as today.
- A change request deleted or changed between generation and store must produce the same outcome as today.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide one agent-neutral implementation of the drafting sequence: look up the change request, check eligibility, call the generator, translate a generator failure into a category carrying nothing else, validate the content, then re-check eligibility, assign the next version and store the output with its audit entry as one uninterrupted step.
- **FR-002**: The system MUST provide one agent-neutral implementation of reading the latest output and the full history for a change request, with today's not-found behaviour and today's guarantee that readers never see an output mid-store.
- **FR-003**: Only behaviour confirmed identical in all three services by line-by-line comparison MUST be shared; behaviour that differs in any service MUST stay in that service.
- **FR-004**: Each agent MUST keep its own eligibility rules, inputs and basis, currency checks, content validation, record and audit event type, prompt and verification (including the Developer agent's quote check).
- **FR-005**: The shared code MUST NOT refer to any specific agent, to intake or approval capabilities beyond what every agent already uses, or to any stored record shape. Reading agreed at convergence: the change request and the audit entry are Intake-level shapes that every agent already stores, so the shared code may use them; it must not name an agent's own record, a record shape specific to one agent, or any approval or automatic-advancement type.
- **FR-006**: The shared code MUST live in one new small project that references Intake and nothing agent-specific; each of the three agents references it. No agent may depend on another agent.
- **FR-007**: A fourth agent MUST be able to use the skeleton by adding itself alone, without editing shared code or any other agent, and a test MUST demonstrate this.
- **FR-008**: Overlapping requests MUST never take the same version, and an output and its audit entry MUST be stored together or not at all; the existing tests proving this MUST continue to pass.
- **FR-009**: The generator-failure translation MUST carry only the failure category (or, where today's behaviour is so, only the exception type name); no model text, endpoint, deployment name or credential may appear in any response, log or error.
- **FR-010**: Every HTTP operation MUST return the same status codes, bodies, headers and error codes as before, for all success and failure categories.
- **FR-011**: Every existing automated test MUST pass with its assertions unchanged; only references to moved types may change. One exception, approved at planning: the three agent structure tests that list each agent's allowed project references gain the new shared project in that list, and nothing else in them changes.
- **FR-012**: Intentional differences between the agents (including cancellation handling, extra audit entries, automatic-marker handling and eligibility order) MUST remain visible in the agents' own code.
- **FR-013**: The shared skeleton MUST have its own tests, each behaviour proven once rather than once per agent.
- **FR-014**: A register MUST record each similar-looking but behaviourally different pair with its reason; every entry of the earlier shared-plumbing slice's register MUST be re-examined and marked resolved by this slice or kept with an updated reason.
- **FR-015**: Duplicated code across the three agents MUST be measured with the earlier slice's script, extended to three agents, before and after, and both figures reported.
- **FR-016**: Where extraction would make an agent's service harder to read than before, the slice MUST record that cost for the user to decide.
- **FR-017**: The slice MUST NOT introduce any new setting, package, endpoint or capability, and MUST NOT change the shape of any stored record. The only permitted addition is the new shared project and the three project references to it (FR-006).

### Key Entities *(include if feature involves data)*

- **Drafting skeleton**: the agent-neutral definition of the step order and guarantees shared by all drafting services. Holds no agent-specific rule, record or event type.
- **Agent-specific parts**: each agent's eligibility rules, inputs and basis, currency checks, content validation, record, audit event type, prompt and verification, supplied to the skeleton.
- **Kept-separate register**: the list of look-alike pairs left separate, each with a reason and a status (resolved or kept).
- **Duplication report**: before and after figures across the three agents, with the method.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of existing automated tests that passed before pass after, with assertions unchanged, and zero recorded HTTP responses differ.
- **SC-002**: Each shared step and read operation exists in exactly one place; zero copies remain in the three agents.
- **SC-003**: Duplicated code across the three agents is lower after than before, with both figures reported using the same method.
- **SC-004**: A stand-in fourth agent is added with zero edits to shared code or existing agents.
- **SC-005**: 100% of earlier register entries are marked resolved or kept with an updated reason, and every new look-alike pair has an entry.
- **SC-006**: Each shared behaviour is proven by exactly one skeleton test.
- **SC-007**: Zero new settings, packages, endpoints, capabilities or stored-record changes; the only new reference is the shared project named in FR-006.

## Assumptions

- Duplication is measured on production code only, as in the earlier slice; test-code figures may be reported separately for information.
- "No change in behaviour" includes the differences noted in Edge Cases; where analysis shows the three services differ, the difference stays rather than being unified.
- The shared code lives in a new small project (see Clarifications); its name and internal layout are planning decisions. The constitution's technology stack is unchanged because no package is added.
- Reading the latest output and history is shared only if the analysis confirms identical behaviour; if not, the difference is recorded in the register.
- Out of scope: a Reviewer agent, changes to workflow rules or automatic advancement, any change to prompts or model output, output shapes, the HTTP interface, authentication, durable storage, any user interface, or the constitution's technology stack. All sample data is synthetic.
