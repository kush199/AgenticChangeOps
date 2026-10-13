# Tasks: Architect Design Drafting for Approved Change Requests

**Input**: Design documents from `specs/008-architect-design-drafting/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). Tests are shown red and approved (T020) before any implementation. Red means failing for the right reason: Phase 2 adds signature-only stubs that throw `NotImplementedException`, so tests compile and fail on behaviour, not on missing types.

**Constitution**: [x] Reviewed against constitution v1.0.2. See plan.md Constitution Check and Complexity Tracking.

**Organization**: Grouped by user story (US1 to US6 from spec.md). Phases run in priority order; US2 to US5 build on the US1 service.

**Rules that hold for every task**:
- Edits outside `src/AgenticChangeOps.Agents.Architect`, `tests/AgenticChangeOps.Agents.Architect.Tests` and the API files named in plan.md are forbidden. In particular no edit to `src/AgenticChangeOps.Agents.Requirement`, intake services or models, or `src/AgenticChangeOps.ModelAccess`. The single permitted intake edit is appending `DesignGenerated` to `AuditEventType` (T003).
- No assertion in an existing test is edited. If an existing test fails, fix the new code, never the assertion. Existing goldens may only gain additions (T045).
- Synthetic data only; no endpoint, deployment name, key or real name in any file (Principle I).
- No new package reference in any project.
- After every task that touches code, `dotnet build AgenticChangeOps.sln` must succeed; after every phase, `dotnet test AgenticChangeOps.sln` must be green (except the deliberately red state recorded at T019).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task). Tasks editing the same file are never `[P]`.
- **[Story]**: US1 to US6, from spec.md.
- **[USER]**: outward-facing step (push, PR). Claude drafts, the user confirms.

---

## Phase 0: Gate

- [X] T000 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md are approved. No test or source file is written before this (Principle V).

## Phase 1: Setup

- [X] T001 Confirm branch `feature/architect-agent` and a clean tree. Run `dotnet test AgenticChangeOps.sln` and save the per-project totals to `specs/008-architect-design-drafting/baseline-tests.txt`. This is the SC-006 reference: every earlier test must still pass afterwards.
- [X] T002 Create `src/AgenticChangeOps.Agents.Architect/AgenticChangeOps.Agents.Architect.csproj` (net8.0, `ImplicitUsings` and `Nullable` enabled, exactly two `ProjectReference`s: `..\AgenticChangeOps.Intake\AgenticChangeOps.Intake.csproj` and `..\AgenticChangeOps.ModelAccess\AgenticChangeOps.ModelAccess.csproj`, same shape as the Requirement agent's csproj). Add it to `AgenticChangeOps.sln` under the `src` folder.
- [X] T003 Append `DesignGenerated` as the **last** member of `AuditEventType` in `src/AgenticChangeOps.Intake/Models/Enums/AuditEventType.cs` (after `DraftGenerated`; no other change in intake). Record nothing else here: the justification is in plan.md Complexity Tracking.
- [X] T004 Create `tests/AgenticChangeOps.Agents.Architect.Tests/AgenticChangeOps.Agents.Architect.Tests.csproj` (net8.0, same packages and `Using Include="Xunit"` as `tests/AgenticChangeOps.Agents.Requirement.Tests`; `ProjectReference`s to the Architect project, Intake, ModelAccess, and `tests/AgenticChangeOps.Intake.Tests` for `FakeSystemClock`/`CountingIdGenerator`, mirroring how the Requirement test project does it). Add it to `AgenticChangeOps.sln` under the `tests` folder.

---

## Phase 2: Foundational (stubs, tests first, blocks everything)

**Purpose**: Make the types exist as signatures only, write every test, show them red, and stop for approval.

- [X] T005 [P] Add stub type `DesignDraftStatus` in `src/AgenticChangeOps.Agents.Architect/DesignDraftStatus.cs`: `enum { Draft }`.
- [X] T006 [P] Add record `DesignDraft` in `src/AgenticChangeOps.Agents.Architect/DesignDraft.cs` implementing `IAgentOutput`, with members and constraints verbatim from [data-model.md](data-model.md): `DesignId` (Guid), `ChangeRequestId` (Guid), `Version` (int, "1-based, consecutive per change request and per output type"), `RequirementsVersion` (int), `GeneratedAt` (DateTime UTC), `Status` (`DesignDraftStatus`, "Always `Draft`"), `AgentName` (string), `ApproachSummary` (string), `AffectedComponents`, `ImpactAndRisks`, `Dependencies`, `RollbackConsiderations`, `OpenQuestions` (each `IReadOnlyList<string>`), plus `public const string ArchitectAgentName = "ArchitectAgent";`.
- [X] T007 [P] Add record `DesignContent` in `src/AgenticChangeOps.Agents.Architect/DesignContent.cs`: `ApproachSummary` (string?), and `AffectedComponents`, `ImpactAndRisks`, `Dependencies`, `RollbackConsiderations`, `OpenQuestions` (each `IReadOnlyList<string>?`; "a null member means the section was absent; an empty list means present but empty").
- [X] T008 [P] Add `RequirementsBasis` record and `IRequirementsBasisSource` interface (`RequirementsBasis? GetLatest(Guid changeRequestId)`) in `src/AgenticChangeOps.Agents.Architect/RequirementsBasis.cs`; members `Version`, `Summary`, `FunctionalRequirements`, `AcceptanceCriteria`, `Assumptions`, `OpenQuestions`.
- [X] T009 [P] Add `IDesignDraftGenerator` (`Task<DesignContent?> GenerateAsync(ChangeRequest changeRequest, RequirementsBasis basis, CancellationToken cancellationToken)`; doc comment: generates content only, never changes status, calls other services or takes any decision) in `src/AgenticChangeOps.Agents.Architect/IDesignDraftGenerator.cs`.
- [X] T010 [P] Add `DesignDraftingResult` in `src/AgenticChangeOps.Agents.Architect/DesignDraftingResult.cs` (`IsSuccess`, `Draft`, `Errors` of `ValidationError`, `FailureCategory` of `GenerationFailureCategory?` set only for generation failures, `Success`/`Failure` factories) and `IDesignDraftingService` in `src/AgenticChangeOps.Agents.Architect/IDesignDraftingService.cs` with `Task<DesignDraftingResult> DesignAsync(Guid, CancellationToken = default)`, `QueryResult<DesignDraft?> GetLatestDesign(Guid)`, `QueryResult<IReadOnlyList<DesignDraft>> GetDesignHistory(Guid)`.
- [X] T011 Add stubs whose bodies are `throw new NotImplementedException()` only: `DesignDraftingService` (constructor `(IChangeRequestStore, ISystemClock, IIdGenerator, IRequirementsBasisSource, IDesignDraftGenerator, DesignContentValidator)`), `DesignContentValidator` (`Validate(DesignContent?)` returning `(DesignContent? Normalised, IReadOnlyList<ValidationError> Errors)`), `DesignResponseParser.Parse(string)`, `DesignPromptBuilder.Build(ChangeRequest, RequirementsBasis)` returning `(string SystemInstruction, string UserContent)` plus public const `DataDelimiter`, `AzureOpenAIDesignGenerator` (constructor `(AzureOpenAIGeneratorOptions, IChatModelClient)`), and `DesignDraftStoreExtensions.SaveDesign/GetDesigns` over `SaveAgentOutput<T>`/`GetAgentOutputs<T>`, each in its own file under `src/AgenticChangeOps.Agents.Architect/` (files named after the types, listed in plan.md).
- [X] T012 Add test support in `tests/AgenticChangeOps.Agents.Architect.Tests/Fakes/`: `DesigningTestFixture.cs` (in-memory store, `FakeSystemClock`, `CountingIdGenerator`, intake and approval services, `FakeRequirementsBasisSource`, `FakeDesignGenerator`; helpers `SubmitSynthetic()`, `SeedStatus(id, status)`, `ApproveWithBasis()` that submits, sets a basis and approves), `FakeDesignGenerator.cs` (returns configured `DesignContent`, can throw a given exception, counts calls, can run a callback during generation), `FakeRequirementsBasisSource.cs` (settable per-id basis, call counting), `FakeChatModelClient.cs` (returns canned text or throws). All data synthetic.
- [X] T013 [P] [US1] Tests in `tests/AgenticChangeOps.Agents.Architect.Tests/Unit/DesigningTests.cs`: for an `Approved` request with a basis, success yields version 1, status `Draft`, `AgentName` `ArchitectAgent`, generation timestamp from the clock, `RequirementsVersion` equal to the basis version (basis at version 3 gives 3), all six sections populated; request status, approver and other fields identical before and after; exactly one new audit entry of type `DesignGenerated` after all earlier entries, with `DraftVersion` equal to the design version, `AgentName` `ArchitectAgent`, `StatusBefore` and `StatusAfter` both `Approved`; generator called once.
- [X] T014 [P] [US2] Tests in `tests/AgenticChangeOps.Agents.Architect.Tests/Unit/DesignVersioningTests.cs`: three requests yield versions 1, 2, 3 with versions 1 and 2 unchanged and one audit entry each; two change requests number independently; a new basis version between requests gives the second design the new `RequirementsVersion` while the first keeps its original.
- [X] T015 [P] [US3] Tests in `tests/AgenticChangeOps.Agents.Architect.Tests/Unit/DesignRetrievalTests.cs`: latest returns highest version; history is ascending and complete; existing request with no designs gives a null latest value and an empty history; unknown id gives a `ChangeRequestId` error for both; retrieval writes no audit entry.
- [X] T016 [P] [US4] Tests in `tests/AgenticChangeOps.Agents.Architect.Tests/Unit/DesignEligibilityTests.cs`: unknown id gives a `ChangeRequestId` error; each of `Submitted`, `UnderReview`, `Deferred`, `Rejected` gives a `Status` error naming the status; `Approved` with no basis gives a `RequirementsDraft` error; every refusal stores nothing, writes no audit entry and never calls the generator or (for status refusals) the basis source; a request that stops being `Approved` during generation is refused and consumes no version; a basis that changed during generation is refused as a `RequirementsDraft` error and stores nothing.
- [X] T017 [P] [US5] Tests in `tests/AgenticChangeOps.Agents.Architect.Tests/Unit/DesignGenerationFailureTests.cs`, `DesignContentValidationTests.cs`, `DesignResponseParserTests.cs`, `DesignPromptTests.cs`, `DesignHygieneTests.cs`: generator throwing each `GenerationFailureCategory` returns a failure carrying only that category and stores nothing; any other exception returns a failure naming only the exception type; null content, missing section, blank `ApproachSummary`, empty `AffectedComponents`, empty `ImpactAndRisks`, empty `RollbackConsiderations`, an item longer than 2000 characters and more than 50 items are each rejected with the failing section named and nothing stored; `Dependencies` and `OpenQuestions` may be empty but not absent; parser accepts the JSON object alone and inside one code fence, rejects everything else as `MalformedOutput` without echoing the text; prompt puts the change request fields and basis after the delimiter and the instruction names the six keys; a throwing generator whose message holds a fake endpoint and key leaks neither into the result, errors, or captured log output.
- [X] T018 [P] [US1] Tests in `tests/AgenticChangeOps.Agents.Architect.Tests/Unit/DesignGeneratorIntegrationTests.cs`, `DesignConcurrencyTests.cs`, `DesignIsolationTests.cs` and `StructureTests.cs`, plus `Live/LiveModelFactAttribute.cs` and `Live/LiveDesignCheckTests.cs`: `AzureOpenAIDesignGenerator` over `FakeChatModelClient` maps oversize and cut-off to `ResponseTooLarge`, blank to `MalformedOutput`, timeout to `TimedOut`, client exception to `Unreachable` with the message not carried; ten simultaneous requests yield distinct consecutive versions; status and approval unchanged after success and after each failure; structure rules A1, A2, A3 from [contracts/dependency-rules.md](contracts/dependency-rules.md) with non-vacuous folder checks (as the Requirement `StructureTests` does); live check skipped unless `AGENTICCHANGEOPS_LIVE_MODEL_CHECK` is `1`, asserts only that a synthetic approved request yields all six sections.
- [X] T019 Run `dotnet test tests/AgenticChangeOps.Agents.Architect.Tests` and record: build succeeds; every behaviour test fails with `NotImplementedException` or the assertion that is missing; `StructureTests` pass or fail only for the intended rule; live test reports skipped. Run `dotnet test AgenticChangeOps.sln --filter "FullyQualifiedName!~Architect"` and confirm all earlier tests are green.
- [ ] T020 **CHECKPOINT (human approval)**: show the user the T013 to T019 results (red tests, failure reasons, earlier tests green). Do not proceed to Phase 3 until the user replies "approved".

---

## Phase 3: User Story 1 - Draft a design for an Approved change request (Priority: P1) MVP

**Goal**: An approved change request with a requirements draft yields a stored, audited version 1 design draft; nothing else changes.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Agents.Architect.Tests --filter "FullyQualifiedName~DesigningTests|FullyQualifiedName~DesignGeneratorIntegrationTests"` passes.

