# Feature Specification: Architect Agent on the Agent Framework

**Feature Branch**: `feature/agent-framework-architect`

**Created**: 2026-10-14

**Status**: Implemented (live checks not run)

**Input**: User description: "Run the Architect agent's model interaction through the Microsoft Agent Framework, with no change in behaviour. A developer maintaining and learning the system replaces the Architect agent's hand-written model call with the framework's agent abstraction, behind the existing generator seam, so the framework's behaviour can be studied and compared on one agent. Callers and users of the system see no difference."

## Clarifications

### Session 2026-10-14

- Q: Should framework wiring be added to the shared model-access area as an Architect-only path, leaving the path the Requirement agent uses untouched? → A: Yes. The framework path is added alongside; the existing hand-written path used by the Requirement agent stays as is.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Architect model call runs through the framework with identical behaviour (Priority: P1)

A developer opens the Architect agent and finds that its single model interaction is performed by the Microsoft Agent Framework's agent abstraction, sitting behind the same generator seam as before. The instructions given to the model, the structure it is asked to produce, the time limit, the response-size limit, the failure categories and the keyless sign-in all behave as they did with the hand-written call.

**Why this priority**: This is the slice. Without it there is nothing to study or compare.

**Independent Test**: Run the existing automated test suite with assertions unchanged, and run the opt-in live check against a real model; both must pass and the live check must produce a valid design draft.

**Acceptance Scenarios**:

1. **Given** an approved change request with a requirements draft, **When** a design is requested, **Then** exactly one model call is made through the framework and the resulting design draft is stored exactly as before.
2. **Given** the model call fails, times out, is cancelled, returns oversized output, or returns unusable output, **When** the design is requested, **Then** the same failure category is reported as before and no model text, endpoint, deployment name or credential appears in the error, log or response.
3. **Given** the keyless sign-in is configured or unconfigured, **When** a design is requested, **Then** the outcome (success or the same not-configured result) is identical to before.
4. **Given** the existing tests that use a fake generator, **When** they run, **Then** they pass without edits to their assertions or to the seam they use.

---

### User Story 2 - Callers and users see no difference (Priority: P1)

An operator or client requests designs over HTTP exactly as before. Every operation returns the same status codes, bodies, headers and error codes, for success and for every failure.

**Why this priority**: A change that alters behaviour is a failed refactor. Co-equal with Story 1.

**Independent Test**: Compare recorded responses of every design operation (success, each failure category, not-configured, ineligible, unknown id) taken before the change with the same operations after.

**Acceptance Scenarios**:

1. **Given** recorded responses from before the slice, **When** the same operations are repeated after, **Then** status codes, bodies, headers and error codes are identical.
2. **Given** the Requirement agent, intake, approval and workflow capabilities, **When** the slice is complete, **Then** none of them have changed and all their tests pass.

---

### User Story 3 - Framework stays contained and stateless (Priority: P1)

A developer checks that the framework is confined to the Architect agent and the shared model-access area, makes one call per request, never retries, exposes no tools, and keeps no conversation, session or memory between requests.

**Why this priority**: These are the guarantees that keep the system's decisions its own and make the comparison fair.

**Independent Test**: Inspect where framework types appear; run two consecutive requests against a counting fake model endpoint and confirm call counts and that the second request carries nothing from the first.

**Acceptance Scenarios**:

1. **Given** the codebase after the slice, **When** framework types are searched for, **Then** they appear only in the Architect agent and the shared model-access area.
2. **Given** a design request, **When** it is observed at the model boundary, **Then** exactly one call is made, with no retry on failure, and no tools are offered.
3. **Given** two successive design requests, **When** the second is observed, **Then** it contains nothing from the first and is independent of it.
4. **Given** the model is asked only to generate content, **When** eligibility, routing and any decision are examined, **Then** they remain the system's own rules, unaffected by the framework.

---

### User Story 4 - The comparison is written down (Priority: P2)

A developer learning the framework reads a short written comparison in the slice folder stating what the framework replaced, what it added, what had to be kept hand-written, and any behaviour that differed.

**Why this priority**: The learning value of the slice is lost without the record, but it adds no capability.

**Independent Test**: Read the comparison and confirm it has all four parts and that every difference it lists is reflected in the slice's recorded evidence.

**Acceptance Scenarios**:

1. **Given** the completed slice, **When** the slice folder is read, **Then** a comparison exists covering replaced, added, kept and differing behaviour.
2. **Given** any unavoidable difference in the model instructions or requested structure, **When** the comparison is read, **Then** that difference is listed explicitly.

---

### User Story 5 - Constitution records how and why the framework is used (Priority: P3)

A maintainer reading the constitution's technology stack note finds how and why the framework is used, limited to what this slice actually does.

**Why this priority**: Keeps governance in step with reality; small and independent.

**Independent Test**: Read the stack note and confirm it names the framework, its confined use, and the reason, with a version bump per the constitution's amendment rules.

