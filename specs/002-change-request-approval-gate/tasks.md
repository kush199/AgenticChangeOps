# Tasks: Change Request Approval Gate

**Input**: Design documents from `specs/002-change-request-approval-gate/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/ ✅

**Tests**: Included — Constitution Principle VI (Test-First) mandates tests written and
approved before implementation for all deterministic logic. Within each story, test tasks
come first, MUST be red (failing for the right reason), and end with a human-approval
CHECKPOINT before any implementation task starts.

**Organization**: Tasks are grouped by user story. This slice extends the existing
`AgenticChangeOps.Intake` project and `AgenticChangeOps.Intake.Tests` project; no new
projects are created.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks).
  Tasks that edit the same file are never marked `[P]`.
- **[Story]**: Which user story this task belongs to (US1–US5)

---

## Phase 1: Setup

**Purpose**: Confirm the slice 001 baseline is green before changing shared types.

- [X] T001 Run `dotnet test AgenticChangeOps.sln` and confirm all slice 001 tests pass; record the passing count as the regression baseline

**Checkpoint**: Baseline green.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared types, store change, and compile-only skeletons so story tests can be
written and fail for the right reason. MUST be complete before any user story work begins.

- [X] T002 [P] Extend AuditEventType enum in src/AgenticChangeOps.Intake/Models/Enums/AuditEventType.cs by appending exactly three values after `Submitted`: Approved, Rejected, Deferred (do not reorder existing values)
- [X] T003 [P] Create ApprovalDecision enum in src/AgenticChangeOps.Intake/Models/Enums/ApprovalDecision.cs with exactly three values: Approve, Reject, Defer
- [X] T004 [P] Extend ChangeRequestAuditEntry record in src/AgenticChangeOps.Intake/Models/ChangeRequestAuditEntry.cs by appending four trailing optional parameters with default null: ApproverName (string?), Reason (string?), StatusBefore (ChangeRequestStatus?), StatusAfter (ChangeRequestStatus?); the existing five parameters and the existing call in ChangeRequestService.Submit must remain unchanged and compile
- [X] T005 [P] Create ApprovalDecisionRequest record in src/AgenticChangeOps.Intake/Models/ApprovalDecisionRequest.cs with properties: ChangeRequestId (Guid), ApproverName (string), Decision (ApprovalDecision), Reason (string? — nullable; required only for Reject and Defer)
- [X] T006 [P] Create DecisionResult class in src/AgenticChangeOps.Intake/Models/DecisionResult.cs mirroring SubmissionResult: bool IsSuccess; ChangeRequest? ChangeRequest; IReadOnlyList<ValidationError> Errors; private constructor; static factories DecisionResult.Success(ChangeRequest) (IsSuccess=true, Errors=empty) and DecisionResult.Failure(IReadOnlyList<ValidationError>) (IsSuccess=false, ChangeRequest=null)
- [X] T007 Add tests in tests/AgenticChangeOps.Intake.Tests/Unit/StoreTests.cs for the new `Update` store method (written before T008): replaces the stored record with the same ChangeRequestId and FindById returns the new value; unknown ID throws InvalidOperationException; tests fail until T008
- [X] T008 Add `void Update(ChangeRequest changeRequest)` to src/AgenticChangeOps.Intake/Stores/IChangeRequestStore.cs and implement it in src/AgenticChangeOps.Intake/Stores/InMemoryChangeRequestStore.cs: replace the stored record with the same ChangeRequestId; throw InvalidOperationException if no record with that ID exists; run T007 tests until green
- [X] T009 [P] Create IApprovalService interface in src/AgenticChangeOps.Intake/Services/IApprovalService.cs with `DecisionResult RecordDecision(ApprovalDecisionRequest request)` and `bool IsClearedToProceed(Guid changeRequestId)`
- [X] T010 [P] Create ApprovalDecisionValidator skeleton in src/AgenticChangeOps.Intake/Services/ApprovalDecisionValidator.cs: public sealed class with `IReadOnlyList<ValidationError> Validate(ApprovalDecisionRequest request, ChangeRequest? existing)` that throws NotImplementedException
- [X] T011 Create ApprovalService skeleton in src/AgenticChangeOps.Intake/Services/ApprovalService.cs implementing IApprovalService; constructor takes IChangeRequestStore, ISystemClock, IIdGenerator, ApprovalDecisionValidator; both methods throw NotImplementedException
- [X] T012 Add test helper in tests/AgenticChangeOps.Intake.Tests/Fakes/ApprovalTestFixture.cs that wires one InMemoryChangeRequestStore, FakeSystemClock and FakeIdGenerator into both ChangeRequestService and ApprovalService, and exposes `Guid SubmitSynthetic()` (submits a valid synthetic request: requester "Avery Quill", system "Sample Billing Portal"), `ApprovalDecisionRequest Decide(Guid id, ApprovalDecision d, string? approver = "Jordan Reed", string? reason = null)`, and a way to read the next unconsumed generator value; supply enough pre-queued GUIDs for submissions plus audit entries
- [X] T013 Verify `dotnet build AgenticChangeOps.sln` — 0 errors, 0 warnings, and `dotnet test` shows the T001 baseline still passing (approval tests not yet written)

**Checkpoint**: Everything compiles; approval methods throw NotImplementedException; slice 001 green.

---

## Phase 3: User Story 1 — Approve a Submitted Change Request (Priority: P1) 🎯 MVP

**Goal**: An approver approves a Submitted (or Deferred) request; status becomes Approved and one audit entry is recorded.

**Independent Test**: Submit, approve with approver name and no reason; status is `Approved` and one `Approved` audit entry holds approver, empty reason, UTC timestamp, StatusBefore=Submitted, StatusAfter=Approved.

### Tests for User Story 1 (write first; must fail)

- [X] T014 [US1] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalDecisionTests.cs add test: Approve on Submitted request with no reason → IsSuccess true, returned ChangeRequest.Status == Approved, GetById returns Approved
- [X] T015 [US1] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalDecisionTests.cs add test: Approve with reason "Low risk, synthetic scenario" → audit entry Reason equals the trimmed reason; Approve without a reason → Reason is an empty string
- [X] T016 [US1] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalDecisionTests.cs add test: after Approve, audit history has exactly 2 entries; second has EventType Approved, ApproverName, OccurredAt == FakeSystemClock.Now, StatusBefore Submitted, StatusAfter Approved, ChangeRequestId matching
- [X] T017 [US1] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalDecisionTests.cs add test: Defer then Approve on same request succeeds and status is Approved (Deferred is decidable)
- [X] T018 [US1] CHECKPOINT: run US1 tests, confirm each fails for the expected reason (NotImplementedException / unmet assertion), and obtain human approval of the tests before starting T019 (Constitution VI)

### Implementation for User Story 1

- [X] T019 [US1] Implement ApprovalDecisionValidator.Validate in src/AgenticChangeOps.Intake/Services/ApprovalDecisionValidator.cs for the success-path rules only: existing != null and existing.Status is Submitted or Deferred (full error rules are completed in US3)
- [X] T020 [US1] Implement ApprovalService.RecordDecision in src/AgenticChangeOps.Intake/Services/ApprovalService.cs: look up request via store.FindById; call validator; on errors return DecisionResult.Failure and touch nothing; otherwise map Approve→Approved, Reject→Rejected, Defer→Deferred; FIRST build the updated record (`existing with { Status = newStatus }`) and the ChangeRequestAuditEntry (AuditEntryId from IIdGenerator, EventType from outcome, OccurredAt = clock.UtcNow, trimmed ApproverName, trimmed Reason (empty string if none), StatusBefore, StatusAfter, Details null); THEN `store.Update(updated)`, THEN `store.SaveAuditEntry(entry)`; return DecisionResult.Success(updated). Run US1 tests until green

**Checkpoint**: US1 independently functional — MVP (approve works end to end).

---

## Phase 4: User Story 2 — Reject or Defer with a Reason (Priority: P2)

**Goal**: Reject (final) and Defer (parks) decisions require a reason and record audit entries.

**Independent Test**: Reject one submitted request and Defer another; statuses are `Rejected`/`Deferred` and audit entries carry the reasons.

### Tests for User Story 2 (write first; must fail)

- [X] T021 [US2] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalDecisionTests.cs add test: Reject with reason on Submitted → status Rejected; audit entry EventType Rejected with approver, reason, StatusBefore Submitted, StatusAfter Rejected
- [X] T022 [US2] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalDecisionTests.cs add test: Defer with reason on Submitted → status Deferred; audit entry EventType Deferred
- [X] T023 [US2] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalDecisionTests.cs add test: Defer on a Deferred request → status stays Deferred and a second Deferred audit entry is appended (history: Submitted, Deferred, Deferred)
- [X] T024 [US2] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalDecisionTests.cs add test: Reject with reason on a Deferred request → status Rejected
- [X] T025 [US2] CHECKPOINT: run US2 tests, confirm expected failures or already-green status (US1 implementation may satisfy them), and obtain human approval of the tests before T026 (Constitution VI)

### Implementation for User Story 2

- [X] T026 [US2] Verify RecordDecision from T020 already satisfies T021–T024 (the mapping is shared); adjust src/AgenticChangeOps.Intake/Services/ApprovalService.cs only if a test fails, and run US2 tests until green

**Checkpoint**: US1 and US2 pass; all three decisions work on valid input.

---

## Phase 5: User Story 3 — Reject Invalid Decisions Without Side Effects (Priority: P3)

**Goal**: Every invalid decision returns specific, collect-all errors and changes nothing.

**Independent Test**: Run each invalid case; assert the error FieldName and that status and audit history are unchanged.

### Tests for User Story 3 (write first; must fail)

Every failure test below also asserts that the next FakeIdGenerator value is unconsumed and
that `GetById` and `GetAuditHistory` are unchanged.

- [X] T027 [US3] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalValidationTests.cs add test: unknown ChangeRequestId → one error with FieldName "ChangeRequestId"; nothing stored (GetAuditHistory(unknown) is null)
- [X] T028 [US3] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalValidationTests.cs add theory: ApproverName null, "", "   " → error FieldName "ApproverName"; status stays Submitted; history length 1
- [X] T029 [US3] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalValidationTests.cs add theory over Reject/Defer × Reason null/""/"   " → error FieldName "Reason" whose message names the decision; status and history unchanged
- [X] T030 [US3] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalValidationTests.cs add test: Approve with whitespace-only reason succeeds and stores an empty reason
- [X] T031 [US3] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalValidationTests.cs add theory: request already Approved, and already Rejected, with each of the three decisions → error FieldName "Status" whose message contains "final" and the current status name; status and history unchanged
- [X] T032 [US3] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalValidationTests.cs add test: request in UnderReview (seeded via store.Update) → error FieldName "Status" containing "not eligible" and "UnderReview"
- [X] T033 [US3] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalValidationTests.cs add test: Decision value `(ApprovalDecision)99` → error FieldName "Decision" listing "Approve, Reject, Defer"
- [X] T034 [US3] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalValidationTests.cs add test: blank approver AND Defer without reason → exactly two errors ("ApproverName", "Reason") (collect-all)
- [X] T035 [US3] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalValidationTests.cs add boundary tests: ApproverName of 100 chars accepted, 101 rejected "must not exceed 100 characters"; Reason of 1,000 chars accepted, 1,001 rejected "must not exceed 1000 characters"
- [X] T036 [US3] CHECKPOINT: run US3 tests, confirm expected failures, and obtain human approval of the tests before T037 (Constitution VI)

### Implementation for User Story 3

- [X] T037 [US3] Complete ApprovalDecisionValidator.Validate in src/AgenticChangeOps.Intake/Services/ApprovalDecisionValidator.cs with collect-all semantics: ChangeRequestId not found ("Change request '<id>' was not found."); ApproverName not null/blank, length ≤ 100 after trim; Decision must be a defined ApprovalDecision (message lists "Approve, Reject, Defer"); Reason required (not null/blank) when Decision is Reject or Defer, length ≤ 1,000 when provided; Status check only when request exists: Approved/Rejected → final-state message naming status, any other status than Submitted/Deferred → "not eligible" message naming status
- [X] T038 [US3] Confirm RecordDecision in src/AgenticChangeOps.Intake/Services/ApprovalService.cs performs no store write (no Update, no SaveAuditEntry) and consumes no IIdGenerator value on failure; run US3 tests until green

**Checkpoint**: Invalid input can never change state.

---

## Phase 6: User Story 4 — Check Whether a Request Is Cleared to Proceed (Priority: P4)

**Goal**: A boolean check true only for Approved.

**Independent Test**: Check requests in every status and an unknown ID; only Approved is true.

### Tests for User Story 4 (write first; must fail)

- [X] T039 [US4] In tests/AgenticChangeOps.Intake.Tests/Unit/ClearanceTests.cs add theory over all five ChangeRequestStatus values (seed non-reachable statuses with store.Update): IsClearedToProceed is true only for Approved
- [X] T040 [US4] In tests/AgenticChangeOps.Intake.Tests/Unit/ClearanceTests.cs add test: unknown ChangeRequestId and Guid.Empty return false without throwing
- [X] T041 [US4] In tests/AgenticChangeOps.Intake.Tests/Unit/ClearanceTests.cs add test: end-to-end — false after submit, false after Defer, true after Approve; and a Rejected request is false
- [X] T042 [US4] CHECKPOINT: run US4 tests, confirm expected failures, and obtain human approval of the tests before T043 (Constitution VI)

### Implementation for User Story 4

- [X] T043 [US4] Implement ApprovalService.IsClearedToProceed in src/AgenticChangeOps.Intake/Services/ApprovalService.cs: `store.FindById(id)?.Status == ChangeRequestStatus.Approved`; run US4 tests until green

**Checkpoint**: Gate check enforced.

---

## Phase 7: User Story 5 — Review Decision History in Order (Priority: P5)

**Goal**: Audit history lists the submission and every decision in chronological order, with same-timestamp entries keeping processing order (slice 001 ordering reused unchanged).

**Independent Test**: Defer, Defer, Approve; list history; get Submitted, Deferred, Deferred, Approved.

### Tests for User Story 5 (write first; must fail)

- [X] T044 [US5] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalAuditTests.cs add test: Defer, Defer, Approve with FakeSystemClock advanced between calls → GetAuditHistory event types are Submitted, Deferred, Deferred, Approved in order with distinct AuditEntryIds
- [X] T045 [US5] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalAuditTests.cs add test: two decisions recorded with the same clock value keep processing (insertion) order
- [X] T046 [US5] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalAuditTests.cs add test: a failed decision between two successful ones adds no entry; history count equals 1 + successful decisions
- [X] T047 [US5] In tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalAuditTests.cs add test: decisions on request A do not appear in the history of request B
- [X] T048 [US5] CHECKPOINT: run US5 tests, confirm expected failures or already-green status (the ordering behavior is inherited), and obtain human approval of the tests before T049 (Constitution VI)

### Implementation for User Story 5

- [X] T049 [US5] Run US5 tests; expected: no production change needed because InMemoryChangeRequestStore.GetAuditEntries already orders by OccurredAt with a stable sort (ties keep insertion order). Do NOT modify GetAuditEntries — slice 001's AuditTests.GetAuditHistory_MultipleEntriesInjectedDirectly_ReturnedInAscendingOrder depends on it. Fix ApprovalService.cs only if a test fails

**Checkpoint**: All five stories pass.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T050 [P] Add a test in tests/AgenticChangeOps.Intake.Tests/Unit/ApprovalValidationTests.cs asserting src/AgenticChangeOps.Intake/AgenticChangeOps.Intake.csproj has no PackageReference (FR-014 / no LLM or external dependency; FR-013 approver is a plain string parameter with no identity lookup)
- [X] T051 [P] Scan new tests and fixtures for synthetic-only data (Principle I): no real names, organisations, secrets
- [X] T052 Run `dotnet build AgenticChangeOps.sln` (0 warnings) and `dotnet test AgenticChangeOps.sln`; confirm all slice 001 tests still pass at the T001 baseline count, all new tests pass, and a single decision completes well under 500 ms (SC-001)
- [X] T053 Walk through the 10 scenarios in specs/002-change-request-approval-gate/quickstart.md and confirm each maps to a passing test
- [X] T054 Constitution compliance check against v1.0.1: tick Principles I–VII against spec, plan, and final diff; confirm the additive ChangeRequestAuditEntry change is recorded in plan Complexity Tracking and the Principle III artefact-version interpretation is in plan.md

---

## Dependencies & Execution Order

- **Phase 1** → **Phase 2** (blocks everything) → **Phases 3–7** → **Phase 8**.
- Within Phase 2: T002–T006, T009, T010 are parallel (different files); T007 before T008; T011 after T009/T010; T012 after T011; T013 last.
- **US1 (P1)** has no story dependencies — MVP.
- **US2** reuses RecordDecision from US1 (T020); start after US1.
- **US3** completes the validator started in T019; start after US1.
- **US4** is independent of US2/US3 once Phase 2 is done (needs only `Update` from T008 for seeding); its tests (ClearanceTests.cs) can be written in parallel with US2/US3 tests by a second contributor.
- **US5** needs US1 and US2 behavior; it makes no production change to the store, so slice 001 stays green.
- Every story's CHECKPOINT task blocks that story's implementation tasks.

## Implementation Strategy

1. **MVP**: Phases 1–3 (US1) — an approver can approve and the result is audited.
2. **Increment 2**: US2 + US3 — full decision set with safe rejection of bad input.
3. **Increment 3**: US4 — the clearance check (the enforcement contract for later slices).
4. **Increment 4**: US5 + Polish — history guarantees and compliance sign-off.
5. Per Principles V and VI, stop at each CHECKPOINT for human approval of the failing tests.
