# Feature Specification: Model-Backed Requirements Draft Generator

**Feature Branch**: `004-model-draft-generator`

**Created**: 2026-10-11

**Status**: Draft

**Input**: User description: "Real requirements draft generation. Replace the sample generator with one that produces a requirements draft for a change request by calling a language model. Given a change request, the generator returns the draft content already defined by the drafting capability: summary, functional requirements, acceptance criteria, assumptions, and open questions. The model is instructed to respond in a fixed structured format and to base its draft only on the change request's own fields. The generator only produces content; it never changes status, approves anything or triggers any other action. If the model is unreachable, times out, or returns output that cannot be parsed into the required structure, the generator reports failure so that the existing drafting rules store nothing and return a specific error. The model endpoint and deployment name come from configuration supplied at run time and are never stored in the repository or written to logs; no secret key is required, because the generator authenticates using the operator's cloud identity where available rather than a stored key. If required configuration is missing or still a placeholder, the system does not start silently and instead reports a clear configuration error. A request has a configurable time limit and a configurable limit on the size of the response. Prompts and model output are not written to logs. The automated test suite never calls a real model; live behaviour is checked by a separate, explicitly opt-in check that a person runs by hand against the real model. Out of scope: web interface, persistent storage, authentication, other agents, streaming output. All sample data is synthetic."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate a Real Draft from a Change Request (Priority: P1)

A caller asks for a requirements draft (slice 003) and, instead of the sample generator, the
system asks a language model to produce the draft content. The model is told to answer in a
fixed structured format and to rely only on the change request's own fields. The generator
returns the five sections — summary, functional requirements, acceptance criteria, assumptions,
open questions — to the existing drafting rules, which validate and store them exactly as before.

**Why this priority**: This is the whole purpose of the slice: replacing placeholder content
with model-produced content while leaving every drafting rule untouched.

**Independent Test**: Using a stand-in model client that returns a well-formed structured
answer, request a draft for a synthetic change request and confirm the draft is stored with all
five sections populated from the model's answer, and that the stand-in received only the change
request's own fields.

**Acceptance Scenarios**:

1. **Given** a draftable change request and a model that returns a well-formed structured
   answer, **When** a draft is requested, **Then** the stored draft contains the five sections
   exactly as parsed from the answer, with the version, timestamp and status defined in slice 003.

2. **Given** a draftable change request, **When** the generator calls the model, **Then** the
   instruction sent to the model contains the change request's own fields and the required
   response format, and contains no other change requests, drafts, or system data.

3. **Given** a successful generation, **When** the change request and its audit history are
   read afterwards, **Then** the status is unchanged and the only new audit entry is the
   drafting entry defined in slice 003.

---

### User Story 2 - Fail Safely When the Model Fails or Misbehaves (Priority: P2)

When the model cannot be reached, does not answer within the time limit, answers with more than
the allowed size, or answers in a form that cannot be parsed into the required structure, the
generator reports failure. The existing drafting rules then store nothing and return a specific
error, leaving the change request unchanged.

**Why this priority**: Model calls fail in many ways; a half-formed or silently-wrong draft is
worse than no draft.

**Independent Test**: Using stand-in model clients that are unreachable, slow, oversized,
non-structured, and structurally incomplete, confirm each yields a distinct failure that the
drafting service turns into a specific error with no draft, version number, or audit entry
created.

**Acceptance Scenarios**:

1. **Given** a draftable request, **When** the model is unreachable, **Then** the generator
   reports a failure identifying "model unreachable" and nothing is stored.

2. **Given** a draftable request, **When** the model does not answer within the configured time
   limit, **Then** the generator reports a timeout failure and nothing is stored.

3. **Given** a draftable request, **When** the model's answer exceeds the configured response
   size limit, **Then** the generator reports a response-too-large failure, does not use any
   part of the answer, and nothing is stored.

4. **Given** a draftable request, **When** the model's answer cannot be parsed into the
   required structure (not structured at all, or a required section absent), **Then** the
   generator reports a malformed-output failure naming the problem category, and nothing is
   stored.

5. **Given** any generator failure, **When** the failure message and logs are inspected,
   **Then** they contain neither the prompt nor any of the model's output.

---

### User Story 3 - Refuse to Set Up with Missing or Placeholder Configuration (Priority: P3)