**Acceptance Scenarios**:

1. **Given** the completed slice, **When** the constitution is read, **Then** its stack note states how and why the framework is used and the version is amended per its own rules.

---

### Edge Cases

- The framework is found to retry internally, make an extra model call, or retain state between requests: this is a failure; it must be disabled or fixed, or recorded with the reason the slice cannot proceed.
- A failure category cannot be preserved through the framework: the gap is recorded and the user decides; it is not silently accepted.
- The framework wraps or rewrites a failure so that its message could carry model text, endpoint, deployment name or credential: nothing of that reaches any error, log or response.
- The framework's own response, usage or metadata objects expose larger or different payloads than the hand-written call: the existing response-size limit still applies to the same content.
- Caller cancellation versus time-limit expiry: cancellation must still propagate as cancellation; only expiry becomes the timed-out category.
- The framework adds its own default instructions, formatting or options to the request: any such difference to the instructions or requested structure is neutralised or listed.
- Live check not configured or unavailable: the opt-in check is skipped as before, not failed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Architect agent's model interaction MUST be performed through the Microsoft Agent Framework's agent abstraction, behind the existing generator seam.
- **FR-002**: The agent MUST make exactly one model call per design request and MUST NOT retry on any failure, including retries the framework would otherwise perform by default.
- **FR-003**: The agent MUST offer the model no tools.
- **FR-004**: The agent MUST keep no conversation, session or memory between requests; every request MUST be independent.
- **FR-005**: The model MUST only generate content; eligibility, routing and all decisions MUST remain the system's own rules.
- **FR-006**: The generator seam MUST be unchanged, so nothing above it, and no test that uses a fake, is affected.
- **FR-007**: Time limits, response-size limits, failure categories and the keyless sign-in behaviour MUST behave exactly as before.
- **FR-008**: No model text, endpoint, deployment name or credential MUST appear in any error, log or response.
- **FR-009**: The instructions given to the model and the structure it is asked to produce MUST be unchanged; any unavoidable difference MUST be listed in the written comparison.
- **FR-010**: Framework types MUST NOT appear outside the Architect agent and the shared model-access area.
- **FR-011**: The Requirement agent, intake, approval and workflow capabilities MUST NOT be changed. In particular, the model-access path the Requirement agent uses MUST remain as it is today; framework wiring in the shared model-access area MUST be an addition used only by the Architect agent.
- **FR-012**: The hand-written response parser MUST be retained; framework structured output MUST NOT replace it.
- **FR-013**: Every HTTP operation MUST return the same status codes, bodies, headers and error codes as before.
- **FR-014**: Every existing automated test MUST pass with its assertions unchanged.
- **FR-015**: The opt-in live check MUST produce a valid design draft.
- **FR-016**: A short written comparison MUST be recorded in the slice folder covering what the framework replaced, what it added, what had to be kept, and any behaviour that differed.
- **FR-017**: The constitution's technology stack note MUST be updated to state how and why the framework is used, with the version amended per the constitution's rules.
- **FR-018**: Any framework retry, extra model call, retained state, or exposure of configuration or model text MUST be fixed, or recorded with the reason the slice cannot proceed.
- **FR-019**: Any failure category that cannot be preserved MUST be recorded for the user to decide, not silently altered.

### Key Entities

- **Generator seam**: The existing boundary between the Architect service and whatever produces design content; unchanged in shape and contract.
- **Design draft**: The Architect agent's output record; unchanged in content and storage.
- **Behaviour comparison**: The written record of replaced, added, kept and differing behaviour for this slice.
- **Before/after response record**: The captured responses of every design operation used to prove no externally visible difference.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of existing automated tests pass with zero assertion changes.
- **SC-002**: 100% of recorded design operations (success and each failure category) return identical status codes, bodies, headers and error codes before and after.
- **SC-003**: The opt-in live check yields a valid design draft in one run against a real model.
- **SC-004**: Across any design request, exactly 1 model call is observed; across any failure, 0 retries are observed.
- **SC-005**: 0 occurrences of framework types outside the Architect agent and the shared model-access area.
- **SC-006**: 0 occurrences of model text, endpoint, deployment name or credential in errors, logs or responses across all failure scenarios exercised.
- **SC-007**: A written comparison with all four required parts exists in the slice folder.

## Assumptions

- The slice builds on the Architect agent and shared plumbing delivered by slices 008, 009 and 010; the model-access area is the shared place created in 009.
- "Before" behaviour is established from the current main branch, including its recorded responses and tests.
- The live check requires the user's own model access and is opt-in; a skipped run is recorded as not run.
- The framework package choice and version are decided at planning time; this spec only requires that it is the Microsoft Agent Framework.
- The constitution already lists the framework in its technology stack; this slice refines that note and bumps the version accordingly.
- Telemetry exporters, tools and MCP, workflows, memory or session persistence, framework structured output, orchestration changes, authentication, durable storage and any user interface are out of scope.
