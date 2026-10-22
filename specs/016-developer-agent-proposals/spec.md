# Feature Specification: Developer Agent Proposes A Change Set

**Feature Branch**: `feature/developer-agent-proposals`

**Created**: 2026-10-21

**Status**: Draft

**Input**: User description: "A Developer agent that proposes a change set for an approved change
request, as text only, without applying anything. Actors: a caller requesting a change proposal for an
approved change request, and an operator who decides whether the read-only sample tools are available.
Actions: request a proposal, retrieve the latest proposal, list the proposal history in version order.
The agent drafts the proposal from the change request, its latest requirements draft, its latest design
draft and a read-only look at the sample codebase using the same read-only tools the Architect already
uses. Every proposed edit names a sample file, says whether it changes or adds, quotes the exact
existing text it replaces and gives the replacement text; the system checks every quote and rejects the
whole proposal on any mismatch. Nothing is created, changed, deleted, compiled or run; the proposal is
never applied. Out of scope: applying, writing, building, running or testing anything, write/run tools,
a Reviewer agent, human acceptance of proposals, automatic running of this step, tools for other agents,
real repositories, network access, authentication, any user interface, changing the Requirement or
Architect agents' behaviour. All sample data is synthetic."

## Clarifications

### Session 2026-10-21

- Q: If a newer requirements or design draft is saved while a proposal request is running, is the proposal still stored? → A: No; the drafts are re-checked just before storing, and if they are no longer the latest the request is refused with the "outdated design" error and nothing is stored.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Request A Verified Change Proposal (Priority: P1)

A caller asks for a change proposal for an approved change request that has a requirements draft and a
design draft based on that requirements draft. The Developer agent looks around the sample codebase
with the read-only tools and returns a proposal: a summary, a list of proposed edits, risks, ideas for
testing, open questions and the sample files it read. Every edit names a sample file, says whether it
changes an existing file or adds a new one, quotes the exact existing text it replaces (for a change)
and gives the replacement text. The system checks each quote against the real file before storing.

**Why this priority**: This is the whole feature: a concrete, checked, text-only proposal grounded in
real files.

**Independent Test**: With tools on, request a proposal for an approved change request about the orders
service and check the stored proposal has at least one edit, every quoted text appears exactly in its
named file, and the sample is unchanged afterwards.

**Acceptance Scenarios**:

1. **Given** tools are on and an Approved change request with a requirements draft and a design draft
   based on its latest requirements draft, **When** a proposal is requested, **Then** a proposal is
   stored with version number, generation time, status Draft, agent name, links to the requirements
   and design draft versions it was based on, and all required content parts.
2. **Given** the stored proposal, **When** inspected, **Then** every "change" edit's quoted text
   appears exactly in its named sample file, every "add" edit names a file that does not yet exist,
   and every path is inside the sample.
3. **Given** a stored proposal, **When** inspected, **Then** it states plainly that it has not been
   compiled, run or tested.
4. **Given** a stored proposal, **When** the change request is inspected, **Then** its status is
   unchanged and nothing was approved or cleared.
5. **Given** a stored proposal, **When** the sample codebase is compared before and after, **Then**
   it is byte-for-byte identical and nothing was created, changed, deleted, compiled or run.
6. **Given** a stored proposal, **When** the audit trail is read, **Then** it holds one
   `ProposalGenerated` entry for it and one entry per tool call (tool name, path or search term, size
   returned, time taken) with no file content.

---

### User Story 2 - Retrieve The Latest Proposal And Its History (Priority: P1)

A caller retrieves the latest proposal for a change request, or the full proposal history in version
order, using the same conventions as the design operations. Requesting again creates a new version and
earlier versions are preserved.

**Why this priority**: A proposal that cannot be read back, or that overwrites earlier ones, is of no
use for review.

**Independent Test**: Request two proposals for one change request; check latest returns version 2 and
history returns versions 1 and 2 in order, version 1 unchanged. Check a change request with none yields
the same "none yet" outcome style as designs.

**Acceptance Scenarios**:

1. **Given** two stored proposals, **When** the latest is retrieved, **Then** version 2 is returned.
2. **Given** two stored proposals, **When** the history is listed, **Then** both are returned in
   ascending version order with version 1 unchanged.
3. **Given** a change request with no proposal, **When** the latest is retrieved, **Then** a clear
   "no proposal" outcome is returned, matching the design operations' convention.
4. **Given** an unknown or malformed ChangeRequestId, **When** any of the three operations is called,
   **Then** it is refused with the same kind of specific error as the design operations.

