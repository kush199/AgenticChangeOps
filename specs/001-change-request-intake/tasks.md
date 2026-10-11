# Tasks: Change Request Intake

**Input**: Design documents from `specs/001-change-request-intake/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/ ✅

**Tests**: Included — Constitution Principle VI (Test-First) mandates tests written and
approved before implementation for all deterministic logic.

**Organization**: Tasks are grouped by user story to enable independent implementation
and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US4)

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Create the .NET 8 solution and project files before any code is written.

- [X] T001 Create AgenticChangeOps.sln solution file at repository root using `dotnet new sln -n AgenticChangeOps`
- [X] T002 Create src/AgenticChangeOps.Intake/AgenticChangeOps.Intake.csproj as a net8.0 classlib with zero production NuGet dependencies; add to solution
- [X] T003 Create tests/AgenticChangeOps.Intake.Tests/AgenticChangeOps.Intake.Tests.csproj as a net8.0 xUnit test project (xunit 2.x, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk); add project reference to AgenticChangeOps.Intake; add to solution
- [X] T004 Verify solution builds clean: `dotnet build AgenticChangeOps.sln` — 0 errors, 0 warnings

**Checkpoint**: Solution and projects compile; ready for foundational types.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: All shared enums, models, infrastructure interfaces, test fakes, store, and
service interface. MUST be complete before any user story work begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Enums (create all in parallel)

- [X] T005 [P] Create Priority enum in src/AgenticChangeOps.Intake/Models/Enums/Priority.cs with exactly four values: Low, Medium, High, Critical
- [X] T006 [P] Create ChangeType enum in src/AgenticChangeOps.Intake/Models/Enums/ChangeType.cs with exactly three values: Feature, Bugfix, ConfigChange
- [X] T007 [P] Create ChangeRequestStatus enum in src/AgenticChangeOps.Intake/Models/Enums/ChangeRequestStatus.cs with exactly five values: Submitted, UnderReview, Approved, Rejected, Deferred
- [X] T008 [P] Create AuditEventType enum in src/AgenticChangeOps.Intake/Models/Enums/AuditEventType.cs with exactly one value for this slice: Submitted

### Models (parallelizable; depend on T005–T008)

- [X] T009 [P] Create ValidationError record in src/AgenticChangeOps.Intake/Models/ValidationError.cs with two required string properties: FieldName and Message
- [X] T010 [P] Create SubmissionRequest record in src/AgenticChangeOps.Intake/Models/SubmissionRequest.cs with properties: Title (string), Description (string), Requester (string), AffectedSystem (string), Priority (Priority), ChangeType (ChangeType)
- [X] T011 [P] Create ChangeRequest record in src/AgenticChangeOps.Intake/Models/ChangeRequest.cs with properties: ChangeRequestId (Guid), Title (string), Description (string), Requester (string), AffectedSystem (string), Priority (Priority), ChangeType (ChangeType), Status (ChangeRequestStatus), SubmittedAt (DateTime); all properties immutable after construction
- [X] T012 [P] Create ChangeRequestAuditEntry record in src/AgenticChangeOps.Intake/Models/ChangeRequestAuditEntry.cs with properties: AuditEntryId (Guid), ChangeRequestId (Guid), EventType (AuditEventType), OccurredAt (DateTime), Details (string? — nullable)
- [X] T013 Create SubmissionResult class in src/AgenticChangeOps.Intake/Models/SubmissionResult.cs with: bool IsSuccess; ChangeRequest? ChangeRequest; IReadOnlyList<ValidationError> Errors; private constructor; static factory methods SubmissionResult.Success(ChangeRequest changeRequest) sets IsSuccess=true, Errors=empty; SubmissionResult.Failure(IReadOnlyList<ValidationError> errors) sets IsSuccess=false, ChangeRequest=null — invariant: Success and Errors are mutually exclusive

### Infrastructure interfaces and implementations (parallelizable)

- [X] T014 [P] Create ISystemClock interface in src/AgenticChangeOps.Intake/Infrastructure/ISystemClock.cs with single property: DateTime UtcNow { get; }
- [X] T015 [P] Create SystemClock class in src/AgenticChangeOps.Intake/Infrastructure/SystemClock.cs implementing ISystemClock; returns DateTime.UtcNow
- [X] T016 [P] Create IIdGenerator interface in src/AgenticChangeOps.Intake/Infrastructure/IIdGenerator.cs with single method: Guid NewId()
- [X] T017 [P] Create GuidIdGenerator class in src/AgenticChangeOps.Intake/Infrastructure/GuidIdGenerator.cs implementing IIdGenerator; returns Guid.NewGuid()

### Test fakes (parallelizable; live in test project)

- [X] T018 [P] Create FakeSystemClock in tests/AgenticChangeOps.Intake.Tests/Fakes/FakeSystemClock.cs implementing ISystemClock; exposes a settable Now property so tests can control the returned DateTime value
- [X] T019 [P] Create FakeIdGenerator in tests/AgenticChangeOps.Intake.Tests/Fakes/FakeIdGenerator.cs implementing IIdGenerator; accepts a queue of pre-configured Guid values via constructor; dequeues one per NewId() call; throws InvalidOperationException if queue is exhausted

### Store (depends on T011, T012)

- [X] T020 Create IChangeRequestStore interface in src/AgenticChangeOps.Intake/Stores/IChangeRequestStore.cs with four methods: void Save(ChangeRequest changeRequest); ChangeRequest? FindById(Guid id); void SaveAuditEntry(ChangeRequestAuditEntry entry); IReadOnlyList<ChangeRequestAuditEntry> GetAuditEntries(Guid changeRequestId)
- [X] T021 Implement InMemoryChangeRequestStore in src/AgenticChangeOps.Intake/Stores/InMemoryChangeRequestStore.cs implementing IChangeRequestStore; backed by Dictionary<Guid, ChangeRequest> for records and Dictionary<Guid, List<ChangeRequestAuditEntry>> for audit entries; GetAuditEntries returns entries sorted ascending by OccurredAt; returns empty list (not null) if ChangeRequestId has no entries

### Service interface (depends on T010–T013)

- [X] T022 Create IChangeRequestService interface in src/AgenticChangeOps.Intake/Services/IChangeRequestService.cs with three methods: SubmissionResult Submit(SubmissionRequest request); ChangeRequest? GetById(Guid changeRequestId); IReadOnlyList<ChangeRequestAuditEntry>? GetAuditHistory(Guid changeRequestId)

**Checkpoint**: `dotnet build` passes — all foundational types compile. No implementation logic yet. Ready for user story phases.

---

## Phase 3: User Story 1 — Submit Valid Change Request (Priority: P1) 🏆 MVP

**Goal**: A valid submission returns the full persisted ChangeRequest record; the record
is retrievable; one audit entry of type Submitted is recorded.

**Independent Test**: Submit a well-formed request with synthetic data; assert IsSuccess=true,
all fields persisted correctly, one Submitted audit entry exists.

### Tests for User Story 1

> **Write tests FIRST. Confirm they FAIL before writing any implementation.**

- [X] T023 [US1] Write SubmissionTests in tests/AgenticChangeOps.Intake.Tests/Unit/SubmissionTests.cs covering: (a) valid submission returns SubmissionResult.IsSuccess=true; (b) returned ChangeRequest.ChangeRequestId matches FakeIdGenerator's first value; (c) returned ChangeRequest.SubmittedAt matches FakeSystemClock.Now in UTC; (d) returned ChangeRequest.Status is Submitted; (e) returned ChangeRequest.Title is trimmed (e.g. "  Export Feature  " → "Export Feature"); (f) whitespace-only input that passes validation stores trimmed values; use synthetic data throughout (no real names or systems)

### Implementation for User Story 1

- [X] T024 [US1] Create ChangeRequestValidator stub in src/AgenticChangeOps.Intake/Services/ChangeRequestValidator.cs with method IReadOnlyList<ValidationError> Validate(SubmissionRequest request); stub returns empty list for all inputs (no rules yet — US2 adds them)
- [X] T025 [US1] Implement ChangeRequestService in src/AgenticChangeOps.Intake/Services/ChangeRequestService.cs implementing IChangeRequestService; constructor accepts IChangeRequestStore, ISystemClock, IIdGenerator, ChangeRequestValidator; implement Submit: call validator; if errors empty, create ChangeRequest with trimmed field values and system-assigned ChangeRequestId (via IIdGenerator) and SubmittedAt (via ISystemClock.UtcNow) and Status=Submitted; call store.Save(changeRequest); create ChangeRequestAuditEntry with new AuditEntryId (via IIdGenerator), same ChangeRequestId, EventType=Submitted, OccurredAt=same timestamp; call store.SaveAuditEntry(entry); return SubmissionResult.Success(changeRequest)
- [X] T026 [US1] Run SubmissionTests — all pass; confirm store.FindById returns the persisted record; confirm store.GetAuditEntries returns exactly one entry with EventType=Submitted

**Checkpoint**: User Story 1 fully functional and independently testable.

---

## Phase 4: User Story 2 — Reject Invalid Change Request (Priority: P2)

**Goal**: Invalid submissions are rejected with field-named error messages; nothing is
persisted when validation fails.

**Independent Test**: Submit requests with known invalid fields; assert IsSuccess=false;
assert Errors contains the correct FieldName and Message per violation; assert store is
unchanged.

### Tests for User Story 2

> **Write tests FIRST. Confirm they FAIL before adding validation rules.**

- [X] T027 [US2] Write ValidationTests in tests/AgenticChangeOps.Intake.Tests/Unit/ValidationTests.cs covering all rules from data-model.md: (a) blank Title → FieldName="Title", message contains "required"; (b) Title of 201 chars → FieldName="Title", message contains "200"; (c) Title of exactly 200 chars → no error for Title; (d) blank Description → FieldName="Description"; (e) Description of 2001 chars → FieldName="Description", message contains "2000"; (f) blank Requester → FieldName="Requester"; (g) Requester of 101 chars → message contains "100"; (h) blank AffectedSystem → FieldName="AffectedSystem"; (i) AffectedSystem of 201 chars → message contains "200"; (j) invalid Priority value (out-of-range cast) → FieldName="Priority", message lists accepted values Low/Medium/High/Critical; (k) invalid ChangeType value → FieldName="ChangeType", message lists Feature/Bugfix/ConfigChange; (l) both Title blank and Description blank simultaneously → Errors.Count=2 (collect-all); (m) failed submission → SubmissionResult.IsSuccess=false, ChangeRequest=null, store unchanged

### Implementation for User Story 2

- [X] T028 [US2] Implement all validation rules in ChangeRequestValidator.Validate in src/AgenticChangeOps.Intake/Services/ChangeRequestValidator.cs: (1) Title: required (null/blank/whitespace-only = error "Title is required."); max 200 chars (error "Title must not exceed 200 characters."); (2) Description: required; max 2000 chars; (3) Requester: required; max 100 chars; (4) AffectedSystem: required; max 200 chars; (5) Priority: must be defined enum value (error "Priority '{value}' is not valid. Accepted: Low, Medium, High, Critical."); (6) ChangeType: must be defined enum value (error "ChangeType '{value}' is not valid. Accepted: Feature, Bugfix, ConfigChange."); all rules run before return (collect-all semantics)
- [X] T029 [US2] Run ValidationTests and SubmissionTests — all pass; confirm US1 tests still pass after adding validation rules

**Checkpoint**: Submit rejects invalid input with field-named errors; valid path (US1) unaffected.

---

## Phase 5: User Story 3 — Retrieve Change Request by ID (Priority: P3)

**Goal**: A submitted change request can be retrieved by ChangeRequestId with all fields
intact; an unknown ID returns null.

**Independent Test**: After a successful submit, call GetById with the returned
ChangeRequestId; assert all fields match; call GetById with an unknown Guid; assert null.

### Tests for User Story 3

> **Write tests FIRST. Confirm they FAIL before implementing GetById.**

- [X] T030 [US3] Write RetrievalTests in tests/AgenticChangeOps.Intake.Tests/Unit/RetrievalTests.cs covering: (a) GetById with a ChangeRequestId from a prior successful submit returns ChangeRequest with all original field values intact (no data loss, no truncation — SC-004); (b) GetById with a Guid not in the store returns null; (c) two consecutive GetById calls with the same id return the same record (idempotent); (d) GetById with Guid.Empty (not in store) returns null

### Implementation for User Story 3

- [X] T031 [US3] Implement ChangeRequestService.GetById in src/AgenticChangeOps.Intake/Services/ChangeRequestService.cs — delegates to IChangeRequestStore.FindById(changeRequestId); returns result directly (ChangeRequest? — null if not found)
- [X] T032 [US3] Run RetrievalTests — all pass; confirm US1 and US2 tests still pass

**Checkpoint**: Retrieval by ID works; not-found returns null; all prior story tests green.

---

## Phase 6: User Story 4 — List Audit History for a Change Request (Priority: P4)

**Goal**: The full chronological audit trail for a change request can be retrieved by
ChangeRequestId; an unknown ID returns null.

**Independent Test**: After a successful submit, call GetAuditHistory with the returned
ChangeRequestId; assert exactly one entry with EventType=Submitted, OccurredAt=SubmittedAt.

### Tests for User Story 4

> **Write tests FIRST. Confirm they FAIL before implementing GetAuditHistory.**

- [X] T033 [US4] Write AuditTests in tests/AgenticChangeOps.Intake.Tests/Unit/AuditTests.cs covering: (a) GetAuditHistory after successful submit returns list with exactly 1 entry; entry.EventType=Submitted; entry.OccurredAt equals ChangeRequest.SubmittedAt; entry.ChangeRequestId equals the submitted record's id (SC-005); (b) GetAuditHistory for an unknown Guid returns null; (c) if store were to contain multiple audit entries for a single id (inject directly), GetAuditHistory returns them in ascending OccurredAt order (validates ordering guarantee)

### Implementation for User Story 4

- [X] T034 [US4] Implement ChangeRequestService.GetAuditHistory in src/AgenticChangeOps.Intake/Services/ChangeRequestService.cs — call store.FindById(changeRequestId); if null, return null; else call store.GetAuditEntries(changeRequestId) and return the result (store already returns sorted list per T021)
- [X] T035 [US4] Run AuditTests — all pass; confirm all prior story tests (US1, US2, US3) still pass

**Checkpoint**: All four user stories fully functional and independently testable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validate quality gates, coverage, and constitution compliance across all stories.

- [X] T036 [P] Run full test suite: `dotnet test AgenticChangeOps.sln --logger "console;verbosity=normal"` — all tests pass, 0 failures, 0 skipped
- [X] T037 [P] Collect code coverage: `dotnet test AgenticChangeOps.sln --collect:"XPlat Code Coverage"` — verify 100% of deterministic logic paths covered (FR-001–FR-017, all validator rules, all service methods, both store paths); confirm SC-006 is met
- [X] T038 [P] Review all test files in tests/AgenticChangeOps.Intake.Tests/ and confirm every string literal used as test data (titles, descriptions, requester names, system names) is fully synthetic — no real organisation names, real people, or real system names (Constitution Principle I)
- [X] T039 Execute Quickstart validation scenarios 1–9 from specs/001-change-request-intake/quickstart.md using a short driver program or test — all expected outcomes match
- [X] T040 [P] Build in Release configuration: `dotnet build AgenticChangeOps.sln --configuration Release` — 0 errors, 0 warnings

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **blocks all user story phases**
- **Phase 3 (US1)**: Depends on Phase 2 — can start immediately after foundational
- **Phase 4 (US2)**: Depends on Phase 3 complete (validator stub exists; service wired)
- **Phase 5 (US3)**: Depends on Phase 2 complete (independent of US2)
- **Phase 6 (US4)**: Depends on Phase 3 complete (Submit must work to produce audit entries)
- **Phase 7 (Polish)**: Depends on all user story phases complete

### User Story Dependencies

- **US1 (P1)**: No dependency on other stories — first to implement
- **US2 (P2)**: Depends on US1 (validator stub + service must exist to add rules to)
- **US3 (P3)**: Independent of US2 — depends only on Phase 2 + US1's service skeleton
- **US4 (P4)**: Independent of US2 and US3 — depends on US1's Submit creating audit entries

### Within Each User Story

1. Write tests → confirm they **FAIL** (constitution Principle VI — Test-First)
2. Implement → confirm tests **PASS**
3. Re-run all prior story tests → confirm **no regressions**

### Parallel Opportunities

- T005–T008 (enums): all in parallel
- T009–T012 (models): all in parallel after enums
- T014–T019 (infrastructure interfaces, implementations, fakes): all in parallel
- T020–T022 (store interface, store impl, service interface): T020 first; T021 after T020; T022 independent
- T036–T040 (polish): T036, T037, T038, T040 in parallel; T039 after T036

---

## Parallel Example: Phase 2 Enums + Infrastructure

```
Parallel batch 1 (T005–T008):
  Task: "Create Priority enum in src/AgenticChangeOps.Intake/Models/Enums/Priority.cs"
  Task: "Create ChangeType enum in src/AgenticChangeOps.Intake/Models/Enums/ChangeType.cs"
  Task: "Create ChangeRequestStatus enum in src/.../Enums/ChangeRequestStatus.cs"
  Task: "Create AuditEventType enum in src/.../Enums/AuditEventType.cs"

