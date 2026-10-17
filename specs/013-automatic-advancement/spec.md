# Feature Specification: Automatic Advancement of Change Requests

**Feature Branch**: `013-automatic-advancement`

**Created**: 2026-10-16

**Status**: Draft

**Input**: User description: "Automatic advancement of change requests through the steps that need
no human decision. The actor is the system itself, acting for an operator who has switched the
feature on; human approvers remain the only ones who make decisions. When switched on, the system
periodically finds change requests that have a step due and runs that step without anyone asking:
the requirements draft (Requirement agent) and the design draft (Architect agent), exactly as the
existing workflow defines them. Input is only facts already recorded about each change request,
and operator settings. Both steps are classified as non-consequential (draft content only, no
status change, no decision recorded, nothing cleared) and only for that reason may run unasked.
The feature is off by default; interval, maximum steps per check and waiting time after a failure
are operator settings with safe defaults. Due-ness is decided by the same rules as the existing
workflow status, never by a language model. At most one step runs per change request at a time,
whether started automatically or by a person; a step already complete for the current inputs is
never run again. A failed automatic attempt is not retried until the waiting time has passed, and
after a configured number of consecutive failures the request is left for a person, with the
reason visible in its workflow status. A restart loses nothing and repeats nothing. Every
automatic step is audited exactly as a manual step and additionally shows it was automatic. The
workflow status shows when automatic advancement is off and when a request was left for a person.
A per-check summary is available to the operator with no model text, endpoint, deployment name or
credential. Out of scope: any automatic decision, Developer or Reviewer agents, notifications,
multiple running copies, a user interface, authentication, Microsoft Agent Framework workflows,
changes to how agents generate content, retention of old records. All sample data is synthetic."

## Clarifications

### Session 2026-10-16

- Q: How should the operator read the per-check summary? → A: A new read-only operator endpoint
  returns the latest check summary (counts and times only), and each check is also logged as one
  summary line; no durable history is kept.
- Q: Does "left for a person" end on its own? → A: No. It stays until a manual advance succeeds or
  a different step becomes due; a restart does not clear it and there is no timed cool-off.
- Q: What happens when an operator setting is invalid? → A: The system still starts, uses the safe
  default for that value only (off for an invalid on/off value) and logs the reason, never echoing
  a secret.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Drafts Appear Without Anyone Asking (Priority: P1)

An operator switches automatic advancement on. From then on, a newly submitted change request
receives a requirements draft within the operator's interval, and an approved request that has a
requirements draft receives a design draft, with nobody calling advance. Requests awaiting a
decision, Rejected, or Deferred with a draft receive nothing further. The system never decides,
approves, rejects, defers or clears anything.

**Why this priority**: This is the whole purpose of the feature; everything else makes it safe.

**Independent Test**: With a stand-in content generator and the feature on, submit a synthetic
request, run one check, and confirm a requirements draft exists with its usual audit entry plus
the automatic marker. Approve it by the existing approval operation, run another check, and
confirm a design draft exists. Confirm requests awaiting a decision, Rejected, and Deferred with a
draft gain nothing, and that no status ever changed.

**Acceptance Scenarios**:

1. **Given** the feature is on and a newly submitted request has no drafts, **When** a check runs,
   **Then** exactly one requirements draft is stored and audited, and the request's status is
   unchanged.
2. **Given** the feature is on and an Approved request has a requirements draft and no current
   design draft, **When** a check runs, **Then** exactly one design draft is stored and audited.
3. **Given** the feature is on and a request is awaiting a decision, Rejected, or Deferred with a
   draft, **When** checks run, **Then** nothing is run, stored, or audited for it.
4. **Given** a step has been completed for the current inputs, **When** further checks run,
   **Then** it is never run again.
5. **Given** a requirements draft was produced automatically, **When** a person later records an
   approval, **Then** only then does the design draft become due; the system itself never records
   that approval.

---

### User Story 2 - Off by Default and Under Operator Control (Priority: P2)

Automatic advancement does nothing unless an operator has explicitly switched it on. The operator
also sets the interval between checks, the maximum number of steps per check, and the waiting time
after a failure; each has a safe default. When the feature is off, the workflow status says so.

**Why this priority**: Safe adoption: the operator, not the system, decides whether unasked model
use happens at all.

**Independent Test**: Start with no settings and confirm nothing runs and status reports the
feature off. Switch it on with a small interval and confirm checks begin; set a maximum of one
step per check with several due requests and confirm only one step runs per check.

**Acceptance Scenarios**:

1. **Given** no operator setting, **When** time passes with requests due, **Then** no step runs and
   the workflow status of a due request shows that automatic advancement is off.
2. **Given** the feature is on, **When** several requests are due and the maximum steps per check
   is N, **Then** no check runs more than N steps, and the remainder are picked up by later
   checks without any being skipped permanently.
3. **Given** an invalid setting value (non-positive interval, negative maximum, and the like),
   **When** the system starts, **Then** it still starts, the invalid value alone is replaced by
   its documented safe default (off for an invalid on/off value), and the reason is logged
   without revealing any secret.
4. **Given** the model is not configured, **When** a check runs, **Then** no step runs and nothing
   is recorded as a failure.

---

### User Story 3 - Failures Wait, Then Hand Over to a Person (Priority: P3)

When an automatic step fails, nothing is stored for the draft, only the failure category is
reported, and the request is not retried until the waiting time has passed. After the configured
number of consecutive failures the system stops trying for that request and leaves it for a
person to advance; its workflow status says so and why.

**Why this priority**: Prevents runaway model spend and makes persistent trouble visible.

**Independent Test**: With a failing stand-in generator, confirm one attempt, no retry before the
waiting time, a retry after it, and after the configured number of consecutive failures no further
automatic attempts, with the status showing the request is left for a person. Then advance it
manually with a working generator and confirm it succeeds.

**Acceptance Scenarios**:

1. **Given** an automatic step fails, **When** the response, records and logs are inspected,
   **Then** no draft was stored, no draft audit entry was written, and only the failure category
   appears (in the check's log line as a count per category).
2. **Given** a failure occurred less than the waiting time ago, **When** checks run, **Then** that
   request is not attempted; **When** the waiting time has passed, **Then** it is attempted again.
3. **Given** the configured number of consecutive failures is reached, **When** later checks run,
   **Then** the request is not attempted and its workflow status shows it is left for a person,
   with the failure category as the reason.
4. **Given** a request left for a person, **When** a person advances it manually, **Then** the
   manual advance works as it always has, and a success clears the failure count.
5. **Given** a request's recorded facts change so that a different step is due (for example a
   decision is recorded), **When** the next check runs, **Then** earlier failures of the old step
   do not block the new one.

---

### User Story 4 - Safe Alongside People, Restarts and Overlaps (Priority: P4)

At most one step runs for a change request at a time, whether started automatically or by a
person. Two overlapping attempts produce exactly one new draft. A restart loses nothing and
repeats nothing: the system resumes from recorded facts only.

**Why this priority**: Correctness under concurrency and restarts; mostly invisible when working.

**Independent Test**: Start an automatic step and a manual advance for the same request at the
same moment with a slow stand-in generator and confirm exactly one new draft and a specific
refusal for the other. Restart the system mid-way and confirm no duplicated or lost drafts and
that failure waiting state is honoured.

**Acceptance Scenarios**:

1. **Given** an automatic step is running for a request, **When** a person advances the same
   request, **Then** exactly one draft results and the other attempt is skipped or refused as
   nothing due, storing nothing.
2. **Given** two overlapping automatic attempts for one request, **When** both finish, **Then**
   exactly one new draft exists.
3. **Given** the system restarts after drafts or failures were recorded, **When** it resumes,
   **Then** completed steps are not repeated, failure waiting times and counts still apply, and
   nothing recorded is lost.
4. **Given** a step is interrupted by a restart before completing, **When** the system resumes,
   **Then** nothing partial exists and the step is simply due again.

---

### User Story 5 - Operator Visibility Without Leaks (Priority: P5)

The operator can see a summary of each check: how many requests were examined, how many steps ran,
and how many failed. Every automatic step in the audit trail shows it was run automatically. The
workflow status shows when automatic advancement is off and when a request was left for a person.
No summary, status, audit entry, log or error contains model text, an endpoint, a deployment name
or a credential. Existing responses keep their shape and meaning.

**Why this priority**: Trust and traceability; the operator can see what the system did unasked.

**Independent Test**: Run checks with a stand-in generator whose output and errors contain a fake
endpoint and credential; confirm the summaries, statuses, audit entries and logs contain none of
it, that automatic entries are marked, and that manual entries and all pre-existing responses are
unchanged.

**Acceptance Scenarios**:

1. **Given** a check completed, **When** the operator asks for the latest summary, **Then** it
   gives counts of requests examined, steps run and steps failed, and the time of the check.
2. **Given** an automatic step ran, **When** the audit history is listed, **Then** the entry is
   identical in content to a manual step's entry and additionally shows it was automatic; manual
   entries do not show it.