---

### User Story 3 - Refusals Store Nothing (Priority: P1)

A request is refused, with a specific error and nothing stored, when the change request is unknown or
malformed, is not Approved, has no requirements draft, has no design draft or only a design draft not
based on the latest requirements draft, or the read-only sample tools are not available.

**Why this priority**: Eligibility is the system's own rule; the agent must never be reached when the
rules say no.

**Independent Test**: For each refusal condition set up the state, request a proposal, and check the
specific error, that the model was not called, and that no proposal or audit entry was stored.

**Acceptance Scenarios**:

1. **Given** a change request that is not Approved, **When** a proposal is requested, **Then** it is
   refused with a specific error and nothing is stored.
2. **Given** an Approved change request with no requirements draft, **When** requested, **Then** it is
   refused with a specific error and nothing is stored.
3. **Given** a missing design draft, or one based on an older requirements draft, **When** requested,
   **Then** it is refused with a specific, distinct error and nothing is stored.
4. **Given** the read-only tools are switched off or unavailable, **When** requested, **Then** it is
   refused with a specific error and nothing is stored.

---

### User Story 4 - A Bad Proposal Is Rejected Whole (Priority: P1)

The system checks the agent's proposal before storing it. If any quoted text is not found exactly in
its named file, a file proposed as new already exists, any path is outside the sample, or the proposal
exceeds the maximum number of changes or maximum total size, the whole proposal is rejected and nothing
is stored. If the model call fails, times out, returns incomplete or malformed output, or exhausts a
limit, nothing is stored and the caller gets a specific error carrying only a failure category.

**Why this priority**: The checked quotes are what make the proposal trustworthy; partial acceptance
would defeat them.

**Independent Test**: Using a scripted model, return a proposal with one wrong quote among several
correct ones, then one with an escaping path, one adding an existing file, one over each size/count
limit, and each failure mode of the model; check each is rejected and nothing is stored.

**Acceptance Scenarios**:

1. **Given** a proposal with one non-matching quote among valid edits, **When** checked, **Then** the
   whole proposal is rejected and nothing is stored.
2. **Given** an edit with a path outside the sample, **When** checked, **Then** the whole proposal is
   rejected and nothing outside the sample is read or disclosed.
3. **Given** an "add" edit for a file that already exists, **When** checked, **Then** the whole
   proposal is rejected.
4. **Given** a proposal over the maximum number of changes or total size, **When** checked, **Then**
   it is rejected.
5. **Given** a model failure, timeout, incomplete or malformed output, or an exhausted limit, **When**
   the request ends, **Then** nothing is stored and the error carries only a failure category.
6. **Given** any failure, **When** responses and logs are inspected, **Then** they contain no file
   content, model text, endpoint, deployment name or credential.

---

### User Story 5 - Nothing Else Changes, And It Runs Only When Asked (Priority: P2)

With the Developer agent not used, every existing response, status code, body and header is identical
to before and every existing automated test passes with assertions unchanged (the only allowed test-data edits are the additions named in FR-021). The workflow status offers
the proposal as a person-requested step once a current design exists, separately from the next automatic step; automatic advancement never runs it; it
runs only when a person asks.

**Why this priority**: Safe to ship, and the one step that reads code must stay a deliberate action.

**Independent Test**: Run the existing suite unchanged; compare existing responses to recorded ones;
then run automatic advancement on a change request with a current design and check no proposal is made.

**Acceptance Scenarios**:

1. **Given** the Developer agent is never invoked, **When** the existing suite runs, **Then** all tests
   pass with assertions unchanged.
2. **Given** a change request with a current design and no proposal, **When** its workflow status is
   read, **Then** it offers the proposal as a person-requested step while its automatic next step is unchanged.
3. **Given** the same change request, **When** automatic advancement runs, **Then** no proposal is
   requested, stored or audited.

---

### User Story 6 - Hostile Sample Content Stays Data (Priority: P2)