Parallel batch 2 (T009–T012, T014–T019 — after T005–T008):
  Task: "Create ValidationError record in src/.../Models/ValidationError.cs"
  Task: "Create SubmissionRequest record in src/.../Models/SubmissionRequest.cs"
  Task: "Create ChangeRequest record in src/.../Models/ChangeRequest.cs"
  Task: "Create ChangeRequestAuditEntry record in src/.../Models/ChangeRequestAuditEntry.cs"
  Task: "Create ISystemClock interface in src/.../Infrastructure/ISystemClock.cs"
  Task: "Create SystemClock in src/.../Infrastructure/SystemClock.cs"
  Task: "Create IIdGenerator interface in src/.../Infrastructure/IIdGenerator.cs"
  Task: "Create GuidIdGenerator in src/.../Infrastructure/GuidIdGenerator.cs"
  Task: "Create FakeSystemClock in tests/.../Fakes/FakeSystemClock.cs"
  Task: "Create FakeIdGenerator in tests/.../Fakes/FakeIdGenerator.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 (tests → validator stub → service → verify)
4. **STOP and VALIDATE**: All SubmissionTests pass; record persisted; audit entry created
5. Demo if ready

### Incremental Delivery

1. Phase 1 + Phase 2 → Foundation ready
2. Phase 3 (US1) → Submit happy path works (**MVP**)
3. Phase 4 (US2) → Validation rejection works
4. Phase 5 (US3) → Retrieval by ID works
5. Phase 6 (US4) → Audit history works
6. Phase 7 → All quality gates green

### Parallel Team Strategy

After Phase 2 completes:
- Developer A: Phase 3 (US1) → Phase 4 (US2)
- Developer B: Phase 5 (US3) → Phase 6 (US4) *(US3 is independent of US2)*
- Merge and run full suite before Phase 7

---

## Notes

- **[P]** tasks operate on different files with no incomplete-task dependencies
- **[USn]** label maps each task to its user story for traceability
- Tests MUST be written and confirmed **failing** before any implementation (Principle VI)
- All test data (titles, requesters, system names) MUST be fully synthetic (Principle I)
- No production NuGet packages — zero external dependencies in AgenticChangeOps.Intake.csproj
- Commit after each story checkpoint (or after each task in TDD cycle)
- Stop at any checkpoint to validate the story independently before proceeding