3. **Given** the feature is on or off, **When** any existing operation is called, **Then** its
   response has the same shape and meaning as before this feature.

---

### Edge Cases

- A request becomes due between a check's listing and its step: the due step is re-derived at the
  moment it runs, and nothing runs if it is no longer due.
- A person records a decision while an automatic step is running: the step's result is stored only
  if its inputs are still current, as for the manual operation; otherwise nothing is stored.
- Requirements are re-drafted after a design draft exists, so the design is stale: a new design
  draft is due and is run automatically; the earlier one is kept.
- A very large number of due requests: each check runs no more than the maximum steps, handling
  requests in a stable, fair order so none is starved.
- A check takes longer than the interval: checks never overlap with each other.
- The operator switches the feature off: settings are read once at start-up, so switching off takes
  effect when the system is restarted; until then checks continue. Stopping the system lets a step in
  progress finish or fail normally, and nothing is repeated on the next start.
- A person calls an agent's own drafting operation directly (not an advance) while an automatic step for the same request is running: direct calls are outside the workflow step and take no gate, so both may store a version, as two direct calls already do; advances (manual or automatic) never do.
- Agent output claiming approval or instructing a next step: stored as draft content only; never
  alters status, routing or due-ness.
- Many failed requests: one request's failures never block others.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST NOT run any step automatically unless an operator has explicitly
  switched automatic advancement on; the default MUST be off.
- **FR-002**: The interval between checks, the maximum number of steps per check, the waiting time
  after a failure, and the number of consecutive failures after which a request is left for a
  person MUST be operator settings, each with a safe default and each validated. An invalid value
  MUST NOT stop the system starting: that value alone falls back to its safe default (off for an
  invalid on/off value) and the reason is logged without any secret.
- **FR-003**: When on, the system MUST periodically examine change requests and run each step that
  is due, up to the maximum steps per check, without anyone asking. Steps that stored a draft and
  steps that failed both count toward the maximum; requests that are skipped (not due, waiting out a
  failure, left for a person, model not configured, or already being advanced) do not.
- **FR-004**: The only steps run automatically MUST be the requirements draft and the design draft,
  and they MUST be exactly the step the existing manual advance would run for the same recorded
  facts.
- **FR-005**: Whether a step is due MUST be decided by the same rules as the existing workflow
  status, from recorded facts only, and MUST never be decided by a language model.
- **FR-006**: Automatic advancement MUST NOT record, change, or imply any human decision, MUST NOT
  change any change request's status, and MUST NOT clear anything.
- **FR-007**: A newly submitted request MUST receive a requirements draft within the operator's
  interval (plus the time the step itself takes) while the feature is on and the model is
  configured.
- **FR-008**: An Approved request with a requirements draft and no current design draft MUST
  receive a design draft automatically.
- **FR-009**: A request awaiting a decision, Rejected, or Deferred with a draft MUST receive
  nothing further automatically.
