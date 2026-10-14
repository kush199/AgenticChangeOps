# Tasks: Workflow Orchestration for Change Requests

**Input**: Design documents from `specs/010-workflow-orchestration/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). Tests are shown red and approved (T017) before any implementation. Red means failing for the right reason: Phase 2 adds signature-only stubs that throw `NotImplementedException`, so tests compile and fail on behaviour, not on missing types. HTTP tests are red because the routes do not exist yet (`route-not-found`).

**Constitution**: [x] Reviewed against constitution v1.0.3. See plan.md Constitution Check and Complexity Tracking.

**Organization**: Grouped by user story (US1 to US4 from spec.md). Phases run in priority order; US2 to US4 build on the US1 rules and service.

**Rules that hold for every task**:
- Edits outside `src/AgenticChangeOps.Workflow`, `tests/AgenticChangeOps.Workflow.Tests`, `AgenticChangeOps.sln` and the API files named in plan.md are forbidden. In particular no edit to `src/AgenticChangeOps.Agents.Requirement`, `src/AgenticChangeOps.Agents.Architect`, `src/AgenticChangeOps.Intake` or `src/AgenticChangeOps.ModelAccess`, and no new audit event type.
- No assertion in an existing test is edited, except the two reviewed updates in T030. If an existing test fails, fix the new code, never the assertion. The recorded API description may only gain additions (T028).
- Orchestration never constructs a draft, an audit entry or a decision, and never writes to the store; it only calls the agents' own services (dependency rules 6 and 7).
- Synthetic data only; no endpoint, deployment name, key or real name in any file (Principle I).
- No new package reference in any project.
- After every task that touches code, `dotnet build AgenticChangeOps.sln` must succeed; after every phase, `dotnet test AgenticChangeOps.sln` must be green (except the deliberately red state recorded at T016).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task). Tasks editing the same file are never `[P]`.
- **[Story]**: US1 to US4, from spec.md.
- **[USER]**: outward-facing step (push, PR). Claude drafts, the user confirms.

---

## Phase 0: Gate

- [X] T000 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md are approved. No test or source file is written before this (Principle V).

## Phase 1: Setup

- [X] T001 Confirm branch `feature/workflow-orchestration` and a clean tree. Run `dotnet test AgenticChangeOps.sln` and save the per-project totals to `specs/010-workflow-orchestration/baseline-tests.txt`. This is the SC-007 reference: every earlier test must still pass afterwards.
- [X] T002 Create `src/AgenticChangeOps.Workflow/AgenticChangeOps.Workflow.csproj` (net8.0, `ImplicitUsings` and `Nullable` enabled, exactly four `ProjectReference`s: Intake, ModelAccess, `AgenticChangeOps.Agents.Requirement`, `AgenticChangeOps.Agents.Architect`, all as `..\<name>\<name>.csproj`). Add it to `AgenticChangeOps.sln` under the `src` folder. No `FrameworkReference` to ASP.NET.
- [X] T003 Create `tests/AgenticChangeOps.Workflow.Tests/AgenticChangeOps.Workflow.Tests.csproj` (net8.0, same packages and `Using Include="Xunit"` as `tests/AgenticChangeOps.Agents.Architect.Tests`; `ProjectReference`s to Workflow, Intake, ModelAccess, both agent projects, and linked fakes from `tests/AgenticChangeOps.Intake.Tests/Fakes` as the Architect test project does). Add it to `AgenticChangeOps.sln` under the `tests` folder.

---

## Phase 2: Foundational (stubs, tests first, blocks everything)

**Purpose**: Make the types exist as signatures only, write every test, show them red, and stop for approval.

- [X] T004 [P] Add enums in `src/AgenticChangeOps.Workflow/`: `WorkflowStep.cs` (`RequirementsDraft`, `DesignDraft`), `WorkflowStage.cs` (`AwaitingRequirementsDraft`, `AwaitingDecision`, `ReadyForDesign`, `DesignComplete`, `Deferred`, `Rejected`), `WorkflowReasonCode.cs` (`AwaitingDecision`, `Deferred`, `Rejected`, `RequirementsDraftRequired`, `NothingDue`), `AdvanceOutcome.cs` (`Advanced`, `NotFound`, `NothingDue`, `NotConfigured`, `StepFailed`).
- [X] T005 [P] Add records in `src/AgenticChangeOps.Workflow/`: `WorkflowStatus.cs` with `ChangeRequestId` (Guid), `ChangeRequestStatus` (existing enum, "copied, never altered"), `Stage`, `CompletedSteps` (`IReadOnlyList<CompletedStep>`, "every stored draft, requirements first then designs, each ascending by version"), `NextStep` (`WorkflowStep?`, "set iff a step can run now"), `Reason` (`WorkflowReason?`, "set iff NextStep is null; never both set, never both null"); `CompletedStep.cs` with `Step`, `Version` (int), `GeneratedAt` (DateTime UTC), `RequirementsVersion` (int?, "for a design step: the requirements version it was based on; null otherwise"); `WorkflowReason.cs` with `Code`, `Message` ("fixed sentence per code, naming the change request status where relevant; contains no model text"); `WorkflowFacts.cs` with `RequirementsFact(int Version, DateTime GeneratedAt)` and `DesignFact(int Version, DateTime GeneratedAt, int RequirementsVersion)`.
- [X] T006 [P] Add `AdvanceResult` in `src/AgenticChangeOps.Workflow/AdvanceResult.cs`: `Outcome`, `Step` (`WorkflowStep?`), `Requirements` (`DraftingResult?`), `Design` (`DesignDraftingResult?`), `Status` (`WorkflowStatus?`, "null for NotFound"), `Errors` (`IReadOnlyList<ValidationError>`, "only for NotFound"), with named factories per outcome.
- [X] T007 [P] Add `IStepAvailability` (`bool IsConfigured(WorkflowStep step)`) in `src/AgenticChangeOps.Workflow/IStepAvailability.cs` and `IWorkflowService` in `src/AgenticChangeOps.Workflow/IWorkflowService.cs` with `QueryResult<WorkflowStatus> GetStatus(Guid changeRequestId)` and `Task<AdvanceResult> AdvanceAsync(Guid changeRequestId, CancellationToken cancellationToken = default)`.
- [X] T008 Add stubs whose bodies are `throw new NotImplementedException()` only: `WorkflowRules.Derive(Guid changeRequestId, ChangeRequestStatus status, IReadOnlyList<RequirementsFact> requirements, IReadOnlyList<DesignFact> designs)` returning `WorkflowStatus` in `src/AgenticChangeOps.Workflow/WorkflowRules.cs`, and `WorkflowService` in `src/AgenticChangeOps.Workflow/WorkflowService.cs` (constructor `(IChangeRequestStore, IRequirementsDraftingService, IDesignDraftingService, IStepAvailability)`).
- [X] T009 Add test support in `tests/AgenticChangeOps.Workflow.Tests/Fakes/`: `WorkflowTestFixture.cs` (in-memory store, `FakeSystemClock`, `CountingIdGenerator`, intake and approval services, the real `RequirementsDraftingService` and `DesignDraftingService` over fake generators, a `StepAvailability` whose configured state is settable per step; helpers `SubmitSynthetic()`, `SeedStatus(id, status)`, `DecideAsync(id, decision)` through the approval service, `Rejected()`, `Deferred()`), `FakeRequirementsGenerator.cs`, `FakeDesignGenerator.cs` (return configured content, can throw a given exception, count calls, can run a callback during generation), `TestRequirementsBasisSource.cs` (reads the latest draft through the real requirements service and copies it into `RequirementsBasis`, test-only, mirrors the API adapter). All data synthetic.
- [X] T010 [P] [US1] Tests in `tests/AgenticChangeOps.Workflow.Tests/Unit/WorkflowRulesTests.cs`: one test per row of the rule table in [research.md](research.md) D3 (rows 1 to 9), each asserting stage, `NextStep`, reason code, and that exactly one of `NextStep` and `Reason` is set; completed steps list requirements then designs, ascending, with version, time and (for designs) `RequirementsVersion`; a design based on an older requirements version gives `ReadyForDesign` with next step design; a design based on the latest gives `DesignComplete`; every `ChangeRequestStatus` value is covered (a test enumerates `Enum.GetValues` and fails on an unhandled value).
- [X] T011 [P] [US1] Tests in `tests/AgenticChangeOps.Workflow.Tests/Unit/WorkflowStatusTests.cs`: status for a fresh request, after a requirements draft, after approval, after a design, for `Rejected` and `Deferred` requests, via the real services; status of an unknown id is a `ChangeRequestId` error; calling status writes no audit entry, stores nothing and never calls a generator; status reflects a newly stored draft or a decision with no other update.
- [X] T012 [P] [US2] Tests in `tests/AgenticChangeOps.Workflow.Tests/Unit/AdvanceTests.cs`: advance on a fresh request stores one requirements draft with one `DraftGenerated` audit entry and returns that draft plus status afterwards; after approval advance stores one design with one `DesignGenerated` entry; audit history after each advance is identical to calling the agent's own operation on an identical fixture; change request status, approver and fields are identical before and after; one advance runs exactly one step (two generators, one call total); a `Deferred` request with no draft is advanced to a requirements draft and not further (Clarification Q1).
- [X] T013 [P] [US3] Tests in `tests/AgenticChangeOps.Workflow.Tests/Unit/AdvanceRefusalTests.cs`: advance is refused with `NothingDue` and the matching reason for: Submitted with a draft (`AwaitingDecision`), UnderReview (`AwaitingDecision`), Rejected (`Rejected`), Deferred with a draft (`Deferred`), Approved with no requirements draft (`RequirementsDraftRequired`), Approved with a current design (`NothingDue`); each stores nothing, writes no audit entry, calls neither generator, and leaves status identical; the due step with its step unconfigured gives `NotConfigured` naming that step and stores nothing; nothing due with the step unconfigured still gives `NothingDue`.
- [X] T014 [P] [US4] Tests in `tests/AgenticChangeOps.Workflow.Tests/Unit/AdvanceFailureTests.cs`: unknown id gives `NotFound` with a `ChangeRequestId` error; for each `GenerationFailureCategory` thrown by either generator, advance returns `StepFailed` carrying the agent's own result with only that category, stores nothing, writes no audit entry, and status afterwards equals status before; incomplete content (missing or blank section) gives a `MalformedOutput` category; a generator throwing an exception whose message holds a fake endpoint and key leaks neither into the result, errors or captured log output; a requirements draft changed during design generation is reported as the agent's own `RequirementsDraft` conflict and stores nothing.
- [X] T015 [P] Cross-cutting tests in `tests/AgenticChangeOps.Workflow.Tests/Unit/`: `AdvanceConcurrencyTests.cs` (ten simultaneous advances on one fresh request store exactly one requirements draft and nine report `NothingDue`; simultaneous advances on different requests do not block each other); `RequirementsEligibilityDriftTests.cs` (rule 9: for every `ChangeRequestStatus`, "a requirements step is offered when no draft exists" equals "the real Requirement service accepts a draft for that status"); `StructureTests.cs` (rules 1 to 8 of [contracts/dependency-rules.md](contracts/dependency-rules.md), with non-vacuous folder checks as the Architect `StructureTests` does). Then HTTP tests in `tests/AgenticChangeOps.Api.Tests/WorkflowEndpointTests.cs` and `WorkflowNoLeakTests.cs` using the existing `DesignHost`/`ApiFactory` fakes: both routes for every row in [contracts/http-api.md](contracts/http-api.md), 201 body is `{ step, draft, status }` with `draft` equal to the agent's own route body for the same draft, `Location` header, 409 `no-step-due` with the reason code in `errors`, 503 with the agent's own code, 502 with category only, malformed id 400, unknown id 404, and the response, error and log text contain no fake endpoint, deployment name or key.
- [X] T016 Run `dotnet test tests/AgenticChangeOps.Workflow.Tests` and `dotnet test tests/AgenticChangeOps.Api.Tests --filter "FullyQualifiedName~Workflow"` and record: build succeeds; every behaviour test fails with `NotImplementedException` (or `route-not-found` for HTTP); `StructureTests` pass or fail only for the intended rule. Run `dotnet test AgenticChangeOps.sln --filter "FullyQualifiedName!~Workflow"` and confirm all earlier tests are green.
- [X] T017 **CHECKPOINT (human approval)**: show the user the T010 to T016 results (red tests, failure reasons, earlier tests green). Do not proceed to Phase 3 until the user replies "approved".

---

## Phase 3: User Story 1 - See where a change request is (Priority: P1) MVP

**Goal**: A caller can read an accurate, derived workflow status for any change request, over code and over HTTP.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Workflow.Tests --filter "FullyQualifiedName~WorkflowRulesTests|FullyQualifiedName~WorkflowStatusTests"` passes, and the status cases of `WorkflowEndpointTests` pass.

