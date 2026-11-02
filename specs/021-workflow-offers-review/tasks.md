# Tasks: The Workflow Status Offers The Review Once A Current Change Proposal Exists

**Input**: Design documents from `specs/021-workflow-offers-review/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/workflow-status.md, quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). Tests are written with compiling stubs, shown red once, and implementation starts only after the user says "approved".

**Constitution**: [x] Reviewed against constitution v1.0.6 (plan.md Constitution Check and the table at the end of this file). Tick at T001.

**Organization**: Grouped by the user stories in spec.md: US1 the review is offered after a current proposal (P1), US2 the offer is replaced only where the rules say so (P1), US3 outdated records neither trigger nor suppress the offer (P2), US4 advancing never runs the offered review (P2). One small slice, so a single delivery step.

**Rules that hold for every task**:
- Every existing response, status code, body, header and error code is identical, except `offeredStep` may now be `"Review"` and a request with a current proposal no longer shows `"Proposal"`. Every existing test passes with its assertions unchanged, except those listed in `specs/021-workflow-offers-review/baseline/expected-test-edits.md`, approved at T003. If any other test fails, fix the new code, never the assertion.
- The offer is derived on every read from recorded facts, never stored, never asks a model, and never changes `stage`, `nextStep`, `reason`, `completedSteps` or `automatic`.
- Nothing in any response, log or error carries model text, file content, a path, an endpoint, a deployment name or a credential.
- No change to any agent project, Intake, stores, endpoints or automatic advancement. Workflow gains no project reference. Synthetic data only. No live model call in the suite.
- After every code task `dotnet build AgenticChangeOps.sln` succeeds; at the end `dotnet test AgenticChangeOps.sln` is green.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel (different files, no dependency on an incomplete task).
- **[USER]**: outward-facing step (push, PR) or human gate. Claude drafts, the user confirms.

---

## Phase 1: Setup

- [x] T001 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md, including the plan's decision that once a current proposal exists the proposal is never offered (review unavailable gives no offer). Tick the **Constitution** line above. No test or source file is written before this (Principle V).
- [x] T002 Confirm branch `feature/workflow-offers-review`. Run `dotnet test AgenticChangeOps.sln` and save per-project totals (and skipped live checks) to `specs/021-workflow-offers-review/baseline-tests.txt`, same layout as `specs/020-reviewer-retrieval-operations/baseline-tests.txt`.
- [x] T003 **GATE ([USER])**: write `specs/021-workflow-offers-review/baseline/expected-test-edits.md` listing every existing test that enumerates step names or asserts `offeredStep` for a request with a current proposal, with the exact edit, and the golden `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json` (the `WorkflowStep` enum gains `3`). Search `tests/` for `WorkflowStep`, `offeredStep` and `OfferedStep` first (known: `ProposalOfferTests`, `ProposalCompositionTests`, `ProposalOffMatchesBaselineTests` assert on a request with no proposal and are expected to need no edit). Wait for the word "approved"; no other existing test is edited.

---

## Phase 2: Stubs and tests first (US1 to US4)

Compiling stubs, all in `src/AgenticChangeOps.Workflow/`: append `Review` to `WorkflowStep` in `WorkflowStep.cs`; add `ProposalFact(int Version, int DesignVersion)` and `ReviewFact(int ProposalVersion)` to `WorkflowFacts.cs`; add `IReviewOffer` (`bool IsOffered { get; }`) in `IReviewOffer.cs`; add `IOfferFactSource` (`IReadOnlyList<ProposalFact> GetProposals(Guid)`, `IReadOnlyList<ReviewFact> GetReviews(Guid)`) in `IOfferFactSource.cs`; add `OfferRules.Derive(...)` in `OfferRules.cs` throwing `NotImplementedException`; add optional trailing constructor parameters `IReviewOffer? reviewOffer = null, IOfferFactSource? offerFacts = null` to `WorkflowService` (not yet used).

- [x] T004 Add the stubs above so the solution builds, and extend `tests/AgenticChangeOps.Workflow.Tests/Fakes/WorkflowTestFixture.cs` additively with a settable fake `IOfferFactSource` and a settable fake `IReviewOffer`. No existing member changes.
- [x] T005 [P] [US1] [US2] [US3] Add `tests/AgenticChangeOps.Workflow.Tests/Unit/OfferRulesTests.cs`, a table test of the rows in `data-model.md`: not `DesignComplete` gives none; `DesignComplete` with no proposal on the latest design gives `Proposal` when proposals are available and none otherwise; with a proposal on the latest design and no review of its version gives `Review` when reviewing is available and none otherwise; with a review whose `ProposalVersion` equals that proposal's version gives none. Plus stale cases: a proposal only on an older design does not trigger `Review` (gives the no-current-proposal result); a review of an older proposal version does not suppress `Review` for a newer current proposal; several proposals on the latest design use the highest version; facts given in any order give the same result.
- [x] T006 [P] [US1] [US2] Add `tests/AgenticChangeOps.Workflow.Tests/Unit/ReviewOfferTests.cs` at service level (real services, fake seams, as `ProposalOfferTests` does): an Approved request at `DesignComplete` with a current proposal fact and reviewing available shows `OfferedStep == Review`, and `Stage`, `NextStep`, `Reason`, `CompletedSteps` and `Automatic` equal the same status read without the seams; serialised status contains `"offeredStep":"Review"`; with no seams (null) the status equals today's; a request that is not Approved (Submitted, UnderReview, Deferred, Rejected) never shows `Review`; unknown id gives the existing not-found result; two reads of unchanged facts are equal; reading never writes (store contents identical before and after) and the reviewing seam is not consulted unless every other condition holds.
- [x] T007 [P] [US4] Add `tests/AgenticChangeOps.Workflow.Tests/Unit/ReviewOfferAdvanceTests.cs`: with the review as the only offer, `AdvanceAsync` returns `AdvanceOutcome.NothingDue`, no generator is called and no record, audit entry or status changes; `TryRunAsync` (the automatic path) likewise never runs it, and `ReviewOffer` is not a possible `NextStep` of any derived status in a sweep over every status and fact combination in the test.
- [x] T008 [P] [US1] [US2] [US3] Add `tests/AgenticChangeOps.Api.Tests/ReviewOfferCompositionTests.cs` with the real composition (follow `ProposalCompositionTests` and `ReviewHost`): after requirements, design and a proposal, `GET /change-requests/{id}/workflow` shows `offeredStep` `"Review"` with `nextStep` null and the same `stage`/`reason`; after requesting the review it shows no `offeredStep`; after regenerating the design and a new proposal it shows `"Review"` again; on a host whose review settings are invalid the status is 200 with no `offeredStep` and no error; advance returns the existing 409 nothing-due body and the model client records no extra call; a malformed id gives the existing 400 and an unknown id the existing 404.
- [x] T009 [P] [US1] Add the no-leak and read-only checks to `ReviewOfferCompositionTests.cs`: repeated status reads leave stored proposals, reviews, audit events and status byte-identical; no log line at warning level or above is written by a read; sentinel endpoint, deployment name, credential, model text and file content never appear in the status response or captured logs.
- [x] T010 **GATE ([USER])**: run the new tests, save the red output to `specs/021-workflow-offers-review/red-run.txt`, show it, apply the approved edits from T003 to the existing tests, and wait for the word "approved". Existing tests other than the listed ones must still pass. The golden description check is expected red until T015.

---

## Phase 3: Implementation (US1 to US4)

- [x] T011 [US1] [US2] [US3] Implement `OfferRules.Derive` in `src/AgenticChangeOps.Workflow/OfferRules.cs` as a pure function (no store, service or model): inputs are the stage, the latest design version, whether proposals are offered, whether reviewing is offered, the proposal facts and the review facts; output is `WorkflowStep?` exactly per the table in `data-model.md`. The review-availability input is a lazy `Func<bool>` evaluated only when a current proposal exists and no review of it does.
- [x] T012 [US1] [US2] In `src/AgenticChangeOps.Workflow/WorkflowService.cs` `Derive`, read the proposal and review facts through `IOfferFactSource` inside the existing `lock (_store.SyncRoot)` block, only when an `IOfferFactSource` exists; replace the inline `if (status.Stage == DesignComplete && _proposalOffer ...)` with a call to `OfferRules.Derive` and set `OfferedStep` from its result. With no `IOfferFactSource` the proposal offer behaves exactly as before. Stage, next step, reason, completed steps and automatic are not touched. `RunNextStepAsync` is not changed.
- [x] T013 [US1] [US2] [US3] In `src/AgenticChangeOps.Api/Composition/` add `OfferFactSource : IOfferFactSource` (reads `IChangeRequestStore.GetAgentOutputs<ChangeProposal>` and `<Review>`, reduces to `ProposalFact(Version, DesignVersion)` and `ReviewFact(ProposalVersion)`) and `ReviewOffer : IReviewOffer` (checks the model and review settings itself and logs nothing; built over `ReviewAvailability.IsConfigured` first, changed because that builds the review setup and logs on a status read), in files beside `ProposalAvailability.cs` and `ReviewAvailability.cs`. Register both in `ApiServiceRegistration.cs` next to `IProposalOffer`, and let `WorkflowService` receive them (constructor parameters from T004).
- [x] T014 Run `dotnet build AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln --filter "FullyQualifiedName~Offer|FullyQualifiedName~Workflow"` until the new and edited tests are green.
- [x] T015 [US4] Regenerate the golden `openapi.json` only through the project's reviewed update variable (`ApiBaselineTests.UpdateVariable` = 1) and check the diff is exactly one added enum value (`3`) in `WorkflowStep` and nothing else.
- [x] T016 [P] Confirm by test (extend `tests/AgenticChangeOps.Workflow.Tests/Unit/` structure checks or add `OfferStructureTests.cs`) that `AgenticChangeOps.Workflow` still references only Intake, ModelAccess, Requirement and Architect, that `OfferRules` names no agent type (`ChangeProposal`, `Review`) and holds no store, and that no `AgenticChangeOps.Agents.*` project references another agent.

---

## Phase 4: Polish

- [x] T017 Run `dotnet test AgenticChangeOps.sln`: every project green with the T002 counts plus the new tests; record in `specs/021-workflow-offers-review/verification.md` the totals, the list of existing-test edits (must equal `expected-test-edits.md`) and the golden diff summary. Tick `checklists/requirements.md` if still valid.
- [x] T018 [P] Update `README.md` where it describes `offeredStep` / the proposal offer: the status offers `Proposal` until a current proposal exists, then `Review` until that proposal is reviewed; information only, never run by advance. Search `*.md` outside `specs/` for other mentions of the offered step.
- [ ] T019 **[USER]**: add a memory note and report the live check as not run (needs real Azure OpenAI settings and a full chain). Commit on `feature/workflow-offers-review`, push and open the PR only when the user asks.

---

## Dependencies and order

- T001 to T003 first, in order; T004 before T005 to T009; T005 to T009 are parallel; T010 gates T011 to T016.
- T011 first (pure rule); T012 needs T011; T013 needs T012 for the constructor shape; T014 needs T013; T015 needs T014; T016 may run beside T014.
- Story coverage: US1 T005, T006, T008, T009, T011 to T013; US2 T005, T006, T008, T011, T012; US3 T005, T008, T011, T013; US4 T007, T008, T015.

## Implementation strategy

MVP is US1 (T011 to T013), but because the slice is one pure rule, two small adapters and one enum value, deliver all stories in one step: tests first, approval, then implementation, then the full suite.

## Constitution compliance check (pre-code)

Checked against constitution v1.0.6.

| Principle | spec.md | plan.md | tasks.md |
|---|---|---|---|
| I. Synthetic data | Pass | Pass | Pass (rules: synthetic only; sentinels in T009 are fictional) |
| II. Deterministic routing | Pass | Pass | Pass (T011 pure predicate; T006 and T009 prove no model is consulted) |
| III. Human gates | Pass (information only) | Pass | Pass (T007 proves advance and automatic never run the review) |
| IV. Typed auditable records | Pass | Pass | Pass (no record changes; enum value appended, T015) |
| V. Spec-driven | Pass | Pass | Pass (T001 gate before any code) |
| VI. Test-first | Pass | Pass | Pass (T010 gate, red run saved) |
| VII. Simplicity | Pass | Pass (seams recorded in Complexity Tracking) | Pass (T016 keeps Workflow free of agent references) |

Technology Stack: no new technology, no amendment. Commit messages reference the slice name
"021 workflow-offers-review".

## Phase 5: Convergence

- [x] T020 Add a test in `tests/AgenticChangeOps.Api.Tests/ReviewOfferCompositionTests.cs` that a host with a valid model but invalid review settings (the `Garbage` values of `ReviewSettingsTests`) answers the workflow status with 200, no `offeredStep` for a current proposal, and writes no "not configured" log line, per FR-009 and the plan's lazy-setup constraint (partial)
- [x] T021 Record in `specs/021-workflow-offers-review/plan.md`, `research.md` (decision 3) and `tasks.md` T013 that `ReviewOffer` checks the model and review settings itself and logs nothing, instead of reading `ReviewAvailability`, and that the adapters live in `src/AgenticChangeOps.Api/Composition/OfferAdapters.cs`, per plan: reviewing availability decision (contradicts)
- [x] T022 [P] Add a test in `tests/AgenticChangeOps.Api.Tests/ReviewOfferCompositionTests.cs` that the offer follows availability on each read (a status read, then a change of the review settings or model, then another read) and that nothing is remembered, per spec Edge Cases (partial)
- [x] T023 [P] Add a test in `tests/AgenticChangeOps.Workflow.Tests/Automatic/` that with automatic advancement on, a request reaching a current proposal is never run for the review by the automatic service itself (not only by `TryRunAsync`), per US4/AC2 (partial)
- [x] T024 [P] Add a test in `tests/AgenticChangeOps.Api.Tests/` that the review offer is derived the same way when the durable storage provider is used (proposal and review read back through the durable store), per FR-001 and FR-006 (partial)