- [X] T021 [P] [US1] Implement `DesignContentValidator` in `src/AgenticChangeOps.Agents.Architect/DesignContentValidator.cs`: trim text, drop blank list items; `ApproachSummary` required, non-blank, "at most 2000 characters"; `AffectedComponents`, `ImpactAndRisks`, `RollbackConsiderations` require at least one non-blank item; `Dependencies` and `OpenQuestions` required to be present, may be empty; each item "at most 2000 characters", each list "at most 50 items"; null content gives a `Generator` error; one error per failing section named by field.
- [X] T022 [P] [US1] Implement `DesignResponseParser` in `src/AgenticChangeOps.Agents.Architect/DesignResponseParser.cs`: JSON object alone or in one code fence (label empty or `json`); keys `approachSummary` (string) and `affectedComponents`, `impactAndRisks`, `dependencies`, `rollbackConsiderations`, `openQuestions` (arrays of strings); anything else throws `DraftGenerationException(MalformedOutput)` with the original exception dropped.
- [X] T023 [P] [US1] Implement `DesignPromptBuilder` in `src/AgenticChangeOps.Agents.Architect/DesignPromptBuilder.cs`: fixed delimiter line, system instruction treating all after it as data and naming the six response keys, user content is the delimiter plus a JSON object holding the change request's own fields and the basis (`version`, `summary`, `functionalRequirements`, `acceptanceCriteria`, `assumptions`, `openQuestions`).
- [X] T024 [US1] Implement `AzureOpenAIDesignGenerator` in `src/AgenticChangeOps.Agents.Architect/AzureOpenAIDesignGenerator.cs`: one `IChatModelClient.CompleteAsync` call, no retry, max output tokens derived from `MaxResponseCharacters` as the Requirement generator does, linked timeout token, `ResponseTooLarge` for cut-off or oversize, `MalformedOutput` for blank, original exceptions never attached, then `DesignResponseParser.Parse`. No reference to the Requirement agent's types.
- [X] T025 [P] [US1] Implement `DesignDraftStoreExtensions` in `src/AgenticChangeOps.Agents.Architect/DesignDraftStoreExtensions.cs` (`SaveDesign`, `GetDesigns` over the generic output point).
- [X] T026 [US1] Implement `DesignDraftingService.DesignAsync` in `src/AgenticChangeOps.Agents.Architect/DesignDraftingService.cs` following `RequirementsDraftingService`: read request and basis, eligibility (`Approved` and basis present), generate, validate, then under `store.SyncRoot` with no await re-check eligibility and that the basis version is unchanged, assign `Version = existing designs + 1`, build `DesignDraft` (`Status` `Draft`, `AgentName` `ArchitectAgent`, `RequirementsVersion` from the basis the model saw, `GeneratedAt` from the clock) and the `DesignGenerated` audit entry (`DraftVersion` = design version, `StatusBefore` = `StatusAfter` = current status, `Details` null), save both together. Messages are fixed strings; never include generator text.
- [X] T027 [US1] Run the US1 filter from the Independent Test; confirm `DesigningTests` and `DesignGeneratorIntegrationTests` green and `DesignContentValidationTests`, `DesignResponseParserTests`, `DesignPromptTests` now green.