- [X] T018 [US1] Implement `WorkflowRules.Derive` in `src/AgenticChangeOps.Workflow/WorkflowRules.cs` as a pure function of its arguments following the rule table in research.md D3 (first matching row wins); stage names per [data-model.md](data-model.md); completed steps ordered requirements then designs, each ascending by version; reason messages are fixed sentences that name the change request status; no dependency on any service, generator or store type.
- [X] T019 [US1] Implement `WorkflowService.GetStatus` in `src/AgenticChangeOps.Workflow/WorkflowService.cs`: unknown id returns `QueryResult<WorkflowStatus>.Failure` with a `ValidationError("ChangeRequestId", ...)` worded as the agents' services word it; otherwise read the request, `GetDrafts` and `GetDesigns` in one step under `_store.SyncRoot`, map to `RequirementsFact`/`DesignFact`, return `WorkflowRules.Derive`. Read only: no write, no audit, no generator call.
- [X] T020 [US1] Add the HTTP read operation: `src/AgenticChangeOps.Api/Endpoints/WorkflowEndpoints.cs` mapping `GET /change-requests/{id}/workflow` (name `GetWorkflowStatus`, summary "Retrieve the workflow status of a change request", produces 200 `WorkflowStatus`, 400, 404) using `RequestParsing.TryParseId`, `ErrorMapper.InvalidIdentifier` and `ErrorMapper.ForQuery`; register `IWorkflowService` and a new `src/AgenticChangeOps.Api/Composition/StepAvailabilityAdapter.cs` (maps `RequirementsDraft` to `DraftingAvailability.IsConfigured` and `DesignDraft` to `DesignAvailability.IsConfigured`) in `ApiServiceRegistration.cs`; add the `AgenticChangeOps.Workflow` project reference to `AgenticChangeOps.Api.csproj`; call `WorkflowEndpoints.Map(app)` in `Program.cs`.
- [X] T021 [US1] Run the US1 filters above; all green; run `dotnet test AgenticChangeOps.sln` and confirm only the reviewed T030 assertions (route list and recorded description) are failing, nothing else.