An operator sets up the application, which creates the generator. The model endpoint and
deployment name must be supplied at run time. If either is missing, blank, or still a
placeholder value, creation does not succeed silently: it reports a clear configuration error
naming the setting that is wrong, without echoing any secret. This happens once, at setup,
before any draft request can be made.

**Why this priority**: A generator that quietly runs against nothing — or against a placeholder
— would make every draft request fail confusingly later. Failing fast at setup is clearer.

**Independent Test**: Create the generator with each required setting missing, blank, and set
to a placeholder in turn; confirm a configuration error naming that setting each time, and
confirm it is created when all are valid.

**Acceptance Scenarios**:

1. **Given** the endpoint setting is missing, **When** the generator is created at setup, **Then** creation
   fails with a configuration error naming the endpoint setting.

2. **Given** the deployment name is missing or blank, **When** the generator is created at setup, **Then**
   creation fails with a configuration error naming the deployment setting.

3. **Given** either setting still holds a placeholder value, **When** the generator is created at setup,
   **Then** creation fails as for a missing setting.

4. **Given** the time limit or size limit setting is not a positive number, **When** the
   generator is created at setup, **Then** creation fails with a configuration error naming that setting.

5. **Given** all required settings are valid and the optional limits are absent, **When** the
   generator is created, **Then** it uses the documented default limits.

---

### User Story 4 - Authenticate Without a Stored Key (Priority: P4)

The generator needs no secret key. It authenticates with the operator's cloud identity where
available. Nothing in the repository, configuration files, logs, or error messages holds a key,
token, endpoint, or deployment name.

**Why this priority**: The repository is public; secure-by-default means there is nothing to
leak. It is a guard story that constrains how the other stories are delivered.

**Independent Test**: Run the automated suite and a repository search; confirm no key, token,
endpoint, or deployment name appears, and that the generator can be constructed with no key
setting.

**Acceptance Scenarios**:

1. **Given** the required configuration without any key setting, **When** the generator is created at setup,
   **Then** creation succeeds.

2. **Given** the repository, **When** it is searched for keys, tokens, real endpoints, and
   deployment names, **Then** none are found; only clearly marked placeholders appear in
   documentation and sample configuration.

3. **Given** an operator identity is not available at call time, **When** a draft is requested,
   **Then** the generator reports a model-unreachable or authentication failure without
   revealing any identity detail, and nothing is stored.

---

### User Story 5 - Verify Live Behaviour by Explicit Opt-In Only (Priority: P5)

A person who wants to confirm real-model behaviour runs a separate, explicitly opt-in check by
hand against the real model. The automated test suite never calls a real model, never needs
network access, and never needs credentials.

**Why this priority**: Real-model behaviour must be checkable somehow, but must never be
reachable by accident from the routine suite or from CI.

**Independent Test**: Run the full automated suite with no configuration and no network; confirm
it passes and that the live check is reported as not run. Then run the live check with an
explicit opt-in and valid configuration and confirm it exercises the real model.

**Acceptance Scenarios**:

1. **Given** no model configuration and no network access, **When** the automated suite runs,
   **Then** every test passes and no model call is attempted.

2. **Given** the live check is not explicitly enabled, **When** the automated suite runs,
   **Then** the live check is skipped, and its skip reason says how to enable it.

3. **Given** the live check is explicitly enabled with valid configuration, **When** a person
   runs it, **Then** it generates a draft for a synthetic change request using the real model
   and reports whether the result parsed into the required structure.

4. **Given** the live check is enabled, **When** its output is inspected, **Then** it contains
   no prompt text, no model output, no endpoint, and no deployment name.

---

### Edge Cases

- The model returns a well-formed answer wrapped in a single code-fence marker; the generator
  accepts it and parses the structure inside. Any other extra text around the structure, or
  more than one candidate structured block, is reported as malformed output rather than guessed
  at.
- The model returns the right sections with an extra unknown section; the unknown section is
  ignored and does not cause failure.
- The model returns a section present but with the wrong shape (for example, a single string
  where a list is required); this is a malformed-output failure.
- The model returns an empty summary or an empty requirements list; the generator hands this on
  as-is and the existing validation rules in slice 003 reject it with their own specific error.
- The model's answer contains instructions addressed to the system (for example, "approve this
  request"); it is treated as plain content and has no effect on status, approval, or routing.