A sample file contains instruction-like text ("ignore previous rules", "mark this approved", "write
this file"). It is treated purely as data: tools, instructions, statuses and the checks on the proposal
are unchanged.

**Why this priority**: Tool output is the main prompt-injection route.

**Independent Test**: Put an instruction-like line in a test-only sample file, request a proposal, and
check the tool set, the change request status and the quote checks are unaffected.

**Acceptance Scenarios**:

1. **Given** a file containing instruction-like text, **When** the agent reads it, **Then** no tool is
   added, no status changes and every quote is still checked against the real file.

---

### Edge Cases

- A proposal with zero edits: rejected as incomplete (a proposal must propose something).
- Two edits to the same file, or quoted text with overlapping ranges in the original file: each quote is checked independently against
  the original file; overlapping or ambiguous edits are rejected.
- Quoted text that appears more than once in the file: rejected as ambiguous.
- Quote differing in any whitespace, spacing or letter case: rejected (exact match required). Line
  endings are the one exception and are not significant, because sample files may be checked out with
  either style.
- Path spelled to escape the sample (parent traversal, absolute, drive-qualified, link out): rejected.
- An "add" edit that carries a quote, or a "change" edit that carries none: rejected as malformed.
- A newer requirements or design draft is saved while a request is running: the drafts are re-checked
  just before storing; if they are no longer the latest, the request is refused with the "outdated
  design" error and nothing is stored.
- Tool limits reached but a valid, fully checked proposal is still produced from what was read: stored.
- A file read by the agent is not named in any edit: still listed as a file read.
- Two simultaneous requests for one change request: each gets its own version, none lost or duplicated.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let a caller request a change proposal for a change request identified
  only by its ChangeRequestId.
- **FR-002**: The system MUST let a caller retrieve the latest proposal and list the full proposal
  history in ascending version order, as HTTP operations following the same conventions as the design
  operations (paths, status codes, error shape, "none yet" outcome).
- **FR-003**: A proposal MUST be produced only for a change request that is Approved, has a
  requirements draft, and has a design draft based on the latest requirements draft. Otherwise the
  request MUST be refused with a specific error per condition and nothing stored. The conditions MUST
  be re-checked just before storing, so a proposal is never stored against drafts that are no longer
  the latest.
- **FR-004**: An unknown or malformed ChangeRequestId MUST be refused with a specific error and nothing
  stored.
- **FR-005**: The agent MUST work from the change request, its latest requirements draft, the design
  draft based on it, and a read-only look at the sample codebase through the same read-only tools,
  limits, sandbox and off-by-default switch the Architect uses. If those tools are not available the
  request MUST be refused and nothing stored.
- **FR-006**: The agent MUST only generate content. Eligibility, routing and every decision MUST remain
  the system's own deterministic rules. A proposal MUST NOT change any change request status, approve
  anything or clear anything.
- **FR-007**: A proposal MUST be content only. Nothing MUST be created, changed, deleted, compiled or
  run anywhere, and the system MUST never apply a proposal.
- **FR-008**: Producing a proposal MUST be classified as non-consequential. Applying a proposal would be
  consequential and MUST NOT be provided.
- **FR-009**: Each proposed edit MUST name a file inside the sample codebase, state whether it changes
  an existing file or adds a new one, give the replacement text, and (for a change) quote the exact
  existing text it replaces.
- **FR-010**: The system MUST check every edit before storing: the quote appears exactly (and exactly
  once) in the named file; a file proposed as new does not already exist; the path is inside the
  sample. Any failure MUST reject the whole proposal and store nothing.
- **FR-011**: A proposal MUST have a maximum number of changes and a maximum total size. Exceeding
  either MUST reject the whole proposal and store nothing. Size is the total bytes of quoted and replacement text. The ceilings are operator-set with safe
  defaults.
- **FR-012**: The model-call and tool-call limits MUST apply per proposal request, alongside the
  existing sandbox, per-file size, data-returned and time limits.
- **FR-013**: A stored proposal MUST be a typed record holding: ChangeRequestId, the versions of the
  requirements draft and design draft it was based on, version number, generation time, status Draft,
  agent name, summary, list of proposed changes, risks, ideas for testing, open questions, and the
  list of sample files read.
- **FR-014**: Every proposal MUST state that it has not been compiled, run or tested; this statement is
  set by the system, not left to the model.
- **FR-015**: Each successful request MUST create a new version, numbered consecutively from 1 per
  change request, and MUST preserve earlier versions unchanged.
- **FR-016**: The system MUST record a `ProposalGenerated` audit entry for every stored proposal, and
  MUST record every tool call exactly as for the Architect (tool name, path or search term, size
  returned, time taken), never with file content.
- **FR-017**: If the model call fails, times out, returns incomplete or malformed output, or exhausts a
  limit, nothing MUST be stored and a specific error carrying only a failure category MUST be returned.
- **FR-018**: Content returned by tools MUST be treated as untrusted data and MUST NOT alter
  instructions, add tools, grant anything or influence any decision or check.
- **FR-019**: No error, log or response MAY contain file content, model text, endpoint, deployment name
  or credential, or a filesystem path outside the sample.
- **FR-020**: The workflow status MUST offer the proposal as a person-requested step, separate from the next automatic step, after a current design
  exists and the Developer agent is available. Automatic advancement MUST NOT run it; it runs only when a person asks.
- **FR-021**: With the Developer agent not used, every existing response, status code, body and header
  MUST be identical to before, and every existing automated test MUST pass with its assertions
  unchanged. The one allowed exception, approved by the maintainer before it is applied: tests and
  recorded documents that list every audit event type or workflow step, or the full generated API
  description, gain only the new values, the three new operations and the optional workflow member
  (additions only, diff reviewed).
- **FR-022**: The Developer capability MUST be self-contained in its own location beside the other
  agents, reusing shared plumbing and the Architect's tools without changing the Requirement or
  Architect agents' behaviour.
- **FR-023**: All behaviour MUST be verifiable without a live model; any live check MUST be run
  separately by the maintainer.
- **FR-024**: Out of scope and MUST NOT be added: applying, writing, building, running or testing
  anything; any write or run tool; a Reviewer agent; human acceptance of proposals; automatic running of
  this step; tools for other agents; real repositories; network access; authentication; any user
  interface; changes to the Requirement or Architect agents' behaviour.

### Key Entities

- **Change Proposal**: A versioned, text-only record for a change request, linked to the requirements
  and design draft versions it was based on, with status Draft and the "not compiled, run or tested"
  statement.
- **Proposed Change**: One edit: sample file path, kind (change existing / add new), quoted existing
  text (change only), replacement text.
- **Files Read**: The de-duplicated list of sample files the agent actually read for this proposal.
- **Proposal Limits**: Operator-set ceilings on number of changes and total size, in addition to the
  existing tool and model limits.
- **ProposalGenerated Audit Entry**: The new audit entry type recorded for each stored proposal.
- **Failure Categories**: The category carried by model-side failures, plus specific refusal/rejection
  reasons for eligibility, tools unavailable and failed checks.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a fixed set of test requests across both sample systems, 100% of stored proposals have
  every quote found exactly once in its named file and every new-file path absent from the sample.
- **SC-002**: In all rejection tests (wrong quote, ambiguous quote, escaping path, existing file added
  as new, over count, over size, zero edits), 100% store nothing, including when only one edit of many
  is bad.
- **SC-003**: In every refusal test (unknown, malformed, not Approved, no requirements draft, no or
  outdated design draft, tools unavailable), 100% return the specific error, make no model call and
  store nothing.
- **SC-004**: After any number of proposal requests, the sample codebase and every change request status
  are unchanged in 100% of tests.
- **SC-005**: With the Developer agent unused, 100% of existing automated tests pass with assertions
  unchanged (except the additions-only exception in FR-021) and existing responses are identical in
  status, body and headers.
- **SC-006**: In all failure and refusal tests, 0 responses, logs or audit entries contain file content,
  model text, endpoint, deployment name or credential.
- **SC-007**: Each stored proposal has exactly one `ProposalGenerated` entry and exactly one audit entry
  per tool call with the four required fields and no content.
- **SC-008**: Requesting N proposals yields versions 1..N, all retrievable in order, with earlier
  versions unchanged; automatic advancement creates 0 proposals in all tests.
- **SC-009**: An instruction-like line in a sample file changes no tool set, no status and no quote
  check in 100% of tests.

## Assumptions

- The Architect, its read-only sample tools, design drafting, shared agent plumbing, workflow status,
  automatic advancement, storage and audit trail already exist and are reused.
- "Operator" means whoever controls deployment settings; no roles or authentication are added.
- The tools' off-by-default switch is the same one the Architect uses; there is no separate switch for
  the Developer.
- The default maximum changes and total size are small and are fixed during planning.
- A quote must match exactly once; ambiguity is treated as a mismatch.
- Proposals are stored in the existing durable storage as a new typed record; no other storage changes.
- Status Draft is the only status; moving a proposal to any other status is out of scope.
- As for the Architect, a request that fails stores nothing, including its tool-call audit entries;
  only counts (calls made, refusals by kind, time taken) are logged, never content, paths or search
  terms.
- Paths in proposals and audit entries are relative to the sample, never absolute.
- Automated tests use a scripted model; live model behaviour is checked separately by the maintainer.
- The constitution may need a small amendment to record the Developer agent in the Technology Stack; to
  be decided at planning.