---

## Phase 4: User Story 2 - Advance one step (Priority: P2)

**Goal**: Advancing runs exactly the next allowed step through the agent's own service and returns that step's result plus status afterwards.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Workflow.Tests --filter "FullyQualifiedName~AdvanceTests|FullyQualifiedName~AdvanceConcurrencyTests"` passes.

- [X] T022 [US2] Implement the success path of `WorkflowService.AdvanceAsync` in `src/AgenticChangeOps.Workflow/WorkflowService.cs`: acquire a per-change-request `SemaphoreSlim` (a `ConcurrentDictionary<Guid, SemaphoreSlim>`, released in `finally`, honouring the caller's `CancellationToken`); inside it derive the status as in `GetStatus`; when `NextStep` is `RequirementsDraft` call `IRequirementsDraftingService.DraftAsync`, when `DesignDraft` call `IDesignDraftingService.DesignAsync`; on success return `Advanced` with the agent's result object and the status re-derived afterwards. No other store or service call; no audit entry written here.
- [X] T023 [US2] Add the HTTP advance operation in `src/AgenticChangeOps.Api/Endpoints/WorkflowEndpoints.cs`: `POST /change-requests/{id}/workflow/advance` (name `AdvanceWorkflow`, summary "Run the next allowed agent step for a change request", produces 201 `AdvanceResponse`, 400, 404, 409, 500, 502, 503), no request body; success returns `Results.Created` with `Location` of the produced draft's own `.../drafts/latest` or `.../designs/latest` and body `{ step, draft, status }`. Add `AdvanceResponse` to `src/AgenticChangeOps.Api/Contracts/AdvanceResponse.cs` (camelCase via the existing serializer options; `draft` typed as `object` only at the serialization edge, as the two agents' own results are already typed records).
- [X] T024 [US2] Run the US2 filters and the advance-success cases of `WorkflowEndpointTests`; all green.

---

## Phase 5: User Story 3 - Refuse advancing when nothing is due (Priority: P3)

**Goal**: An advance with no step due, or whose step cannot run for lack of model settings, is refused with the specific reason and changes nothing.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Workflow.Tests --filter "FullyQualifiedName~AdvanceRefusalTests"` passes, plus the 409 and 503 cases of `WorkflowEndpointTests`.