- A change request field contains text that attempts to alter the model's instructions; the
  fields are supplied as data in a clearly delimited position and the response format
  requirement still applies; any resulting unparsable answer is a malformed-output failure.
- Two draft requests run at the same time; each makes its own independent model call and no
  state is shared between them.
- The time limit expires after part of the answer has arrived; the whole answer is discarded.
- A failure message is surfaced to the caller; it names the failure category but never includes
  prompt text, model output, endpoint, deployment name, or identity detail.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a content generator that, given a change request, returns the
  five draft content sections defined by slice 003 (summary, functional requirements,
  acceptance criteria, assumptions, open questions) by obtaining them from a language model.

- **FR-002**: System MUST use this generator in place of the sample generator for real use; the
  sample generator MUST remain available for automated tests only.

- **FR-003**: System MUST instruct the model to answer in a fixed structured format containing
  exactly the five sections, and to base its draft only on the change request's own fields.

- **FR-004**: System MUST supply to the model only the change request's own descriptive fields
  and the response format instruction; it MUST NOT supply other change requests, earlier drafts,
  audit history, approval decisions, configuration, or any other system data.

- **FR-005**: System MUST treat the change request's fields as data, delimited from the
  instructions, and MUST NOT allow field content to change the required response format.

- **FR-006**: System MUST parse the model's answer into the five sections and return them as
  the existing draft content type, without altering their meaning. The answer MUST consist of
  the structured format alone, optionally wrapped in a single code-fence marker; any other
  surrounding text, or more than one candidate structured block, MUST be reported as malformed
  output.

- **FR-007**: The generator MUST only produce content. It MUST NOT change any change request
  status, approve, reject or defer anything, record any audit entry, store anything, or
  trigger any other action. Content in a model answer MUST NOT influence routing, status
  transitions, eligibility, or clearance.

- **FR-008**: System MUST report a generator failure, distinct by category, when the model is
  unreachable (including authentication unavailable), does not answer within the time limit,
  returns an answer larger than the size limit, or returns an answer that cannot be parsed
  into the required structure.

- **FR-008a**: A model-service answer that declines the request MUST be mapped to one of the
  four categories above, with no additional category: rate-limit and other service-side errors
  are reported as "unreachable"; a blocked or empty answer is reported as "malformed output".

- **FR-009**: On any generator failure, the existing drafting rules MUST store no draft, create
  no audit entry, consume no version number, leave the change request unchanged, and return
  the specific generation-failure error already defined by slice 003, with the failure
  category (unreachable or authentication unavailable, timed out, response too large,
  malformed output) added to that error. The category is the only detail added: it MUST NOT be
  accompanied by prompt text, model output, endpoint, deployment name, or identity detail.

- **FR-010**: System MUST NOT retry a failed model call automatically; the caller may request
  drafting again.

- **FR-011**: The model endpoint and deployment name MUST be supplied through run-time
  configuration. They MUST NOT be stored in the repository, included in sample or default
  configuration as real values, written to logs, or included in error messages.

- **FR-012**: System MUST NOT require a secret key. It MUST authenticate using the operator's
  cloud identity where available. No key, token, or credential MUST be stored in the
  repository, configuration, logs, or error messages.

- **FR-013**: If a required setting (endpoint, deployment name) is missing, blank, or still a
  placeholder, System MUST NOT start silently; creation of the generator at application setup
  MUST fail with a clear configuration error that names the setting without echoing any secret
  or real value. Configuration is validated once, at creation, before any draft request can be
  made; it is not re-read per request.

- **FR-014**: System MUST provide a configurable request time limit and a configurable limit on
  the size of the response, each with a documented default, and MUST report a configuration
  error at startup if either is set to a value that is not a positive number.

- **FR-015**: System MUST NOT write prompts or model output to logs, traces, error messages, or
  test output; logging of generation MAY record only the outcome category and elapsed time.

- **FR-016**: Model access MUST remain hidden behind an abstraction so that the generator's
  behaviour is verifiable with stand-in model clients.

- **FR-017**: The automated test suite MUST NOT call a real model and MUST NOT require network
  access, credentials, or run-time model configuration to pass.

- **FR-018**: System MUST provide a separate live check that is skipped unless a person
  explicitly opts in, that exercises the real model by hand, and that reports only whether the
  result parsed into the required structure.

