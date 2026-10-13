# Feature Specification: Shared Agent Plumbing

**Feature Branch**: `feature/shared-agent-plumbing`

**Created**: 2026-10-13

**Status**: Implemented

**Input**: User description: "Extract the plumbing shared by the Requirement and Architect agents, with no change in behaviour. A developer adding or maintaining agents removes the duplicated code between the two existing agents by moving the parts that are identical in purpose and behaviour into one agent-neutral shared place, so that each agent keeps only what is specific to it. Callers and users of the system see no difference."

## Clarifications

### Session 2026-10-13

- Q: Should the shared place also take over the generator-failure translation step that both drafting services repeat? → A: No. It stays in each service, because it ends in each agent's own result type; the reason is recorded in the kept-separate register.
- Q: How should duplicated code between the two agents be measured? → A: Count of lines in production code that appear in both agents after normalising names, blank lines and comments, using a small script kept in the slice folder; the same script runs before and after.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Maintain the shared behaviour in exactly one place (Priority: P1)

A developer needs to change something that both agents do identically: how the single, time-limited model call is made and its failures categorised, how a model answer is read (bare object or one fenced block, strict value types), or how list sections are trimmed, stripped of blank items and size-limited. Today they must make the same edit twice, once per agent, and keep the copies in step. After this slice each of these behaviours exists once, in an agent-neutral shared place, and an edit there applies to both agents.

**Why this priority**: This is the point of the slice. Without it, nothing else (a third agent, lower duplication) is achieved.

**Independent Test**: Can be fully tested by listing each shared behaviour named in the rules, confirming each is implemented in exactly one place, and confirming neither agent holds a second copy.

**Acceptance Scenarios**:

1. **Given** the two agents after the slice, **When** a developer searches for the time-limited model call and its failure-to-category mapping, **Then** there is one implementation, used by both agents, and neither agent contains its own copy.
2. **Given** the two agents after the slice, **When** a developer searches for reading a model answer (bare object or single fenced block, strict text and list type checks), **Then** there is one implementation used by both agents.
3. **Given** the two agents after the slice, **When** a developer searches for trimming, blank-item removal and size limits on list sections, **Then** there is one implementation used by both agents.
4. **Given** the shared behaviours, **When** the two agents are inspected, **Then** each agent still holds its own prompt, record and content types, required-section rules, eligibility rules, and storage and audit behaviour.

---

### User Story 2 - Callers and users see no difference (Priority: P1)

An operator or client of the system requests requirement drafts and designs exactly as before. Every operation returns the same status codes, bodies, headers and error codes, including every failure category, and no model text, endpoint, deployment name or credential appears in any response, log or error.

**Why this priority**: Co-equal with Story 1. A refactor that changes behaviour is a failed refactor; this is the guarantee that makes the extraction safe.

**Independent Test**: Can be fully tested by running the complete existing automated test suite unchanged and by comparing the recorded responses of every requirement-draft and design operation, success and failure, taken before the change against the same operations after it.

**Acceptance Scenarios**:

1. **Given** the system after the slice, **When** the full existing automated test suite is run, **Then** every test that passed before passes, with its assertions unchanged.
2. **Given** a recorded set of responses for every requirement-draft and design operation (success, every failure category, not-configured, ineligible, unknown id), **When** the same operations are repeated after the slice, **Then** status codes, bodies, headers and error codes are identical.
3. **Given** any failure of the model call or of reading the model answer, **When** the operation responds, logs or raises an error, **Then** none of them contain model text, endpoint, deployment name or credential.

---

### User Story 3 - A third agent adds only itself (Priority: P2)

A developer adding a further agent that is backed by a language model reuses the shared pieces by writing only that agent's own prompt, content type, required-section rules, eligibility rules and storage. They do not edit the shared code, the Requirement agent or the Architect agent.

**Why this priority**: It is the payoff of the extraction and the test that the shared pieces are genuinely agent-neutral, but no third agent is built in this slice.

**Independent Test**: Can be fully tested by writing a throwaway stand-in agent in a test that uses every shared piece and has its own sections, and confirming it needs no change to shared code or to either existing agent, and that the shared code names no agent.

**Acceptance Scenarios**:

1. **Given** the shared place, **When** it is inspected for references to the Requirement agent, the Architect agent, change-request intake, approval, or any stored record, **Then** none are found, and it builds without either agent present.
2. **Given** a stand-in third agent with different section names and different required sections, **When** it uses the shared pieces, **Then** it works without any edit to the shared code or to the two existing agents.
3. **Given** the two existing agents, **When** their project dependencies are inspected, **Then** neither depends on the other, and both depend on the shared place that both already depend on.

---

### User Story 4 - Intentional differences stay visible (Priority: P2)

