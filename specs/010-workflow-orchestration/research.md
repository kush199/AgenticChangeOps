# Research: Workflow Orchestration

No `NEEDS CLARIFICATION` remained after `/speckit-clarify`. The decisions below close the two items
the clarify pass deferred to planning (HTTP shape, concurrency) and record the structural choices.

## D1. Where the rules live

- **Decision**: new library `AgenticChangeOps.Workflow`, referencing Intake, both agents and ModelAccess.
- **Rationale**: the rules need both agents' outputs. The Architect deliberately does not reference the
  Requirement agent (slice 008) and ModelAccess must stay agent-neutral (slice 009), so the only place
  that may know both is a new orchestration layer. A library keeps it testable without HTTP.
- **Alternatives**: in `Api` (testable only over HTTP, mixes rules with transport); in the Architect
  (would make it reference the Requirement agent, breaking its dependency rules).

## D2. How facts are read

- **Decision**: read the change request, its requirements drafts and its design drafts in one step
  under `IChangeRequestStore.SyncRoot`, using the stores' existing `FindById` and the agents' public
  `GetDrafts` / `GetDesigns` store extensions. Pass plain values to a pure `WorkflowRules.Derive`.
- **Rationale**: one lock gives a consistent snapshot, so status can never describe a half-saved
  state; the pure function makes the rule table a plain data test. No new store member is needed.
- **Alternatives**: call each service's `Get...History` (three separate reads, no single snapshot;
  also pulls `ValidationError` handling into the rules).

## D3. Deriving the stage and the next step

Rule order (first match wins), with `R` = requirements drafts, `D` = design drafts, latest versions:

| # | Change request status | Facts | Stage | Next step | Reason |
|---|---|---|---|---|---|
| 1 | Rejected | any | `Rejected` | none | `Rejected` |
| 2 | Deferred | R empty | `Deferred` | requirements draft | none |
| 3 | Deferred | R present | `Deferred` | none | `Deferred` |
| 4 | Submitted | R empty | `AwaitingRequirementsDraft` | requirements draft | none |
| 5 | Submitted | R present | `AwaitingDecision` | none | `AwaitingDecision` |
| 6 | UnderReview | any | `AwaitingDecision` | none | `AwaitingDecision` |
| 7 | Approved | R empty | `AwaitingRequirementsDraft` | none | `RequirementsDraftRequired` |
| 8 | Approved | R present, D empty or latest D based on an older R | `ReadyForDesign` | design draft | none |
| 9 | Approved | latest D based on latest R | `DesignComplete` | none | `NothingDue` |

Requirement eligibility mirrors the Requirement agent's own rule (Submitted or Deferred only), which
is why rows 6 and 7 offer no step. A drift check test pins this: for every status, "a requirements
step is offered" equals "the Requirement agent accepts a draft".

## D4. Concurrency

- **Decision**: a per-change-request `SemaphoreSlim` held across derive-then-run in `AdvanceAsync`.
  A second advance waits, re-derives and finds the step done. Status reads take no lock.
- **Rationale**: spec edge case requires no duplicated or skipped version and "second reports
  nothing due". The agents' own save lock cannot provide that because both advances would already
  have decided the same step.
- **Alternatives**: one global lock (blocks unrelated requests for the length of a model call);
  optimistic re-check after the step (would already have spent a model call and stored a version).
- **Note**: direct calls to the agents' own operations are not serialised against advance; they keep
  their current behaviour (distinct consecutive versions). Locks are never removed (in-memory,
  bounded by the number of change requests, consistent with the non-durable store).

## D5. Not-configured handling

- **Decision**: `IStepAvailability.IsConfigured(step)`, implemented in the API composition from the
  existing `DraftingAvailability` and `DesignAvailability`. Checked only after the step has been
  determined, before the agent is called.
- **Rationale**: preserves the agents' routes' convention (503 with `drafting-not-configured` or
  `design-not-configured`, nothing stored, nothing audited) while letting "nothing due" win.

## D6. HTTP shape

- **Decision**: `GET /change-requests/{id}/workflow` (200 `WorkflowStatus`) and
  `POST /change-requests/{id}/workflow/advance` (201 with the step result and status afterwards).
  No request body. See [contracts/http-api.md](contracts/http-api.md).
- **Rationale**: same `{id}` routing and the same `RequestParsing`/`ErrorMapper` as every other route.
  201 matches the agents' own routes, because an advance that succeeds has created a draft.
- **Alternatives**: `PUT`/`PATCH` on a workflow resource (implies stored workflow state, which the
  spec forbids); a combined route that accepts a step name (lets the caller choose the order, which
  the spec reserves to the system's rules).

## D7. Failure mapping

- **Decision**: advance returns the agent's own result object untouched on failure and the API maps
  it with the existing `ErrorMapper.ForDrafting` / `ForDesigning`. One new mapper entry, `NoStepDue()`,
  is added for the refusal that has no agent behind it.
- **Rationale**: spec requires agent failures be reported "as for the agent's own operation"; reuse
  makes that true by construction and keeps the no-leak guarantees (only a category is ever carried).

## D8. Audit

- **Decision**: no new audit event type and no orchestration-written entries.
- **Rationale**: FR-012. Because the agent services are called as-is, the entries are identical to a
  direct call, including `DraftVersion` and agent name.

## D9. Existing tests that must change

Same two reviewed updates as slice 008, nothing else:

1. The API description test's list of routes gains the two new operations.
2. The recorded API description (`tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json`)
   gains the two operations, the new schemas, and the new error code in the description text.

The design and requirements HTTP baselines must be unchanged, which is the evidence that agent routes
did not move.
