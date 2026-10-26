# Tasks: HTTP Operation To Request A Review

**Input**: Design documents from `specs/019-reviewer-request-operation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/http-api.md, quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). Tests are written with compiling stubs, shown red once, and implementation starts only after the user says "approved".

**Constitution**: [x] Reviewed against constitution v1.0.6 before approval (plan.md Constitution Check). Tick at T001.

**Organization**: Grouped by the user stories in spec.md: US1 request a review (P1), US2 fixed check order (P1), US3 distinct refusals and failures store nothing (P1), US4 overlapping requests (P2), US5 description lists it and nothing else changes (P2). One small slice, so a single delivery step.

**Rules that hold for every task**:
- Every existing response, status code, body, header and error code is identical, and every existing test passes with its assertions unchanged. The only edits to existing tests are those listed in `specs/019-reviewer-request-operation/baseline/expected-test-edits.md`, approved at T003. If any other test fails, fix the new code, never the assertion.
- Nothing in any response, log or error carries model text, finding text, a reference, file content, a path, an endpoint, a deployment name or a credential. Refusals report fixed text, rule field names, a failure category or a rejection name only.
- The operation adds no rule: eligibility, outdated inputs, limits and reference checks stay in `ReviewService`. It changes no status and has no `Location` header.
- No change to `AgenticChangeOps.Agents.Reviewer`, Workflow, Intake, stores or any other agent. Synthetic data only. No live model call in the suite.
- After every code task `dotnet build AgenticChangeOps.sln` succeeds; at the end `dotnet test AgenticChangeOps.sln` is green.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel (different files, no dependency on an incomplete task).
- **[USER]**: outward-facing step (push, PR) or human gate. Claude drafts, the user confirms.

---

## Phase 1: Setup

- [x] T001 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md. Tick the **Constitution** line above. No test or source file is written before this (Principle V).
- [x] T002 Confirm branch `feature/reviewer-request-operation`. Run `dotnet test AgenticChangeOps.sln` and save per-project totals (and skipped live checks) to `specs/019-reviewer-request-operation/baseline-tests.txt`, same layout as `specs/018-reviewer-agent-review/baseline-tests.txt`.
- [x] T003 **GATE ([USER])**: list in `specs/019-reviewer-request-operation/baseline/expected-test-edits.md` every existing test that enumerates operations or the published description, with the exact edit each needs. Known: `tests/AgenticChangeOps.Api.Tests/DescriptionTests.cs` (route list gains `POST /change-requests/{id}/reviews`), `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json` (gains only `RequestReview`, reviewed diff), `tests/AgenticChangeOps.Api.Tests/ReviewOffMatchesBaselineTests.cs` (`NoRoute_MentionsReviews` and `TheApiDescription_MentionsNoReviewRouteTagOrSchema` assert the absence of a review route and must be replaced by tests that the single review route and nothing more exists), and any comment in `Fakes/ReviewHost.cs` saying no route exists. Search `tests/` for any other test that enumerates routes or the description. Show the list and wait for the word "approved"; no other existing test is edited.

---

## Phase 2: Stubs and tests first (US1 to US5)

Compiling stubs: `ReviewAvailability` (`src/AgenticChangeOps.Api/Composition/ReviewAvailability.cs`, `IsConfigured` property), `ErrorMapper.ForReviewing` and `ErrorMapper.ReviewNotConfigured` (throwing `NotImplementedException`), and `ReviewEndpoints.Map` not yet mapped.

- [x] T004 Add the stubs above so the solution builds, and extend `tests/AgenticChangeOps.Api.Tests/Fakes/ReviewHost.cs` additively with a helper that posts `POST /change-requests/{id}/reviews` (no body) and returns the response. No existing member changes.
- [x] T005 [P] [US1] Add `tests/AgenticChangeOps.Api.Tests/ReviewEndpointTests.cs`: eligible request gives 201, JSON body equal to the stored review (`version` 1, `status` `Draft`, `agentName` `ReviewerAgent`, `systemCheck`, `findings`, `openQuestions`); no `Location` header and no retrieval link in headers or body; a second request gives version 2 and version 1 unchanged; change request status unchanged before and after; the audit list gains exactly one `ReviewGenerated` entry per success; a request body (valid, malformed or oversized JSON) is ignored and gives the same 201; the success body uses camel-case members and string enums, and the error body shape matches the proposal operation's (`code`, `message`, optional `errors`, optional `category`).
- [x] T006 [P] [US2] Add `ReviewCheckOrderTests.cs`: malformed id on an unconfigured server gives 400 `invalid-identifier`; unknown id on an unconfigured server gives 404 `not-found`; an existing but ineligible change request on an unconfigured server gives 503 `review-not-configured`; on a configured server the same ineligible request gives 409 `conflict`; in every earlier-check case the scripted model client records zero calls and nothing is stored. Not configured is produced by an invalid review/model setting, never by a test hook that skips the real setup.
- [x] T007 [P] [US3] Add `ReviewRefusalEndpointTests.cs` and `ReviewFailureEndpointTests.cs`: each conflict gives 409 `conflict` with `errors` naming exactly `Status`, `RequirementsDraft`, `DesignDraft`, `DesignDraftVersion`, `Proposal` or `ProposalVersion`, fixed messages, model not called, nothing stored; a record changing while generating gives 409 `conflict` with `ProposalVersion`; model unreachable, timeout, answer too large and malformed answer give 502 `generation-failed` with only `category`; each reachable `ReviewRejection` value (`TooManyFindings`, `TooLarge`, `MissingReference`, `UnresolvedReference`; `MalformedFinding` is defined but never produced today) gives 502 `review-rejected` with only that `category`; a storage failure and a version conflict keep their existing 503 `storage-unavailable` and 409 `version-conflict` shapes; every case leaves zero stored reviews and zero `ReviewGenerated` entries; the status code and `code` are asserted for each. For every conflict case the response `errors` equal the field names `ReviewService` reported (compared to the service's own error list), proving the endpoint adds no check beyond id, existence and configuration. A model client that throws an unexpected exception gives 502 `generation-failed` with a category only, nothing leaked, and stores nothing (the engine turns every generator exception into a failure category; the 500 `unexpected-error` code is therefore not reachable from a model call and stays untested).
- [x] T008 [P] [US3] Add `ReviewLoggingTests.cs` and extend the sentinel pattern of `ProposalLoggingTests`/`NoLeakTests` in a new file: with sentinel endpoint, deployment name, credential, model text, finding text, reference text and file content planted, no response body, header or captured log line (success, refusal, failure, rejection) contains any of them; each request that stores nothing logs exactly one warning on `AgenticChangeOps.Api.Reviewing` naming only the rejection name, failure category or refused field names; a successful request logs no warning.
- [x] T009 [P] [US4] Add `ReviewConcurrencyEndpointTests.cs`: N (for example 8) simultaneous requests for one eligible change request through the route give stored versions exactly 1..N with no repeat and exactly N audit entries; with some requests made to fail (scripted model failure) the successful ones still give consecutive versions starting at 1.
- [x] T010 [P] [US5] Add `ReviewDescriptionTests.cs`: the published description contains `POST /change-requests/{id}/reviews` with operation id `RequestReview` and responses 201, 400, 404, 409, 500, 502 and 503, and no other new path, tag or schema beyond what `Review` and its members need; no `GET .../reviews` route exists; the workflow status body still has no review member.
- [x] T011 **GATE ([USER])**: run the new tests, save the red output to `specs/019-reviewer-request-operation/red-run.txt`, show it, apply the approved edits from T003 to the existing tests, and wait for the word "approved". Existing tests other than the listed ones must still pass. The two replaced route/description tests are expected red until T014 and green again at T015.

---

## Phase 3: Implementation (US1 to US5)

- [x] T012 [US2] In `src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs`, give `ReviewSetup` an `IsConfigured` flag (true on success, false in the `GeneratorConfigurationException` branch), and register `ReviewAvailability` from it with `services.AddSingleton(sp => new ReviewAvailability(sp.GetRequiredService<ReviewSetup>().IsConfigured))`. The setup stays lazy: nothing is read at start-up. `ReviewAvailability` holds no setting value.
- [x] T013 [US3] In `src/AgenticChangeOps.Api/Errors/ErrorMapper.cs` add `ForReviewing(ReviewResult)` and `ReviewNotConfigured()` exactly as research.md: not found when errors carry `ChangeRequestId`; `review-rejected` 502 with the rejection name as `category` and fixed message; `generation-failed` 502 with the category only and fixed message "The review could not be generated."; `conflict` 409 when every error field is in `Status`, `RequirementsDraft`, `DesignDraft`, `DesignDraftVersion`, `Proposal`, `ProposalVersion`; otherwise `Unexpected()`; `review-not-configured` 503 with fixed text. No existing member changes.
- [x] T014 [US1] Create `src/AgenticChangeOps.Api/Endpoints/ReviewEndpoints.cs` following `ProposalEndpoints.ProposeAsync`: `MapPost("/change-requests/{id}/reviews", ...)` named `RequestReview` with `Produces` for 201 (`Review`), 400, 404, 409, 500, 502, 503; checks id, then `IChangeRequestService.GetById`, then `ReviewAvailability.IsConfigured`, then `IReviewService.ReviewAsync(id, context.RequestAborted)`; success returns 201 with the review and no `Location` (use `Results.Json(result.Review, statusCode: StatusCodes.Status201Created)` or the project's equivalent that sets no header); failure logs one warning (rejection name, failure category or distinct field names only) and returns `ErrorMapper.ForReviewing`. Read no request body. Add `ReviewEndpoints.Map(app);` to `src/AgenticChangeOps.Api/Program.cs` after `ProposalEndpoints.Map(app);`.
- [x] T015 Run `dotnet build AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln --filter "FullyQualifiedName~Review"` until the new tests are green; then regenerate the golden `openapi.json` only by the project's reviewed update variable and check the diff is the `RequestReview` operation and the schemas it needs, nothing else.
- [x] T016 [P] Add a structure test in `tests/AgenticChangeOps.Api.Tests/ReviewEndpointStructureTests.cs` (or extend the Workflow structure suite additively): Workflow and Intake do not reference the Reviewer project or `ReviewEndpoints`, and `EndpointDataSource` has exactly one route containing `reviews`, a POST.

---

## Phase 4: Polish

- [x] T017 Run `dotnet test AgenticChangeOps.sln`: every project green with the T002 counts plus the new tests; record in `specs/019-reviewer-request-operation/verification.md` the totals, the list of existing-test edits (must equal `expected-test-edits.md`) and the golden diff summary. Tick the checklist in `specs/019-reviewer-request-operation/checklists/requirements.md` if still valid.
- [x] T018 [P] Update the README or API notes only if they list the operations (search for `proposals` in `*.md` outside `specs/`); add the one new operation with its responses.
- [x] T019 **[USER]**: add a memory note and report the live check as not run (needs real Azure OpenAI settings and a full chain). Commit on `feature/reviewer-request-operation`, push and open the PR only when the user asks.

---

## Dependencies and order

- T001 to T003 first, in order; T004 before T005 to T010; T005 to T010 are parallel; T011 gates T012 to T016.
- T012 and T013 are independent files and may run in parallel; T014 needs both; T015 needs T014; T016 may run beside T015.
- Story coverage: US1 T005, T014; US2 T006, T012; US3 T007, T008, T013; US4 T009; US5 T010, T015, T017.

## Implementation strategy

MVP is US1 plus the checks it needs (T012 to T014), but because the slice is one route, deliver all stories in one step: tests first, approval, then implementation, then the full suite.

## Phase 5: Convergence

- [x] T020 Add tests in `tests/AgenticChangeOps.Api.Tests/ReviewFailureEndpointTests.cs` that a storage failure while storing a review gives the existing 503 `storage-unavailable` shape (with a failure `category`) and that a lost version race gives the existing 409 `version-conflict` shape, both through `POST /change-requests/{id}/reviews`, with nothing stored and no model text, sample content, endpoint or deployment name in the response or logs; reuse the throwing-store pattern of `tests/AgenticChangeOps.Api.Tests/Storage/StorageErrorTests.cs` per spec Edge Cases and tasks T007 (partial)
- [x] T021 Add a test in `tests/AgenticChangeOps.Api.Tests/ReviewEndpointTests.cs` that with the sample folder missing the route still returns 201 with a stored review whose `systemCheck` status is `NotPerformed` and no edits, and that the route works with the sample tools switched off, proving the operation adds no rule of its own per spec Edge Cases and FR-002 (partial)