A developer reading the two agents can still see where they deliberately differ, and can see why code that looks alike was left separate.

**Why this priority**: Over-eager merging is the main risk of a de-duplication slice. Recording what was not merged, and why, prevents a later reader from "fixing" a difference that is on purpose.

**Independent Test**: Can be fully tested by reviewing a written register that lists every pair of similar code left separate, with the reason, and by checking each agent-specific rule is still expressed in the agent that owns it.

**Acceptance Scenarios**:

1. **Given** the slice's written record, **When** a reviewer reads it, **Then** every pair of similar-looking pieces that stayed separate is listed with the reason they are not identical in behaviour.
2. **Given** the two agents, **When** their rules are compared, **Then** each still differs where it differed before: which sections are required, who may be drafted (Submitted or Deferred versus Approved), the requirement that a design be linked to the requirements version it was based on, the audit event raised, and the shape of the call into the generator.

---

### User Story 5 - Duplication is measurably lower (Priority: P3)

A maintainer wants evidence that the slice achieved its purpose. The slice reports the amount of code duplicated between the two agents before and after, measured the same way both times.

**Why this priority**: It turns "less duplication" from an impression into a figure, but it adds no capability.

**Independent Test**: Can be fully tested by reading the slice's report and re-running the stated measurement on the pre-slice and post-slice revisions to reproduce both figures.

**Acceptance Scenarios**:

1. **Given** the pre-slice revision, **When** the stated measurement is run, **Then** it produces the "before" figure recorded in the report.
2. **Given** the post-slice revision, **When** the same measurement is run, **Then** it produces an "after" figure that is lower than "before", and it matches the report.

---

### Edge Cases

- A model answer that is a bare object, one fenced block with no label, one fenced block labelled json (any letter case), a block with another label, an unterminated block, two blocks, empty text, or text that is not an object: each must be read or rejected exactly as it is today.
- A text value that is present but not text, a list that is not a list, a list item that is not text, a missing key: all are malformed output today and must remain so, without the answer text appearing anywhere.
- A list section that is missing, empty, all blank, over the item limit, or containing an over-length item: the same error for the same section and the same wording as today.
- Required-section rules differ: the Architect treats its rollback section as required and its dependencies section as optional, where the Requirement agent has a different set. The shared list handling must accept the required/optional choice from each agent rather than fix one.
- The model call is cancelled by the caller versus exceeds its time limit: the caller's cancellation must still propagate as cancellation, and only the time limit becomes the timed-out category.
- A failure raised by the model client that already carries a failure category must pass through unchanged and not be re-mapped.
- Two overlapping requests for the same change request: version assignment and the eligibility re-check must stay one uninterrupted step in each agent's own storage logic.
- Code that looks alike but differs: the two services' eligibility checks, the audit event raised, and the generator call shape look similar and must stay separate (see FR-013).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The time-limited model call, with its mapping of every failure to a failure category and with no failure detail exposed, MUST exist in one agent-neutral shared place and be used by both agents.
- **FR-002**: The shared model call MUST make exactly one call per request, never retry, apply the same time limit and response-size handling as today, let the caller's own cancellation propagate unchanged, and report a time-limit expiry as the timed-out category.
- **FR-003**: The shared model call MUST NOT attach the original failure, its message or any model text, endpoint, deployment name or credential to what it reports.
- **FR-004**: Reading a model answer that is either a bare structured object or exactly one fenced block (unlabelled or labelled json) MUST exist once in the shared place, with the same accepted and rejected inputs as today, and MUST report anything unusable as malformed output without including the answer text.
- **FR-005**: Reading text values and list values from the structured object, with the same strict type checks as today, MUST exist once in the shared place, parameterised by section name so that any agent can use it.
- **FR-006**: Trimming text, dropping blank list items, and enforcing the item-count and item-length limits for list sections MUST exist once in the shared place, with the same limits and the same error wording per section as today.
- **FR-007**: The shared list handling MUST let each agent state, per section, whether at least one non-blank item is required.
- **FR-008**: Each agent MUST keep, in its own place: its prompt, its record and content types, its choice of which sections are required, its eligibility rules, and its storage and audit behaviour.
- **FR-009**: The shared place MUST NOT refer to either agent, to change-request intake or approval, or to any stored record, and MUST build and be tested without either agent present.
- **FR-010**: The shared place MUST be one that both agents already depend on, and the slice MUST NOT introduce a dependency from one agent to the other.
- **FR-011**: A third agent MUST be able to use every shared piece by adding only itself, without editing shared code or either existing agent; this MUST be shown by a test using a stand-in agent with its own sections.
- **FR-012**: Every operation that existed before MUST return the same status codes, bodies, headers and error codes, including every failure category, and no response, log or error MUST contain model text, endpoint, deployment name or credential.
- **FR-013**: Where two pieces of code resemble each other but are not identical in behaviour, they MUST stay separate, and the slice MUST record each such pair with the reason. The register MUST at least consider the two drafting services (eligibility, basis-version check, audit event, and the generator-failure translation step, which stays in each service), the two generator entry points (call shape and construction from the environment), and the two sets of required sections.
- **FR-014**: The slice MUST add no new setting, dependency, endpoint, capability or stored-record change; no stored record changes shape.
- **FR-015**: Every existing automated test MUST pass with its assertions unchanged; the only permitted edits are to references to types that have moved.
- **FR-016**: The shared pieces MUST have their own tests, each behaviour proven once in the shared tests rather than once per agent. Agent tests that duplicate a shared behaviour's proof MUST NOT be deleted or weakened to meet this; any that become redundant are recorded for a later slice instead.
- **FR-017**: Before any code moves, the slice MUST record a baseline of responses for the requirement-draft and design operations (success and every failure category) and the existing suite's pass counts, so FR-012 and FR-015 can be verified. The existing recorded baseline covers requirement-draft operations only, so design operations MUST be added to it before the change.
- **FR-018**: The slice MUST report the duplication between the two agents before and after, measured the same way on both revisions (the line-count method in Clarifications), and the "after" figure MUST be lower.
- **FR-019**: Identical test-support helpers duplicated across the two agents' test projects MAY be shared, provided no test assertion changes; if any are left separate, the reason is recorded under FR-013.