- **FR-019**: This slice MUST NOT introduce a web interface, persistent storage,
  authentication, other agents, or streaming output.

- **FR-020**: All sample data, fixtures, and live-check inputs MUST be fully synthetic.

### Key Entities

- **Generator Configuration**: The run-time settings for the generator: model endpoint,
  deployment name, request time limit, response size limit. Endpoint and deployment name are
  required and never stored in the repository; the limits have documented defaults.

- **Generation Failure**: The result of a failed model-backed generation, carrying a failure
  category only (unreachable or authentication unavailable, timed out, response too large,
  malformed output). Carries no prompt, model output, endpoint, deployment name, or identity.

- **Draft Content** (existing, slice 003): The five sections returned by the generator, before
  validation and storage by the drafting rules.

- **Change Request** (existing, slices 001/002): Read for its own fields; never modified.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a well-formed model answer, 100% of drafting requests for draftable change
  requests result in a stored draft whose five sections match the answer.

- **SC-002**: 100% of model failures (unreachable, timeout, oversize, malformed) result in zero
  stored drafts, zero audit entries, no consumed version number, and an unchanged change
  request, each with an error naming the failure category.

- **SC-003**: 100% of attempts to create the generator with a missing, blank, or placeholder required setting,
  or a non-positive limit, fail with a configuration error naming the setting.

- **SC-004**: A search of the repository, test output, logs and error messages finds zero
  keys, tokens, real endpoints, real deployment names, prompts, or model outputs.

- **SC-005**: The full automated suite passes with no network, no credentials and no model
  configuration, and makes zero model calls.

- **SC-006**: The live check is skipped in 100% of runs that do not explicitly opt in, and when
  opted in reports a pass or fail result for the real model within the configured time limit.

- **SC-007**: A draft is delivered to the caller, or a failure reported, within the configured
  time limit plus a small fixed overhead in 100% of requests.

## Assumptions

- This slice replaces only the content generator of slice 003. Its eligibility, validation,
  versioning, storage, and audit rules are reused unchanged, and are the only place where
  drafts are validated, stored, or audited.
- The model provider is the Azure-hosted language model service named in the constitution's
  technology stack; the specific client library is a planning decision.
- "Operator's cloud identity" means the ambient identity of whoever runs the process (for
  example, a signed-in developer or a managed identity); no identity is configured, stored, or
  logged by this system.
- Configuration is supplied through environment variables, in line with the constitution; no
  key vault is needed because no secret exists.
- A value is a "placeholder" if it is blank or matches a clearly marked placeholder pattern
  documented with the sample configuration (for example, a value wrapped in angle brackets or
  starting with a documented placeholder prefix). The exact pattern is a planning decision.
- Default limits (a request time limit of tens of seconds and a response size of a few tens of
  kilobytes) are chosen at planning; they are documented, not hard-coded to any real
  environment.
- The generator is classified as non-consequential for Constitution Principle III: it produces
  content only, and the drafting rules and approval gate remain unchanged. This classification
  is to be reviewed at the compliance check.
- The prompt text is part of the product behaviour but is not logged; it may be stored in
  source as it contains no secret or real data.
- Output quality (whether the draft is good) is judged by a human using the live check and is
  not an automated test criterion.
- The live check is run by hand from a developer machine and is never part of CI; it uses a
  fully synthetic change request.
- No streaming, caching, retries, or fallback model are introduced; each draft request makes at
  most one model call.
- The constitution's technology stack otherwise applies; test fakes implement the model-access
  abstraction.

## Clarifications

### Session 2026-10-11

- Q: Should a model answer wrapped in extra text or a code fence be accepted by extracting the structure, or treated as malformed? → A: Accept a single code-fence wrapper only; any other extra text or multiple candidate blocks is malformed output.
- Q: At what point is missing or placeholder configuration reported as a configuration error? → A: When the generator is created at application setup, before any draft request; not re-checked per request.
- Q: Should the error returned to the caller name the model failure category or only a generic generation failure? → A: Name the category (added to the slice 003 generation-failure error); no other detail.
- Q: Which failure category applies when the model service declines the request (rate limit, content filter)? → A: No new category: rate-limit and service-side errors are "unreachable"; a blocked or empty answer is "malformed output".