- **FR-010**: At most one workflow step (an advance; the agents' own direct operations are not workflow steps) MUST run for a given change request at a time, whether started
  automatically or by a person; an attempt that would overlap MUST store nothing and be refused
  or skipped with a specific reason.
- **FR-011**: A step already complete for the current inputs MUST never be run again; two
  overlapping attempts for the same request MUST produce exactly one new draft.
- **FR-012**: A failed automatic attempt MUST store no draft, MUST NOT be retried until the
  operator's waiting time has passed, and MUST be reported with only its failure category: the
  check's log line carries the number of failures per category, and a request left for a person
  shows its category in its workflow status.
- **FR-013**: After the configured number of consecutive failures for the same due step, the
  system MUST stop automatic attempts for that request and leave it for a person, and the
  workflow status MUST show that and the failure category as the reason. This state MUST persist
  across restarts and MUST end only when a manual advance succeeds or a different step becomes
  due (FR-014); it MUST NOT end by the passing of time.
- **FR-014**: A successful step, a successful manual advance, or a change to which step is due
  MUST reset the consecutive-failure count for that request.
- **FR-015**: If the model is not configured, no automatic step MUST run and nothing MUST be
  recorded as a failure.
- **FR-016**: Failure counts, failure times and the left-for-a-person state MUST be derivable
  after a restart so that a restart loses nothing and repeats nothing; they MUST be kept apart
  from drafts and audit entries and MUST NOT make a failure look like a stored draft.
- **FR-017**: Every automatic step MUST be recorded in the audit trail exactly as a manual step is
  and MUST additionally show that it was run automatically; manual steps MUST NOT show it.
- **FR-018**: The workflow status MUST show when automatic advancement is off, and when a request
  has been left for a person after repeated failures, with the reason.
- **FR-019**: The operator MUST be able to retrieve the summary of the most recent check (time,
  requests examined, steps run, steps failed) through a read-only operator endpoint, and every
  check MUST also be logged as one summary line. Summaries are not stored durably; before the
  first check since a start, the endpoint reports that no check has run yet.
- **FR-020**: No summary, status, audit entry, log or error MUST contain model text, an endpoint,
  a deployment name or a credential.
- **FR-021**: Checks MUST NOT overlap with each other, and one request's failure MUST NOT prevent
  other requests from being examined in the same check.
- **FR-022**: Handling order within a check MUST be deterministic and fair so that no due request
  is starved when the maximum steps per check is smaller than the number due.
- **FR-023**: Existing responses MUST keep their shape and meaning; any additions MUST be purely
  additive and leave every existing construction, stored entry and serialized value valid.
- **FR-024**: Nothing in this feature MUST change how the agents generate content, add agents,
  send notifications, or require authentication or a user interface.
- **FR-025**: The requirements draft step and the design draft step are classified
  **non-consequential**: each produces draft content only, changes no change request status,
  records no decision and clears nothing. Only for that reason may they run without a human asking.
  The classification applies to no other step, and every decision and consequential action still
  requires a recorded human decision.

### Key Entities

- **Automatic Advancement Settings**: Operator-controlled values: on/off (default off), interval,
  maximum steps per check, waiting time after failure, consecutive failures before hand-over.
- **Check Summary**: One record per check: time, requests examined, steps run, steps failed.
  Contains counts and times only. Only the latest is kept, in memory.
- **Automatic Attempt Record**: Per change request and due step: consecutive failure count, last
  failure category and time. Bookkeeping only; not a draft, decision or audit entry.
- **Automatic Origin Marker**: An additive indication on an audit entry that its step was run
  automatically.
- **Change Request, Requirements Draft, Design Draft, Audit Entry, Workflow Status**: Existing
  entities, unchanged in meaning; workflow status gains the off and left-for-a-person indications.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With the feature on and the model configured, 100% of newly submitted requests have a
  requirements draft within one interval plus the step duration, with no one asking.
- **SC-002**: With the feature off, or the model not configured, zero automatic steps run and zero
  failures are recorded over any period.
- **SC-003**: Across all runs, zero change request statuses, decisions or clearances are changed or
  created by automatic activity.
- **SC-004**: For any request, overlapping attempts (any mix of automatic and manual) yield exactly
  one new draft in 100% of tested overlaps.
- **SC-005**: A failing request is attempted no more than once per waiting time, and never after
  the configured consecutive failures until a person acts.
- **SC-006**: After a restart at any point, zero drafts are duplicated or lost and failure waiting
  and hand-over state is honoured in 100% of tested restarts.
- **SC-007**: 100% of automatic steps appear in the audit trail with their automatic marker, and
  0% of manual steps do.
- **SC-008**: Zero occurrences of model text, endpoint, deployment name or credential in any
  summary, status, audit entry, log or error across all tests, including with deliberately leaky
  stand-ins.
- **SC-009**: Every pre-existing automated test continues to pass unchanged, and no existing
  response changes shape or meaning.

## Assumptions

- Safe defaults, to be fixed in planning: feature off; interval five minutes; five steps per check;
  fifteen minutes' wait after a failure; three consecutive failures before hand-over.
- The non-consequential classification in FR-025 is to be reviewed in the constitution compliance
  check (Principle III).
- "Failure" means the same failure categories the agents already report; "model not configured" is
  not a failure.
- An automatic attempt that finds a step already running for that request is skipped (not a
  failure, nothing stored). A manual advance that overlaps a running automatic step behaves as two
  overlapping manual advances already do: it waits, then re-derives, so it either finds nothing due
  (refused with that reason) or runs the step that is still due. Its response shape is unchanged.
- Failure bookkeeping (count, category, time) is persisted so a restart honours it; it is the only
  new fact recorded for failures and is not an audit entry.
- Checks are scheduled by the single running copy of the system; running multiple copies is out of
  scope and no coordination between copies is provided.
- Existing durable storage, workflow status rules, agent operations and audit trail are reused
  unchanged except for additive fields.
- Out of scope: any automatic decision, Developer or Reviewer agent, notifications, multiple
  running copies, user interface, authentication, Microsoft Agent Framework workflows, changes to
  how agents generate content, retention of old records. All sample data is synthetic.