**Checkpoint**: US1 works end to end without HTTP.

---

## Phase 4: User Story 2 - Redesign creates a new version (Priority: P2)

**Goal**: Repeated requests number consecutively and preserve earlier versions.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Agents.Architect.Tests --filter "FullyQualifiedName~DesignVersioningTests|FullyQualifiedName~DesignConcurrencyTests"` passes.

- [X] T028 [US2] Verify, and adjust only `DesignDraftingService.cs`, so that version assignment and save are one uninterrupted step under `store.SyncRoot`, a failed or refused request consumes no version, and the basis is re-read at store time (R2, R3 in [research.md](research.md)). Run the US2 filter.

---

## Phase 5: User Story 3 - Retrieve latest design draft and history (Priority: P3)

**Goal**: Latest and full ascending history can be read back.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Agents.Architect.Tests --filter "FullyQualifiedName~DesignRetrievalTests"` passes.

- [X] T029 [US3] Implement `GetLatestDesign` and `GetDesignHistory` in `src/AgenticChangeOps.Agents.Architect/DesignDraftingService.cs`: unknown id returns a `ChangeRequestId` error; latest is the last by version or a null value when none; history is the full ascending list (empty when none); reads taken under `store.SyncRoot`; no audit entry written. Run the US3 filter.

---