- [X] T025 [US3] Extend `WorkflowService.AdvanceAsync`: when the derived status has no `NextStep`, return `NothingDue` with the status and its reason, before any step or availability is consulted; when a step is due and `IStepAvailability.IsConfigured(step)` is false, return `NotConfigured` for that step, without calling the agent.
- [X] T026 [US3] Add `ErrorMapper.NoStepDue(WorkflowReason reason)` in `src/AgenticChangeOps.Api/Errors/ErrorMapper.cs`: 409, code `no-step-due`, `Message` = the reason message, `Errors` = one `ErrorField("Reason", <reason code name>)`. Map `NothingDue` to it and `NotConfigured` to the existing `ErrorMapper.DraftingNotConfigured()` or `DesignNotConfigured()` in `WorkflowEndpoints.cs`. Add `no-step-due (409)` to the description text in `Program.cs`. No existing mapper entry changes.
- [X] T027 [US3] Run the US3 filters; all green.

---

## Phase 6: User Story 4 - Refuse bad identifiers and fail safely (Priority: P4)

**Goal**: Unknown or malformed ids are refused; step failures store nothing and surface only their category, exactly as on the agents' own routes.

**Independent Test**: `dotnet test tests/AgenticChangeOps.Workflow.Tests --filter "FullyQualifiedName~AdvanceFailureTests"` passes, plus the failure and no-leak cases of the Api workflow tests.

