# Tasks: HTTP Operations To Retrieve The Latest Review And The Review History

**Input**: Design documents from `specs/020-reviewer-retrieval-operations/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/http-api.md, quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). Tests are written with compiling stubs, shown red once, and implementation starts only after the user says "approved".

**Constitution**: [x] Reviewed against constitution v1.0.6 (plan.md Constitution Check and the table at the end of this file). Tick at T001.

**Organization**: Grouped by the user stories in spec.md: US1 latest review (P1), US2 review history (P1), US3 refusals follow the conventions (P1), US4 the review request links to the latest review (P2), US5 description lists both and nothing else changes (P2). One small slice, so a single delivery step.

**Rules that hold for every task**:
- Every existing response, status code, body, header and error code is identical, except the `Location` header added to the successful review request. Every existing test passes with its assertions unchanged, except those listed in `specs/020-reviewer-retrieval-operations/baseline/expected-test-edits.md`, approved at T003. If any other test fails, fix the new code, never the assertion.
- Nothing in any response, log or error carries model text, finding text, a reference, file content, a path, an endpoint, a deployment name or a credential, nor a stack trace.
- The operations add no rule and are read-only: no review, audit entry, status or workflow state is written. They never consult `ReviewAvailability` and never call the model.
- No change to `AgenticChangeOps.Agents.Reviewer`, Workflow, Intake, stores or any other agent. Synthetic data only. No live model call in the suite.
- After every code task `dotnet build AgenticChangeOps.sln` succeeds; at the end `dotnet test AgenticChangeOps.sln` is green.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel (different files, no dependency on an incomplete task).
- **[USER]**: outward-facing step (push, PR) or human gate. Claude drafts, the user confirms.

---

## Phase 1: Setup

- [x] T001 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md. Tick the **Constitution** line above. No test or source file is written before this (Principle V). Plan approval was given; this confirms tasks.
- [x] T002 Confirm branch `feature/reviewer-retrieval-operations`. Run `dotnet test AgenticChangeOps.sln` and save per-project totals (and skipped live checks) to `specs/020-reviewer-retrieval-operations/baseline-tests.txt`, same layout as `specs/019-reviewer-request-operation/baseline-tests.txt`.
- [x] T003 **GATE ([USER])**: show `specs/020-reviewer-retrieval-operations/baseline/expected-test-edits.md` (already drafted: every existing test that enumerates operations or the description, asserts the absence of a review retrieval route, or asserts no `Location` on the review response, with the exact edit). Re-search `tests/` for any other such test before approval. Wait for the word "approved"; no other existing test is edited.

---

## Phase 2: Stubs and tests first (US1 to US5)

Compiling stubs: `ErrorMapper.NoReview()` (throwing `NotImplementedException`), and `GetLatest` / `GetHistory` handlers in `src/AgenticChangeOps.Api/Endpoints/ReviewEndpoints.cs` that are not yet mapped.

- [x] T004 Add the stubs above so the solution builds, and extend `tests/AgenticChangeOps.Api.Tests/Fakes/ReviewHttp.cs` / `ReviewHost.cs` additively with helpers `GetLatestReviewAsync(id)` and `GetReviewHistoryAsync(id)` (plain GETs) and one that stores N reviews through the real route using the scripted client. No existing member changes.
- [x] T005 [P] [US1] Add `tests/AgenticChangeOps.Api.Tests/ReviewLatestEndpointTests.cs`: with reviews at versions 1 and 2, `GET /change-requests/{id}/reviews/latest` gives 200 `application/json` whose body equals the stored version 2 review member for member (`reviewId`, `version`, `requirementsVersion`, `designVersion`, `proposalVersion`, `generatedAt`, `status`, `agentName`, `systemCheck`, `findings`, `openQuestions`, `advisoryNote`); with one review it returns that review; a request body is ignored; the body uses camel-case members and string enums.
- [x] T006 [P] [US2] Add `ReviewHistoryEndpointTests.cs`: with three reviews, `GET /change-requests/{id}/reviews` gives 200 and exactly three reviews with versions 1, 2, 3 in that order, each equal to its stored record; version 1 captured right after the first request is identical in the later history; a change request with no review gives 200 and `[]`; reviews of another change request never appear.
- [x] T007 [P] [US3] Add `ReviewRetrievalRefusalTests.cs`: for both routes a malformed id gives 400 `invalid-identifier` and an unknown well-formed id gives 404 `not-found`; for the latest route an existing change request with no review gives 404 `no-review`, a different `code` from `not-found`; a storage failure while reading gives the existing 503 `storage-unavailable` shape (reuse the throwing-store pattern of `Storage/StorageErrorTests.cs`); each error body has only `code`, `message` and optionally `errors` or `category`, with no stack trace or internal detail.
- [x] T008 [P] [US1] [US2] Add `ReviewRetrievalUnconfiguredTests.cs`: on a host whose review/model settings are invalid (the same way `ReviewCheckOrderTests` produces "not configured"), reviews stored directly in the store are still returned by both routes with 200, `no-review` / `[]` / `not-found` still apply, and the scripted model client records zero calls.
- [x] T009 [P] [US3] Add `ReviewRetrievalReadOnlyTests.cs`: before and after any number of calls to both routes, the stored reviews, the audit event list, the change request status and the workflow status body are byte-identical; no log line at warning level or above is written by a successful or refused retrieval; with sentinel endpoint, deployment name, credential, model text, finding text and file content planted, none appears in any retrieval response, header or captured log line.
- [x] T010 [P] [US4] Add the new link test to `ReviewEndpointTests.cs`: a successful `POST /change-requests/{id}/reviews` gives 201 with `Location` equal to `/change-requests/{id}/reviews/latest`, and following it with GET returns the review just created; the body, status code and every other header are as before. Add `ReviewRetrievalDescriptionTests.cs` (US5): the published description lists `GET /change-requests/{id}/reviews/latest` (operation id `GetLatestReview`, responses 200, 400, 404) and `GET /change-requests/{id}/reviews` (operation id `GetReviewHistory`, responses 200, 400, 404), `RequestReview` is unchanged, and no other path, tag or schema beyond what `Review` needs is added.
- [x] T011 **GATE ([USER])**: run the new tests, save the red output to `specs/020-reviewer-retrieval-operations/red-run.txt`, show it, apply the approved edits from T003 to the existing tests, and wait for the word "approved". Existing tests other than the listed ones must still pass. The replaced route/description tests are expected red until T014 and green again at T015.

---

## Phase 3: Implementation (US1 to US5)

- [x] T012 [US3] In `src/AgenticChangeOps.Api/Errors/ErrorMapper.cs` add `NoReview()` exactly as `NoProposal()`: `(404, new ErrorResponse("no-review", "No review exists yet for this change request."))`. No existing member changes.
- [x] T013 [US1] [US2] In `src/AgenticChangeOps.Api/Endpoints/ReviewEndpoints.cs` add `GetLatest` and `GetHistory` exactly as `ProposalEndpoints.GetLatest` / `GetHistory`: parse the id (`ErrorMapper.InvalidIdentifier`), call `IReviewService.GetLatestReview` / `GetReviewHistory`, map failures through `ErrorMapper.ForQuery(result.Errors)`, return `NoReview()` when the latest value is null, otherwise `Results.Ok(value)`. Take only `IReviewService`: no `ReviewAvailability`, no logger.
- [x] T014 [US4] [US5] In the same file map `MapGet("/change-requests/{id}/reviews/latest", GetLatest)` named `GetLatestReview` (summary "Retrieve the latest review"; `Produces<Review>()`, 400, 404) and `MapGet("/change-requests/{id}/reviews", GetHistory)` named `GetReviewHistory` (summary "List every review version in ascending order"; `Produces<IReadOnlyList<Review>>()`, 400, 404); change the success of `ReviewAsync` to `Results.Created($"/change-requests/{changeRequestId}/reviews/latest", result.Review)`. Nothing in `Program.cs` changes (the class is already mapped).
- [x] T015 Run `dotnet build AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln --filter "FullyQualifiedName~Review"` until the new and edited tests are green; then regenerate the golden `openapi.json` only through the project's reviewed update variable (`ApiBaselineTests.UpdateVariable` = 1) and check the diff is the two new operations and their schemas, plus nothing else.
- [x] T016 [P] Confirm by test (extend `ReviewEndpointStructureTests.cs`) that Workflow and Intake still do not reference the Reviewer project or `ReviewEndpoints`, and that `EndpointDataSource` has exactly three routes containing `reviews`: one POST and two GET.

---

## Phase 4: Polish

- [x] T017 Run `dotnet test AgenticChangeOps.sln`: every project green with the T002 counts plus the new tests; record in `specs/020-reviewer-retrieval-operations/verification.md` the totals, the list of existing-test edits (must equal `expected-test-edits.md`) and the golden diff summary. Tick `checklists/requirements.md` if still valid.
- [x] T018 [P] Update `README.md` where it says reading reviews back is internal only (lines about "Reviews can be requested over HTTP; reading them back is internal only for now" and the "one HTTP operation to request" heading/section): list the two retrieval routes, the `no-review` code and the `Location` header on the request response. Search `*.md` outside `specs/` for other mentions.
- [x] T019 **[USER]**: add a memory note and report the live check as not run (needs real Azure OpenAI settings and a full chain). Commit on `feature/reviewer-retrieval-operations`, push and open the PR only when the user asks.

---

## Dependencies and order

- T001 to T003 first, in order; T004 before T005 to T010; T005 to T010 are parallel; T011 gates T012 to T016.
- T012 first (small); T013 needs T012; T014 needs T013; T015 needs T014; T016 may run beside T015.
- Story coverage: US1 T005, T008, T013; US2 T006, T008, T013; US3 T007, T009, T012; US4 T010, T014; US5 T010, T014, T015, T017.

## Implementation strategy

MVP is US1 (T012, T013 and the latest route in T014), but because the slice is two read routes and one header, deliver all stories in one step: tests first, approval, then implementation, then the full suite.

## Constitution compliance check (pre-code)

Checked against constitution v1.0.6.

| Principle | spec.md | plan.md | tasks.md |
|---|---|---|---|
| I. Synthetic data | Pass | Pass | Pass (rules: synthetic only, no credentials; sentinel values in T009 are fictional) |
| II. Deterministic routing | Pass | Pass | Pass (T013 plain lookups; T008 proves the model is never called) |
| III. Human gates | Pass (read-only) | Pass | Pass (T009 proves nothing changes; T016 keeps Workflow clear of review endpoints) |
| IV. Typed auditable records | Pass | Pass | Pass (stored `Review` returned unchanged, T005/T006) |
| V. Spec-driven | Pass | Pass | Pass (T001 gate before any code) |
| VI. Test-first | Pass | Pass | Pass (T011 gate, red run saved) |
| VII. Simplicity | Pass | Pass | Pass (no new class; one error-mapper method) |

Technology Stack: no new technology, no amendment. Commit messages reference the slice name
"020 reviewer-retrieval-operations".

## Phase 5: Convergence

- [x] T020 Add a test in `tests/AgenticChangeOps.Api.Tests/ReviewHistoryEndpointTests.cs` that a malformed request body sent to `GET /change-requests/{id}/reviews` is ignored and still gives 200 with the stored reviews, as `ARequestBody_IsIgnored` does for the latest route per spec Edge Cases (partial)
- [x] T021 Add a test in `tests/AgenticChangeOps.Api.Tests/ReviewHistoryEndpointTests.cs` that a change request holding a requirements draft, design and proposal as well as reviews returns only reviews from both retrieval routes, per spec Edge Cases "only reviews are returned" (partial)
- [x] T022 Add a test in `tests/AgenticChangeOps.Api.Tests/ReviewRetrievalReadOnlyTests.cs` that retrievals made while review requests are in flight each return only whole stored reviews with consecutive versions starting at 1, never a partial one, per spec Edge Cases and FR-008 (partial)
