# Tasks: Durable Storage for Change Requests

**Input**: Design documents from `specs/012-durable-storage/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). New tests are shown red against signature-only stubs whose members throw `NotImplementedException`, and approved (T025) before the stubs are filled in. Red means failing for the right reason: behaviour, not missing types.

**Constitution**: [x] Reviewed against constitution v1.0.3. See plan.md Constitution Check.

**Organization**: Grouped by user story (US1 to US5 from spec.md). The store is shared by all stories, so the project skeleton, exceptions, interface additions and the shared contract suite are Foundational; all story tests are written together (Phase 3) and shown red; each story's implementation then turns them green (Phases 4 and 5).

**Rules that hold for every task**:
- Permitted production edits: new `src/AgenticChangeOps.Persistence/`; new `src/AgenticChangeOps.Intake/Stores/StorageException.cs`; `src/AgenticChangeOps.Intake/Stores/IChangeRequestStore.cs` and `InMemoryChangeRequestStore.cs` (the two new members `SaveWithAudit` and `UpdateWithAudit` only); `src/AgenticChangeOps.Intake/Services/ChangeRequestService.cs` and `ApprovalService.cs` (replace the paired store calls with the new members, nothing else); `src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs`; `src/AgenticChangeOps.Api/Errors/ErrorMapper.cs`; `src/AgenticChangeOps.Api/Errors/JsonErrorMiddleware.cs`; `src/AgenticChangeOps.Api/Program.cs` (OpenAPI description text only); `src/AgenticChangeOps.Api/AgenticChangeOps.Api.csproj` (one project reference); `AgenticChangeOps.sln`; `.gitignore`; `README.md`. Nothing else in `src` changes: no agent, other service, endpoint or record edit, and no change to an existing member of `IChangeRequestStore`.
- No assertion in an existing test is edited. The only edit to an existing test file is `tests/AgenticChangeOps.Api.Tests/Fakes/ApiFactory.cs` (T014: default the storage setting to `InMemory`, keep every parameter and behaviour). New tests go in new files. If an existing test fails, fix the new code, never the assertion.
- `src/AgenticChangeOps.Persistence` names no agent, no agent project and no `ModelAccess`; its only project reference is Intake. Intake's project file stays reference-free.
- No error, log line or response carries a path, connection detail, SQL or driver message (FR-011). Driver exceptions are never passed as an inner exception.
- Synthetic data only (Principle I). Temp database files live under the test run's temp folder and are deleted after each test; no `.db` file is ever tracked.
- After every task that touches code, `dotnet build AgenticChangeOps.sln` succeeds; after every phase, `dotnet test AgenticChangeOps.sln` is green (except the deliberately red state at T024).
- Layout version for this slice is `1`.
- Record comparison in tests is member-wise: records compare list members by reference, so tests compare list members element by element (`SequenceEqual`), never with plain record equality on records that hold lists.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task). Tasks editing the same file are never `[P]`.
- **[Story]**: US1 to US5, from spec.md.
- **[USER]**: outward-facing step (push, PR). Claude drafts, the user confirms.

---

## Phase 1: Setup

- [X] T001 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md are approved (given in chat after the analysis edits, 2026-10-16). No test or source file is written before this (Principle V). Tick this box when implementation starts.
- [X] T002 Confirm branch `feature/persistence` and a clean tree apart from `specs/012-durable-storage/`. Run `dotnet test AgenticChangeOps.sln` and save per-project totals (and skipped live checks) to `specs/012-durable-storage/baseline-tests.txt`, same layout as `specs/011-architect-agent-framework/baseline-tests.txt`. This is the SC-002 reference.
- [X] T003 In `specs/012-durable-storage/baseline-tests.txt`, list the existing API test files that already assert status, body and headers (`tests/AgenticChangeOps.Api.Tests/ErrorShapeTests.cs`, `IntakeEndpointTests.cs`, `DecisionEndpointTests.cs`, `DraftEndpointTests.cs`, `DesignEndpointTests.cs`, `WorkflowEndpointTests.cs`). This is the SC-003 reference; no new golden files.
- [X] T004 Check NuGet access for `Microsoft.Data.Sqlite` 8.0.x with `dotnet package search Microsoft.Data.Sqlite --exact-match`; T007's first restore is the final proof. If unavailable, **stop and tell the user**; the package is not in the local cache.
- [X] T005 [P] Add `*.db`, `*.db-journal`, `*.db-wal`, `*.db-shm`, `*.sqlite` and `*.sqlite3` to `.gitignore` (FR-008).

## Phase 2: Foundational (blocks every story)

- [X] T006 Create `src/AgenticChangeOps.Intake/Stores/StorageException.cs` with `enum StorageFailureCategory { Unavailable, UnsupportedLayout }`, `sealed class StorageUnavailableException : Exception` (property `Category`; fixed message `The change request storage is unavailable.`; constructor takes only the category, no inner exception) and `sealed class StorageVersionConflictException : Exception` (fixed message `The request conflicts with a concurrent change; try again.`). Also add to `src/AgenticChangeOps.Intake/Stores/IChangeRequestStore.cs` the members `void SaveWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry)` and `void UpdateWithAudit(ChangeRequest changeRequest, ChangeRequestAuditEntry auditEntry)` (summary: stored together or neither; refuses mismatched change request ids), and signature-only members throwing `NotImplementedException` in `InMemoryChangeRequestStore.cs`. Intake's project file gains no reference. Existing tests stay green because no service calls the new members yet.
- [X] T007 Create `src/AgenticChangeOps.Persistence/AgenticChangeOps.Persistence.csproj` (`net8.0`, `ImplicitUsings`, `Nullable` enabled; `ProjectReference` to Intake only; `PackageReference Microsoft.Data.Sqlite` 8.0.x) and add it to `AgenticChangeOps.sln`; run `dotnet restore` (network required).
- [X] T008 Create signature-only stubs in `src/AgenticChangeOps.Persistence/`: `SqliteStorageOptions.cs` (`Path` string; `static string DefaultPath()` returning `<LocalApplicationData>/AgenticChangeOps/changeops.db`, falling back to the user profile folder when `LocalApplicationData` is empty, never the working directory), `StorageSchema.cs` (`const int LayoutVersion = 1`), `RecordJson.cs` (shared `JsonSerializerOptions` with string enums) and `SqliteChangeRequestStore.cs` implementing `IChangeRequestStore` and `IDisposable`, constructor `SqliteChangeRequestStore(SqliteStorageOptions options)`, every store member throwing `NotImplementedException`.
- [X] T009 Create `tests/AgenticChangeOps.Persistence.Tests/AgenticChangeOps.Persistence.Tests.csproj` (xUnit, same package versions as `AgenticChangeOps.Api.Tests`; references Intake and Persistence; `Using Include="Xunit"`) and add it to the solution.
- [X] T010 Create `tests/AgenticChangeOps.Persistence.Tests/Support/TempDatabase.cs` (unique temp file path per test; deletes the file and its journal files on dispose), `Support/FakeAgentOutput.cs` (test-only record implementing `IAgentOutput` with a `DateTime`, an enum and an `IReadOnlyList<string>` member), `Support/SampleRecords.cs` (synthetic `ChangeRequest` and `ChangeRequestAuditEntry` builders with caller-set ids and times) and `Support/RecordAssert.cs` (member-wise comparison helpers: list members compared with `SequenceEqual`, `DateTime` compared by ticks and `Kind`).
- [X] T011 Create `tests/AgenticChangeOps.Persistence.Tests/Contract/ChangeRequestStoreContract.cs`: abstract class with `protected abstract IChangeRequestStore CreateStore()` implementing cases C1 to C13 of `contracts/store-contract.md`. C6, C7 and C13 assert "refused and nothing stored" with `Assert.ThrowsAny<Exception>` followed by checks that neither the item nor its audit entry is readable. C11 uses `RecordAssert` (member-wise). C12 races 8 threads for one version and asserts exactly one success.
- [X] T012 [P] Create `tests/AgenticChangeOps.Persistence.Tests/Contract/InMemoryStoreContractTests.cs`: subclass returning `new InMemoryChangeRequestStore()`. Green for C1 to C12 immediately (proving the suite describes today's behaviour); C13 is red until T030.
- [X] T013 [P] Create `tests/AgenticChangeOps.Persistence.Tests/Contract/SqliteStoreContractTests.cs`: subclass returning a `SqliteChangeRequestStore` over a `TempDatabase` disposed with the test. Red until Phase 4.
- [X] T014 Edit `tests/AgenticChangeOps.Api.Tests/Fakes/ApiFactory.cs` only: add `["ChangeOps:Storage:Provider"] = "InMemory"` to the applied settings unless the caller's `settings` already contain that key. Every constructor parameter and all existing behaviour stay. Run the Api tests: still green.

**Checkpoint**: solution builds; `InMemoryStoreContractTests` green except C13; Sqlite contract tests red.

## Phase 3: Tests first (all shown red at T024)

- [X] T015 [P] [US1] Create `tests/AgenticChangeOps.Persistence.Tests/Durable/RestartTests.cs`: write change requests (one updated to Approved through `UpdateWithAudit`), audit entries (equal and distinct timestamps) and two `FakeAgentOutput` versions with audit entries; dispose the store; open a new `SqliteChangeRequestStore` on the same file; assert with `RecordAssert` that every record equals what was written, audit order is unchanged, outputs are ascending, and the next version recorded after reopening is 3 (D1; spec US1 scenarios 1 to 3).
- [X] T016 [P] [US3] Create `tests/AgenticChangeOps.Persistence.Tests/Durable/AtomicityTests.cs`: open a second connection to the temp file and create a trigger `BEFORE INSERT ON audit_entries` that aborts (`SELECT RAISE(ABORT, 'x')`); then call `SaveAgentOutput`, `SaveWithAudit` and `UpdateWithAudit` and assert each throws, and that no output row, no change request row, and no changed change request payload remain; drop the trigger and assert the same calls then succeed (D2, FR-003, FR-004).
- [X] T017 [P] [US3] Create `tests/AgenticChangeOps.Persistence.Tests/Durable/ConcurrencyTests.cs`: 50 threads call `SaveAgentOutput` for one change request, output type and version without holding `SyncRoot`; assert exactly one success, 49 `StorageVersionConflictException`, and, after N-1 further sequential saves, stored versions 1..N with no gap or duplicate (D6, SC-004). A second test: threads for different change requests and different output types all succeed.
- [X] T018 [P] [US4] Create `tests/AgenticChangeOps.Persistence.Tests/Durable/LayoutVersionTests.cs`: a new file is stamped `PRAGMA user_version` 1 (read through a separate connection); a file stamped 99, a SQLite file with unrelated tables and `user_version` 0, and a non-SQLite file are each refused on first operation with `StorageUnavailableException` category `UnsupportedLayout` and their bytes (SHA-256) are unchanged (D3, FR-010).
- [X] T019 [P] [US4] Create `tests/AgenticChangeOps.Persistence.Tests/Durable/UnavailableStorageTests.cs`: construction never throws and never touches the disk (lazy open); a location whose directory cannot be created, and a path that is a directory, are refused with category `Unavailable`; the store works once the location becomes valid, without recreating the process (D4, FR-011); `SqliteStorageOptions.DefaultPath()` is absolute, ends in `AgenticChangeOps/changeops.db`, and is not under the working directory.
- [X] T020 [P] [US4] Create `tests/AgenticChangeOps.Persistence.Tests/Durable/NoLeakTests.cs`: for every failure above, `Message`, `ToString()`, `InnerException == null` and `Data` contain none of the path, the folder name, `sqlite`, `SQL`, `Microsoft.Data` (SC-006).
- [X] T021 [P] [US5] Create `tests/AgenticChangeOps.Persistence.Tests/Durable/StructureTests.cs`: source scans proving `src/AgenticChangeOps.Persistence` (code and project file) names no agent (`Agents.`, `Requirement`, `Architect`, `DesignDraft`), no `ModelAccess`, references only Intake, and that `.gitignore` contains `*.db` (FR-008, FR-016, D5). Green on the stub; kept as a guard.
- [X] T022 [P] [US2] Create `tests/AgenticChangeOps.Api.Tests/Storage/DurableApiTests.cs`: with `Provider=Sqlite` and a temp path, run submit, approve, draft and design (fake generators) and workflow status; build a new `ApiFactory` on the same file and assert the GET responses (status, body, headers) equal those before the rebuild. Run the same sequence with `Provider=InMemory` and assert identical statuses, bodies and headers apart from generated ids and timestamps (SC-003). Red until Phase 5.
- [X] T023 [P] [US4] Create `tests/AgenticChangeOps.Api.Tests/Storage/StorageErrorTests.cs`: with an unusable `Path` the host starts; `POST /change-requests` returns 503 `storage-unavailable`, category `Unavailable`, in the existing error-body shape, `application/json`; with an unsupported-layout file the category is `UnsupportedLayout`; route 404 and Swagger still answer; no body, header or captured log line contains the path or `sqlite`; a replaced store that throws `StorageVersionConflictException` yields 409 `version-conflict`. Red until Phase 5.
- [X] T024 Run `dotnet test AgenticChangeOps.sln`. Expected: all pre-existing tests, `InMemoryStoreContractTests` (except C13) and `StructureTests` green; every Sqlite, durable and new API storage test, and in-memory C13, red with `NotImplementedException` or the missing behaviour (the right reason). Save the output.
- [X] T025 **GATE (human approval)**: show the user the red output from T024 and the new test files; get "approved" before any stub is filled in (project test-approval checkpoint).

## Phase 4: Store implementation (User Stories 1, 3, 5)

**Goal**: restart survival, all-or-nothing writes, unique versions, parity with the in-memory store.
**Independent test**: `dotnet test tests/AgenticChangeOps.Persistence.Tests` all green.

- [X] T026 [US1] Implement `StorageSchema.cs` and the open logic in `src/AgenticChangeOps.Persistence/SqliteChangeRequestStore.cs`: lazy open on first operation under the store's lock; create the parent directory; a file that has no tables gets, in one transaction, tables `change_requests(id TEXT PRIMARY KEY, payload TEXT)`, `audit_entries(seq INTEGER PRIMARY KEY AUTOINCREMENT, change_request_id TEXT, occurred_at_ticks INTEGER, payload TEXT)` with an index on `(change_request_id, occurred_at_ticks, seq)`, `agent_outputs(change_request_id TEXT, output_type TEXT, version INTEGER, payload TEXT, PRIMARY KEY (change_request_id, output_type, version))`, and `PRAGMA user_version = 1`; a file with `user_version` 1 is used; anything else (another version, other tables with `user_version` 0, a non-SQLite file) throws `StorageUnavailableException(UnsupportedLayout)` having written nothing; any other open failure throws `StorageUnavailableException(Unavailable)` and discards the connection so the next operation retries. `PRAGMA synchronous = FULL`. Driver exceptions are caught and replaced by the fixed-message types with no inner exception.
- [X] T027 [US1] In the same file implement `Save`, `Update`, `FindById` with JSON payloads via `RecordJson`. `Save` of an existing id and `Update` of an unknown id throw `InvalidOperationException` as the in-memory store does, with messages that contain no path.
- [X] T028 [US1] In the same file implement `SaveAuditEntry` and `GetAuditEntries`, ordered by `occurred_at_ticks` then `seq` (matches the in-memory stable sort by `OccurredAt`).
- [X] T029 [US3] In the same file implement `SaveAgentOutput<T>`, `GetAgentOutputs<T>`, `SaveWithAudit` and `UpdateWithAudit`: same argument checks as the in-memory store before any write; one transaction per call inserts the output (key `typeof(T).FullName`, `Version` and `ChangeRequestId` read through `IAgentOutput` only) or the change request, and the audit entry; a primary-key violation on an output rolls back and throws `StorageVersionConflictException`; any other failure rolls back and throws `StorageUnavailableException(Unavailable)`; `SaveWithAudit` of an existing id and `UpdateWithAudit` of an unknown id throw `InvalidOperationException` and store nothing. `SyncRoot` returns the store's lock object and every public method takes it (re-entrant).
- [X] T030 [US3] Implement `SaveWithAudit` and `UpdateWithAudit` in `src/AgenticChangeOps.Intake/Stores/InMemoryChangeRequestStore.cs`: check everything first (matching ids, existing or missing id), then mutate under `SyncRoot`, so both items are stored or neither; messages and exception types as the existing `Save` and `Update`. In-memory C13 is green.
- [X] T031 [US1] Implement `Dispose` (close the connection) and run `dotnet test tests/AgenticChangeOps.Persistence.Tests`: contract suite green for both stores, restart, atomicity, concurrency, layout, unavailable, no-leak and structure tests all green. Show the user.

## Phase 5: Wiring and HTTP behaviour (User Stories 2 and 4)

**Goal**: the API uses the durable store by default, reports the two new failures, and nothing else changes.
**Independent test**: full solution green; `DurableApiTests` and `StorageErrorTests` green.

- [X] T032 [US2] Add a `ProjectReference` to `..\AgenticChangeOps.Persistence\AgenticChangeOps.Persistence.csproj` in `src/AgenticChangeOps.Api/AgenticChangeOps.Api.csproj`.
- [X] T033 [US2] In `src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs` replace the fixed in-memory store registration with a factory reading `ChangeOps:Storage:Provider` (`Sqlite` when absent; `InMemory`; any other value is treated as `Sqlite`) and `ChangeOps:Storage:Path` (default `SqliteStorageOptions.DefaultPath()`); update the method's summary comment. Nothing else in the file changes.
- [X] T034 [US3] In `src/AgenticChangeOps.Intake/Services/ChangeRequestService.cs` replace `_store.Save(changeRequest); _store.SaveAuditEntry(auditEntry);` with `_store.SaveWithAudit(changeRequest, auditEntry);`, and in `src/AgenticChangeOps.Intake/Services/ApprovalService.cs` replace `_store.Update(updated); _store.SaveAuditEntry(entry);` with `_store.UpdateWithAudit(updated, entry);`. Keep the surrounding `lock (_store.SyncRoot)` and every other line. All existing Intake tests stay green with unchanged assertions.
- [X] T035 [P] [US4] In `src/AgenticChangeOps.Api/Errors/ErrorMapper.cs` add `StorageUnavailable(StorageFailureCategory)` returning 503 `storage-unavailable` with the fixed message and `Category`, and `VersionConflict()` returning 409 `version-conflict`, both in the existing `ErrorResponse` shape per `contracts/error-responses.md`.
- [X] T036 [US4] In `src/AgenticChangeOps.Api/Errors/JsonErrorMiddleware.cs` add `catch (StorageUnavailableException)` and `catch (StorageVersionConflictException)` before the generic catch, writing the mapped errors; the refusal log line stays the existing one (no message, no exception object).
- [X] T037 [P] [US4] **Not done, by design**: adding the two codes to the OpenAPI description in `src/AgenticChangeOps.Api/Program.cs` broke the existing `ApiBaselineTests.ApiDescription_IsTheRecordedOne`, which pins that text exactly. The rule is to fix new code, never an existing golden, so the edit was reverted. The two codes are documented in `contracts/error-responses.md` and the README instead.
- [X] T038 [US2] Run `dotnet test AgenticChangeOps.sln`: every pre-existing test green with assertions unchanged, plus all new tests. Compare totals with `baseline-tests.txt`; only additions allowed (SC-002).

## Phase 6: Polish and cross-cutting concerns

- [X] T039 [P] Update `README.md`: remove "persistent storage (data is held in memory and lost when the API stops)" from "Not built yet", add a Durable storage row to the capability table, document `ChangeOps__Storage__Path` and `ChangeOps__Storage__Provider`, and add Persistence (depends on Intake only) to the layout and dependency description.
- [X] T040 Run the manual checks in `specs/012-durable-storage/quickstart.md` steps 2 to 5 with a temp path and synthetic data; confirm `git status` shows no database file; record results under a "Verified" note in `quickstart.md`, including that a real process kill mid-write was not exercised (covered by injected failure in T016).
- [X] T041 Constitution compliance check of spec, plan, tasks and the diff against v1.0.3 (Principles I, IV, VI, VII; no connection string or secret; Complexity Tracking items match what was built); record the result in `specs/012-durable-storage/plan.md`.
- [X] T042 Run `dotnet build AgenticChangeOps.sln -c Release` with no new warnings, then `dotnet test AgenticChangeOps.sln -c Release` as CI does.
- [ ] T043 [USER] Draft the commit and pull request (title, description with the constitution checklist line); the user confirms before any push or PR creation.

---

## Dependencies and order

- T001 gates everything. Phase 1 → Phase 2 → Phase 3 → T024 → **T025 gate** → Phase 4 → Phase 5 → Phase 6.
- Phase 2: T006 → T007 → T008; T009 → T010 → T011 → T012, T013; T014 independent of the store.
- Phase 4: T026 → T027 → T028 → T029 → T031 (one file, sequential); T030 (different file) can run alongside T026 to T029.
- Phase 5: T032 → T033; T034 after T030 and T031; T035 → T036; T037 after T035; T038 last.
- Story completion: US5 (parity) and US1, US3 complete at T031 (T034 completes US3 end to end); US2 and US4 complete at T038.

## Parallel opportunities

- T005 alongside T002 to T004.
- T012 and T013 (different files).
- T015 to T023 (nine different files, written together and shown red together).
- T030 alongside T026 to T029 (different file).
- T035 and T037 (different files); T039 alongside T040.

## Independent test per story

| Story | Passing when |
|---|---|
| US1 restart | `RestartTests` and the contract suite pass on a reopened file |
| US2 no difference | Existing suites unchanged and green; `DurableApiTests` shows identical responses across a rebuild and across providers |
| US3 all-or-nothing and versions | `AtomicityTests`, `ConcurrencyTests`, contract C5 to C9, C12 and C13 |
| US4 unavailable / unsupported | `LayoutVersionTests`, `UnavailableStorageTests`, `NoLeakTests`, `StorageErrorTests` |
| US5 parity and neutrality | Contract suite green for both stores including C10; `StructureTests` green |

## Implementation strategy

MVP = Phases 1 to 4 (the store, proven by the shared and durable suites); Phase 5 makes the API use it. Stop at T025 for the red-test approval before any implementation.


## Phase 7: Convergence

- [X] T044 Reconcile the artifacts with the delivered code: remove the line "The OpenAPI description lists the two new codes." from `specs/012-durable-storage/contracts/error-responses.md` and the `Program.cs` entry from the source tree in `specs/012-durable-storage/plan.md`, and add a note that the existing pinned API description is unchanged and the two codes are documented in the contract and README (T037 was closed as not done, by design) per contracts/error-responses.md (partial)
- [X] T045 Make the audit ordering rule explicit and tested: add a contract case C14 to `tests/AgenticChangeOps.Persistence.Tests/Contract/ChangeRequestStoreContract.cs` and `specs/012-durable-storage/contracts/store-contract.md` asserting that entries recorded with an earlier timestamp after a later one come back in timestamp order, then insertion order, identically for the in-memory and SQLite stores, and add the same statement to the Assumptions in `specs/012-durable-storage/spec.md` (FR-007 says "order recorded", research R5 chose timestamp then insertion order to match the existing in-memory behaviour) per FR-007 (partial)