## Phase 6: User Story 4 - Refuse ineligible requests (Priority: P4)

**Goal**: Every ineligible request is refused with a distinct specific error, nothing stored, generator not called.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Agents.Architect.Tests --filter "FullyQualifiedName~DesignEligibilityTests"` passes.

- [X] T030 [US4] Complete eligibility in `src/AgenticChangeOps.Agents.Architect/DesignDraftingService.cs`: unknown id gives `ChangeRequestId`, status other than `Approved` gives a `Status` error stating the current status and that only Approved requests can be designed, missing basis gives a `RequirementsDraft` error; checks run before the generator and the basis source is not called for status refusals; same checks repeated at store time. Run the US4 filter.

---

## Phase 7: User Story 5 - Fail safely on model problems (Priority: P5)

**Goal**: Model problems never store anything or leak anything.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Agents.Architect.Tests --filter "FullyQualifiedName~DesignGenerationFailureTests|FullyQualifiedName~DesignHygieneTests|FullyQualifiedName~DesignIsolationTests"` passes.

- [X] T031 [US5] Complete failure handling in `src/AgenticChangeOps.Agents.Architect/DesignDraftingService.cs`: `DraftGenerationException` gives a failure carrying only the category; any other exception names only its type; validator errors give `MalformedOutput` as the category; no model text, endpoint, deployment name or credential in any error. Run the US5 filter.
- [X] T032 [P] [US5] Verify no logging in `src/AgenticChangeOps.Agents.Architect` takes model text or exception messages (the project takes no logger); confirm by search and note the result in the PR description.

