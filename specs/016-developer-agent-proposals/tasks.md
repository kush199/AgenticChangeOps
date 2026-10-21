# Tasks: Developer Agent Proposes A Change Set

**Input**: Design documents from `specs/016-developer-agent-proposals/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (http-api, verification-rules, audit-and-record, settings-and-failures, http-api-unchanged), quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). New types are added as compiling **stubs** (Phase 2) so new tests fail for the right reason (not-implemented behaviour, not a compile error). All new tests are shown red once and approved at T028 before any behaviour is implemented.

**Constitution**: [x] Reviewed against constitution v1.0.5 before approval (see plan.md Constitution Check; the v1.0.6 amendment is a separate PR). Tick at T001.

**Organization**: Grouped by user story from spec.md: US1 verified proposal (P1), US2 retrieve latest and history (P1), US3 refusals store nothing (P1), US4 bad proposal rejected whole (P1), US5 nothing else changes and runs only when asked (P2), US6 hostile content stays data (P2). The verifier, generator and service are one flow built under US1; US3 and US4 harden it.

**Rules that hold for every task**:
- With the Developer unused (tools off, the default) every existing response, status code, body and header is byte-identical and every existing test passes **with no assertion edited** (FR-021). Existing test files only gain new files beside them. The only edits to existing test data allowed are those listed in `baseline/expected-test-edits.md`, approved by the user at T005 (enum-enumerating tests, applied in Phase 2) and at T028 (OpenAPI and goldens, applied at T029): files that enumerate every `AuditEventType` or `WorkflowStep` value, and the generated `openapi.json` and golden transcripts, which may gain only additions (new routes, new enum values, the optional `offeredStep`), regenerated and diff-reviewed. If any other existing test fails, fix the new code, never the assertion.
- Nothing is written, created, deleted, compiled or run anywhere by this feature (FR-007). Verification only reads the sample.
- Nothing in any error, log, response or audit entry carries file content, quoted or replacement text, model text, an endpoint, a deployment name, a credential, or a path outside the sample (FR-019). Rejections return the reason code only.
- `AgenticChangeOps.Agents.Developer` references only Intake and ModelAccess. It must not contain `CancellationTokenSource`, `JsonDocument`, `GetProperty(`, `IsNullOrWhiteSpace(` or code fences (the shared-behaviour guard scans every `Agents.*` folder). Framework and MCP types stay where they are today.
- The model never decides what is due, whether a transition happens, or whether anything is approved (Principle II). No live model call in the suite; scripted fakes only.
- Synthetic data only (Principle I); hostile text is generated filler with a made-up marker such as `HOSTILE-MARKER-0001`.
- After every code task `dotnet build AgenticChangeOps.sln` succeeds. After every phase from Phase 4 on, `dotnet test AgenticChangeOps.sln` is green (except the deliberately red state at T027).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel (different files, no dependency on an incomplete task). Tasks editing the same file are never `[P]`.
- **[Story]**: US1 to US6, from spec.md.
- **[USER]**: outward-facing step (push, PR, amendment PR). Claude drafts, the user confirms.

---

## Phase 1: Setup

- [X] T001 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md are approved, and the pre-code constitution check (v1.0.5, Development Workflow step 4) is appended to plan.md under "Constitution compliance check (pre-code)": proposals are non-consequential (Principle III), additive record members only (IV), model generates content only (II), synthetic sample (I), Developer is one of the four agents (VII). Tick the **Constitution** line above. No test or source file is written before this (Principle V).
- [X] T002 Draft `specs/016-developer-agent-proposals/constitution-amendment-draft.md` (v1.0.5 to v1.0.6, PATCH): Technology Stack, Agent Framework and MCP lines record that the Developer agent also uses the read-only sample tools, to propose text-only changes verified against the sample, with the same switch; routing and decisions stay deterministic; applying anything needs its own slice. Include rationale and impact on slices 011, 015 and 016. Opened as its own PR at T056.
- [X] T003 Confirm branch `feature/developer-agent-proposals` (note: `.specify` reports the folder name `016-developer-agent-proposals`; keep the branch as is). Run `dotnet test AgenticChangeOps.sln` and save per-project totals (and skipped live checks) to `specs/016-developer-agent-proposals/baseline-tests.txt`, same layout as `specs/015-architect-reads-sample-code/baseline-tests.txt`.
- [X] T004 Record the unchanged-behaviour recordings required by contracts/http-api-unchanged.md under `specs/016-developer-agent-proposals/baseline/`: with a fixed synthetic change request and scripted model answers, the exact status, headers and body of the design route, design history, workflow status at every stage (including `DesignComplete`), advance, and the audit listing, plus the stored design payload from the in-memory and SQLite stores. Commit the scenario definition as `baseline/scenario.json` so T040 can replay it in the suite. Capture with a throwaway local script outside the repository; commit only the recorded text and the scenario file.
- [X] T005 **GATE ([USER])**: check which existing tests enumerate every `AuditEventType` or `WorkflowStep` value or assert the full OpenAPI document (`grep` under `tests/`), and list them with the exact additive edit each would need in `specs/016-developer-agent-proposals/baseline/expected-test-edits.md`. Show the list to the user and wait for the explicit word "approved" (as slice 015 did at its T006); the approved additive edits to enumerating tests are applied in Phase 2 (T006, T007) so T010 can be green, and any test not on the list stays untouched.

## Phase 2: Foundational (stubs and additive shapes; blocks every story)

Shapes only: every new behaviour method throws `NotImplementedException` until its story implements it.

- [X] T006 [P] Append `ProposalGenerated` after `ToolCalled` in `src/AgenticChangeOps.Intake/Models/Enums/AuditEventType.cs`. Apply the T005-approved additive edit to any test that enumerates every `AuditEventType` value.
- [X] T007 [P] Append `Proposal` after `DesignDraft` in `src/AgenticChangeOps.Workflow/WorkflowStep.cs`. In `WorkflowStatus.cs` append the last record parameter `WorkflowStep? OfferedStep = null` with the same `JsonIgnore(WhenWritingNull)` attribute as `Automatic`; include it in `Equals`/`GetHashCode`. Add `src/AgenticChangeOps.Workflow/IProposalOffer.cs`: `bool IsOffered { get; }`. Add an optional trailing `IProposalOffer? proposalOffer = null` constructor argument to `WorkflowService`; do not use it yet. Apply the T005-approved additive edit to any test that enumerates every `WorkflowStep` value.
- [X] T008 Create `src/AgenticChangeOps.Agents.Developer/AgenticChangeOps.Agents.Developer.csproj` (net8.0, ImplicitUsings, Nullable, project references to Intake and ModelAccess only) and add it to `AgenticChangeOps.sln`. Add stub types per data-model.md, one file each under `src/AgenticChangeOps.Agents.Developer/`: `ProposalStatus` (`Draft`), `ProposalChangeKind` (`Change`, `Add`), `ProposalRejection` (`NoChanges`, `TooManyChanges`, `TooLarge`, `PathOutsideSample`, `FileNotFound`, `QuoteNotFound`, `QuoteAmbiguous`, `NewFileExists`, `OverlappingEdits`, `MalformedEdit`), `ProposedChange(Path, Kind, ExistingText?, ReplacementText)`, `ChangeProposal : IAgentOutput` (members exactly as data-model.md; `public const string DeveloperAgentName = "DeveloperAgent"` and `VerificationNoteText = "Not compiled, run or tested."`), `ProposalContent` (nullable members, plus `SampleFilesRead` and `ToolCalls`), `ProposalLimits(MaxChanges, MaxTotalBytes)` with `Defaults` 5 and 6000, `ProposalBasis` (`ProposalRequirementsBasis`, `ProposalDesignBasis`, `IProposalBasisSource` returning requirements and design bases or null), `ISampleFiles` (`Exists(path)`, `TryReadText(path)` returning the text or a `ProposalRejection`), `IProposalGenerator`, `ProposalResult` (success with proposal, or failure with `ValidationError`s, optional `GenerationFailureCategory`, optional `ProposalRejection`), `IProposalService` (`ProposeAsync`, `GetLatestProposal`, `GetProposalHistory`), and stub classes `ProposalVerifier`, `ProposalContentValidator`, `ProposalPromptBuilder`, `ProposalResponseParser`, `AzureOpenAIProposalGenerator`, `ProposalService`, `ProposalStoreExtensions` (`SaveProposal`, `GetProposals` over `SaveAgentOutput`/`GetAgentOutputs`). Constraints quoted from data-model.md: Summary "required, non-blank"; Changes "1 to the maximum number of changes"; Risks "at least one item"; TestIdeas "may be empty"; OpenQuestions "may be empty"; SampleFilesRead "from the tool session; relative, de-duplicated, may be empty"; `ExistingText` "required for `Change`... absent for `Add`".
- [X] T009 Create `tests/AgenticChangeOps.Agents.Developer.Tests/AgenticChangeOps.Agents.Developer.Tests.csproj` (same packages and layout as `tests/AgenticChangeOps.Agents.Architect.Tests`, references Developer, Intake, ModelAccess) and add it to the solution. Add `Fakes/`: `FakeProposalGenerator` (scripted behaviour, call count), `FakeProposalBasisSource` (settable bases), `FakeSampleFiles` (in-memory path to text map, case-sensitive), `FakeToolSession` copy-pattern from the Architect fakes (calls, files read), and `ProposalTestFixture` (in-memory store, fixed clock, sequential ids, Approved change request, requirements and design bases).
- [X] T010 Run `dotnet build AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln`: build succeeds and every existing test is green with the T003 counts (the stubs are unreachable). Record the result in `baseline-tests.txt`.

## Phase 3: Tests first (all shown red at T027)

### Tests for US1: a verified proposal

- [X] T011 [P] [US1] Add `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalVerifierTests.cs` against `FakeSampleFiles`: a valid `Change` (quote once) and a valid `Add` pass; quote compared with CRLF/LF differences on either side passes; a quote differing by one space, tab, trailing space or letter case fails `QuoteNotFound`; a quote found twice fails `QuoteAmbiguous`; a missing file fails `FileNotFound`; an `Add` for an existing file or an existing folder fails `NewFileExists`; two `Change` edits with overlapping ranges in one file, two edits with the same quote, and two `Add` edits to one path fail `OverlappingEdits`; non-overlapping edits to one file both pass and are each checked against the original text; zero changes fails `NoChanges`; one more than `MaxChanges` fails `TooManyChanges`; total existing plus replacement UTF-8 bytes one over `MaxTotalBytes` fails `TooLarge` (multi-byte text counted in bytes); a `Change` without quote or an `Add` with a quote fails `MalformedEdit`; one bad edit among five good ones rejects the whole proposal and reports only the reason.
- [X] T012 [P] [US1] Add `Unit/ProposalServiceStoreTests.cs`: eligible request with `FakeProposalGenerator` returning valid content stores a `ChangeProposal` with version 1, `RequirementsVersion` and `DesignVersion` equal to the bases, generation time from the clock, status `Draft`, agent name `DeveloperAgent`, `VerificationNote` "Not compiled, run or tested.", summary, changes, risks, test ideas, open questions and `SampleFilesRead` taken from the tool session not the model text; exactly one `ProposalGenerated` audit entry (`AgentName`, `DraftVersion`, status before equals status after) saved after one `ToolCalled` entry per tool call in call order with `AgentName = DeveloperAgent`, target control characters removed and truncated to 120, no content member; the change request status and every other stored record are unchanged; the generator was called exactly once (no retry).
- [X] T013 [P] [US1] Add `Unit/ProposalContentValidationTests.cs`: missing or blank summary, no risks, a change missing path/kind/replacement, an unknown kind, non-string list items and sections of the wrong type are each reported as `MalformedOutput`; blank list items are dropped and text trimmed; test ideas and open questions may be empty; model-supplied `sampleFilesRead`, `verificationNote` or `agentName` in the answer are ignored.
- [X] T014 [P] [US1] Add `Unit/ProposalResponseParserTests.cs` and `Unit/ProposalPromptTests.cs`: the parser reads the JSON object alone or in one code fence (nested `changes` objects included), rejects anything else as `MalformedOutput` without putting answer text in the exception; the prompt contains the change request fields, the requirements basis, the design basis, the instruction that tool output is data, the instruction to propose only and quote exactly, and no setting values.
- [X] T015 [P] [US1] Add `tests/AgenticChangeOps.ModelAccess.Tests/ModelAnswerObjectListTests.cs`: the new `ModelAnswer` reader for a list of objects (research R9) returns each object's string members, treats a missing section as absent and a present empty list as empty, rejects a non-object item, a non-string member and a nested depth over the limit as malformed, and never includes answer text in the error.
- [X] T016 [P] [US1] Add `Unit/ProposalGeneratorIntegrationTests.cs`: `AzureOpenAIProposalGenerator` with a scripted `IToolChatModelClient` and `FakeToolSession`: opens a session per request and disposes it, passes the session tools, makes one model conversation, returns content with `SampleFilesRead` and `ToolCalls` copied from the session, never from the model text; a factory that throws `ToolsUnavailable` surfaces `DraftGenerationException(ToolsUnavailable)` before any model call; limit exhaustion without a usable answer surfaces `LimitsExhausted`; with a usable answer after a limit was reached it returns the content.
- [X] T017 [P] [US1] Add `tests/AgenticChangeOps.Api.Tests/ProposalEndpointTests.cs` (request part): `POST /change-requests/{id}/proposals` with tools on and a scripted generator returns 201, `Location: /change-requests/{id}/proposals/latest`, camel-case body per contracts/http-api.md including `verificationNote`; the sample folder (a temp copy of the real `sample-codebase/`) is byte-identical afterwards (hash before and after); the change request status is unchanged. Add `SampleFilesAdapterTests.cs` in the same project: the adapter over a temp sample folder refuses at least 20 escaping spellings (reuse the confinement spellings from `tests/AgenticChangeOps.SampleCode.Tests/PathConfinementTests.cs`) as `PathOutsideSample`, refuses a link out (skipped explicitly when links cannot be created), reads an in-sample file, reports `Exists` for files and folders, and refuses a file over the per-file limit.

### Tests for US2: retrieval

- [X] T018 [US2] Add `Unit/ProposalRetrievalTests.cs` and the retrieval cases in `ProposalEndpointTests.cs`: two proposals give latest = version 2, history = 1 then 2 with version 1 unchanged; none gives a successful null (service) and 404 `no-proposal` (route); unknown id gives not found, malformed id gives 400 `invalid-identifier`, on all three routes; overlapping concurrent requests for one change request produce distinct consecutive versions with none lost or duplicated; reading under the store lock never shows a proposal mid-store. Add a case to the shared store behaviour suite in `tests/AgenticChangeOps.Persistence.Tests` for `ChangeProposal` round trip (typed content, order, immutability) against both stores, as a new file beside the existing suite.

### Tests for US3: refusals

- [X] T019 [P] [US3] Add `Unit/ProposalEligibilityTests.cs`: not Approved (each other status), no requirements draft, no design draft, design based on an older requirements version, each refused with its own field (`Status`, `RequirementsDraft`, `DesignDraft`, `DesignDraftVersion`) and fixed message; the generator and tool session are never called; nothing is stored; check order is existence, status, requirements, design present, design current; the basis source is not consulted for a non-Approved request. Re-check cases: a newer requirements draft, or a newer design draft, saved after generation and before storing refuses with the outdated error and stores nothing (clarification Q1).
- [X] T020 [US3] Add the refusal cases to `ProposalEndpointTests.cs`: 404 before 503 for an unknown id; tools switched off gives 503 `proposal-tools-unavailable` and invalid proposal limits give 503 `proposal-not-configured`, both before any model call and with nothing stored; a state refusal gives 409 `conflict` with `fields` naming the rule; messages are the fixed text. Add `ProposalSettingsTests.cs`: defaults (5, 6000); limits are read only when the tools switch is on (garbage with the switch off raises nothing); zero, negative, decimal, signed and non-numeric values are invalid with an error naming the setting only, never the value; invalid proposal limits do not make design drafting unavailable.

### Tests for US4: rejection and failure

- [X] T021 [P] [US4] Add `Unit/ProposalRejectionTests.cs`: through the service with a scripted generator, each rejection reason from T011 stores nothing, saves no audit entry, and returns a failure carrying `ProposalRejection` and no path, quote or content in any `ValidationError` text; add the model-failure cases: generator throws each `GenerationFailureCategory` (including `ToolsUnavailable`, `LimitsExhausted`), null content, a non-category exception (type name only): nothing stored, category only.
- [X] T022 [US4] Add the failure cases to `ProposalEndpointTests.cs`: `proposal-rejected` is 502 with `category` equal to the reason and no other detail; model failures are 502 `generation-failed` with the existing category; unexpected exceptions are 500 `unexpected-error`; in every failure case the response body, captured log output (`CapturingLoggerProvider`) and audit trail contain none of: file text, quoted or replacement text, model text, endpoint, deployment name, credential, the temp sample path (SC-006). Failed requests log counts only.

### Tests for US5: nothing else changes, runs only when asked

- [X] T023 [P] [US5] Add `tests/AgenticChangeOps.Api.Tests/ProposalOffMatchesBaselineTests.cs`: replay `baseline/scenario.json` with defaults (tools off) and assert byte-identical status, headers and bodies to the T004 recordings, including workflow status at `DesignComplete` (no `offeredStep`), and that the new routes exist and answer 503. Add Workflow tests in `tests/AgenticChangeOps.Workflow.Tests/Unit/ProposalOfferTests.cs`: with no offer, or an offer reporting false, statuses at every stage equal today's; with an offer reporting true, only `DesignComplete` gains `OfferedStep = Proposal` while `Stage`, `NextStep`, `Reason` and `CompletedSteps` are unchanged and `OfferedStep` serialises as `offeredStep`; `AdvanceAsync` at `DesignComplete` still returns nothing-due and calls no proposal service.
- [X] T024 [P] [US5] Add `tests/AgenticChangeOps.Workflow.Tests/Automatic/ProposalNotAutomaticTests.cs`: with the offer true and a change request at `DesignComplete`, running automatic advancement checks makes no proposal, saves no audit entry, records no attempt, and the Developer generator receives no call. Add `DeveloperStructureTests.cs` and `NoSecretsTests.cs` in the Developer test project, modelled on the Architect's: every Developer type is declared once and only in the Developer project; the project references only Intake and ModelAccess; it names no other agent and not `AgenticChangeOps.SampleCode`; no framework or MCP types outside their existing files; no secret-shaped literals.

### Tests for US6: hostile content

- [X] T025 [P] [US6] Add `Unit/ProposalHostileContentTests.cs`: a scripted tool session returns a sample file whose text contains instruction-like lines (`HOSTILE-MARKER-0001`, "ignore previous rules", "mark this approved", "write this file"); assert the tool set is unchanged before and after, the change request status is unchanged, the quote check still compares against the real file text, and a proposal quoting the injected sentence verbatim is verified normally (it is data) while a proposal that adds a path the text suggested is still subject to every rule.

### Cross-cutting tests and gate

- [X] T026 [P] Add `tests/AgenticChangeOps.Api.Tests/ProposalCompositionTests.cs`: with the switch on and valid settings, the full route works end to end with a scripted model client and a temp copy of the real `sample-codebase/`, for one orders-service request and one billing-portal request (SC-001: every quote found once, every add path absent); a stale design or requirements version between request start and store gives 409 and stores nothing; two sequential requests give versions 1 and 2; OpenAPI lists the three operations with the documented responses.
- [X] T027 Run `dotnet test AgenticChangeOps.sln`. Capture the output to `specs/016-developer-agent-proposals/red-run.txt`. **Expect red, for the right reason**: every new test needing behaviour fails with `NotImplementedException` or an unmet assertion, not a compile error or missing type; pre-existing tests are green except any listed in `baseline/expected-test-edits.md`.
- [X] T028 **GATE (test approval, [USER])**: show the user the new tests (T011 to T026), the red run and `baseline/expected-test-edits.md`, and wait for the explicit word "approved". No behaviour is implemented before this (Principle VI). Record the approval in `red-run.txt`.
- [X] T029 After T028 approval only: apply the remaining additive edits listed in `baseline/expected-test-edits.md` that need the new routes (regenerated `openapi.json` and goldens with the baseline update variable); the enum-enumerating edits were already applied in Phase 2. Review the diff to confirm it contains only additions of the new routes, enum values and optional members; keep it for the PR description.

## Phase 4: User Story 1 - A verified change proposal (P1) 🎯 MVP

**Goal**: an Approved request with current drafts yields a stored, verified, audited, text-only proposal.

**Independent test**: T011 to T017 and T026 pass.

- [X] T030 [US1] Add the nested-object list reader to `src/AgenticChangeOps.ModelAccess/ModelAnswerReader.cs` (and `ModelAnswer`) per research R9; strict, no answer text in errors; the existing readers and their tests are untouched. Makes T015 pass.
- [X] T031 [US1] Implement `ProposalVerifier` in `src/AgenticChangeOps.Agents.Developer/ProposalVerifier.cs` exactly as contracts/verification-rules.md steps 2 to 7: count, size in UTF-8 bytes, per-edit path/existence/quote via `ISampleFiles`, LF normalisation for comparison only, ordinal exactly-once match, overlap detection against the original text. It returns the first `ProposalRejection` or success; it never writes anything. Makes T011 pass.
- [X] T032 [P] [US1] Implement `ProposalResponseParser` and `ProposalContentValidator` (shared `SectionNormaliser` for text and lists; shape rules from verification step 1), reporting `MalformedOutput` and `MalformedEdit`. Makes T013 and T014 (parser) pass.
- [X] T033 [P] [US1] Implement `ProposalPromptBuilder` (system and user text, tools required) per research R9. Makes T014 (prompt) pass.
- [X] T034 [US1] Implement `AzureOpenAIProposalGenerator`: constructor takes options, `IChatModelClient`, `IToolSessionFactory`, `IToolChatModelClient` (all required, no tool-less path); `GenerateAsync` opens a session, builds the prompt, calls `ModelCaller` with the session, parses, and returns the content with `SampleFilesRead` and `ToolCalls` copied from the session. Makes T016 pass.
- [X] T035 [US1] Implement `ProposalService.ProposeAsync` store path, `ProposalStoreExtensions` and `GetLatestProposal`/`GetProposalHistory` following `DesignDraftingService`: eligibility, generate (category-only failures), validate, verify, then one lock block that re-runs eligibility and checks both versions, assigns the version, saves tool-call entries then the `ProposalGenerated` entry with the proposal. The Developer's tool-call audit mapping is a private copy of the Architect's (plan.md Complexity Tracking), `AgentName = DeveloperAgent`. Makes T012, T018 (service and store parts) and T019 (service part) pass.
- [X] T036 [US1] Add `src/AgenticChangeOps.Api/Composition/SampleFilesAdapter.cs` (implements `ISampleFiles` over `SamplePathResolver`, strict UTF-8, per-file size limit, read-only, refusals map to `PathOutsideSample`/`FileNotFound`), `ProposalBasisAdapter.cs` (implements `IProposalBasisSource` from `IRequirementsDraftingService` and `IDesignDraftingService`, copying fields into the Developer's own read model), and `ProposalAvailability.cs`. Makes the adapter parts of T017 pass.
- [X] T037 [US1] Wire `ProposalSetup` in `ApiServiceRegistration.cs`: read `SampleToolSettings` first (off gives "tools unavailable", nothing else read), then read `AGENTICCHANGEOPS_PROPOSAL_MAX_CHANGES` and `AGENTICCHANGEOPS_PROPOSAL_MAX_BYTES` only when on (R4), build the generator with a `CountingToolSessionFactory` and the registered `IToolChatModelClient`, register `IProposalService`, `ProposalAvailability`, and the adapters; invalid values log the setting name only and set "not configured". Do not touch the design or requirements registrations. Add `ErrorMapper.ForProposing`, `NoProposal`, `ProposalNotConfigured`, `ProposalToolsUnavailable`, `ProposalRejected(category)` per contracts/http-api.md. Add `Endpoints/ProposalEndpoints.cs` with the three routes (order: id, existence, availability, service) and map it in `Program.cs`. Makes T017 (request), T020, T022 and T026 pass.

**Checkpoint**: `dotnet test` green except Phases 5 to 9 tests.

## Phase 5: User Story 2 - Retrieve latest and history (P1)

**Goal**: latest and ordered history, versions preserved.

**Independent test**: T018 passes.

- [X] T038 [US2] Verify and finish the two GET routes and `no-proposal` mapping in `ProposalEndpoints.cs` and `ErrorMapper.cs`; confirm reads run under `store.SyncRoot`; run the shared store suite case from T018 against the SQLite store using a temp file and confirm no schema or layout version change (`StorageSchema.LayoutVersion` stays 2). Makes T018 pass.

## Phase 6: User Story 3 - Refusals store nothing (P1)

**Goal**: every ineligible or unavailable request is refused specifically, with no model call and nothing stored.

**Independent test**: T019 and T020 pass.

- [X] T039 [US3] Complete `CheckEligibility` for the four rules with the fixed messages, the two fields `DesignDraft` and `DesignDraftVersion`, and the `ErrorMapper.ForProposing` mapping to 409; ensure the availability checks run before any generator call and after the unknown-id check. Makes T019 and T020 pass.

## Phase 7: User Story 4 - Bad proposal rejected whole (P1)

**Goal**: any failed check or model failure stores nothing and reports a category only.

**Independent test**: T021 and T022 pass.

- [X] T040 [US4] Finish failure handling in `ProposalService`: `DraftGenerationException` gives its category, any other exception gives the type name only, validation failures give `MalformedOutput`, verification failures give `ProposalRejection`; log counts only (calls made, refusals by kind, time taken, category or reason). Confirm no failed path calls the store. Makes T021 and T022 pass.

## Phase 8: User Story 5 - Nothing else changes; runs only when asked (P2)

**Goal**: unused means byte-identical; the workflow offers the proposal without ever scheduling it.

**Independent test**: T023 and T024 pass and the full existing suite is green with no assertion edited.

- [X] T041 [US5] Implement the offer in `WorkflowService.Derive`: after the rules and the automatic block, when the stage is `DesignComplete` and `_proposalOffer?.IsOffered == true`, set `OfferedStep = WorkflowStep.Proposal`; nothing else changes. Add `src/AgenticChangeOps.Api/Composition/ProposalOffer.cs` (true only when tools are on, settings valid and the model is configured) and register it for `WorkflowService`. `AutomaticAdvancementService` and `WorkflowRules` are not edited. Makes T023 (workflow) and T024 pass.
- [X] T042 [US5] Replay `baseline/scenario.json` and diff against the T004 recordings; record "byte-identical" with the Developer unused in `specs/016-developer-agent-proposals/comparison.md`, plus the list of additive members actually added. Makes T023 (replay) pass.

## Phase 9: User Story 6 - Hostile content stays data (P2)

- [X] T043 [US6] Confirm the prompt builder frames tool output as data and that nothing in `ProposalService` or the verifier takes any decision from file text; add the residual-risk note (a model may still be influenced in what it writes, never in what the system accepts) to research.md. Makes T025 pass.

## Phase 10: Polish and Cross-Cutting Concerns

- [X] T044 [P] Update `README.md`: a "Developer agent proposals (optional)" section covering what it does and does not do (text only, never applied), the three routes, the two new settings, that it needs the sample tools on, `offeredStep`, and the new project and test project in the layout lists. No real endpoint, deployment or key anywhere.
- [X] T045 [P] Add the opt-in live check `tests/AgenticChangeOps.Agents.Developer.Tests/Live/LiveProposalCheckTests.cs` using the existing `LiveModelFactAttribute` pattern: skipped when model settings are absent, asserts only that a returned proposal verifies against the real sample and no audit entry has content. Not run as part of this slice.
- [X] T046 [P] Fill in plan.md Complexity Tracking results (what was added) and finish `comparison.md`, including a line recording that producing a proposal is classified non-consequential (FR-008) with the reason (nothing leaves the system, nothing is applied, no status moves), and add the same line to the PR description checklist in T050.
- [X] T047 Run `dotnet build AgenticChangeOps.sln -c Release` and `dotnet test AgenticChangeOps.sln`, then quickstart.md sections 1 and 2 and record results in `specs/016-developer-agent-proposals/verification.md`. Section 3 (live) is recorded as not run unless the maintainer runs it.
- [X] T048 Final scan: grep the Developer project for the guard tokens and `SampleCode`, run the Architect and Requirement test projects to confirm they are unchanged and green, and confirm `git diff` shows no edited line in any existing test file beyond the approved list (T005, T029).
- [X] T049 [USER] Open the constitution amendment PR (drafted at T002; the user confirms before it is opened). It must merge before the slice PR.
- [ ] T050 [USER] Commit on `feature/developer-agent-proposals` and open the slice PR after the user confirms; the PR description carries the constitution checkbox (v1.0.5, amendment PR linked), the baseline and unchanged-behaviour comparison, the T006, T007 and T029 diff summaries, and states plainly that the live check was not run.

---

## Dependencies and Execution Order

- Phase 1: T001 gates everything. T003 to T005 run before any code change (baseline); T005 is also a user gate for the additive test edits. T002 is independent.
- Phase 2 (T006 to T010) blocks all tests and stories. T006 and T007 are parallel and include the T005-approved additive edits to enumerating tests, so T010 can be green; T008 then T009; T010 last.
- Phase 3 tests T011 to T026 are mostly parallel (new files); the shared `ProposalEndpointTests.cs` cases (T017, T018, T020, T022) are written in one pass by one person or in order. T027 needs all of them; T028 is a human gate; T029 only after approval. Nothing in Phases 4 to 10 starts before T028.
- Phase 4: T030 independent; T031 independent of T030; T032 and T033 parallel; T034 needs T030, T032, T033; T035 needs T031 and T034; T036 independent of T035; T037 needs T035 and T036.
- Phases 5 to 9 follow Phase 4. Phases 5, 6, 7 and 9 are independent of each other; Phase 8 needs T037 (registration).
- Phase 10 after all stories. T049 must merge before T050's PR merges.

### Parallel Opportunities

- Phase 2: T006 with T007.
- Phase 3: T011 to T016, T019, T021, T023 to T025 together (separate files).
- Phase 4: T030 with T031; T032 with T033.
- Phases 5, 6, 7, 9 once Phase 4 is done.

## Implementation Strategy

### MVP

MVP is US1 plus the US5 regression check (T041 to T042): an operator can switch tools on, request a verified proposal that is stored and audited, and unused behaviour is provably identical. Stop and validate there, then US2, US3, US4 and US6 harden retrieval, refusals, rejection and hostile content.

### Incremental delivery

1. Setup, baselines, amendment draft, stubs (compiling, all existing tests green).
2. Tests written red and approved once.
3. Verifier and service (US1), then retrieval (US2), refusals (US3), rejection (US4).
4. Workflow offer and byte-identical proof (US5), hostile content (US6), polish, amendment PR, slice PR.

### Notes

- Never edit an existing assertion, recording or golden file except the additive list approved at T028.
- The audit gap for failed requests is a known consequence of the spec (tool calls are audited only with a stored proposal); do not "fix" it without a spec change.
- Follow-up, not in this slice: extract the shared tool-call audit mapping once a shared home exists.

## Implementation notes (recorded during T011 to T027)

- The endpoint tests for T017, T018, T020 and T022 were split into separate files instead of one
  `ProposalEndpointTests.cs`: `ProposalEndpointTests.cs` (request and retrieval), `ProposalRefusalEndpointTests.cs`
  and `ProposalFailureEndpointTests.cs`.
- The storage round trip (T018) lives in `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalStorageRoundTripTests.cs`
  (that project references Persistence), so no existing Persistence test project file is changed.
- The "outdated design through the real flow" composition case was dropped: a second requirements draft
  cannot be made once a request is Approved, so it is covered with a fake basis instead.
- Availability order: tools switched off is reported before model settings are read.

## Phase 11: Convergence

- [X] T051 Log, for every proposal request that stores nothing, one counts-and-category line (failure category or rejection reason name, never a path, quote, model text or setting value) in `src/AgenticChangeOps.Api/Endpoints/ProposalEndpoints.cs` (or the proposal service result mapping), and add a test in `tests/AgenticChangeOps.Api.Tests/ProposalFailureEndpointTests.cs` (new file beside it if that file stays unedited) that the line appears for a rejected proposal and a model failure and contains none of the forbidden markers, per contracts/settings-and-failures.md "Logging" and research R5 (partial)
- [X] T052 Add a test in `tests/AgenticChangeOps.Agents.Developer.Tests/Unit/ProposalGeneratorIntegrationTests.cs` (or a new file beside it) that the tool session's `MaxModelCalls` and `TimeLimit` reach the model call for each proposal request and that a second request starts from a fresh session, per FR-012 (partial)
