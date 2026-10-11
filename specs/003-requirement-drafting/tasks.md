# Tasks: Requirement Drafting for Change Requests

**Input**: Design documents from `specs/003-requirement-drafting/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/ ✅

**Tests**: Included — Constitution Principle VI (Test-First) mandates tests written and
approved before implementation for all deterministic logic. Within each story, test tasks
come first, MUST be red (failing for the right reason), and end with a human-approval
CHECKPOINT before any implementation task starts. No test may call a live model, the network,
or read credentials; the generator is always `FakeDraftGenerator`.

**Organization**: Tasks are grouped by user story. This slice extends the existing
`AgenticChangeOps.Intake` and `AgenticChangeOps.Intake.Tests` projects; no new projects and no
new NuGet packages are added. A production model adapter is explicitly out of this slice
(plan.md, research.md decision 1).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks).
  Tasks that edit the same file are never marked `[P]`.
- **[Story]**: Which user story this task belongs to (US1–US6)

---

## Phase 1: Setup

**Purpose**: Confirm the slice 001/002 baseline is green before changing shared types.

- [X] T001 Run `dotnet test AgenticChangeOps.sln` and confirm all slice 001 and 002 tests pass; record the passing count as the regression baseline

**Checkpoint**: Baseline green.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared types, store change, and compile-only skeletons so story tests can be
written and fail for the right reason. MUST be complete before any user story work begins.

- [X] T002 [P] Extend AuditEventType enum in src/AgenticChangeOps.Intake/Models/Enums/AuditEventType.cs by appending exactly one value after `Deferred`: DraftGenerated (do not reorder existing values)
- [X] T003 [P] Create DraftStatus enum in src/AgenticChangeOps.Intake/Models/Enums/DraftStatus.cs with exactly one value: Draft
- [X] T004 [P] Extend ChangeRequestAuditEntry record in src/AgenticChangeOps.Intake/Models/ChangeRequestAuditEntry.cs by appending two trailing optional parameters with default null after `StatusAfter`: AgentName (string?), DraftVersion (int?); all existing parameters and every existing construction in ChangeRequestService and ApprovalService must remain unchanged and compile
- [X] T005 [P] Create DraftContent record in src/AgenticChangeOps.Intake/Models/DraftContent.cs with nullable members so an absent section is representable: Summary (string?), FunctionalRequirements (IReadOnlyList<string>?), AcceptanceCriteria (IReadOnlyList<string>?), Assumptions (IReadOnlyList<string>?), OpenQuestions (IReadOnlyList<string>?)
- [X] T006 [P] Create RequirementsDraft record in src/AgenticChangeOps.Intake/Models/RequirementsDraft.cs with members: DraftId (Guid), ChangeRequestId (Guid), Version (int — 1-based, per change request, no gaps, never reused), GeneratedAt (DateTime — UTC), Status (DraftStatus), AgentName (string — constant "RequirementAgent"), Summary (string), FunctionalRequirements (IReadOnlyList<string>), AcceptanceCriteria (IReadOnlyList<string>), Assumptions (IReadOnlyList<string>), OpenQuestions (IReadOnlyList<string>)
- [X] T007 [P] Create DraftingResult class in src/AgenticChangeOps.Intake/Models/DraftingResult.cs mirroring DecisionResult: bool IsSuccess; RequirementsDraft? Draft; IReadOnlyList<ValidationError> Errors; private constructor; static factories DraftingResult.Success(RequirementsDraft) (IsSuccess=true, Errors=empty) and DraftingResult.Failure(IReadOnlyList<ValidationError>) (IsSuccess=false, Draft=null)
- [X] T008 [P] Create generic QueryResult<T> class in src/AgenticChangeOps.Intake/Models/QueryResult.cs: bool IsSuccess; T? Value; IReadOnlyList<ValidationError> Errors; private constructor; static factories QueryResult<T>.Success(T? value) (IsSuccess=true, Errors=empty) and QueryResult<T>.Failure(IReadOnlyList<ValidationError>) (IsSuccess=false, Value=default)
- [X] T009 Add tests in tests/AgenticChangeOps.Intake.Tests/Unit/StoreTests.cs for the new draft storage methods (written before T010): `SaveDraft(draft, auditEntry)` makes the draft appear in `GetDrafts` and the entry appear in `GetAuditEntries`; `GetDrafts` returns drafts in ascending Version order and `Array.Empty` for an id with none; drafts of two change requests do not mix; `SaveDraft` with a Version already stored for that ChangeRequestId throws InvalidOperationException and stores neither draft nor entry; `SaveDraft` where draft.ChangeRequestId differs from auditEntry.ChangeRequestId throws ArgumentException and stores neither; tests fail until T010
- [X] T010 Add `void SaveDraft(RequirementsDraft draft, ChangeRequestAuditEntry auditEntry)` and `IReadOnlyList<RequirementsDraft> GetDrafts(Guid changeRequestId)` to src/AgenticChangeOps.Intake/Stores/IChangeRequestStore.cs and implement them in src/AgenticChangeOps.Intake/Stores/InMemoryChangeRequestStore.cs: perform all checks (matching ChangeRequestId, duplicate Version) before mutating so the call stores both items or neither; GetDrafts returns ascending Version; run T009 tests until green
- [X] T011 [P] Create IRequirementsDraftGenerator interface in src/AgenticChangeOps.Intake/Services/IRequirementsDraftGenerator.cs with `Task<DraftContent?> GenerateAsync(ChangeRequest changeRequest, CancellationToken cancellationToken)`
- [X] T012 [P] Create IRequirementsDraftingService interface in src/AgenticChangeOps.Intake/Services/IRequirementsDraftingService.cs with `Task<DraftingResult> DraftAsync(Guid changeRequestId, CancellationToken cancellationToken = default)`, `QueryResult<RequirementsDraft?> GetLatestDraft(Guid changeRequestId)`, and `QueryResult<IReadOnlyList<RequirementsDraft>> GetDraftHistory(Guid changeRequestId)`
- [X] T013 [P] Create DraftContentValidator skeleton in src/AgenticChangeOps.Intake/Services/DraftContentValidator.cs: public sealed class with `(DraftContent? Normalised, IReadOnlyList<ValidationError> Errors) Validate(DraftContent? content)` that throws NotImplementedException
- [X] T014 Create RequirementsDraftingService skeleton in src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs implementing IRequirementsDraftingService; constructor takes IChangeRequestStore, ISystemClock, IIdGenerator, IRequirementsDraftGenerator, DraftContentValidator; all three methods throw NotImplementedException
- [X] T015 [P] Create FakeDraftGenerator in tests/AgenticChangeOps.Intake.Tests/Fakes/FakeDraftGenerator.cs implementing IRequirementsDraftGenerator: exposes `int CallCount`, `ChangeRequest? LastRequest`, and a settable `Func<ChangeRequest, CancellationToken, Task<DraftContent?>> Behavior`; static helpers `Returns(DraftContent)`, `Throws(Exception)`, `ReturnsNull()`; default behavior returns `ValidContent()`; static `ValidContent()` returns synthetic content with a non-empty Summary, two functional requirements, two acceptance criteria, one assumption and one open question (all fictional, about a "Sample Billing Portal" schedule change)
- [X] T016 Create DraftingTestFixture in tests/AgenticChangeOps.Intake.Tests/Fakes/DraftingTestFixture.cs that wires one InMemoryChangeRequestStore, FakeSystemClock, CountingIdGenerator (from ApprovalTestFixture.cs), FakeDraftGenerator and DraftContentValidator into ChangeRequestService, ApprovalService and RequirementsDraftingService; expose Store, Clock, Generator, Intake, Approval, Drafting, `Guid SubmitSynthetic()` (valid synthetic request: requester "Avery Quill", system "Sample Billing Portal"), and `void SeedStatus(Guid id, ChangeRequestStatus status)` (as in ApprovalTestFixture)
- [X] T017 Verify `dotnet build AgenticChangeOps.sln` — 0 errors, 0 warnings, and `dotnet test` shows the T001 baseline still passing (drafting tests not yet written)

**Checkpoint**: Everything compiles; drafting methods throw NotImplementedException; slices 001/002 green.

---

## Phase 3: User Story 1 — Draft Requirements for a Change Request (Priority: P1) 🎯 MVP

**Goal**: A caller explicitly drafts a `Submitted` or `Deferred` request; a version 1 `Draft` with all five sections is stored and one audit entry is recorded; the request is unchanged.

**Independent Test**: Submit a synthetic request, call `DraftAsync` with a fake returning complete content; the result is a draft with Version 1, Status `Draft`, GeneratedAt equal to the clock, five sections, and one `DraftGenerated` audit entry; the request's Status is still `Submitted`.

### Tests for User Story 1 (write first — MUST fail)

- [X] T018 [US1] Create tests/AgenticChangeOps.Intake.Tests/Unit/DraftingTests.cs using DraftingTestFixture: (a) Submitted request + valid content → IsSuccess, Version 1, Status `Draft`, AgentName "RequirementAgent", GeneratedAt == clock.UtcNow, all five sections equal the fake's content, ChangeRequestId matches; (b) Deferred request (via SeedStatus) → same outcome; (c) request status and all other ChangeRequest fields identical before and after; (d) audit history ends with exactly one new entry: EventType DraftGenerated, DraftVersion 1, AgentName "RequirementAgent", OccurredAt == draft.GeneratedAt, StatusBefore == StatusAfter == Submitted, ApproverName/Reason null, ordered after the Submitted entry; (e) padded items are stored trimmed and blank items dropped (Clarification: "Drop blank items and store the draft if each required list still has at least one valid item"); (f) the generator receives the stored ChangeRequest (CallCount 1, LastRequest equal to FindById result)
- [X] T019 [US1] CHECKPOINT: run US1 tests, confirm each fails for the expected reason (NotImplementedException), and obtain human approval of the tests before starting T020 (Constitution VI)

### Implementation for User Story 1

- [X] T020 [US1] Implement normalisation in src/AgenticChangeOps.Intake/Services/DraftContentValidator.cs: trim Summary and every list item, drop blank or whitespace-only items, return the normalised DraftContent with an empty error list; null content returns (null, one error with FieldName "Generator"); section rules arrive in US5 (T041)
- [X] T021 [US1] Implement `DraftAsync` happy path in src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs: read the request from the store (until T030 adds eligibility, an unknown id returns a single "ChangeRequestId" error rather than throwing); call the generator; run the validator; then inside a private `lock`: compute `Version = store.GetDrafts(id).Count + 1`, build the RequirementsDraft (Status Draft, AgentName "RequirementAgent", GeneratedAt `_clock.UtcNow`, new id from IIdGenerator) and the audit entry (EventType DraftGenerated, DraftVersion, AgentName, StatusBefore = StatusAfter = request.Status, OccurredAt = draft.GeneratedAt, ApproverName/Reason/Details null), build both before touching the store, then one `store.SaveDraft(draft, entry)` call; return `DraftingResult.Success`. Never call `store.Update`. Run T018 tests until green

**Checkpoint**: US1 green — a caller can draft and the result is audited. MVP.

---

## Phase 4: User Story 2 — Redraft Creates a New Version (Priority: P2)

**Goal**: Repeated drafting creates versions 2, 3, … and preserves earlier versions; numbering is per change request.

**Independent Test**: Draft one request three times; versions 1, 2, 3 exist and versions 1 and 2 are unchanged; a second request starts at 1.

### Tests for User Story 2 (write first)

- [X] T022 [US2] Add tests to tests/AgenticChangeOps.Intake.Tests/Unit/DraftingTests.cs: (a) second call returns Version 2 and the stored version 1 is equal (record equality) to what was returned the first time; (b) after N calls N drafts exist with versions 1..N; (c) two different requests each start at Version 1; (d) a different generated content on the second call is stored as version 2 while version 1 keeps the first content; (e) two overlapping `DraftAsync` calls for the same request (generator behavior awaits a TaskCompletionSource so both are in flight) end with versions 1 and 2, no duplicates and no gaps; (f) each stored draft has its own audit entry: N drafts → N `DraftGenerated` entries with DraftVersion 1..N
- [X] T023 [US2] CHECKPOINT: run US2 tests, confirm expected failures or already-green status (US1 implementation may satisfy them), and obtain human approval of the tests before T024 (Constitution VI)

### Implementation for User Story 2

- [X] T024 [US2] Make T022 green in src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs: confirm that version computation and the `SaveDraft` call share one `lock` section with no `await` inside it, so overlapping calls cannot take the same version; fix only if a test fails

**Checkpoint**: US1 and US2 green.

---

## Phase 5: User Story 3 — Retrieve Latest Draft and Version History (Priority: P3)

**Goal**: Callers read back the latest draft and the ordered history; unknown ids return not-found; no drafts is a distinct, non-error outcome.

**Independent Test**: Draft twice; latest is version 2 and history is 1 then 2; an existing request with no drafts gives null latest and empty history; an unknown id gives an error.

### Tests for User Story 3 (write first)

- [X] T025 [P] [US3] Create tests/AgenticChangeOps.Intake.Tests/Unit/DraftRetrievalTests.cs: (a) three drafts → GetLatestDraft returns Success with Version 3; (b) GetDraftHistory returns all three in ascending Version order, each equal to what DraftAsync returned; (c) existing request with no drafts → GetLatestDraft IsSuccess with Value null, GetDraftHistory IsSuccess with empty list; (d) unknown id → both return Failure with one error whose FieldName is "ChangeRequestId" and whose message contains the id; (e) retrieval still works after the request becomes Approved (SeedStatus) and returns the earlier drafts; (f) retrieval never calls the generator (CallCount unchanged)
- [X] T026 [US3] CHECKPOINT: run US3 tests, confirm expected failures (NotImplementedException), and obtain human approval of the tests before T027 (Constitution VI)

### Implementation for User Story 3

- [X] T027 [US3] Implement `GetLatestDraft` and `GetDraftHistory` in src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs: look up the request; unknown id → `QueryResult.Failure` with `ValidationError("ChangeRequestId", ...)` naming the id; otherwise read `store.GetDrafts` and return the last item (or null) / the full ascending list; no status restriction; run T025 tests until green

**Checkpoint**: US1–US3 green.

---

## Phase 6: User Story 4 — Refuse Drafting for Ineligible Requests (Priority: P4)

**Goal**: Unknown and non-`Submitted`/`Deferred` requests are refused with specific errors before the generator is called; eligibility is re-checked after generation.

**Independent Test**: Attempt drafting for an unknown id and for `Approved`, `Rejected`, `UnderReview` requests; each returns a distinct specific error, nothing is stored, and the generator call count is 0.

### Tests for User Story 4 (write first)

- [X] T028 [P] [US4] Create tests/AgenticChangeOps.Intake.Tests/Unit/DraftEligibilityTests.cs: (a) unknown id → Failure, one error FieldName "ChangeRequestId" naming the id; (b) theory over Approved, Rejected, UnderReview → Failure, one error FieldName "Status" whose message contains the current status name; (c) for every refused case Generator.CallCount == 0, GetDrafts is empty, and the audit history is unchanged; (d) request that is Approved *during* generation (generator behavior calls `Store.Update(request with { Status = Approved })` then returns valid content) → Failure with FieldName "Status", nothing stored, no version consumed, and a later draft attempt after seeding the status back to Submitted gets Version 1
- [X] T029 [US4] CHECKPOINT: run US4 tests, confirm expected failures, and obtain human approval of the tests before T030 (Constitution VI)

### Implementation for User Story 4

- [X] T030 [US4] Add eligibility to `DraftAsync` in src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs via one private predicate that is true only for `Submitted` and `Deferred`: check before calling the generator (unknown id → "ChangeRequestId" error naming the id; other status → "Status" error naming the current status) and re-read and re-check inside the lock section before `SaveDraft`; run T028 tests until green

**Checkpoint**: US1–US4 green.

---

## Phase 7: User Story 5 — Fail Safely on Bad Model Output (Priority: P5)

**Goal**: Generator failure or incomplete/malformed content stores nothing, consumes no version, and returns a specific error naming every failing section.

**Independent Test**: Use fakes that throw, return null, omit a section, return empty requirements, and return oversized items; each yields a specific error and leaves request, audit history, and drafts unchanged; a following valid draft gets the next version with no gap.

### Tests for User Story 5 (write first)

- [X] T031 [P] [US5] Create tests/AgenticChangeOps.Intake.Tests/Unit/DraftContentValidationTests.cs against DraftContentValidator directly and through DraftAsync: (a) null Summary / whitespace Summary → error "Summary"; (b) null FunctionalRequirements, empty list, or list of only blank items → error "FunctionalRequirements"; same three cases for "AcceptanceCriteria"; (c) null Assumptions or null OpenQuestions → error naming the section; empty Assumptions/OpenQuestions lists are valid and stored; (d) several failing sections → one error per section, all reported; (e) Summary of 2,001 characters, a list item of 2,001 characters, and a list of 51 items (after dropping blanks) → error naming the section; exactly 2,000 characters and exactly 50 items are accepted and stored untruncated; (f) a list with one valid item and blank items is accepted with only the valid item stored
- [X] T032 [P] [US5] Create tests/AgenticChangeOps.Intake.Tests/Unit/DraftGenerationFailureTests.cs using DraftingTestFixture: (a) generator throws `InvalidOperationException("secret-endpoint-value")` → Failure with one error FieldName "Generator", message contains the exception type name and does NOT contain "secret-endpoint-value"; (b) generator returns null → Failure FieldName "Generator"; (c) generator throws OperationCanceledException (cancelled token) → Failure FieldName "Generator"; (d) after every failing case the request is unchanged, `GetDrafts` is empty and the audit history has no new entry; (e) failure after an earlier success leaves version 1 unchanged and the next success is version 2 (no gap); (f) a failure is not retried (CallCount 1)
- [X] T033 [US5] CHECKPOINT: run US5 tests, confirm expected failures, and obtain human approval of the tests before T034 (Constitution VI)

### Implementation for User Story 5

- [X] T034 [US5] Complete src/AgenticChangeOps.Intake/Services/DraftContentValidator.cs: after normalisation apply the rules exactly as in data-model.md — Summary "Trimmed, non-empty, ≤ 2,000 chars"; FunctionalRequirements and AcceptanceCriteria "≥ 1 item, ≤ 50, each ≤ 2,000 chars"; Assumptions and OpenQuestions "0–50 items" (null is an error, empty is valid); never truncate; collect one `ValidationError(<SectionName>, ...)` per failing section; run T031 tests until green
- [X] T035 [US5] Add generator failure handling in src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs: wrap the `GenerateAsync` call in try/catch for `Exception` (including OperationCanceledException) and return `DraftingResult.Failure` with one `ValidationError("Generator", ...)` whose message contains `ex.GetType().Name` and never `ex.Message`; return the validator's errors when non-empty; neither path stores anything or computes a version; run T032 tests until green

**Checkpoint**: US1–US5 green; failed attempts leave no trace.

---

## Phase 8: User Story 6 — Drafting Never Decides or Triggers Itself (Priority: P6)

**Goal**: Drafting happens only on explicit request and has no effect on decisions, status, or clearance.

**Independent Test**: Submit and decide requests without ever asking for a draft and confirm zero generator calls; draft with decision-like wording and confirm status, clearance and decision rules are unaffected.

### Tests for User Story 6 (write first)

- [X] T036 [US6] Create tests/AgenticChangeOps.Intake.Tests/Unit/DraftIsolationTests.cs using DraftingTestFixture: (a) after `Intake.Submit` alone, `GetDrafts` is empty and Generator.CallCount == 0; (b) after `Approval.RecordDecision` (Defer with a reason) alone, Generator.CallCount == 0 and no drafts exist; (c) generated Summary and items containing "approved", "reject" and "defer" → stored draft, request Status unchanged, `Approval.IsClearedToProceed` false; (d) after a draft exists, `Approval.RecordDecision(Approve)` behaves as in slice 002 (status Approved, one Approved entry) and the draft history is unchanged; (e) after the request is Approved, `DraftAsync` is refused (status error) but earlier drafts remain retrievable
- [X] T037 [US6] CHECKPOINT: run US6 tests, confirm expected failures or already-green status (the isolation holds by construction), and obtain human approval of the tests before T038 (Constitution VI)

### Implementation for User Story 6

- [X] T038 [US6] Make T036 green: if any test fails, correct src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs so it never calls `IChangeRequestStore.Update`, `IApprovalService`, or `IChangeRequestService`; confirm by search that no member of those types is referenced from RequirementsDraftingService.cs and DraftContentValidator.cs

**Checkpoint**: US1–US6 green.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T039 Run `dotnet build AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln`: 0 errors, 0 warnings, all slice 001, 002 and 003 tests pass; record the final passing count against the T001 baseline
- [X] T040 [P] Secret and endpoint scan: run `git grep -inE "api[_-]?key|openai\.azure|endpoint=|bearer " -- src tests` and confirm no real credential, key, or endpoint appears (SC-008, FR-021); the scan covers code and tests only, because the spec documents quote the pattern itself and would match; the intentional placeholder "secret-endpoint-value" in DraftGenerationFailureTests.cs is a fake string and is allowed
- [X] T041 [P] Confirm that no production file under src/ references a model SDK or HTTP client and that src/AgenticChangeOps.Intake/AgenticChangeOps.Intake.csproj still lists no PackageReference (SC-007)
- [X] T042 Constitution compliance review of spec, plan, tasks and the diff against constitution v1.0.1, covering especially Principle III (drafting classified non-consequential) and Principle IV (additive audit-record change recorded in plan.md Complexity Tracking); record the result in plan.md under the Constitution Check heading
- [X] T043 Walk through specs/003-requirement-drafting/quickstart.md scenarios 1–7 and confirm each maps to a passing test

---

## Dependencies & Execution Order

- **Phase 1** → **Phase 2** (blocks everything) → **Phases 3–8** → **Phase 9**.
- Within Phase 2: T002–T008, T011–T013 and T015 are parallel (different files); T009 before T010; T014 after T011–T013; T016 after T014 and T015; T017 last.
- **US1 (P1)** has no story dependencies — MVP.
- **US2** reuses `DraftAsync` from US1 (T021); start after US1.
- **US3** needs drafts to exist, which comes from US1; its tests (DraftRetrievalTests.cs) can be written in parallel with US2 tests.
- **US4** extends `DraftAsync` from US1 and is independent of US2/US3; its tests (DraftEligibilityTests.cs) can be written in parallel with US2/US3 tests by a second contributor.
- **US5** completes the validator started in T020 and extends `DraftAsync`; start after US1. Its two test files (T031, T032) are parallel.
- **US6** needs US1 and US4 behaviour; it makes no production change unless a test fails.
- Tasks T021, T024, T030 and T035 all edit RequirementsDraftingService.cs and are therefore sequential.
- Every story's CHECKPOINT task blocks that story's implementation tasks.

## Parallel Example

After Phase 2, one contributor drives US1 → US2, while another writes the red tests for US3 (T025), US4 (T028) and US5 (T031, T032) in separate files, since each is a new file against the shared fixture.

## Implementation Strategy

1. **MVP**: Phases 1–3 (US1) — a caller can draft and the result is stored and audited.
2. **Increment 2**: US2 + US3 — versioning and read-back.
3. **Increment 3**: US4 + US5 — refusal and fail-safe behaviour, which protect state.
4. **Increment 4**: US6 + Polish — isolation guarantees and compliance sign-off.
5. Per Principles V and VI, stop at each CHECKPOINT for human approval of the failing tests.

## Phase 10: Convergence

- [X] T044 Guard draft reads against concurrent writes: in src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs take the same `_gate` lock in GetLatestDraft and GetDraftHistory around the `_store.GetDrafts` call (and add a test in tests/AgenticChangeOps.Intake.Tests/Unit/DraftRetrievalTests.cs that runs retrievals while overlapping DraftAsync calls complete and asserts no exception and that each returned history is a gap-free prefix 1..k) so a reader never observes a partially stored draft, per spec Assumptions "partial results are never visible to other callers" (partial)