### Key Entities

- **Shared model call**: the single, time-limited request to the language model and the translation of every failure into a failure category, carrying no detail. Used by both agents; belongs to neither.
- **Model answer reader**: turns the model's text into a structured object, accepting a bare object or one fenced block, and reads text and list values with strict type checks. Agent-neutral; knows no section names.
- **List section normaliser**: trims, removes blank items, and enforces item-count and item-length limits for one list section, with the required/optional choice supplied by the agent.
- **Agent-specific parts**: each agent's prompt, content and record types, required-section rules, eligibility rules, and storage and audit behaviour. These stay with the agent.
- **Kept-separate register**: the written list of similar-looking code deliberately left separate, each with its reason.
- **Duplication report**: the before and after figures and the method used to obtain them.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the existing automated tests that passed before the slice pass after it, with zero assertion edits.
- **SC-002**: 100% of the recorded requirement-draft and design operations (success and every failure category) produce byte-identical status codes, bodies, headers and error codes before and after.
- **SC-003**: Each of the four named shared behaviours (model call and failure mapping, answer reading, text and list value reading, list normalisation) is implemented in exactly one place, and zero copies remain in either agent.
- **SC-004**: Duplicated code between the two agents falls from the recorded "before" figure to a lower "after" figure, measured by the same method on both, and both figures are reported.
- **SC-005**: Zero references to either agent, to intake or approval, or to stored records are found in the shared place, and zero dependencies exist between the two agents.
- **SC-006**: A stand-in third agent can be written in a test using all shared pieces with zero edits to shared code or to the two existing agents.
- **SC-007**: Zero occurrences of model text, endpoint, deployment name or credential in any response, log or error produced by a failure test across both agents.
- **SC-008**: Zero new settings, dependencies, endpoints, capabilities or stored-record shape changes, as confirmed by the compliance review.

## Assumptions

- The shared place is the existing agent-neutral model-access location that both agents already depend on; no new location is needed unless planning shows that location cannot hold the shared pieces without referring to an agent.
- The shared pieces satisfy the constitution's simplicity principle because each has two existing callers; the extraction adds no behaviour. The plan records the justification in its Complexity Tracking.
- The existing failure type and failure categories already live in the shared place and are agent-neutral in behaviour; any rename is out of scope because it would change nothing observable and widen the diff.
- The two drafting services' catch-and-translate step has the same logic but produces agent-specific result types. It stays in each service (see Clarifications); the register entry for it states that sharing it would require a generic result type, which is close to merging the services and is out of scope.
- The existing structure tests list agent-specific types that must be declared in the agent's own place. Types that move to the shared place may require those lists and the references to them to change; this is a reference change, not an assertion change (FR-015).
- Duplication is measured as the count of production-code lines that appear in both agents after normalising names, blank lines and comments, by a small script kept in the slice folder and run unchanged before and after (see Clarifications). Test code is reported separately and is not part of the headline figure.
- The live model check remains skipped by default and is not run as part of this slice, as in previous slices.
- Principles of the constitution (synthetic data only, deterministic routing, human approval gates, typed auditable records, test-first) continue to apply; test-first here means the shared tests are written and approved before the code moves.
- Compliance review against constitution v1.0.3 is part of this spec's approval.
