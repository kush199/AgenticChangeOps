# Tasks: A Change Proposal Requires An Accepted Design

**Input**: Design documents from `specs/023-proposal-requires-accepted-design/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/http-api.md, quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). Tests are written with compiling stubs, shown red once, and implementation starts only after the user says "approved".

**Constitution**: [x] Reviewed against constitution v1.0.6 (plan.md Constitution Check and the table at the end of this file). Tick at T001.

**Organization**: Grouped by the user stories in spec.md: US1 an Accepted design lets the proposal through unchanged (P1), US2 an undecided design blocks it (P1), US3 a rejected design blocks it (P1), US4 only the latest design's decision counts (P2), US5 the design changing mid-preparation refuses the proposal (P2), US6 existing refusals keep their reasons and order (P2). One small slice, a single delivery step. The decision member and the refusal are shared by all stories, so they are built once in Phase 3 and each story phase proves its own behaviour.

**Rules that hold for every task**:
- Every existing response, status code, body, header and error code is identical, except the two new 409 reasons in `contracts/http-api.md`. Every existing test passes with its assertions unchanged, except the ones listed in `specs/023-proposal-requires-accepted-design/baseline/expected-test-edits.md`, approved at T003, which gain only an acceptance step in their setup. If any other test fails, fix the new code, never the assertion.
- The gate reads recorded facts only: no model, no write, no status change, no audit entry, no decision recorded or changed. A refused request makes no generator call.
- Nothing in any response, log or error carries model text, file content, a path, an endpoint, a deployment name, a credential, or caller input.
- No change to Workflow, its offered step, automatic advancement, reviews, decisions, the SQLite schema or how proposals are produced. The shared `Drafting` project and the other three agents are not changed. Synthetic data only. No live model call in the suite.
- After every code task `dotnet build AgenticChangeOps.sln` succeeds; at the end `dotnet test AgenticChangeOps.sln` is green.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel (different files, no dependency on an incomplete task).
- **[USER]**: outward-facing step (push, PR) or human gate. Claude drafts, the user confirms.

---

## Phase 1: Setup

- [x] T001 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md, including the storing-time approach (the service pre-checks eligibility once and re-maps a later decision error to the outdated-design error, with no change to the shared engine) and the `Decision` default of `None`. Tick the **Constitution** line above. No test or source file is written before this (Principle V).
- [x] T002 Confirm branch `feature/proposal-requires-accepted-design`. Run `dotnet test AgenticChangeOps.sln` and save per-project totals (and skipped live checks) to `specs/023-proposal-requires-accepted-design/baseline-tests.txt`, same layout as `specs/022-design-acceptance-decision/baseline-tests.txt`.
- [x] T003 **GATE ([USER])**: write `specs/023-proposal-requires-accepted-design/baseline/expected-test-edits.md` listing every existing test that requests a proposal without first accepting the design, each with the exact setup edit (an acceptance step, or `Decision: Accepted` on the fake basis). Do this after T004, by running the suite once with the stubs in place (`Decision` defaults to `None`, so every affected test fails) and cross-checking with a search of `tests/` (start with `Agents.Developer.Tests/Fakes/FakeProposalBasisSource.cs`, `Unit/Proposal*Tests.cs`, `Characterization/CancellationCharacterizationTests.cs`, `Api.Tests/Fakes/ProposalHost.cs`, `Proposal*EndpointTests.cs`, `ProposalLoggingTests.cs`, `ProposalOffMatchesBaselineTests.cs`, `ProposalCompositionTests.cs`, `Baseline/DraftingSkeletonBaselineTests.cs`, `DesignDecisionIsolationTests.cs`, the Review tests that first request a proposal, and any automatic-advancement test that reaches the proposal step). Include the golden `openapi.json` only if it changes (expected: unchanged). Wait for the word "approved"; no other existing test is edited. T019 corrects the list if the red run finds more.

---

## Phase 2: Stubs and tests first (US1 to US6)

Compiling stubs: in `src/AgenticChangeOps.Agents.Developer/ProposalBasis.cs` add `enum ProposalDesignVerdict { None, Accepted, Rejected }` and append `ProposalDesignVerdict Decision = ProposalDesignVerdict.None` as the last member of `ProposalDesignBasis`. In `ProposalService.cs` add the field-name constant `DesignAcceptanceField = "DesignAcceptance"` only. No behaviour yet, so the new tests are red for the right reason.

- [x] T004 Add the stubs above so the solution builds, then run the suite once and keep the failing list for T003. Extend `tests/AgenticChangeOps.Agents.Developer.Tests/Fakes/FakeProposalBasisSource.cs` additively with a way to set the latest design's `Decision` (default `None`) and to change the design mid-run. No existing member changes meaning.
- [x] T005 [P] [US1] Add `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalDecisionAcceptedTests.cs`: with the latest design `Accepted`, `ProposeAsync` stores a proposal exactly as before (same fields, version 1, `ProposalGenerated` audit entry, generator called once); a Rejected older version with an Accepted latest version proceeds; a second proposal request on the same accepted design stores version 2.
- [x] T006 [P] [US2] Add `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalDecisionUndecidedTests.cs`: latest design `None` gives a failure with one `DesignAcceptance` error whose message says the design has not been decided and differs from the rejected message; generator call count 0; no proposal stored; no audit entry added; change request status unchanged; no decision written.
- [x] T007 [P] [US3] Add `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalDecisionRejectedTests.cs`: latest design `Rejected` gives a failure with one `DesignAcceptance` error saying the design was rejected, distinct from the undecided message; generator call count 0; nothing stored or audited; an Accepted older version does not rescue it.
- [x] T008 [P] [US4] Add `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalDecisionLatestOnlyTests.cs`: version 1 Accepted with latest version 2 undecided is refused as not decided; version 1 Rejected with latest version 2 Accepted proceeds; the basis adapter case lives in T012.
- [x] T009 [P] [US5] Add `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalDecisionStaleAtStoreTests.cs`: with a generator that, while running, switches the fake basis to a newer design (undecided or Accepted) or to a design whose decision is no longer `Accepted`, `ProposeAsync` fails with the existing outdated-design error (`DesignDraftVersion`), not the decision error; nothing stored, no audit entry; proposals stored earlier are unchanged (same objects, same count) after a later newer design or decision.
- [x] T010 [P] [US6] Add `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalDecisionOrderTests.cs`: with the design undecided, each existing refusal still wins and keeps its message: unknown request (not found), not Approved (`Status`), no requirements draft (`RequirementsDraft`), no design draft (`DesignDraft`), design based on an older requirements version (`DesignDraftVersion`); the decision error appears only when all four pass. The basis source is not consulted for a non-Approved request (existing rule).
- [x] T011 [P] [US2] [US5] Add `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalDecisionPreCheckTests.cs`: the service evaluates eligibility (including the decision) before the engine, so a refusal makes no generator call and stores nothing; when the first check passes and the fake basis then changes (a newer design, or a latest design that is no longer Accepted) before storing, the result is the existing outdated-design error (`DesignDraftVersion`) and never the `DesignAcceptance` error; when the first check itself fails on the decision, the `DesignAcceptance` error is returned unchanged; every other engine failure (generator failure, rejection, storage) is returned unchanged.
- [x] T012 [P] [US4] Add `tests/AgenticChangeOps.Api.Tests/ProposalBasisAdapterDecisionTests.cs` (follow `ReviewBasisAdapterTests`): the adapter reports `None` for an undecided latest design, `Accepted` or `Rejected` from the recorded decision of the latest version only, ignores a decision on an older version, and reads decisions through `IDesignDecisionService` without writing.
- [x] T013 [P] [US1] [US2] [US3] Add `tests/AgenticChangeOps.Api.Tests/ProposalDecisionEndpointTests.cs` (follow `ProposalEndpointTests` and `ProposalHost`): Accepted design gives 201 with the same body and `Location` as before; undecided gives 409 `conflict` with a `DesignAcceptance` error entry saying not decided; Rejected gives 409 `conflict` with a different message; the error body has the same shape as the existing proposal conflicts; fake generator call count 0 and no proposal, audit or decision stored after each refusal; the workflow status and change request status are unchanged.
- [x] T014 [P] [US4] [US6] Add `tests/AgenticChangeOps.Api.Tests/ProposalDecisionOrderEndpointTests.cs`: end to end, a decision on version 1 followed by a new design (version 2) gives 409 not decided until version 2 is Accepted, then 201; each existing refusal (400 invalid id, 404 unknown, 409 not Approved, 409 no requirements, 409 no design, 409 outdated design, 503/availability errors) is returned unchanged for an undecided design.
- [x] T015 [P] [US5] Add `tests/AgenticChangeOps.Api.Tests/ProposalDecisionRaceEndpointTests.cs`: a smoke test (the deterministic proof is T009) in which a proposal request races a new design request, run many times, ends either 201 against the then-latest Accepted design or 409 outdated/not decided, never a stored proposal whose `DesignVersion` is not an Accepted latest version at the time of storing; earlier proposals are untouched.
- [x] T016 [P] [US2] [US3] Add `tests/AgenticChangeOps.Api.Tests/ProposalDecisionNoLeakTests.cs` (follow `NoLeakTests`): refusal bodies and captured logs for both new reasons contain no sentinel endpoint, deployment name, credential, model text, sample content, nor the caller's input; the log line for a refusal names only the field.
- [x] T017 [P] [US6] Add `tests/AgenticChangeOps.Api.Tests/ProposalDecisionIsolationTests.cs`: a refused proposal changes nothing in `GET .../workflow`, reviews, design decisions or automatic advancement results; with automatic advancement on, the proposal step of an undecided or Rejected design is refused the same way and no decision is ever recorded by the automatic path.
- [x] T018 [P] Add a structure check to `tests/AgenticChangeOps.Agents.Developer.Tests/DeveloperStructureTests.cs` (additive): the Developer project still references no Architect, Requirement or Reviewer project and `ProposalService` holds no decision store or writer. The shared `Drafting` project is untouched, so its tests need no change.
- [x] T019 **GATE ([USER])**: run the new tests, save the red output to `specs/023-proposal-requires-accepted-design/red-run.txt`, show it, apply the approved edits from T003 to the existing tests (acceptance step only), and wait for the word "approved". Existing tests other than the listed ones must still pass.

---

## Phase 3: Implementation (US1 to US6)

- [x] T020 [US2] [US3] In `src/AgenticChangeOps.Agents.Developer/ProposalService.cs` extend `Agent.CheckEligibility`: after the existing design-based-on-latest-requirements check, `None` gives `ValidationError(DesignAcceptanceField, "The latest design draft for change request {id} has not been decided; a person must accept it before a proposal can be drafted.")`, `Rejected` gives `ValidationError(DesignAcceptanceField, "The latest design draft for change request {id} was rejected; a proposal cannot be drafted until a newer design draft is accepted.")`, `Accepted` passes. Messages contain only the id. Update the order summary on `CheckEligibility`.
- [x] T021 [US5] In the same file change `ProposeAsync`: first call `_agent.CheckEligibility(id, _store.FindById(id))`; if it returns an error, return `ProposalResult.Failure` with that single error (same shape as the engine's own eligibility failure). Otherwise call the engine as today; if the engine's failure contains a `DesignAcceptanceField` error, return `ProposalResult.Failure` with `OutdatedDesign(id)` instead (the first check passed and decisions are immutable, so a newer design is the only cause). All other outcomes are returned unchanged. Do not change `src/AgenticChangeOps.Drafting`.
- [x] T022 [US5] Keep `Agent.CheckStillCurrent` as it is (version comparison). Optionally also return `OutdatedDesign` when `current.Design.Decision != ProposalDesignVerdict.Accepted`: this cannot occur with real data because decisions are final, so keep it only as a guard that the fake-basis tests in T009 exercise; delete it if it adds noise.
- [x] T023 [US4] In `src/AgenticChangeOps.Api/Composition/ProposalAvailability.cs` give `ProposalBasisAdapter` a `Func<Guid, int, ProposalDesignVerdict>` constructor parameter and set `ProposalDesignBasis.Decision` from it for the latest design version. In `src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs` register the adapter with a factory that supplies the function, built from `IDesignDecisionService.GetDecisions` (the decision whose `Version` equals the version asked for: `Accepted`/`Rejected`, else `None`). Reason: the existing `DesignDecisionStructureTests` (Api and Architect) allow only the design endpoints and the composition to name the decision service, and forbid the text `DesignDecision` in the Developer project, so the lookup lives in the composition file and the Developer's names are `ProposalDesignVerdict` and `DesignAcceptance`.
- [x] T024 [US2] [US3] In `src/AgenticChangeOps.Api/Errors/ErrorMapper.cs` add `DesignAcceptanceField` to the fields in `ForProposing` that map to `Conflict(errors)`, and update the comment. No new code, status or body shape.
- [x] T025 Run the full suite: `dotnet build AgenticChangeOps.sln` then `dotnet test AgenticChangeOps.sln`. Every new test passes; every existing test passes with assertions unchanged (only the T003 setup edits). Fix code, not assertions.

---

## Phase 4: Polish and cross-cutting

- [x] T026 [P] Update the doc comments for `/change-requests/{id}/proposals` (`ProposalEndpoints.cs` summary only if the golden stays unchanged) and the order comment in `ProposalService.cs`. Add a short note on the gate to `README.md` only if it already documents proposal prerequisites.
- [x] T027 [P] Write `specs/023-proposal-requires-accepted-design/verification.md`: spec requirement to test mapping, final test totals vs `baseline-tests.txt`, the list of edited existing tests (setup only), and the statement that no live model check is needed because the gate runs before the model.
- [ ] T028 [USER] Confirm no secret or real data was added (`git diff main --stat`, search for endpoints and keys). Commit on `feature/proposal-requires-accepted-design` referencing slice 023, push, and open the PR only when the user asks. Add a memory note for the slice after the PR exists.

---

## Dependencies and order

T001 -> T002 -> T004 -> T003 (gate) -> T005 to T018 (parallel, different files) -> T019 (gate) -> T020 -> T021 -> T022 -> T023 -> T024 -> T025 -> T026, T027 -> T028.
User stories share the Phase 3 code; each story's tests (T005 to T017) prove it independently.

**MVP**: US1 + US2 + US3 (T005 to T007, T013) with T020, T021 and T024; US4 to US6 complete the slice and ship in the same PR.

## Constitution compliance (v1.0.6)

| Principle | Status |
|---|---|
| I. Synthetic data | Pass. Fixtures are synthetic; T028 checks the diff. |
| II. Deterministic routing | Pass. T020 to T022 are plain predicates on recorded facts; no model client is touched (T018). |
| III. Human gates | Pass. A proposal now needs a recorded human decision; nothing here records or automates one (T017). |
| IV. Typed auditable records | Pass. No new record; the added member is defaulted and not persisted. |
| V. Spec-driven | Pass. T001 gates all code; T019 gates implementation. |
| VI. Test-first | Pass. T004 to T018 precede T020 to T024; red output shown at T019. |
| VII. Simplicity | Pass. No new agent or shared abstraction; the shared `Drafting` project is unchanged. |

Technology Stack: no new technology, no amendment.

---

## Phase 5: Convergence

Found by `/speckit-converge` on 2026-11-04. The code satisfies every functional requirement, success criterion and plan decision in `spec.md` and `plan.md`; the remaining items are tests that T011, T016 and T017 promised but that were only partly written. Add each test, show it, and only change source if a test finds a real defect.

- [x] T029 [P] Add `tests/AgenticChangeOps.Api.Tests/ProposalDecisionAutomaticAdvancementTests.cs`: with automatic advancement switched on (as in `Automatic/OperatorAndStatusTests.cs`) and a real-chain `ProposalHost`, a request whose latest design is undecided and another whose latest design is Rejected receive the same automatic passes as without a decision (same steps run, same generator call counts), the automatic path never requests a proposal for them and never records a design decision or a `DesignDecided` audit entry, per T017, spec out of scope: automatic advancement (partial)
- [x] T030 [P] In `tests/AgenticChangeOps.Api.Tests/ProposalDecisionNoLeakTests.cs` replace the weak log assertion with one that a refused request writes exactly one line containing "Proposal request stored nothing", that the line names `DesignAcceptance` and contains no id, message text, path, endpoint, deployment name or caller input, and that a successful proposal for an Accepted design writes no such line, per T016, FR-014 (partial)
- [x] T031 [P] In `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalDecisionPreCheckTests.cs` add two cases after a passing first check: content the system rejects (a quote that does not match the sample) is returned as the same rejection as before, and a storage failure while saving is returned as before; neither is reported as `DesignAcceptance` or turned into the outdated-design error, per T011 (partial)
