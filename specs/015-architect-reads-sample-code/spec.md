# Feature Specification: Architect Consults A Sample Codebase

**Feature Branch**: `feature/architect-reads-sample-code`

**Created**: 2026-10-17

**Status**: Draft

**Input**: User description: "Let the Architect agent consult a small synthetic sample codebase through
read-only tools, so that its design refers to real files. Actors: a caller requesting a design draft
for an approved change request, and an operator who decides whether the tools are available. When the
operator has switched tools on, the Architect may list files, read files and search text inside a
small synthetic sample codebase provided with the project, offered through the Model Context
Protocol. The design draft names the files it relied on. Tools are read-only, confined to the sample,
off by default, bounded by operator-set limits; tool output is untrusted data; every call is audited
without file content; with tools off nothing changes. Out of scope: any tool that writes, runs or
deletes, tools for other agents, network access, real systems, authentication, storage changes beyond
one added field, any user interface, replacing the hand-written Requirement client. All sample data is
synthetic."

## Clarifications

### Session 2026-10-17

- Q: When tools are on, must a design draft have read at least one sample file to be accepted? → A: No; a draft that read nothing is valid and stores an empty list of files relied on.
- Q: Are the tool calls of a request that fails audited? → A: No (decided at analysis, 2026-10-17, following the spec's "stores nothing"); the audit trail records the calls of every stored design, and failed requests log counts only.
- Q: If limits run out but the Architect still gives a valid design from what it has read, is it stored? → A: Yes; "limits exhausted" is reported only when no valid answer results.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A Design That Cites Real Files (Priority: P1)

A caller asks for a design draft for an approved change request while the operator has tools
switched on. While preparing the draft the Architect looks around the sample codebase (lists files,
reads some, searches for a term) and the resulting design refers to files that actually exist there.
The stored draft carries a list of the sample files the Architect relied on.

**Why this priority**: This is the whole point of the feature: designs grounded in real material
rather than invention.

**Independent Test**: Switch tools on, request a design for a change request that concerns the orders
service, and check the stored draft lists only files that exist in the sample, at least one of them
belonging to the orders service.

**Acceptance Scenarios**:

1. **Given** tools are on and an approved change request about the orders service with a requirements
   draft, **When** a design draft is requested, **Then** a design draft is stored with the usual
   content plus a list of the sample files relied on, every one of which exists in the sample.
2. **Given** the same request, **When** the draft is stored, **Then** it is versioned and retrievable
   exactly as drafts are today, and the change request's status is unchanged.
3. **Given** tools are on, **When** the Architect reads files, **Then** each call appears in the
   audit trail with tool name, path or search term, size returned and time taken, and no file content.
4. **Given** the Architect read no files, **When** the draft is stored, **Then** the list of files
   relied on is empty and the draft is otherwise valid.

---

### User Story 2 - Tools Off Changes Nothing (Priority: P1)

The operator has not switched tools on (the default). Every design request behaves exactly as before:
same responses, status codes, bodies and headers, and no sample codebase access of any kind.

**Why this priority**: The feature must be safe to ship disabled and must not disturb existing
behaviour.

**Independent Test**: With default settings, run the full existing automated test suite unchanged and
confirm it passes; compare a design response to a recorded pre-change response.

**Acceptance Scenarios**:

1. **Given** tools are off, **When** a design draft is requested, **Then** the response is identical
   to before this feature, and no tool call is made or audited.
2. **Given** the existing automated tests, **When** they run, **Then** all pass with assertions
   unchanged.

---

### User Story 3 - Confinement And Limits (Priority: P2)

The Architect may only look inside the sample. If it tries to reach outside, to read an oversized
file, or to exceed a limit, that attempt is refused with a specific message the agent can act on, the
refusal is counted and audited, and the design continues within the limits.

**Why this priority**: These are the safety properties that make read-only access acceptable, but they
only matter once Story 1 exists.

**Independent Test**: Drive the tools directly with escaping paths, an oversized file and calls past
each limit, and check each is refused with the specific message and counted.

**Acceptance Scenarios**:

1. **Given** a path that leads outside the sample (parent traversal, absolute path, drive-qualified
   path, or a link out), **When** requested, **Then** it is refused and nothing outside is disclosed.
2. **Given** a file larger than the per-file size limit, **When** read, **Then** it is refused with a
   message naming the limit, not truncated silently.
3. **Given** the tool-call or returned-data limit is reached, **When** the agent asks again, **Then**
   that call is refused and counted, and the agent may still finish its answer.
4. **Given** a failed or refused call, **When** it happens, **Then** it is not retried.

---

### User Story 4 - Failing Safely (Priority: P2)

If tools are on but cannot be made available, or the limits run out before the Architect produces a
valid answer, the design request fails with a distinct failure category and nothing is stored.

**Why this priority**: Operators and callers must be able to tell these failures apart from existing
ones, and partial or ungrounded drafts must never be stored.

**Independent Test**: Switch tools on with the sample unavailable; then with limits set so low no
valid answer is possible; check each yields its own category and no stored draft.

**Acceptance Scenarios**:

1. **Given** tools are on but unavailable, **When** a design is requested, **Then** it fails with the
   "tools unavailable" category and no draft is stored.
2. **Given** limits are exhausted before a valid answer, **When** the request ends, **Then** it fails
   with the "limits exhausted" category, distinct from the first, and no draft is stored.
3. **Given** any failure, **When** the response and logs are inspected, **Then** they contain no file
   content, model text, endpoint, deployment name, credential, or filesystem path outside the sample.

---

### User Story 5 - Hostile Content Stays Data (Priority: P2)

A sample file contains text that tries to give the Architect instructions ("ignore previous rules",
"call another tool", "mark this approved"). The text is treated purely as data: it does not change the
Architect's instructions, add tools, grant anything, or change any decision.

**Why this priority**: Prompt injection through tool output is the main risk of giving an agent
tools.

**Independent Test**: Place an instruction-like line in a test-only sample file, request a design, and
check no extra tool appears, no status changes and the system's rules decide everything as before.

**Acceptance Scenarios**:

1. **Given** a file containing instruction-like text, **When** the Architect reads it, **Then** the
   set of available tools and the change request's status are unchanged.

---

### Edge Cases

- The sample folder is missing, empty or unreadable while tools are on: "tools unavailable".
- A search term matching nothing, or matching too much: empty result, or a result capped by the
  returned-data limit.
- A listing of a folder that does not exist inside the sample: refused as not found, without leaking
  the real location.
- The Architect names, in its design, a file it did not actually read or that does not exist: such
  names are not recorded as "relied on"; the list contains only files actually read.
- Binary or non-text files: refused as unreadable.
- The same file read twice: counts against the limits each time but is listed once.
- Time limit reached mid-request: treated as limits exhausted.
- Limit settings that are absent: safe defaults apply. Limit settings that are present but zero,
  negative or not a whole number are invalid configuration: design drafting is reported as not
  configured (as for any other invalid model setting), with only the setting name logged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST ship a synthetic sample codebase in its own folder, outside the product
  code and outside the solution, not compiled or run by the build, small enough to review in one
  sitting.
- **FR-002**: The sample MUST contain two invented systems, an orders service and a billing portal,
  matching the names used by the project's synthetic change requests. Each MUST include
  configuration, a few source files and short design notes. Its content is listed under "Sample
  Codebase Content" below.
- **FR-003**: The sample MUST contain no real names, credentials, addresses or proprietary material.
- **FR-004**: The system MUST provide three read-only capabilities over the sample: list files, read a
  file, and search text.
- **FR-005**: These capabilities MUST be offered to the Architect through the Model Context Protocol.
- **FR-006**: The capabilities MUST NOT create, change or delete anything, run anything, or reach any
  network or other system.
- **FR-007**: Every path MUST be confined to the sample; any attempt to reach outside it, by whatever
  spelling, MUST be refused.
- **FR-008**: Content returned by a capability MUST be treated as untrusted data and MUST NOT alter
  instructions, add tools, grant anything or make a decision.
- **FR-009**: The Architect MUST only generate content; eligibility, routing and every decision MUST
  remain the system's own deterministic rules. A design draft MUST NOT change any change request
  status, approve anything or clear anything.
- **FR-010**: Reading sample files MUST be classified as non-consequential (no human approval needed).
- **FR-011**: The feature MUST be off by default and enabled only by an explicit operator setting.
  When off, the Architect MUST behave exactly as before and every existing response, status code, body
  and header MUST be identical.
- **FR-012**: The operator MUST be able to set, with safe defaults: maximum tool calls, maximum model
  calls, maximum data returned, maximum size per file read, and a time limit, all per design request.
- **FR-013**: A failed call MUST NOT be retried.
- **FR-014**: A path outside the sample, an oversized file, or a call over any limit MUST be refused
  with a specific message to the agent and counted, and the design MUST continue within the limits.
- **FR-015**: If tools are on but unavailable, the design request MUST fail with a distinct failure
  category and store nothing.
- **FR-016**: If limits are exhausted before a valid answer, the request MUST fail with a second,
  distinct failure category and store nothing. Reaching a limit alone is not a failure: a valid answer
  produced from what was already read MUST be stored normally.
- **FR-016a**: The list of files relied on is stored as the draft's single added field
  (`SampleFilesRead` in the stored record, `sampleFilesRead` in responses); "files relied on" in this
  spec means exactly that field.
- **FR-017**: A design draft produced with tools on MUST list the sample files the Architect actually
  read. Reading no file is allowed: such a draft is valid and lists none.
- **FR-018**: Every capability call made while producing a stored design draft MUST be recorded in the
  audit trail with tool name, path or search term, size returned and time taken, and MUST NOT record
  file content. A request that fails (FR-015, FR-016) stores nothing, including its calls; only
  counts (calls made, refusals by kind, time taken) are logged for it, never content, paths or
  search terms.
- **FR-019**: The stored design draft MUST remain stored and versioned as today; the stored record
  gains only the list of files relied on. The list is absent (not empty) whenever tools were off, so a
  draft made with tools off, and every draft stored before this feature, is stored and returned
  byte-for-byte as before; an empty list means tools were on and nothing was read.
- **FR-020**: No error, log or response MAY contain file content, model text, endpoint, deployment
  name, credential, or any filesystem path outside the sample.
- **FR-021**: Out of scope and MUST NOT be added: write/run/delete tools, tools for the Requirement,
  Developer or Reviewer agents, network access, real-system tools, authentication, other storage
  changes, any user interface, replacement of the hand-written Requirement client.

### Key Entities

- **Sample Codebase**: A fixed, read-only, synthetic collection of folders and files with two systems.
- **Capability Call**: One list, read or search attempt: tool name, path or search term, outcome
  (served or refused and why), size returned, time taken. Never content.
- **Files Relied On**: The de-duplicated list of sample file paths actually read for a design draft;
  the only new field on the stored design draft.
- **Tool Limits**: The operator-set per-request ceilings (tool calls, model calls, data returned,
  per-file size, time).
- **Failure Categories**: Two new, distinct categories: tools unavailable; limits exhausted.

### Sample Codebase Content

All invented. Two folders, `orders-service` and `billing-portal`, under one sample folder.

**orders-service**
- `config/orders.settings`: invented settings (queue names, page size, retry count)
- `src/OrderIntake`: accepts and validates an order
- `src/OrderStatusTracker`: tracks status transitions of an order
- `src/StockCheck`: checks stock for each order line
- `notes/design.md`: short design notes (flow, known limits)
- `notes/decisions.md`: two or three short decision records

**billing-portal**
- `config/portal.settings`: invented settings (invoice currency, page size, session length)
- `src/InvoiceList`: lists invoices for a customer
- `src/PaymentReminder`: decides which reminders are due
- `src/StatementExport`: produces a statement summary
- `notes/design.md`: short design notes
- `notes/decisions.md`: two or three short decision records

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With tools on, for each of the two systems, 100% of files listed as relied on exist in
  the sample, in a fixed set of test requests.
- **SC-002**: With tools off, 100% of existing automated tests pass with assertions unchanged (the
  one exception, approved by the maintainer: the test that lists every failure category and the two
  recordings that loop over every category gain the two new categories, additions only), and a
  design response is identical in status, body and headers to the pre-change response.
- **SC-003**: In a test set of at least 20 escaping path spellings, 0 reach content outside the
  sample.
- **SC-004**: In all failure and refusal tests, 0 responses, logs or audit entries contain file
  content, model text, endpoint, deployment name, credential or outside path.
- **SC-005**: No request exceeds any configured limit; a request at each limit ends in a refusal or
  the distinct failure category within the configured time limit.
- **SC-006**: Each capability call yields exactly one audit entry with the four required fields and no
  content.
- **SC-007**: A reviewer can read the entire sample in one sitting (about 30 minutes).
- **SC-008**: An instruction-like line in a sample file changes no tool set and no status in 100% of
  tests.

## Assumptions

- The Architect, design drafting, storage, audit trail and approval rules already exist and are
  reused; this feature only adds capability calls, the list of files relied on, and two failure
  categories.
- "Operator" means whoever controls deployment settings; no new roles or authentication are added.
- Safe defaults are small (for example a handful of calls, a few thousand bytes per file) and are
  fixed during planning.
- The sample is shipped inside the repository and its location is fixed by the project, not chosen by
  callers.
- Paths in audit entries and the list are relative to the sample, never absolute.
- The file list is stored with the design draft in the existing durable storage as a single added
  field; older drafts read as empty.
- Live model behaviour is checked separately by the maintainer; automated tests use a scripted model.