---

## Phase 8: User Story 6 - Expose the actions over HTTP (Priority: P6)

**Goal**: Three HTTP operations whose conventions match requirements drafting.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Api.Tests --filter "FullyQualifiedName~Design"` passes; the rest of the API tests stay green.

### Tests first

- [X] T033 [US6] Add `tests/AgenticChangeOps.Api.Tests/DesignEndpointTests.cs` using the existing `ApiFactory` pattern with a fake design generator and fake requirements source (replace the registrations in the test host): every row of [contracts/http-api.md](contracts/http-api.md), including 201 with `Location`, 400, 404 `not-found`, 409 `Status`, 409 `RequirementsDraft`, 502 with `category` only for each failure category and for incomplete output, 503 `design-not-configured`, 404 `no-design-draft`, empty history list, ascending history order; assert no model text, endpoint or deployment name in any body and that nothing is stored after a refusal or failure; for each equivalent situation assert the status code equals the requirements drafting operation's.
- [X] T034 [US6] Add `tests/AgenticChangeOps.Api.Tests/RequirementsBasisAdapterTests.cs`: the adapter returns null for a request with no requirements draft and the latest version's sections and version otherwise, through the real requirements drafting service over the shared in-memory store.
- [X] T035 [US6] Show the user T033 and T034 red (stubbed endpoint absent gives 404 `route-not-found`) and wait for "approved" (project test-approval checkpoint).

### Implementation

- [X] T036 [P] [US6] Add `src/AgenticChangeOps.Api/Composition/RequirementsBasisAdapter.cs` implementing `IRequirementsBasisSource` over `IRequirementsDraftingService.GetLatestDraft`, mapping `RequirementsDraft` to `RequirementsBasis`; null for a missing request or no draft.
- [X] T037 [P] [US6] Add to `src/AgenticChangeOps.Api/Errors/ErrorMapper.cs` (additive only; no existing member touched): `ForDesigning(DesignDraftingResult)` per R4 in [research.md](research.md), `NoDesignDraft()` (404 `no-design-draft`), `DesignNotConfigured()` (503 `design-not-configured`), and a generation-failed variant with message "The design draft could not be generated." and `Category` only.
- [X] T038 [US6] Add `src/AgenticChangeOps.Api/Endpoints/DesignEndpoints.cs` with `POST /change-requests/{id}/designs`, `GET /change-requests/{id}/designs/latest`, `GET /change-requests/{id}/designs`, named and annotated as `DraftEndpoints` is (`RequestDesign`, `GetLatestDesign`, `GetDesignHistory`), malformed id handled by `RequestParsing.TryParseId` before anything else, `DesignAvailability.IsConfigured` checked before the service call.
- [X] T039 [US6] Register in `src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs` (additive): `IRequirementsBasisSource` to `RequirementsBasisAdapter`, `DesignContentValidator`, `IDesignDraftGenerator` built by a new private `CreateDesignSetup` that reads the same options keys through `AzureOpenAIGeneratorOptions.FromEnvironment` and its own `AzureOpenAIChatModelClient`, a new `src/AgenticChangeOps.Api/Composition/DesignAvailability.cs` singleton recording configured or not-configured (one warning logged when not configured, with no setting name or value), `IDesignDraftingService`; existing registration lines and the private `DraftingSetup` are not edited; add the project reference to `src/AgenticChangeOps.Api/AgenticChangeOps.Api.csproj`. Call `DesignEndpoints.Map(app)` and extend the description text in `src/AgenticChangeOps.Api/Program.cs` with `no-design-draft` and `design-not-configured`.
- [X] T040 [US6] Run `dotnet test tests/AgenticChangeOps.Api.Tests`. Expect only the baseline OpenAPI golden to differ; handled in T045.

---

## Phase 9: Polish and cross-cutting

- [X] T041 [P] Add tests that no design type is declared outside the Architect project and that intake names no design type (rule A5), in `tests/AgenticChangeOps.Agents.Architect.Tests/StructureTests.cs`.
- [X] T042 [P] Confirm the existing secret and endpoint scans cover `src/AgenticChangeOps.Agents.Architect`, `tests/AgenticChangeOps.Agents.Architect.Tests` and `specs/008-architect-design-drafting`; extend a scan's folder list only if a folder is missed (an additive test edit).
- [X] T043 [P] Update `README.md` with the Architect agent, the three routes and the opt-in live check, keeping its existing structure.
- [X] T044 Run `git diff --stat main...HEAD -- src/AgenticChangeOps.Agents.Requirement src/AgenticChangeOps.ModelAccess src/AgenticChangeOps.Intake` and confirm the only change is the one appended enum value (rule A4, SC-006).
- [X] T045 Regenerate the API baseline golden for `GET /openapi/v1.json` only; review the diff and confirm it contains only: the three new paths, their new schemas, new codes in the description text, and one appended value (`5`) in the existing `AuditEventType` enum schema. Show the diff to the user. No existing path, schema member, status code or existing enum value may change.
- [X] T046 Run `dotnet build AgenticChangeOps.sln -c Release` and `dotnet test AgenticChangeOps.sln -c Release`; confirm totals are the T001 baseline plus the new tests and the live check is skipped (SC-006). Walk through [quickstart.md](quickstart.md) steps 1 to 3.
- [ ] T047 [USER] Constitution compliance checklist in the PR description (v1.0.2), then commit, push and open the PR for the user to confirm.

---

## Dependencies and execution order

- T000 gates everything; Phase 1 precedes Phase 2; T020 gates Phases 3 to 8; T035 gates T036 to T040.
- Phase 2: T005 to T010 are parallel (distinct files); T011 needs T005 to T010; T012 needs T011; T013 to T018 are parallel after T012; T019 then T020.
- US1 (T021 to T027) must finish first; US2 to US5 all edit `DesignDraftingService.cs`, so they run in order T028, T029, T030, T031. US6 needs US1 and US3 and needs the service complete.
- Within US1: T021 to T023 and T025 are parallel; T024 needs T022 and T023; T026 needs T021, T025.
- Polish T041 to T043 are parallel; T044 to T046 run after all code tasks.

## Parallel example (Phase 2)

```text
T005, T006, T007, T008, T009, T010   # distinct stub files
T013, T014, T015, T016, T017, T018   # distinct test files
```

## Implementation strategy

- **MVP**: Phases 0 to 3 (US1). It delivers a service-level design drafting capability, fully tested without a model.
- **Increment**: US2 to US5 harden the service; US6 exposes it over HTTP. Each phase ends with a green suite.
- Every outward-facing step (push, PR) waits for the user (T047).

## Phase 10: Convergence

- [X] T048 CRITICAL: Resolve the Principle IV minimum-fields gap for `DesignDraft` in `src/AgenticChangeOps.Agents.Architect/DesignDraft.cs`. The user decides between (a) a constitution clarification stating how a record's input and output are carried by its linked requirements version and stored sections, applied to both agents, and (b) adding explicit input and output members to both `DesignDraft` and `RequirementsDraft` with their tests. Do not edit the Requirement agent without that decision. per Constitution IV (contradicts)
  - Done 2026-10-13: option (a) chosen. Drafted as constitution v1.0.3 in `.specify/memory/constitution.md`; it must merge by its own PR (as #12 did) before this slice merges.
- [X] T049 [P] Add `tests/AgenticChangeOps.Agents.Architect.Tests/NoSecretsTests.cs` asserting no string literal in `src/AgenticChangeOps.Agents.Architect/*.cs` is an upper-snake name containing KEY, TOKEN, SECRET or PASSWORD, with a guard that the folder exists and yields source files, mirroring `RequirementAgentProject_DefinesNoSettingNameThatCouldHoldASecret`. per plan: R9 secrets hygiene (partial)
- [X] T050 [P] Add a test in `tests/AgenticChangeOps.Agents.Architect.Tests/Unit/DesignResponseParserTests.cs` that an answer containing an extra, unrecognised key is parsed from the six known keys only and the extra key never appears in `DesignContent` or a stored `DesignDraft`. per spec Edge Cases: unrecognised sections (partial)
- [X] T051 [P] Add two parity assertions in `tests/AgenticChangeOps.Api.Tests/DesignFailureTests.cs`: a not-configured design request returns the same status code as a not-configured requirements draft request, and "no design yet" returns the same status code and body shape as "no draft yet". per T033, SC-007 (partial)
- [X] T052 Amend the wording of SC-006 in `specs/008-architect-design-drafting/spec.md` to name the one updated existing assertion (`DescriptionTests` route list, eight to eleven) alongside the golden baselines, and state the same in the PR description. per SC-006 (partial)
