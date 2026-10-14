# Tasks: Architect Agent on the Agent Framework

**Input**: Design documents from `specs/011-architect-agent-framework/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). The new tests are shown red against a signature-only stub that throws `NotImplementedException` and approved (T017) before the stub is filled in. Red means failing for the right reason: behaviour, not missing types.

**Constitution**: [x] Reviewed against constitution v1.0.3. See plan.md Constitution Check; Principle VII flag recorded in Complexity Tracking.

**Organization**: Grouped by user story (US1 to US5 from spec.md). US1 carries the swap; US2 proves nothing observable changed; US3 proves containment and statelessness; US4 and US5 are the written comparison and the constitution note.

**Rules that hold for every task**:
- Permitted production edits: `src/AgenticChangeOps.ModelAccess` (new client, new probe helper, package references, an internal constructor overload on the hand-written client, delegating the content-filter probe), `src/AgenticChangeOps.Agents.Architect/AzureOpenAIDesignGenerator.cs` (`FromProcessEnvironment` only), `src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs` (`CreateDesignSetup` only). Nothing else in `src` changes. In particular no edit to the Requirement agent, Intake, Workflow, any prompt builder, parser, validator, content or record type.
- No assertion in an existing test is edited and no existing test file is edited. New tests go in new files. If an existing test fails, fix the new code, never the assertion. Existing goldens are never rewritten.
- Framework types appear only in `src/AgenticChangeOps.ModelAccess/AgentFrameworkChatModelClient.cs` (and the ModelAccess project file for packages).
- Synthetic data only; no endpoint, deployment name, key or real name in any file (Principle I). Use the sentinel strings the existing tests already use.
- No new setting, endpoint, project or record shape.
- After every task that touches code, `dotnet build AgenticChangeOps.sln` must succeed; after every phase, `dotnet test AgenticChangeOps.sln` must be green (except the deliberately red state at T016).
- T005 (internal constructor overload) and T008 (package add) are enabling, behaviour-neutral changes made before the T017 gate on purpose: the wire baseline must be captured on unchanged behaviour. Their green results are shown to the user at T017 together with the red tests.
- Where research says "verify at implementation" (R-003, R-004, R-005, R-006, R-008, R-009) and a finding blocks a category or guarantee, **stop and record it for the user** (spec FR-018, FR-019); do not work around it silently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task). Tasks editing the same file are never `[P]`.
- **[Story]**: US1 to US5, from spec.md.
- **[USER]**: outward-facing step (push, PR). Claude drafts, the user confirms.

---

## Phase 0: Gate

- [X] T000 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md are approved. No test or source file is written before this (Principle V).

## Phase 1: Setup (before any code moves)

- [X] T001 Confirm branch `feature/agent-framework-architect` and a clean tree apart from `specs/011-architect-agent-framework/`. Run `dotnet test AgenticChangeOps.sln` and save the per-project totals (and the skipped live checks) to `specs/011-architect-agent-framework/baseline-tests.txt`, in the same layout as `specs/009-shared-agent-plumbing/baseline-tests.txt`. This is the SC-001 reference.
- [X] T002 Record the exact versions in `baseline-tests.txt` header: `Azure.AI.OpenAI` 2.1.0 and the resolved `OpenAI` version from `dotnet list src/AgenticChangeOps.ModelAccess package --include-transitive`. This is the "before" for the R-003 package-lift check.

## Phase 2: Foundational (blocks every story): wire baseline of the hand-written client

**Purpose**: nothing today records what the hand-written client puts on the wire. Capture it on the unchanged packages so the framework client and the package lift can both be compared to it.

- [X] T003 [P] Create `tests/AgenticChangeOps.ModelAccess.Tests/Fakes/RecordingTransport.cs`: a `PipelineTransport` (System.ClientModel) that records each request (method, path, headers, body text) into a list, increments a call counter with `Interlocked`, and returns the next scripted response (status, body, optional delay or thrown exception). Helpers: `ReturnsCompletion(text, finishReason)`, `ReturnsStatus(code, body)`, `Throws(exception)`, `Requests`, `CallCount`.
- [X] T004 [P] Create `tests/AgenticChangeOps.ModelAccess.Tests/Fakes/FakeTokenCredential.cs`: a `TokenCredential` returning a fixed synthetic token with a far-future expiry and counting calls. No real token shape or secret.
- [X] T005 Add to `src/AgenticChangeOps.ModelAccess/AzureOpenAIChatModelClient.cs` an `internal` constructor `(AzureOpenAIGeneratorOptions options, AzureOpenAIClientOptions clientOptions, TokenCredential credential)` that the public constructor now delegates to with its current values (`RetryPolicy = new ClientRetryPolicy(0)`, `DefaultAzureCredential`). Add `InternalsVisibleTo("AgenticChangeOps.ModelAccess.Tests")` in the ModelAccess project file. Behaviour of the public constructor must be identical; `dotnet test` stays green.
- [X] T006 Create `tests/AgenticChangeOps.ModelAccess.Tests/HandWrittenClientWireBaselineTests.cs`: using `RecordingTransport` and `FakeTokenCredential`, run `AzureOpenAIChatModelClient.CompleteAsync` with a fixed synthetic system instruction, user content and token cap, and write the single recorded request body (property names and values, message list, response format, absence of tools) to `tests/AgenticChangeOps.ModelAccess.Tests/golden/hand-written-wire.txt` through the existing golden helper pattern. Headers are recorded by name only. The test asserts exactly one request.
- [X] T007 On the **unchanged packages** run `AGENTICCHANGEOPS_UPDATE_BASELINE=1 dotnet test tests/AgenticChangeOps.ModelAccess.Tests --filter HandWrittenClientWireBaseline` to write the golden, then run it again without the variable and confirm green and byte-stable. Record the commit hash in `baseline-tests.txt`.
- [X] T008 (gate passed after user chose Azure.AI.OpenAI 2.9.0-beta.1, see baseline-tests.txt) Add `Microsoft.Agents.AI` and `Microsoft.Agents.AI.OpenAI` (pin the latest stable that has a net8.0 target; 1.24.0 at plan time) to `src/AgenticChangeOps.ModelAccess/AgenticChangeOps.ModelAccess.csproj`. Run `dotnet build` then `dotnet test AgenticChangeOps.sln`. **R-003 gate**: the full suite and `HandWrittenClientWireBaseline` must be green with the golden **unchanged**. Record the resolved `OpenAI` version in `baseline-tests.txt`. If anything differs, stop and report to the user.

**Checkpoint**: packages added, nothing behaves differently, no framework type used yet.

---

## Phase 3: Tests first (Priority: P1) and stub

**Goal**: every behaviour of the framework client is specified by a failing test before it is built.

- [X] T009 [P] [US1] Create `tests/AgenticChangeOps.ModelAccess.Tests/FrameworkClientWireParityTests.cs` per `contracts/wire-parity.md`: run both clients with the same inputs through `RecordingTransport`; assert one request each, same method and operation, same `messages` (count, order, roles, text verbatim), same response format, no `tools`/`tool_choice`/`functions`, same output-token value. The token-cap property name and header differences are written to `tests/AgenticChangeOps.ModelAccess.Tests/golden/framework-wire-diff.txt` so they can be copied into `comparison.md`; any other difference fails the test. Also assert keyless sign-in on both clients: a bearer `Authorization` header is present, no `api-key` header is sent, and the fake credential was asked for a token once per call.
- [X] T010 [P] [US1] Create `tests/AgenticChangeOps.ModelAccess.Tests/FrameworkClientResultTests.cs`: normal completion returns the text and `WasCutOff: false`; finish reason Length returns `WasCutOff: true`; finish reason ContentFilter throws `DraftGenerationException(MalformedOutput)`; empty text returns an empty string; text returned verbatim with no trimming.
- [X] T011 [P] [US1] Create `tests/AgenticChangeOps.ModelAccess.Tests/FrameworkClientFailureTests.cs` per `contracts/framework-client.md`: HTTP 408 → TimedOut; 400 with a body naming `content_filter` → MalformedOutput; 401, 403, 404, 429, 500, 503 → Unreachable; transport throws `HttpRequestException` → Unreachable; transport throws `TimeoutException` → TimedOut; transport throws a custom exception whose message contains the sentinel endpoint → Unreachable with no message, no inner exception and no sentinel in `ToString()`; cancellation by the caller propagates as `OperationCanceledException`. Where the framework wraps SDK exceptions the test must still expect the same category (R-008).
- [X] T012 [P] [US3] Create `tests/AgenticChangeOps.ModelAccess.Tests/FrameworkClientOneCallTests.cs`: for each of status 200, 429, 500, 503 and a thrown transport exception, assert `CallCount == 1` and no sleep or delay beyond the transport's own (R-004); assert the request body contains no `tools` property (R-005).
- [X] T013 [P] [US3] Create `tests/AgenticChangeOps.ModelAccess.Tests/FrameworkClientStatelessTests.cs`: make two `CompleteAsync` calls on one client instance with different instructions and user content; assert the second request body contains none of the first request's text and equals the body of the same call made on a fresh client instance; assert both succeed independently; run 20 parallel calls and assert every body contains only its own text (R-005).
- [X] T014 [P] [US3] Create `tests/AgenticChangeOps.ModelAccess.Tests/FrameworkClientNoLeakTests.cs`: using a captured logger provider and a failing transport whose messages contain the sentinel endpoint, deployment name and a sentinel answer text, assert none appear in any thrown exception (message, `ToString`, inner exceptions, `Data`) or captured log line (FR-008, R-011).
- [X] T015 [P] [US3] Create `tests/AgenticChangeOps.ModelAccess.Tests/FrameworkContainmentTests.cs` per `contracts/dependency-rules.md` D1 to D3: scan `src` for `Microsoft.Agents` and `Microsoft.Extensions.AI` and assert they appear only in `AgentFrameworkChatModelClient.cs`; scan every `*.csproj` and assert the two framework packages appear only in the ModelAccess project file. Also assert that exactly one source file uses the framework namespaces and that it is `AgentFrameworkChatModelClient.cs`, so the test is red until the client exists.
- [X] T015a [P] [US1] Create `tests/AgenticChangeOps.ModelAccess.Tests/FrameworkClientThroughCallerTests.cs`: drive `ModelCaller` over `AgentFrameworkChatModelClient` and `RecordingTransport` (FR-007, SC-004). Cases: a response slower than the configured time limit → `TimedOut`; text longer than `MaxResponseCharacters` → `ResponseTooLarge`; finish reason Length → `ResponseTooLarge`; blank text → `MalformedOutput`; caller cancellation propagates as cancellation, not TimedOut; a failure status produces exactly one request. Each case also asserts `CallCount == 1` and that the output-token cap sent equals `ceil(MaxResponseCharacters / 3)`.
- [X] T016 [US1] Create signature-only stubs in `src/AgenticChangeOps.ModelAccess/`: `AgentFrameworkChatModelClient : IChatModelClient` with a public constructor `(AzureOpenAIGeneratorOptions options)`, an `internal` constructor `(AzureOpenAIGeneratorOptions, AzureOpenAIClientOptions, TokenCredential)`, and `CompleteAsync` throwing `NotImplementedException`; and `ContentFilterProbe` with `public static bool ResponseMentions(ClientResultException)` throwing `NotImplementedException`. Run `dotnet test` and confirm T009 to T015a fail for the right reason (`NotImplementedException`, or the containment test failing only because the file has no framework use yet) and every older test is still green. Paste the red output to the user.
- [X] T017 **GATE (human approval)**: the user reviews the red tests and replies "approved". Nothing in T018 onward starts before this.

## Phase 4: User Story 1: Architect call runs through the framework (Priority: P1) 🎯 MVP

**Goal**: the Architect agent's model call goes through the framework with identical behaviour.

**Independent Test**: T009 to T014 and T015a green; the existing Architect and Api suites green with unchanged assertions.

- [X] T018 [US1] Implement `ContentFilterProbe.ResponseMentions` in `src/AgenticChangeOps.ModelAccess/ContentFilterProbe.cs`: move the body check from `AzureOpenAIChatModelClient.ResponseMentionsContentFilter` verbatim (same marker, case-insensitive, swallow read errors and return false). Change `AzureOpenAIChatModelClient` to call it and delete its private copy. `dotnet test` stays green, `HandWrittenClientWireBaseline` byte-identical.
- [X] T019 [US1] Implement `AgentFrameworkChatModelClient` in `src/AgenticChangeOps.ModelAccess/AgentFrameworkChatModelClient.cs` per `contracts/framework-client.md` and research R-001, R-004 to R-009: build the Azure client with `RetryPolicy = new ClientRetryPolicy(0)` and `DefaultAzureCredential` (public constructor) or the injected pair (internal constructor); get the deployment's chat client and bridge it to the framework chat-client abstraction with **no middleware**; create one agent with no tools; per call pass the system instruction as the agent's instructions and the user content as the sole user message, no session, JSON-object response format and the given output-token cap; read text and finish reason; map failures with the exact ladder used by the hand-written client (`DraftGenerationException` passthrough, cancellation rethrown, `ClientResultException` by status and `ContentFilterProbe`, everything else through `ModelFailureMapper.FromException`, nothing attached). If the framework wraps SDK exceptions, unwrap by inspecting inner exceptions for a status without keeping any message. Do not catch-and-retry anywhere.
- [X] T020 [US1] Run `dotnet test tests/AgenticChangeOps.ModelAccess.Tests`. T009 to T015a must be green. Any parity difference beyond the allowed list, any retry, any retained state, or any category that cannot be preserved is a stop-and-report (FR-018, FR-019). Copy the observed differences from `golden/framework-wire-diff.txt` into a scratch list for T031.
- [X] T021 [US1] Edit `AzureOpenAIDesignGenerator.FromProcessEnvironment` in `src/AgenticChangeOps.Agents.Architect/AzureOpenAIDesignGenerator.cs` to construct `new AgentFrameworkChatModelClient(options)` in place of `new AzureOpenAIChatModelClient(options)`. No other line of the file changes; update its summary comment to mention the framework.
- [X] T022 [US1] Edit `CreateDesignSetup` in `src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs` the same way. `CreateDraftingSetup` must stay byte-identical (confirm with `git diff`).
- [X] T023 [US1] Run `dotnet build AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln`. Everything green; compare per-project totals to `baseline-tests.txt` (existing counts unchanged, only new ModelAccess tests added).

**Checkpoint**: MVP. The Architect path runs on the framework; every test green.

---

## Phase 5: User Story 2: Callers see no difference (Priority: P1)

**Goal**: prove status codes, bodies, headers and error codes are the same.

**Independent Test**: the existing Api golden transcripts pass byte-for-byte.

- [X] T024 [US2] Run `dotnet test tests/AgenticChangeOps.Api.Tests` and confirm `SharedPlumbingBaselineTests` and `CallerCancellationBaselineTests` pass with their existing goldens and no `AGENTICCHANGEOPS_UPDATE_BASELINE` set. Confirm with `git status` that no file under `tests/**/golden` changed.
- [X] T025 [US2] Confirm no existing test file changed: `git diff --stat main -- tests` lists only added files. Record the output in `comparison.md` working notes.
- [X] T026 [P] [US2] Confirm the Requirement agent, Intake and Workflow are untouched: `git diff --stat main -- src/AgenticChangeOps.Agents.Requirement src/AgenticChangeOps.Intake src/AgenticChangeOps.Workflow` is empty (FR-011, D5, D6). Also confirm `git diff --stat main --` over `IDesignDraftGenerator.cs`, `DesignResponseParser.cs`, `DesignPromptBuilder.cs`, `DesignContentValidator.cs` and `IChatModelClient.cs` is empty (FR-005, FR-006, FR-012).

## Phase 6: User Story 3: Contained and stateless (Priority: P1)

**Goal**: the guarantees that keep decisions the system's own.

**Independent Test**: T012 to T015 green; the Architect project file unchanged.

- [X] T027 [US3] Confirm `src/AgenticChangeOps.Agents.Architect/AgenticChangeOps.Agents.Architect.csproj` is unchanged and the Architect `StructureTests` pass unchanged (D4).
- [X] T028 [US3] Search `src` for `Microsoft.Agents` and `Microsoft.Extensions.AI`; confirm exactly one source file matches and record the file list in `comparison.md` notes (SC-005).
- [X] T029 [US3] Confirm no tool, handoff, workflow, memory, session or telemetry registration exists in `AgentFrameworkChatModelClient.cs` by reading the file, and record the confirmation in `comparison.md` notes (FR-003, FR-004, FR-005).

## Phase 7: User Story 4: The comparison is written down (Priority: P2)

- [ ] T030 [US4] Run the Architect live check if the user has configured their own model access (`[USER]`: the user sets the existing model variables and opt-in switch and runs `dotnet test tests/AgenticChangeOps.Agents.Architect.Tests --filter LiveDesignCheck`). Record "run" with the outcome, or "not run", in `comparison.md`. As in slices 004 to 010, "not run" is recorded and does not block the slice; SC-003 is then reported as unverified, not met. Never record endpoint, deployment or model text.
- [X] T031 [US4] Create `specs/011-architect-agent-framework/comparison.md` with four sections: **What the framework replaced** (the hand-written request construction and response reading in `AzureOpenAIChatModelClient` for the Architect path), **What it added** (agent abstraction, package and dependency footprint including the `OpenAI` lift with before and after versions, any default behaviours disabled), **What had to be kept** (`ModelCaller`, `ModelFailureMapper`, `ModelAnswerReader`, `DesignResponseParser`, retry-off policy, keyless sign-in, content-filter probe, exception unwrapping if used), **Behaviour that differed** (each wire difference from T020 with its observed value, and any category or limit that needed a decision; "none" only if the diff file is empty). Add the evidence table: test names for one-call, no-retry, no-state, no-tools, no-leak.
- [X] T032 [US4] List every unavoidable difference in the instructions or requested structure in `comparison.md` explicitly (FR-009), or state that the diff file shows none beyond the allowed list.

## Phase 8: User Story 5: Constitution note (Priority: P3)

- [ ] T033 [US5] Depends on T020 and T029: the wording states only facts those tasks confirmed, so it may be drafted earlier but is finalised after them. On a separate branch `docs/constitution-1-0-4` cut from `main`, amend `.specify/memory/constitution.md` Technology Stack note per research R-010 (framework is Microsoft Agent Framework, used for the Architect agent's single stateless tool-less generation call behind `IChatModelClient`; routing stays deterministic C#; why: study and compare on one agent), bump `Version` to 1.0.4 and `Last Amended` to the merge date, and record the rationale in the PR body. `[USER]`: push and open the PR; it merges before the slice PR.

## Phase 9: Polish & cross-cutting

- [X] T034 [P] Run the secret and synthetic-data scans the project already has (`NoSecretsTests`, CI scan) over `src`, `tests`, `specs`; confirm green and no endpoint, deployment name, key or tenant string in the new files.
- [X] T035 [P] Update `README.md` only if it describes the Architect model path; one line stating that the Architect agent's model call runs through the Microsoft Agent Framework and why.
- [X] T036 Run the full `quickstart.md` validation (steps 1 to 5) and tick every item; save the final per-project totals next to the baseline in `baseline-tests.txt`.
- [X] T037 Update `checklists/requirements.md` Notes with the final status and set spec.md Status to Implemented.
- [ ] T038 `[USER]` Commit with message `feat(011): run the Architect model call through the Microsoft Agent Framework`, push `feature/agent-framework-architect` and open the slice PR referencing the constitution PR. Claude drafts; the user confirms each outward-facing step.

---

## Dependencies & Execution Order

- Phase 0 gate, then Phase 1, then Phase 2 (T003 and T004 parallel; T005 before T006; T007 before T008).
- Phase 3: T009 to T015a are parallel (separate new files); T016 depends on them; T017 gates all implementation.
- Phase 4: T018 → T019 → T020 → T021 → T022 → T023.
- Phases 5, 6 depend on Phase 4; T024, T025, T026 and T027 to T029 can run in parallel.
- Phase 7 depends on Phases 4 to 6; T030 needs the user's own model access and may be recorded "not run".
- Phase 8 (constitution PR) may be drafted after T000 but is finalised after T020 and T029 so its wording matches what was verified; it must merge before the slice PR.
- Phase 9 last.

## Parallel Example

```text
T003 + T004      (recording transport and fake credential)
T009 .. T015a    (eight new test files)
T024 + T025 + T026
T034 + T035
```

## Implementation Strategy

1. MVP is Phases 0 to 4: the swap with the full parity, one-call and failure test set green.
2. Then prove no external difference (Phase 5) and containment (Phase 6).
3. Then record the comparison and constitution note, and open the PRs.
4. Stop and ask the user whenever a "verify at implementation" finding cannot be satisfied.