- [X] T028 [US4] Extend `WorkflowService.AdvanceAsync`: unknown id returns `NotFound` with the `ChangeRequestId` error; a failed agent result returns `StepFailed` carrying the agent's own `DraftingResult` or `DesignDraftingResult` unchanged and the status re-derived (identical to before). In `WorkflowEndpoints.cs` map `StepFailed` through the existing `ErrorMapper.ForDrafting` / `ForDesigning`, and `NotFound` through `ErrorMapper.ForQuery`. Confirm no code path logs or returns exception text.
- [X] T029 [US4] Run the US4 filters and the full `WorkflowEndpointTests` and `WorkflowNoLeakTests`; all green.

---

## Phase 7: Polish and cross-cutting

- [X] T030 **Reviewed test updates (the only two permitted edits to existing tests)**: in `tests/AgenticChangeOps.Api.Tests/DescriptionTests.cs` change the test name and expected list to include `GET /change-requests/{id}/workflow` and `POST /change-requests/{id}/workflow/advance`, and add `no-step-due` to the documented error codes; regenerate `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json` so it gains only the two operations, the `WorkflowStatus`, `CompletedStep`, `WorkflowReason` and `AdvanceResponse` schemas and the description text. Show the diff of both to the user; it must be additions only.
- [X] T031 Run `dotnet test AgenticChangeOps.sln`; compare per-project totals with `baseline-tests.txt`: every earlier test passes, the existing agent route baselines (`ApiBaselineTests`, `DesignOperationsBaselineTests`, `SharedPlumbingBaselineTests`) pass unedited.
- [X] T032 Run the secret and synthetic-data scans that already cover `src`, `tests`, `specs` (`NoSecretsTests`); confirm no new endpoint, deployment name or key appears in any new file.
- [X] T033 Walk through [quickstart.md](quickstart.md) against the fake-generator host and record the outcome in the PR description. Live model check stays opt-in and is not run unless the user asks.
- [X] T034 Re-check Constitution v1.0.3 against the finished diff (especially II: no model output reaches routing; III: no decision is recorded or implied by advance; VII: no helper without two callers, no engine or state library); tick the checklist item in the PR description.
- [ ] T035 [USER] Commit on `feature/workflow-orchestration` with the slice name in the message, push, and open the PR. Claude drafts the description; the user confirms before anything is pushed.

