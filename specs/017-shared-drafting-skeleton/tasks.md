# Tasks: Shared Drafting Skeleton

**Input**: Design documents from `specs/017-shared-drafting-skeleton/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (engine-contract, dependency-rules, http-api-unchanged, kept-separate-register), quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). Characterization tests of the three existing services are run green on the old code before anything moves and then stay unchanged. New skeleton tests are added against compiling **stubs** (Phase 2) so they fail for the right reason, are shown red once, and are approved at T021 before any engine behaviour is implemented.

**Constitution**: [X] Reviewed against constitution v1.0.6 before approval (see plan.md Constitution Check). Tick at T001.

**Organization**: Grouped by user story from spec.md: US1 one place for the drafting steps (P1), US2 callers see no difference (P1), US3 a fourth agent adds only itself (P2), US4 register and duplication figures (P2), US5 each shared behaviour proven once (P3). The engine is built under US1; each service is converted as its own task so every conversion is checked by the full suite.

**Rules that hold for every task**:
- Every existing test passes with its assertions unchanged (FR-011). The only edits to existing tests are the three agent structure tests' expected reference lists (T008), approved at T001. If any other existing test fails, fix the new code, never the assertion.
- No new setting, package, endpoint, capability or record shape. The only new reference is `AgenticChangeOps.Drafting` and each agent's reference to it.
- The Drafting project must not name an agent, a stored record type, an approval type or automatic advancement (rules D1, D2), and must not reference an agent (D3).
- A difference between agents that is intentional stays in that agent's own file and is listed in the register (FR-012).
- No live model call in the suite; fakes only. Synthetic data only (Principle I).
- After every code task `dotnet build AgenticChangeOps.sln` succeeds. After every task in a conversion (T023 to T025) `dotnet test AgenticChangeOps.sln` is green.
- If a conversion makes a service harder to read than before, record it in `readability.md` (T036); the user decides.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel (different files, no dependency on an incomplete task). Tasks editing the same file are never `[P]`.
- **[Story]**: US1 to US5, from spec.md.
- **[USER]**: outward-facing or approval step. Claude drafts, the user confirms.

---

## Phase 1: Setup (the "before" picture)

- [X] T001 **GATE ([USER])**: the user confirms spec.md, plan.md and tasks.md are approved, and the pre-code constitution check (v1.0.6) is appended to plan.md under "Constitution compliance check (pre-code)": no routing change (II), no approval change (III), no record change (IV), relocation of existing behaviour with three callers (VII). Tick the **Constitution** line above. No source or test file is written before this (Principle V).
- [X] T002 Confirm the branch is `feature/shared-drafting-skeleton` and the tree is clean. Run `dotnet test AgenticChangeOps.sln`; save per-project totals and skipped live checks to `specs/017-shared-drafting-skeleton/baseline-tests.txt`, same layout as `specs/016-developer-agent-proposals/baseline-tests.txt`.
- [X] T003 Extend `specs/009-shared-agent-plumbing/measure-duplication.ps1` into `specs/017-shared-drafting-skeleton/measure-duplication.ps1` for three agents: same normalisation (blank, comment, `using`, `namespace`, brace-only lines removed), word list extended with `Proposal`, `Proposals`, `Developer`, `Change`; report each agent's normalised lines, the lines shared by each pair, the total of pairwise shared lines (headline, so it extends the earlier two-agent figure), and the lines present in all three. Keep `-Scope Tests` and `-Root`. Sanity-check the script by running it with `-Root` on a worktree of slice 009's merge commit and confirming its two-agent figures match `specs/009-shared-agent-plumbing/duplication.md` (155 shared lines after). Today's commit is not expected to give that number, because slices 011 to 016 changed both agents; state this in `duplication.md`.
- [X] T004 Run the T003 script on the current commit (`7bf6f44`) and write the "Before" figures, with the method, to `specs/017-shared-drafting-skeleton/duplication.md` (table layout as slice 009: per-agent lines, pairwise shared, all-three shared, percentage of the smaller agent).
- [X] T005 Record the HTTP baseline required by contracts/http-api-unchanged.md under `specs/017-shared-drafting-skeleton/baseline/`: with fixed synthetic change requests and scripted generators, the exact status, headers and body of every operation in the contract list (produce, latest, history for all three agents: success, unknown id, ineligible status, missing requirements draft, missing or outdated design draft, not configured, each generator failure category, unusable content, each Developer rejection reason). Commit the scenario definition as `baseline/scenario.json`. Capture with a throwaway local script outside the repository; commit only the recorded text and the scenario file. Run the capture twice and confirm the two recordings are byte-identical; if anything varies (ids, timestamps, ordering), fix the scenario with a fixed clock and sequential ids before continuing.
- [X] T005a Add `tests/AgenticChangeOps.Api.Tests/Baseline/DraftingSkeletonBaselineTests.cs`, which replays `baseline/scenario.json` and compares every status, header and body byte for byte with the T005 recordings. Run it on the **old** code: it must pass. It stays unchanged afterwards.
- [X] T006 **Gap review** of `RequirementsDraftingService.cs`, `DesignDraftingService.cs` and `ProposalService.cs`: for each branch of the sequence in contracts/engine-contract.md (eligibility failure; category failure; other-exception failure with type name only; cancellation; invalid content; Developer rejection; locked re-check failing; currency failure; version assignment under overlapping calls; atomic save; reads including not found) find the existing test that asserts it, per service. Write the table to `specs/017-shared-drafting-skeleton/gap-review.md`. Every branch without an assertion in a service is a gap.
- [X] T007 For each gap in `gap-review.md` add a characterization test in the matching agent test project under `tests/AgenticChangeOps.Agents.*.Tests/Characterization/` (new files only). Run them on the **old** code: all must pass. Commit them; they stay unchanged afterwards. In particular cover the cancellation difference (Developer rethrows when the caller's token is cancelled; Requirement and Architect return a `Generator` failure naming the exception type) and the Developer's extra tool-call entries written before the main entry inside the store step. Do not convert any service until T021 approves these.

---

## Phase 2: Foundational (stubs and references; blocks every story)

Stubs only: engine behaviour methods throw `NotImplementedException` until US1 implements them.

- [X] T008 Create `src/AgenticChangeOps.Drafting/AgenticChangeOps.Drafting.csproj` (net8.0, ImplicitUsings, Nullable, project references to Intake and ModelAccess only; no packages) and add it to `AgenticChangeOps.sln`. Add a `ProjectReference` to it in each of the three agent projects. Edit the expected list in the three structure tests (`tests/AgenticChangeOps.Agents.Requirement.Tests/StructureTests.cs`, `tests/AgenticChangeOps.Agents.Architect.Tests/StructureTests.cs`, `tests/AgenticChangeOps.Agents.Developer.Tests/DeveloperStructureTests.cs`) to `AgenticChangeOps.Drafting`, `AgenticChangeOps.Intake`, `AgenticChangeOps.ModelAccess` (ordinal order). This is the only existing-test edit (FR-011, approved at T001).
- [X] T009 Add stub types under `src/AgenticChangeOps.Drafting/`, one per file, exactly as data-model.md: `IDraftingAgent<TRecord,TBasis,TGenerated,TContent,TRejection>` (members as listed), `ReviewOutcome<TContent,TRejection>` (`Valid`, `Invalid(errors)`, `Rejected(reason)`), `DraftingOutcome<TRecord,TRejection>` (success; failure with errors and optional `GenerationFailureCategory`; rejected), `NoRejection` (one-value enum for agents without a rejection) and `DraftingEngine<...>` with `RunAsync`, `GetLatest`, `GetHistory` throwing `NotImplementedException`. Namespace `AgenticChangeOps.Drafting`. Names no agent.
- [X] T010 Create `tests/AgenticChangeOps.Drafting.Tests/AgenticChangeOps.Drafting.Tests.csproj` (same packages and layout as `tests/AgenticChangeOps.ModelAccess.Tests`, references Drafting, Intake, ModelAccess) and add it to the solution. Add `Fakes/`: `StandInAgent` (a throwaway `IDraftingAgent` with its own record, basis, content and rejection types, scripted for each branch, counting calls), `StandInRecord`, and `EngineTestFixture` (in-memory store, fixed clock, sequential ids, one eligible change request).
- [X] T011 Run `dotnet build AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln`: build succeeds, every existing test is green with the T002 counts plus the T007 characterization tests. Record the result in `baseline-tests.txt`.

---

## Phase 3: User Story 1 - One place for the drafting steps (Priority: P1)

**Goal**: The sequence in contracts/engine-contract.md and the latest and history reads exist once, in `Drafting`, and all three services use them.

**Independent Test**: Search the three agent folders for the generator-failure text, the lock around version assignment and the latest/history bodies: none remain; `Drafting` holds exactly one of each; full suite green with assertions unchanged.

### Tests for US1 (written first, shown red at T021)

- [X] T012 [P] [US1] Add `tests/AgenticChangeOps.Drafting.Tests/SequenceTests.cs` against `StandInAgent`: steps run in the order lookup, eligibility, generate, review, then locked re-check, currency check, version, save (assert via a call log); an unknown id or ineligible request returns the agent's error with no category and the generator is never called.
- [X] T013 [P] [US1] Add `Drafting.Tests/GeneratorFailureTests.cs`: a `DraftGenerationException` with each `GenerationFailureCategory` becomes the single `Generator` error `The content generator failed (<Category>).` carrying that category; any other exception becomes `The content generator failed (<ExceptionTypeName>).` with no category; the exception message, inner exception and any text it holds appear nowhere in the outcome (sentinel string test); with `PropagatesCancellation` true and the caller's token cancelled the exception is rethrown; with it false the same exception becomes the type-name failure; with it true but the token **not** cancelled (an unrelated `OperationCanceledException`) it becomes the type-name failure.
- [X] T014 [P] [US1] Add `Drafting.Tests/ReviewTests.cs`: `Invalid(errors)` returns failure with those errors and `MalformedOutput`; `Rejected(reason)` returns a rejected outcome carrying exactly that reason and no stored record; a null generated content is passed to the agent's `Review` unchanged (the engine does not interpret it); nothing is stored for either.
- [X] T015 [P] [US1] Add `Drafting.Tests/LockedStepTests.cs`: the eligibility re-check runs again inside the lock and a request that became ineligible after generation returns its error and stores nothing; `CheckStillCurrent` is called with the basis used for generation and the basis found inside the lock, and its error returns failure and stores nothing; the clock is read once and its value is passed to the save delegate; version is `CountExisting + 1` and increases by one per success; the save delegate runs while `SyncRoot` is held by the calling thread and nothing is awaited between the re-check and the save (assert by holding the lock from the delegate and checking a second thread cannot enter).
- [X] T016 [P] [US1] Add `Drafting.Tests/OverlapAndAtomicityTests.cs`: 32 overlapping `RunAsync` calls for one request produce 32 distinct consecutive versions with no duplicates and no gaps; a save delegate that throws leaves the collection and the audit trail exactly as before and the exception reaches the caller; the record and its main audit entry written by the stand-in's save are both present or both absent.
- [X] T017 [P] [US1] Add `Drafting.Tests/ReadTests.cs`: `GetLatest` and `GetHistory` return the not-found error `No change request with id <id> exists.` on field `ChangeRequestId` for an unknown id; `GetLatest` returns null success when nothing is stored and the last stored item otherwise; `GetHistory` returns all items in version order; both read under `SyncRoot` (a second thread holding the lock blocks the read until released).
- [X] T018 [P] [US1] Diff the `ToolCallEntry` and `MaxAuditTargetLength` code in `DesignDraftingService.cs` and `ProposalService.cs` (research R10). Write the result in `contracts/kept-separate-register.md` entry N2. If the two are identical apart from the agent name constant, add `Drafting.Tests/ToolCallAuditTests.cs` for a shared `ToolCallAuditEntries.Create(agentName, ...)` helper: control characters removed from the target, target cut to 120, `OccurredAt` never later than the output's generation time, elapsed clamped to 0 to `int.MaxValue` milliseconds, outcome text, agent name as given. Test against exactly the signature in data-model.md. If they differ, record the difference, write no test, and skip T022's helper part.
- [X] T019 [US1] Add the stub helper from T018 (only when shared) to `src/AgenticChangeOps.Drafting/ToolCallAuditEntries.cs`, throwing `NotImplementedException`, with exactly the signature in data-model.md.
- [X] T020 [US1] Run `dotnet build` then `dotnet test tests/AgenticChangeOps.Drafting.Tests`: the build succeeds and every test in T012 to T018 **fails** because the stubs throw (not because of a compile error). Save the output to `specs/017-shared-drafting-skeleton/red-run.txt`.
- [X] T021 **GATE ([USER])**: show (a) the characterization tests added at T007, with the list of gaps they close and their green run on the old code, and (b) the red skeleton tests with the red run. Wait for the explicit word "approved". No engine behaviour is implemented, and no service is converted, before this.

### Implementation for US1

- [X] T022 [US1] Implement `DraftingEngine.RunAsync` in `src/AgenticChangeOps.Drafting/DraftingEngine.cs` exactly as contracts/engine-contract.md, copying the shared behaviour from the three services unchanged (including the comment that no await may appear in the locked step), `GetLatest` and `GetHistory` with the `NotFound` error text, and the shared tool-call helper if T018 found it identical. Add `GeneratorFailure` translation as a private part of the engine. Run `dotnet test tests/AgenticChangeOps.Drafting.Tests`: green.
- [X] T023 [US1] Convert `src/AgenticChangeOps.Agents.Requirement/RequirementsDraftingService.cs`: keep the public type, constructor, `DraftAsync`, `GetLatestDraft`, `GetDraftHistory` signatures; add its `IDraftingAgent` implementation (eligibility including `IsDraftable` and `HasDraft`, no currency check, `PropagatesCancellation = false` with a comment that this differs from the Developer on purpose, validator, collection reads); pass a per-call save delegate that closes over `automatic` and builds `RequirementsDraft` and the `DraftGenerated` entry; map the engine outcome to `DraftingResult`. Run `dotnet test AgenticChangeOps.sln`: green with assertions unchanged.
- [X] T024 [US1] Convert `src/AgenticChangeOps.Agents.Architect/DesignDraftingService.cs` the same way (eligibility and requirements basis, currency check comparing the basis version with the same message, `PropagatesCancellation = false`, tool-call entries through the shared helper if T018 shared it, `DesignGenerated` entry, `automatic` marker, map to `DesignDraftingResult`). Run `dotnet test AgenticChangeOps.sln`: green with assertions unchanged.
- [X] T025 [US1] Convert `src/AgenticChangeOps.Agents.Developer/ProposalService.cs` (eligibility with its own order and messages, `OutdatedDesign` currency check on both versions, `PropagatesCancellation = true` with a comment saying so, review step that runs the validator then the `ProposalVerifier` and maps to `Invalid` or `Rejected(ProposalRejection)`, no automatic marker, `ProposalGenerated` entry, map to `ProposalResult`). Keep the verifier call inside `Review` so the quote check stays Developer-owned. Run `dotnet test AgenticChangeOps.sln`: green with assertions unchanged.
- [X] T026 [US1] Delete from the three services every line now provided by the engine (generator-failure `catch` blocks, query bodies, lock and version code) and check that no agent folder contains `The content generator failed (` or the version-assignment lock (rule D6). Run `dotnet build` and the full suite: green.

**Checkpoint**: one implementation of the sequence and reads, three thin services, all tests green.

---

## Phase 4: User Story 2 - Callers and users see no difference (Priority: P1)

**Goal**: Same status codes, bodies, headers and error codes; no leak; overlap and atomicity still proven.

**Independent Test**: The full suite and the HTTP baseline comparison.

- [X] T027 [US2] Re-run `DraftingSkeletonBaselineTests` (added at T005a, unchanged) against the final code: green. The recordings were taken on the old code, so this proves the refactor. Record the result in `verification.md`.
- [X] T028 [P] [US2] Run the existing leak and secret scans (ModelAccess, Architect, Developer, Api structure tests) and confirm green; add the Drafting source folder to the secret-and-endpoint scan if it is driven by a folder list, otherwise confirm the scan covers `src/**` already. Record the result in `verification.md`.
- [X] T029 [P] [US2] Confirm that the existing overlap and atomicity tests of each agent (found in T006) still pass unchanged, and record their names in `verification.md`.
- [X] T030 [US2] Verify, and record in `verification.md`: `git diff HEAD -- tests` shows only new files and the three one-line expected-list edits; no existing golden or `openapi.json` differs; `git diff HEAD -- '*.csproj' '*.sln'` shows only the new projects and the three references; the full suite has 0 failed and the same skipped live checks as T002.

---

## Phase 5: User Story 3 - A fourth agent adds only itself (Priority: P2)

**Goal**: The skeleton is agent-neutral and a new agent needs no edit elsewhere.

**Independent Test**: The stand-in agent and the neutrality scan.

- [X] T031 [P] [US3] Add `tests/AgenticChangeOps.Drafting.Tests/StructureTests.cs` for rules D1 to D3 and D6 and D8 in contracts/dependency-rules.md: no agent, record, approval or automatic-advancement names in `src/AgenticChangeOps.Drafting`; its project file references only Intake and ModelAccess and no package; no file under any `src/AgenticChangeOps.Agents.*` folder (found by pattern, so a fourth agent is covered with no edit) contains `The content generator failed (` or a lock on the store around version assignment; ModelAccess still has no project reference.
- [X] T032 [P] [US3] Add `Drafting.Tests/StandInFourthAgentTests.cs`: the stand-in (T010) with its own types, eligibility, currency check, review and save runs success, each failure and an overlap through `DraftingEngine` with no edit to any file in Drafting or any agent (the test project references Drafting, Intake and ModelAccess only).
- [X] T033 [US3] Write the "Fourth agent walk-through" in `duplication.md`: the three things a new agent adds (an `IDraftingAgent`, a service, a save delegate) and the project references it needs; name `StandInFourthAgentTests` and `StructureTests` as the executable proof.

---

## Phase 6: User Story 4 - Register and duplication figures (Priority: P2)

**Goal**: Everything deliberately not shared is recorded with a reason; the before and after figures are reported.

**Independent Test**: Every earlier entry has a final status; both figures present.

- [X] T034 [US4] Complete `contracts/kept-separate-register.md`: compare each pair line by line on the final code and set a final status and reason for K1 to K15 (resolved by this slice, partly resolved, or kept with an updated reason) and for N1 to N4 (N2 as decided at T018); add any further similar-but-different pair found (for example the three `NotFound` helpers if any remains, the result types, the store-extension methods). Each entry names the files.
- [X] T035 [US4] Run the T003 script on the final code and write the "After" figures to `duplication.md` next to "Before": per-agent lines, pairwise shared, all-three shared, new neutral lines in `Drafting`, total production lines, with the difference and percentage change. The after figure must be lower than before (SC-003); explain the remainder by pointing to register entries. Also report test-code figures separately with `-Scope Tests`, unchanged except for new files.
- [X] T036 [US4] Write `specs/017-shared-drafting-skeleton/readability.md`: for each of the three services, line counts before and after and a plain statement of whether it reads better, the same or worse (five type arguments, the interface members, the save delegate). Any "worse" is recorded as a cost with a proposed remedy, and the user decides at T040.

---

## Phase 7: User Story 5 - Each shared behaviour proven once (Priority: P3)

**Goal**: One skeleton test per behaviour.

**Independent Test**: The mapping table.

- [X] T037 [US5] Add to `duplication.md` a table mapping every behaviour in contracts/engine-contract.md (order, ineligible, category failure, type-name failure, no leak, cancellation both ways, invalid, rejected, re-check, currency, clock once, version, overlap, atomic save, reads, not found, tool-call helper) to exactly one test in `Drafting.Tests`. Fix any behaviour with zero or two tests. List, as slice 009 did, the existing agent tests that now prove a behaviour a second time; they are kept as they are.

---

## Phase 8: Polish and wrap-up

- [X] T038 Run `dotnet build AgenticChangeOps.sln -c Release` and `dotnet test AgenticChangeOps.sln`; record final totals in `baseline-tests.txt` ("After") and `verification.md`.
- [X] T039 Update `README.md` only if it lists projects or the agent structure: add `AgenticChangeOps.Drafting` and one sentence. Add the 017 slice to the project memory index after merge, not before.
- [ ] T040 **GATE ([USER])**: show `readability.md`, `duplication.md` and `kept-separate-register.md`; the user decides on any recorded readability cost. Then commit with the slice name and a Co-Authored-By trailer, push `feature/shared-drafting-skeleton`, and open the PR (draft the description from `verification.md`; no live model check is needed because no model code changes).

---

## Dependencies

- T001 gates everything. Phase 1 (T002 to T007) before Phase 2; T007 (green characterization tests on old code) before any source change.
- Phase 2 before US1. In US1: T012 to T018 in parallel, T019, T020, T021 (approval), then T022, then T023 to T025 in order (one service at a time, suite green between), then T026.
- US2 needs US1 complete. US3 can start after T022 (tests T031, T032 are written against the finished engine and run green). US4 needs US1 and US2. US5 needs US1.
- Polish after all stories.

## Parallel examples

- After T011: T012, T013, T014, T015, T016, T017, T018 are different new files.
- After T026: T028, T029, T031, T032 touch different files.

## Phase 9: Convergence

- [X] T041 Resolve how FR-005 applies to `src/AgenticChangeOps.Drafting/ToolCallAuditEntries.cs`, which builds a stored audit record and is used by two of the three agents (contradicts, MEDIUM). Ask the user to choose: (a) keep it shared and record that the change request and audit entry are Intake-level shapes every agent already stores, by adding that reading to FR-005 in `spec.md`, to rule D2 in `contracts/dependency-rules.md` and to register entry N2; or (b) move `ToolCallEntry` back into the Architect and Developer services and delete `ToolCallAuditEntries.cs` and `ToolCallAuditTests.cs`. Per FR-005 (contradicts)
- [X] T042 [P] Rename `AgentProject_ReferencesOnlyIntakeAndModelAccess` to `AgentProject_ReferencesOnlyIntakeModelAccessAndDrafting` in `tests/AgenticChangeOps.Agents.Requirement.Tests/StructureTests.cs`, `tests/AgenticChangeOps.Agents.Architect.Tests/StructureTests.cs` and `tests/AgenticChangeOps.Agents.Developer.Tests/DeveloperStructureTests.cs`, changing no assertion, and record the rename in `verification.md` per FR-011 (partial)
- [X] T043 [P] Reconcile `plan.md` Project Structure with the built code (remove `GeneratorFailure.cs`; add `NoBasis.cs`, `NoRejection.cs`, `SaveStep.cs`, `DraftingErrors.cs`, `ToolCallAuditEntries.cs`, the `Drafting.Tests` files and the characterization tests), and add `NoBasis` and `DraftingErrors` to `data-model.md`, per plan: Project Structure (partial)

## Implementation strategy

MVP is US1 plus US2 (the engine, three converted services, and proof of no difference). US3 to US5 then add the evidence: the fourth-agent proof, the register and figures, and the one-test-per-behaviour map. Stop at the T021 gate for approval of the red tests and at T040 for the readability decision.
