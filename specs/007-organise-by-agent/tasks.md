# Tasks: Organise Agent Code by Agent

**Input**: Design documents from `specs/007-organise-by-agent/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. This is a behaviour-preserving move, so the "tests" are (a) a characterization baseline captured on pre-move code, (b) tests for the one new generic store point, and (c) structure tests for the dependency rules. Constitution Principle VI and the project's test-approval checkpoint apply: they are written first, shown, and approved before any code moves (T010).

**Constitution**: [x] Reviewed against the current constitution (v1.0.2, which carries the Principle VII amendment merged in PR #12). See plan.md Constitution Check.

**Organization**: Grouped by user story. Phases run in dependency order, not priority order: US2 (neutral project) first because the agent project depends on it, then US1, then US3, US4, US5. Priorities are P1 for US1, US2, US5 and P2 for US3, US4.

**Rules that hold for every task**:
- Use `git mv` so history follows each file. Change only namespaces, `using` directives and project references in moved files.
- No assertion in an existing test is edited. If a test fails after a move, fix the reference, never the assertion.
- No type is renamed or removed; no setting, package or capability is added.
- After every task that touches code, `dotnet build AgenticChangeOps.sln` must succeed; after every phase, `dotnet test AgenticChangeOps.sln` must be green.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task). Tasks that edit the same file are never `[P]`.
- **[Story]**: US1 to US5, from spec.md.
- **[USER]**: outward-facing step (push, PR). Claude drafts, the user confirms.

---

## Phase 0: Gate

- [X] T000 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md are approved, and that the Principle VII amendment (analysis finding C1) is merged or an explicit alternative is chosen. No test or source file is written before this (Principle V).
  - Recorded 2026-10-13: user replied "approved" to spec, plan and tasks. The Principle VII amendment (v1.0.2) is drafted but NOT merged; it must merge before this slice's PR does.

## Phase 1: Setup

- [X] T001 Confirm branch `feature/organise-by-agent` and a clean tree. Run `dotnet test AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln --list-tests`; save the totals per project and the full sorted list of test names (class + method, namespace stripped) to `specs/007-organise-by-agent/baseline-tests.txt`. This is the SC-001 reference.

---

## Phase 2: Foundational (tests first, blocks everything)

**Purpose**: Capture proof of current behaviour and write the new tests red, then stop for approval.

- [X] T002 [P] Add `tests/AgenticChangeOps.Api.Tests/Baseline/ApiBaselineTests.cs` and `tests/AgenticChangeOps.Api.Tests/Baseline/BaselineNormaliser.cs`. Using the existing `ApiFactory`, `FakeDraftGenerator` and `SyntheticData`, drive all eight operations from [contracts/http-api-unchanged.md](contracts/http-api-unchanged.md) over success and each error path already covered by existing endpoint tests (drafting not configured, each generator failure category, malformed output, validation failure, unknown id, wrong status), plus `GET /openapi/v1.json`. For each response record status code, `Content-Type`, body and the error code. The normaliser replaces generated ids and timestamps with stable tokens and changes nothing else. Compare against golden files.
- [X] T003 Generate the goldens once, on unmodified pre-move code, into `tests/AgenticChangeOps.Api.Tests/Baseline/golden/*.json`. Review every file: synthetic data only, no endpoint, deployment name or secret (Principle I). Add them to the csproj as copied content if needed.
- [X] T004 [P] Add `tests/AgenticChangeOps.Intake.Tests/Unit/AgentOutputStoreTests.cs` covering the generic store point with a local synthetic `IAgentOutput` record (no agent type): saves output and audit entry together, rejects mismatched change request ids and stores neither, rejects a duplicate version for the same type and stores neither, returns ascending version order, returns an empty list for an unknown id, keeps two change requests separate, and keeps two output types for one change request separate. Must fail to compile now (type does not exist yet). Keep it out of the build until T011: add `<Compile Remove="Unit\AgentOutputStoreTests.cs" />` to `AgenticChangeOps.Intake.Tests.csproj` and show the red state as the compile error obtained by temporarily removing that line. T011 deletes the `Compile Remove` line.
- [X] T005 Create test projects `tests/AgenticChangeOps.ModelAccess.Tests` and `tests/AgenticChangeOps.Agents.Requirement.Tests` (net8.0, same packages and `Using Include="Xunit"` as `AgenticChangeOps.Intake.Tests`, `IsTestProject`), add both to `AgenticChangeOps.sln` under the `tests` folder. No project references yet, except that `ModelAccess.Tests` gets a linked include of `..\AgenticChangeOps.Intake.Tests\Fakes\FakeEnvironment.cs` (namespace `AgenticChangeOps.Intake.Tests.Fakes`; it holds no intake type) and, once T012 creates the project, a `ProjectReference` to `src/AgenticChangeOps.ModelAccess`.
- [X] T006 [P] Add `tests/AgenticChangeOps.Intake.Tests/Unit/StructureTests.cs` for rules D1 and D2 in [contracts/dependency-rules.md](contracts/dependency-rules.md): the Intake `.csproj` has no `ProjectReference`; no file under `src/AgenticChangeOps.Intake` mentions `RequirementsDraft`, `RequirementAgent` or `AgenticChangeOps.Agents`. Walk up to `AgenticChangeOps.sln` for the root, as `NoSecretsTests` does, and assert the scanned folder exists and yields files so the test cannot pass vacuously.
- [X] T007 [P] Add `tests/AgenticChangeOps.ModelAccess.Tests/StructureTests.cs` for D3 and D4: `src/AgenticChangeOps.ModelAccess` exists, has no `ProjectReference` to Intake or an agent project, and no file mentions `Requirement`, `RequirementAgent`, `RequirementsDraft`, `DraftPromptBuilder` or `AgenticChangeOps.Intake`.
- [X] T008 [P] Add `tests/AgenticChangeOps.Agents.Requirement.Tests/StructureTests.cs` for D5 and D6: each of the eleven agent types in [quickstart.md](quickstart.md) step 3 is declared by exactly one file and that file is under `src/AgenticChangeOps.Agents.Requirement`; the agent `.csproj` references only Intake and ModelAccess; no other `src` project declares these types.
- [X] T009 Run `dotnet test AgenticChangeOps.sln` (T004 is excluded from compilation, so the intake test project still builds) and record: baseline tests **green** on current code; structure tests **red** for the right reason (folders and types not where expected); `AgentOutputStoreTests` red (no `IAgentOutput`).
- [X] T010 **CHECKPOINT (human approval)**: show the user T002 to T009 results (baseline green, new tests red, failure reasons). Do not proceed to Phase 3 until the user replies "approved".

---

## Phase 3: User Story 2 - Reuse model access without touching any agent (Priority: P1)

**Goal**: Agent-neutral `AgenticChangeOps.ModelAccess` exists and builds alone.

**Independent Test**: `dotnet test tests/AgenticChangeOps.ModelAccess.Tests` passes D3 and D4; `dotnet build src/AgenticChangeOps.ModelAccess` succeeds with no project references.

- [X] T011 [US2] Add intake generic point (additive, old members kept for now): in `src/AgenticChangeOps.Intake/Models/IAgentOutput.cs` add `IAgentOutput { Guid ChangeRequestId { get; } int Version { get; } }`; in `src/AgenticChangeOps.Intake/Stores/IChangeRequestStore.cs` and `InMemoryChangeRequestStore.cs` add `SaveAgentOutput<T>(T output, ChangeRequestAuditEntry auditEntry) where T : IAgentOutput` and `IReadOnlyList<T> GetAgentOutputs<T>(Guid changeRequestId) where T : IAgentOutput`. Rules, verbatim from [data-model.md](data-model.md): mismatched change request ids are rejected and nothing is stored; a version already stored for the same change request and output type is rejected and nothing is stored; output and audit entry are stored together or neither; reads are in ascending version order; an unknown change request returns an empty list; all under `SyncRoot`. Make T004 compile and pass.
- [X] T012 [US2] Create `src/AgenticChangeOps.ModelAccess/AgenticChangeOps.ModelAccess.csproj` (net8.0, ImplicitUsings, Nullable, `Azure.AI.OpenAI` 2.1.0, `Azure.Identity` 1.21.0, no project references). Add to `AgenticChangeOps.sln` under `src`.
- [X] T013 [US2] `git mv` into `src/AgenticChangeOps.ModelAccess/` and set namespace `AgenticChangeOps.ModelAccess`: `IChatModelClient.cs` (with `ChatModelResponse`), `AzureOpenAIChatModelClient.cs`, `AzureOpenAIGeneratorOptions.cs`, `GeneratorConfigurationException.cs`, `ModelFailureMapper.cs` from `src/AgenticChangeOps.Intake.AzureOpenAI/`; `DraftGenerationException.cs` from `src/AgenticChangeOps.Intake/Models/`; `GenerationFailureCategory.cs` from `src/AgenticChangeOps.Intake/Models/Enums/`. Remove the now-unused `AgenticChangeOps.Intake.*` usings from these files. Names unchanged.
- [X] T014 [US2] Temporary wiring so the solution stays green: add `ProjectReference` to ModelAccess in `AgenticChangeOps.Intake.csproj` (drafting still lives there until US1), in `AgenticChangeOps.Intake.AzureOpenAI.csproj`, and in the test and Api projects that use these types. Fix `using` directives in `src`, `tests/AgenticChangeOps.Intake.Tests` and `tests/AgenticChangeOps.Api.Tests`. The Intake reference is removed in T028.
- [X] T015 [P] [US2] Move neutral tests to `tests/AgenticChangeOps.ModelAccess.Tests/`, assertions untouched: from `ModelFailureMapperTests.cs` the methods that call only `ModelFailureMapper` (`RequestTimeoutStatus_IsTimedOut`, `AuthThrottleAndServiceSideStatuses_AreUnreachable`, `ContentFilterBlock_IsMalformedOutput`, `TimeoutException_IsTimedOut`, `NetworkAndOtherExceptions_AreUnreachable`); from `GeneratorConfigurationTests.cs` the methods that call only `AzureOpenAIGeneratorOptions.FromEnvironment` (settings validation, defaults, trimming, messages never containing the value). Methods that construct `AzureOpenAIDraftGenerator` (`IdentityFailures_...`, `GeneratorCreation_RejectsMissingOptionsOrClient`, the live-folder scan) stay for T023. Uses the linked `FakeEnvironment` set up in T005.
- [X] T016 [US2] Move `tests/AgenticChangeOps.Intake.Tests/Unit/NoSecretsTests.cs` to `tests/AgenticChangeOps.ModelAccess.Tests/NoSecretsTests.cs`. Re-point the folder constant in `GeneratorProject_DefinesNoSettingNameThatCouldHoldASecret` from `src/AgenticChangeOps.Intake.AzureOpenAI` to `src/AgenticChangeOps.ModelAccess`,. A second scan of `src/AgenticChangeOps.Agents.Requirement` with the same rule is added in T026, once that folder exists. Add a guard asserting the scanned folder exists and contains at least one `.cs` file (research R6). Update the excluded-self path to the new location. No existing assertion changes.
- [X] T017 [US2] Run `dotnet test AgenticChangeOps.sln`. Expect: all baseline tests green, `ApiBaselineTests` green against unchanged goldens, ModelAccess structure tests D3 and D4 green.

**Checkpoint**: ModelAccess stands alone.

---

## Phase 4: User Story 1 - Everything about the Requirement agent in one place (Priority: P1)

**Goal**: `AgenticChangeOps.Agents.Requirement` holds every agent-specific type; the old `Intake.AzureOpenAI` project is gone.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Agents.Requirement.Tests` passes D5 and D6 plus all moved tests.

- [X] T018 [US1] Create `src/AgenticChangeOps.Agents.Requirement/AgenticChangeOps.Agents.Requirement.csproj` (net8.0, ImplicitUsings, Nullable) referencing `AgenticChangeOps.Intake` and `AgenticChangeOps.ModelAccess`. Add to the solution under `src`.
- [X] T019 [US1] `git mv` into `src/AgenticChangeOps.Agents.Requirement/`, namespace `AgenticChangeOps.Agents.Requirement`, names unchanged. From Intake: `Models/RequirementsDraft.cs`, `Models/DraftContent.cs`, `Models/DraftingResult.cs`, `Models/Enums/DraftStatus.cs`, `Services/DraftContentValidator.cs`, `Services/IRequirementsDraftGenerator.cs`, `Services/IRequirementsDraftingService.cs`, `Services/RequirementsDraftingService.cs`. From `Intake.AzureOpenAI`: `AzureOpenAIDraftGenerator.cs`, `DraftPromptBuilder.cs`, `DraftResponseParser.cs`.
- [X] T020 [US1] Add `src/AgenticChangeOps.Agents.Requirement/RequirementDraftStoreExtensions.cs` with `SaveDraft(this IChangeRequestStore, RequirementsDraft, ChangeRequestAuditEntry)` and `GetDrafts(this IChangeRequestStore, Guid)` delegating to `SaveAgentOutput<RequirementsDraft>` and `GetAgentOutputs<RequirementsDraft>`. Make `RequirementsDraft` implement `IAgentOutput` (no member, order or serialized change). Remove `SaveDraft`, `GetDrafts` and the `_drafts` field from `IChangeRequestStore` and `InMemoryChangeRequestStore`.
- [X] T021 [US1] Delete the empty `src/AgenticChangeOps.Intake.AzureOpenAI` project and remove it from `AgenticChangeOps.sln` and every `ProjectReference`. Confirm no file remains in that folder before deleting it.
- [X] T022 [US1] Update `src/AgenticChangeOps.Api`: `AgenticChangeOps.Api.csproj` references Intake, ModelAccess and Agents.Requirement; fix `using` directives in `Composition/ApiServiceRegistration.cs`, `Endpoints/DraftEndpoints.cs`, `Errors/ErrorMapper.cs` (and any other file the compiler flags). Registration order and lifetimes unchanged.
- [X] T023 [US1] Move agent tests to `tests/AgenticChangeOps.Agents.Requirement.Tests/` (namespace `AgenticChangeOps.Agents.Requirement.Tests`, assertions untouched): `DraftContentValidationTests`, `DraftEligibilityTests`, `DraftGenerationFailureTests`, `DraftGeneratorFailureTests`, `DraftingFailureCategoryTests`, `DraftingTests`, `DraftIsolationTests`, `DraftPromptTests`, `DraftResponseParserTests`, `DraftRetrievalTests`, `GeneratorDraftingIntegrationTests`, `StoreConcurrencyTests`, the remaining methods of `ModelFailureMapperTests` and `GeneratorConfigurationTests` (T015), the `Live/` folder, and fakes `DraftingTestFixture`, `FakeDraftGenerator`, `FakeChatModelClient`. From `StoreTests.cs` move the `MakeDraft`/`MakeDraftEntry` helpers and the nine `SaveDraft_*` / `GetDrafts_*` methods into a new `DraftStoreTests.cs`; the intake-store methods and the `GuidIdGenerator` / `SystemClock` tests stay in `StoreTests.cs`.
- [X] T024 [US1] Share small fakes without duplicating or referencing the intake test project: in the agent tests csproj, link `..\AgenticChangeOps.Intake.Tests\Fakes\ApprovalTestFixture.cs` (contains `CountingIdGenerator`), `FakeSystemClock.cs`, `FakeIdGenerator.cs`, `FakeEnvironment.cs` (plus any further fake the compiler flags as missing), keeping namespace `AgenticChangeOps.Intake.Tests.Fakes`. Add project references: Agents.Requirement, Intake, ModelAccess. Add the same linked `FakeEnvironment.cs` and `FakeChatModelClient.cs` as needed to ModelAccess.Tests.
- [X] T025 [US1] Add `tests/AgenticChangeOps.Api.Tests` references for the three projects and fix `using` directives (`Fakes/ApiFactory.cs`, `Fakes/FakeDraftGenerator.cs`, `DescriptionTests.cs`, `DraftEndpointTests.cs`, `DraftFailureTests.cs`, `NotConfiguredTests.cs` and others the compiler flags). Update the comment in `NoSecretsTests.cs` that names the intake test project's scans.
- [X] T026 [US1] Re-point `OutsideTheLiveFolder_NoTestCreatesTheRealClientOrReadsTheProcessEnvironment` (now in `Agents.Requirement.Tests`): change only `testsRoot` (to the `tests` folder, so every test project is walked), `liveFolder` (to `tests/AgenticChangeOps.Agents.Requirement.Tests/Live/`) and `thisFile` (to its new path). The `forbidden` tokens and `Assert.Empty(offenders)` are unchanged. List this test as a named path-only exception in the PR (T034). Also add, in the ModelAccess `NoSecretsTests` file, the second scan of `src/AgenticChangeOps.Agents.Requirement` deferred from T016 (additive; same rule, same guard).
- [X] T027a [P] [US1] Add `tests/AgenticChangeOps.Agents.Requirement.Tests/MovedTypeShapeTests.cs`: assert `DraftStatus` and `GenerationFailureCategory` member names and values equal literal lists taken from the pre-move source, and that `RequirementsDraft` serializes with the baseline JSON property names (FR-012).
- [X] T027 [US1] Run `dotnet test AgenticChangeOps.sln`. Expect everything green, D5 and D6 green, goldens unchanged.

**Checkpoint**: One location holds the agent; `Intake.AzureOpenAI` no longer exists.

---

## Phase 5: User Story 3 - Intake and approval stand independent of agents (Priority: P2)

**Goal**: Intake and its tests build and pass with no agent or model-access project present.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Intake.Tests` with ModelAccess and Agents.Requirement not referenced.

- [X] T028 [US3] Remove the temporary `ProjectReference` to ModelAccess from `src/AgenticChangeOps.Intake/AgenticChangeOps.Intake.csproj`. Intake now has no project references.
- [X] T029 [US3] Remove any reference to Agents.Requirement or ModelAccess from `tests/AgenticChangeOps.Intake.Tests/AgenticChangeOps.Intake.Tests.csproj` (only `AgenticChangeOps.Intake` remains). Remove leftover agent fakes or usings if the build flags them.
- [X] T030 [US3] Verify: `dotnet build src/AgenticChangeOps.Intake` and `dotnet test tests/AgenticChangeOps.Intake.Tests` succeed; D1 and D2 green; `git grep -n "RequirementsDraft\|AgenticChangeOps.Agents\|AgenticChangeOps.ModelAccess" -- src/AgenticChangeOps.Intake tests/AgenticChangeOps.Intake.Tests` returns nothing.

---

## Phase 6: User Story 4 - Add a second agent by adding one location (Priority: P2)

**Goal**: The structure is demonstrably repeatable. No second agent is built.

**Independent Test**: The dry-run in T031 finds zero required edits to the first agent or ModelAccess.

- [X] T031 [US4] Dry-run in a scratch copy of the tree under the scratchpad (not committed): add an empty `AgenticChangeOps.Agents.Probe` project referencing Intake and ModelAccess, plus its test project, add to the solution, register one dummy service in the API composition root. Confirm the solution builds, D5 and D6 still pass, and `git status` shows no change under `src/AgenticChangeOps.Agents.Requirement` or `src/AgenticChangeOps.ModelAccess`. Record the file list and the result under "Walk-through result" at the end of `specs/007-organise-by-agent/quickstart.md`. Discard the scratch copy.

---

## Phase 7: User Story 5 - Callers see no difference (Priority: P1)

**Goal**: Prove SC-001 to SC-003 and SC-007.

**Independent Test**: Full suite green; goldens byte-identical; test-name list equals baseline plus only the new tests.

- [X] T032 [US5] Run `dotnet test AgenticChangeOps.sln -c Release`. `ApiBaselineTests` must pass against goldens with `git diff --stat tests/AgenticChangeOps.Api.Tests/Baseline/golden` empty. If any difference appears, stop, find the cause, and either remove it or list it in [contracts/http-api-unchanged.md](contracts/http-api-unchanged.md) with the reason; the slice is not complete until then.
- [X] T033 [US5] Compare `dotnet test --list-tests` (namespace stripped, as in T001) with `baseline-tests.txt`: zero tests missing; additions are exactly `ApiBaselineTests`, `AgentOutputStoreTests`, the three `StructureTests` files and the new guards. Total executed count = baseline total + additions, same skipped set (live checks skipped).
- [X] T034 [US5] Assertion integrity: run `git diff -M --stat main...HEAD -- tests` and review every moved test file with `git diff -M main...HEAD -- <file>`; the only differences allowed are namespace, `using`, project reference, `link` and the T015/T016/T023/T026 method moves. List any exception in the PR description. Include the secrets-test addition from T026.
- [X] T035 [P] [US5] Finalise [contracts/type-move-map.md](contracts/type-move-map.md) against the actual diff (every moved type present, no rename or removal) and the table in [contracts/http-api-unchanged.md](contracts/http-api-unchanged.md) (still "none", or the listed differences).
- [X] T036 [P] [US5] Confirm no new setting, package or capability: `git diff main...HEAD -- '*.csproj'` shows only project moves and reference changes; package names and versions identical; `git grep -n "AGENTICCHANGEOPS_"` shows only the existing four model variables, the live-check opt-in variable and `ASPNETCORE_URLS`.

---

## Phase 8: Polish and cross-cutting

- [X] T037 Update `README.md`: flow diagram and "Repository layout" to the new structure (Intake, ModelAccess, Agents.Requirement, Api, four test projects), slice table row `specs/007-organise-by-agent`, and a short "Adding an agent" section with the steps from [quickstart.md](quickstart.md) step 6 (FR-013, SC-008).
- [X] T038 [P] Update references to the old project name in docs under `specs/` only where they are living documentation for the current structure (e.g. the contract index in slice 005 if it names `Intake.AzureOpenAI`); leave historical slice specs untouched and say so in the PR description.
- [X] T039 [P] Confirm `.github/workflows/ci.yml` needs no change (it builds and tests `AgenticChangeOps.sln`; the executed-test-count guard still applies). If the solution file lists all four new projects, no edit is required.
- [X] T040 Run the full `quickstart.md` validation, steps 1 to 7, and tick each.
- [ ] T041 [USER] Commit in logical steps with messages naming the slice (`refactor(007-organise-by-agent): ...`), push `feature/organise-by-agent`, open a PR to `main` with a constitution compliance checklist item (v1.0.2) and the Complexity Tracking exceptions from plan.md quoted. Claude drafts; the user confirms before anything is pushed.

---

## Dependencies and Execution Order

- Phase 1 then Phase 2. **T010 is a hard stop.**
- Phase 3 (US2) before Phase 4 (US1): the agent project references ModelAccess. T011 (generic store point) precedes T020.
- Phase 5 (US3) after Phase 4: the temporary Intake to ModelAccess reference exists until T028.
- Phase 6 (US4) and Phase 7 (US5) after Phase 5. Phase 8 last.
- Within phases: T002, T004, T006, T007, T008 are independent files (parallel). T015 is parallel with T016 only after T013 and T014. T035 and T036 are parallel.

### Parallel example (Phase 2)

```text
T002 ApiBaselineTests        T004 AgentOutputStoreTests   T006 Intake StructureTests
T007 ModelAccess Structure   T008 Agent StructureTests
```

## Implementation Strategy

1. Lock behaviour first: T001 to T010 and wait for "approved".
2. Add before remove: introduce the generic store point (T011) while old members still exist, so each commit stays green.
3. Move bottom-up: ModelAccess (US2), then the agent (US1), then cut the last intake reference (US3).
4. Prove, don't assert: US5 compares goldens, test names and diffs mechanically.
5. MVP stop point: after Phase 4 plus T032 the structure is delivered and verified; Phases 5 to 8 complete the dependency rule, the repeatability proof and the docs.

## Phase 9: Convergence

- [X] T042 CRITICAL: Resolve the Principle VII conflict (the generic `IAgentOutput` point and the neutral `ModelAccess` project each have one consumer): merge the v1.0.2 amendment drafted in `.specify/memory/constitution.md` through its own `docs/` branch and PR, then re-check this slice's plan.md Constitution Check against v1.0.2, change its gate from PENDING to PASS, and tick the constitution review lines in tasks.md (header) and `checklists/requirements.md` (Governance) per Constitution VII (contradicts)
- [X] T043 Add the deferred second scan to `tests/AgenticChangeOps.ModelAccess.Tests/NoSecretsTests.cs`: apply the `GeneratorProject_DefinesNoSettingNameThatCouldHoldASecret` rule to `src/AgenticChangeOps.Agents.Requirement`, with the same guard that the folder exists and yields at least one `.cs` file, per T026 and plan R6 (partial)
- [X] T044 Document `AGENTICCHANGEOPS_UPDATE_BASELINE` in `research.md` R8 as a test-only switch that rewrites the goldens and is read nowhere in `src`, or remove it and require a manual golden refresh, per FR-011 (unrequested)
- [X] T045 Reword the comment at `src/AgenticChangeOps.ModelAccess/AzureOpenAIChatModelClient.cs` line 27 ("One attempt per draft request; the caller may simply ask again (FR-010)") to name no agent and no slice requirement, e.g. "One attempt per model request; the caller may simply ask again", per FR-003 (partial)
- [X] T046 Remove the leading blank line left in `src/AgenticChangeOps.ModelAccess/DraftGenerationException.cs` and `src/AgenticChangeOps.ModelAccess/ModelFailureMapper.cs` after the unused `using` lines were deleted, per T013 (partial)