---

## Dependencies and order

- Phase 0 to Phase 2 are strictly sequential; T017 gates all implementation.
- Phase 3 (US1) is the MVP and blocks Phases 4 to 6, because advance reuses the rules and snapshot read.
- Phases 4, 5 and 6 each edit `WorkflowService.cs` and `WorkflowEndpoints.cs`, so they run in order; their test tasks were already written in Phase 2.
- Phase 7 follows all stories; T030 is the only place existing assertions or goldens change.

## Parallel opportunities

- T004 to T007 touch different files.
- T010 to T015 are separate test files and can be authored in parallel.
- After T017, no implementation tasks are parallel because they share the two service/endpoint files.

## MVP scope

Phases 0 to 3: status over code and HTTP, derived from recorded facts. It is independently useful (it tells a caller what to do next) and already satisfies FR-001, FR-003 to FR-006 and FR-009 reporting.

## Phase 8: Convergence

- [X] T036 Give `AdvanceResponse.Draft` in `src/AgenticChangeOps.Api/Contracts/AdvanceResponse.cs` a concrete type instead of `object`, so the draft keeps its typed shape in the recorded API description (today the schema is an untyped nullable member); keep the serialised JSON identical to the agent's own route body and update the `AdvanceWorkflow` response declaration in `src/AgenticChangeOps.Api/Endpoints/WorkflowEndpoints.cs` and, as a reviewed additions-only change, `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json` per contracts/http-api.md, FR-011 and Constitution IV (partial)
- [X] T037 [P] Add HTTP tests in `tests/AgenticChangeOps.Api.Tests/WorkflowEndpointTests.cs` for the two situations `contracts/http-api.md` lists but no HTTP test exercises: advance on a due requirements step with drafting not configured returns 503 `drafting-not-configured`, and an advance whose requirements draft changed during design generation returns 409 `conflict` with field `RequirementsDraft`, each storing nothing, per T015 and contracts/http-api.md (partial)
- [X] T038 [P] Make `CompletedStep.RequirementsVersion` in `src/AgenticChangeOps.Workflow/CompletedStep.cs` omit the member from JSON when null (`JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)`), so requirements steps carry no `requirementsVersion` and design steps do, and add an assertion for both in `tests/AgenticChangeOps.Api.Tests/WorkflowEndpointTests.cs`, per contracts/http-api.md WorkflowStatus JSON (partial)

