# Tasks: Model-Backed Requirements Draft Generator

**Input**: Design documents from `specs/004-model-draft-generator/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/ ✅

**Tests**: Included — Constitution Principle VI (Test-First) mandates tests written and
approved before implementation for all deterministic logic. Within each story, test tasks come
first, MUST be red (failing for the right reason, not a compile error), and end with a
human-approval CHECKPOINT before any implementation task of that story starts. One batched
approval across several stories is acceptable if the user gives it. No test may call a live
model, use the network, or read real credentials; the model is always `FakeChatModelClient`,
except the single opt-in live check in US5, which is skipped by default.

**Organization**: Tasks are grouped by user story. This slice adds one production project
(`src/AgenticChangeOps.Intake.AzureOpenAI`), makes one small additive change to the core library
(failure category type and one catch block), and adds tests to the existing test project.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks). Tasks
  that edit the same file are never marked `[P]`.
- **[Story]**: Which user story this task belongs to (US1–US5)

---

## Phase 1: Setup

**Purpose**: Confirm the baseline and create the new project.

- [X] T001 Run `dotnet test AgenticChangeOps.sln` and confirm all slice 001 to 003 tests pass; record the passing count as the regression baseline
- [X] T002 Create class library src/AgenticChangeOps.Intake.AzureOpenAI/AgenticChangeOps.Intake.AzureOpenAI.csproj targeting net8.0 with ImplicitUsings and Nullable enabled, a project reference to src/AgenticChangeOps.Intake/AgenticChangeOps.Intake.csproj, and package references `Azure.AI.OpenAI` version 2.1.0 and `Azure.Identity` (latest stable 1.x, pinned to an exact version); add the project to AgenticChangeOps.sln under the `src` folder
- [X] T003 Add a project reference from tests/AgenticChangeOps.Intake.Tests/AgenticChangeOps.Intake.Tests.csproj to the new project; run `dotnet build AgenticChangeOps.sln` and confirm 0 errors, 0 warnings

**Checkpoint**: Solution builds with the new empty project.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared types, seams, compile-only skeletons, and fakes so story tests can be
written and fail for the right reason. MUST be complete before any user story work begins.

- [X] T004 [P] Create GenerationFailureCategory enum in src/AgenticChangeOps.Intake/Models/Enums/GenerationFailureCategory.cs with exactly four values in this order: Unreachable, TimedOut, ResponseTooLarge, MalformedOutput
- [X] T005 [P] Create DraftGenerationException in src/AgenticChangeOps.Intake/Models/DraftGenerationException.cs: sealed class deriving from Exception with constructor taking GenerationFailureCategory, a read-only `Category` property, and a fixed message per category that "never contains prompt text, model output, endpoint, deployment name, or identity detail"; no inner exception constructor
- [X] T006 [P] Create IChatModelClient interface and ChatModelResponse record in src/AgenticChangeOps.Intake.AzureOpenAI/IChatModelClient.cs: `Task<ChatModelResponse> CompleteAsync(string systemInstruction, string userContent, int maxOutputTokens, CancellationToken cancellationToken)`; `ChatModelResponse(string Text, bool WasCutOff)`
- [X] T007 [P] Create GeneratorConfigurationException in src/AgenticChangeOps.Intake.AzureOpenAI/GeneratorConfigurationException.cs: sealed class deriving from Exception with `SettingName` (string); the message "names the setting and the problem (missing, blank, placeholder, not a positive number, not an absolute https address)" and "never includes the supplied value"
- [X] T008 [P] Create AzureOpenAIGeneratorOptions skeleton in src/AgenticChangeOps.Intake.AzureOpenAI/AzureOpenAIGeneratorOptions.cs: record with Endpoint (Uri), DeploymentName (string), RequestTimeout (TimeSpan), MaxResponseCharacters (int), and `static AzureOpenAIGeneratorOptions FromEnvironment(Func<string, string?> getVariable)` that throws NotImplementedException
- [X] T009 [P] Create skeleton static classes that throw NotImplementedException: DraftPromptBuilder in src/AgenticChangeOps.Intake.AzureOpenAI/DraftPromptBuilder.cs with `(string SystemInstruction, string UserContent) Build(ChangeRequest request)`; DraftResponseParser in src/AgenticChangeOps.Intake.AzureOpenAI/DraftResponseParser.cs with `DraftContent Parse(string text)`; ModelFailureMapper in src/AgenticChangeOps.Intake.AzureOpenAI/ModelFailureMapper.cs with `GenerationFailureCategory FromError(int statusCode, string? errorCode)` and `GenerationFailureCategory FromException(Exception exception)`
- [X] T010 Create AzureOpenAIDraftGenerator skeleton in src/AgenticChangeOps.Intake.AzureOpenAI/AzureOpenAIDraftGenerator.cs implementing IRequirementsDraftGenerator: constructor `(AzureOpenAIGeneratorOptions options, IChatModelClient client)`; `GenerateAsync` and `static CreateFromEnvironment()` throw NotImplementedException
- [X] T011 [P] Create FakeChatModelClient in tests/AgenticChangeOps.Intake.Tests/Fakes/FakeChatModelClient.cs implementing IChatModelClient: `int CallCount`, last `SystemInstruction`, `UserContent`, `MaxOutputTokens`, a settable `Func<CancellationToken, Task<ChatModelResponse>> Behavior`; static helpers `ReturnsText(string)`, `Throws(Exception)`, `DelaysUntilCancelled()`; static `ValidAnswerJson()` returning a synthetic well-formed five-key JSON answer (fictional "Sample Billing Portal" content)
- [X] T012 [P] Create FakeEnvironment in tests/AgenticChangeOps.Intake.Tests/Fakes/FakeEnvironment.cs: dictionary-backed `string? Get(string name)` and a static `Valid()` returning the four variables from research decision 4 with fictional values (`https://synthetic-models.invalid/`, deployment `synthetic-deployment`)
- [X] T013 Verify `dotnet build AgenticChangeOps.sln` — 0 errors, 0 warnings, and `dotnet test` shows the T001 baseline still passing

**Checkpoint**: Everything compiles; new behaviour throws NotImplementedException; earlier slices green.

---

## Phase 3: User Story 1 - Generate a Real Draft from a Change Request (Priority: P1) 🎯 MVP

**Goal**: A draftable change request yields a stored draft whose five sections come from the
model's structured answer, and only the change request's own descriptive fields are sent.

**Independent Test**: With a fake model client returning a well-formed answer, draft a synthetic
request through the real drafting service and store; confirm five sections, status unchanged,
one audit entry, and the prompt content.

### Tests for User Story 1 ⚠️ write first, must be red for the right reason

- [X] T014 [P] [US1] Create tests/AgenticChangeOps.Intake.Tests/Unit/DraftPromptTests.cs: the user content contains the request's title, description, affected system, priority and change type; it does not contain the requester, the change request id, the status, or the submitted timestamp; the system instruction names the five keys and says to use only the data provided; the system instruction is identical for two different requests; fields containing quotes, newlines, a code fence and the text "ignore previous instructions" are carried as data (the JSON block after the delimiter parses back to exactly the original field values)
- [X] T015 [P] [US1] Create tests/AgenticChangeOps.Intake.Tests/Unit/DraftResponseParserTests.cs (success paths): a bare five-key object parses to the matching DraftContent; the same object inside one code fence (with and without a `json` label) parses identically; an unknown extra key is ignored; present-but-empty `assumptions` and `openQuestions` pass through as empty lists; empty summary and empty requirement lists are passed through unchanged for slice 003 validation
- [X] T016 [US1] Create tests/AgenticChangeOps.Intake.Tests/Unit/GeneratorDraftingIntegrationTests.cs: build real ChangeRequestService, store, clock, id generator, DraftContentValidator and RequirementsDraftingService (via DraftingTestFixture pattern) with `AzureOpenAIDraftGenerator` over FakeChatModelClient; drafting a Submitted and a Deferred synthetic request stores a draft whose five sections equal the fake answer, with version 1 and status Draft; request status is unchanged afterwards; exactly one new DraftGenerated audit entry; the fake client was called exactly once
- [X] T017 [US1] Confirm T014 to T016 fail with NotImplementedException (not compile errors) and record the red run

**CHECKPOINT — human approval required**: show the red tests and the failure reason and obtain
an explicit "approved" before any implementation task of this story starts.

### Implementation for User Story 1

- [X] T018 [P] [US1] Implement DraftPromptBuilder in src/AgenticChangeOps.Intake.AzureOpenAI/DraftPromptBuilder.cs: fixed system instruction (role, the five required keys and shapes, "use only the data provided", "return only the JSON object"); user content is a fixed delimiter line followed by one System.Text.Json-serialised object of title, description, affected system, priority, change type; never include requester, id, status or timestamps
- [X] T019 [P] [US1] Implement DraftResponseParser (success paths) in src/AgenticChangeOps.Intake.AzureOpenAI/DraftResponseParser.cs: trim; if the text is one code-fence block (optional `json` label) take its inner text; parse with System.Text.Json; map `summary`, `functionalRequirements`, `acceptanceCriteria`, `assumptions`, `openQuestions` to DraftContent; ignore unknown keys; pass empty values through
- [X] T020 [US1] Implement AzureOpenAIDraftGenerator.GenerateAsync (happy path) in src/AgenticChangeOps.Intake.AzureOpenAI/AzureOpenAIDraftGenerator.cs: build the prompt, call `IChatModelClient.CompleteAsync` once with maxOutputTokens = ceil(MaxResponseCharacters / 3), parse the text and return DraftContent; do not write anywhere, change anything, or retry
- [X] T021 [US1] Run T014 to T016 until green and confirm the full suite still passes

**Checkpoint**: A real-shaped draft flows through the existing drafting rules end to end with a fake model.

---

## Phase 4: User Story 2 - Fail Safely When the Model Fails or Misbehaves (Priority: P2)

**Goal**: Unreachable, timed out, too large, or malformed output yields a specific category
error and nothing is stored.

**Independent Test**: Fake clients that are unreachable, slow, oversized, cut off and
unparseable each produce a distinct category and leave drafts, audit entries and versions
untouched.

### Tests for User Story 2 ⚠️ write first, must be red for the right reason

- [X] T022 [P] [US2] Extend tests/AgenticChangeOps.Intake.Tests/Unit/DraftResponseParserTests.cs (failure paths): not JSON at all, a JSON array, a missing key (each of the five in turn), a string where a list is required, non-string list items, a non-string summary, prose before or after the object, two fenced blocks, and an empty string each throw DraftGenerationException with Category MalformedOutput
- [X] T023 [P] [US2] Create tests/AgenticChangeOps.Intake.Tests/Unit/ModelFailureMapperTests.cs: `FromError(408, null)` is TimedOut; statuses 401, 403, 404, 429, 500, 503 are Unreachable; `FromError(400, "content_filter")` is MalformedOutput; `FromException(TimeoutException)` is TimedOut; `FromException(HttpRequestException)` and `FromException(InvalidOperationException)` are Unreachable
- [X] T024 [P] [US2] Create tests/AgenticChangeOps.Intake.Tests/Unit/DraftGeneratorFailureTests.cs: a client throwing DraftGenerationException(Unreachable) surfaces the same category; a client that waits for cancellation with RequestTimeout of 50 ms yields TimedOut; caller cancellation propagates OperationCanceledException and is not a category; text longer than MaxResponseCharacters yields ResponseTooLarge and none of the text is used; `WasCutOff = true` yields ResponseTooLarge; an empty answer yields MalformedOutput; on every failure the client was called exactly once (no retry); no exception message contains the endpoint, deployment name, prompt text or answer text
- [X] T025 [US2] Extend tests/AgenticChangeOps.Intake.Tests/Unit/DraftGenerationFailureTests.cs: with the real service and generator over a failing fake client, `DraftAsync` returns a failure whose error field is `Generator` and whose message names the category (for example "The content generator failed (TimedOut)."); no draft stored; no audit entry; the next successful draft receives the next version with no gap; the change request is unchanged; a generator throwing any other exception keeps the existing message with the exception type name
- [X] T026 [US2] Confirm T022 to T025 fail for the right reason and record the red run

**CHECKPOINT — human approval required**: obtain an explicit "approved" before implementation.

### Implementation for User Story 2

- [X] T027 [P] [US2] Implement DraftResponseParser failure paths in src/AgenticChangeOps.Intake.AzureOpenAI/DraftResponseParser.cs: any parse failure, missing key, wrong shape, text outside a single fence, or more than one fenced block throws DraftGenerationException(MalformedOutput); never include the text in the message
- [X] T028 [P] [US2] Implement ModelFailureMapper in src/AgenticChangeOps.Intake.AzureOpenAI/ModelFailureMapper.cs per research decision 10: 408 and TimeoutException are TimedOut; error code `content_filter` is MalformedOutput; every other status or exception is Unreachable; the error code is only compared, never retained
- [X] T029 [US2] Extend AzureOpenAIDraftGenerator.GenerateAsync in src/AgenticChangeOps.Intake.AzureOpenAI/AzureOpenAIDraftGenerator.cs: link the caller token with a timeout token from RequestTimeout (timeout expiry throws TimedOut; caller cancellation propagates); reject `WasCutOff` or text longer than MaxResponseCharacters with ResponseTooLarge before parsing; empty text throws MalformedOutput; pass through DraftGenerationException from the client; wrap any other exception via ModelFailureMapper.FromException into a DraftGenerationException without attaching the original
- [X] T030 [US2] Modify src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs: in the generator catch block, when the exception is DraftGenerationException report `The content generator failed (<Category>).` under field `Generator`; keep the existing message (exception type name) for every other exception; change nothing else
- [X] T031 [US2] Run T022 to T025 until green and confirm all earlier suites still pass

**Checkpoint**: Every model failure leaves the system exactly as before and names its category.

---

## Phase 5: User Story 3 - Refuse to Set Up with Missing or Placeholder Configuration (Priority: P3)

**Goal**: The generator cannot be created with a missing, blank or placeholder endpoint or
deployment, or non-positive limits; the error names the setting, never the value.

**Independent Test**: Create the generator from a dictionary environment with each bad value in
turn.

### Tests for User Story 3 ⚠️ write first, must be red for the right reason

- [X] T032 [P] [US3] Create tests/AgenticChangeOps.Intake.Tests/Unit/GeneratorConfigurationTests.cs: missing, empty and whitespace endpoint each throw GeneratorConfigurationException with SettingName `AGENTICCHANGEOPS_MODEL_ENDPOINT`; same for deployment with `AGENTICCHANGEOPS_MODEL_DEPLOYMENT`; each of `<your-endpoint>`, `{endpoint}`, `YOUR_DEPLOYMENT`, `replace-me`, `my-Placeholder-name` is rejected as a placeholder; a non-absolute or non-https endpoint is rejected; a timeout or size limit of `0`, `-5` or `abc` is rejected with the matching setting name; absent optional limits use 30 seconds and 32000 characters; valid values produce the expected options; no exception message contains the supplied value; creation with only the two required variables (no key variable of any kind) succeeds
- [X] T033 [US3] Confirm T032 fails for the right reason and record the red run

**CHECKPOINT — human approval required**: obtain an explicit "approved" before implementation.

### Implementation for User Story 3

- [X] T034 [US3] Implement AzureOpenAIGeneratorOptions.FromEnvironment in src/AgenticChangeOps.Intake.AzureOpenAI/AzureOpenAIGeneratorOptions.cs per data-model.md: Endpoint "Required; absolute `https`; not a placeholder"; DeploymentName "Required; non-blank; not a placeholder"; RequestTimeout "Positive; default 30 s"; MaxResponseCharacters "Positive; default 32,000"; placeholder rule from research decision 5; throw GeneratorConfigurationException naming the variable and never echoing the value
- [X] T035 [US3] Implement the validation use in the AzureOpenAIDraftGenerator constructor in src/AgenticChangeOps.Intake.AzureOpenAI/AzureOpenAIDraftGenerator.cs (reject null options or client; options are validated once, at creation) and the options-reading half of `CreateFromEnvironment` (reads `Environment.GetEnvironmentVariable`; the client is added in T040)
- [X] T036 [US3] Run T032 until green and confirm the full suite still passes

**Checkpoint**: Bad configuration fails fast with a clear, value-free error.

---

## Phase 6: User Story 4 - Authenticate Without a Stored Key (Priority: P4)

**Goal**: The real adapter authenticates with the operator's identity; no key, token, endpoint
or deployment name exists in the repository.

**Independent Test**: A repository scan finds nothing; the generator is created with no key
setting; identity failure maps to Unreachable with no identity detail.

### Tests for User Story 4 ⚠️ write first, must be red for the right reason

- [X] T037 [P] [US4] Create tests/AgenticChangeOps.Intake.Tests/Unit/NoSecretsTests.cs: scan every `.cs`, `.csproj`, `.json`, `.md` and `.yml` file under src, tests and specs (excluding bin, obj and this test file) for: a 32-character hexadecimal run, `api-key`, `apikey` (case-insensitive) used as a setting or header name in src, `.openai.azure.com`, `Bearer ` followed by a token, and `AZURE_OPENAI_API_KEY`; assert none are found; assert the new project defines no environment variable name containing KEY, TOKEN, SECRET or PASSWORD
- [X] T038 [P] [US4] Extend tests/AgenticChangeOps.Intake.Tests/Unit/ModelFailureMapperTests.cs: a credential-unavailable style exception (a test-local exception type named `CredentialUnavailableException`) and an authentication-failed style exception map to Unreachable, and the resulting DraftGenerationException message contains neither the original exception message nor its type name
- [X] T039 [US4] Confirm T037 and T038 fail for the right reason (or pass trivially where nothing exists yet — in that case record that explicitly and keep them as guards) and record the run

**CHECKPOINT — human approval required**: obtain an explicit "approved" before implementation.

### Implementation for User Story 4

- [X] T040 [US4] Create AzureOpenAIChatModelClient in src/AgenticChangeOps.Intake.AzureOpenAI/AzureOpenAIChatModelClient.cs implementing IChatModelClient: constructed from AzureOpenAIGeneratorOptions; builds the Azure OpenAI client with `DefaultAzureCredential` (no key anywhere), retry policy set to zero retries, JSON response mode, the supplied max output tokens; maps `finish reason = length` to `WasCutOff = true`; wraps every SDK or identity exception through ModelFailureMapper (passing HTTP status and error code when present) into DraftGenerationException; never logs, and never includes the SDK message
- [X] T041 [US4] Complete `AzureOpenAIDraftGenerator.CreateFromEnvironment` in src/AgenticChangeOps.Intake.AzureOpenAI/AzureOpenAIDraftGenerator.cs: read options from the process environment (T034), construct AzureOpenAIChatModelClient, return the generator; no key variable is read
- [X] T042 [US4] Run T037 and T038 until green and confirm the full suite still passes

**Checkpoint**: Production path exists, authenticates by identity, and the repository is clean.

---

## Phase 7: User Story 5 - Verify Live Behaviour by Explicit Opt-In Only (Priority: P5)

**Goal**: A person can check the real model by hand; the routine suite never does.

**Independent Test**: `dotnet test` with no configuration passes and reports the live check as
skipped with its reason; enabling it runs one real call.

- [X] T043 [P] [US5] Create LiveModelFactAttribute in tests/AgenticChangeOps.Intake.Tests/Live/LiveModelFactAttribute.cs deriving from FactAttribute: sets `Skip` to a message naming `AGENTICCHANGEOPS_LIVE_MODEL_CHECK=1` unless that variable equals `1`
- [X] T044 [US5] Create tests/AgenticChangeOps.Intake.Tests/Live/LiveModelCheckTests.cs with one `[LiveModelFact]`: build the generator with `CreateFromEnvironment`, generate for one synthetic change request, and assert only that the call returned and all five sections are non-null; write nothing from the prompt, answer, endpoint or deployment name to output; the assertion messages are fixed strings
- [X] T045 [US5] Add to tests/AgenticChangeOps.Intake.Tests/Unit/GeneratorConfigurationTests.cs a guard that, with no model variables set in the process, the automated suite never constructs the real client (the unit tests use only the fake client and dictionary environment); run `dotnet test` with the model variables unset and network disabled where practicable, and confirm all pass, the live check is skipped with its reason shown, and no model call occurred

**Checkpoint**: The live check is visible, safe by default, and runnable on demand.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T046 [P] Update specs/003-requirement-drafting/contracts/IRequirementsDraftingService.md: in the `DraftAsync` outcome table, note that a `DraftGenerationException` adds its category to the `Generator` error message (additive, slice 004)
- [X] T047 [P] Run the secret scan from quickstart step 5 manually over src, tests and specs and confirm zero findings; also confirm no prompt or answer text appears in test output (run `dotnet test --logger "console;verbosity=detailed"` and inspect)
- [X] T048 Run `dotnet build AgenticChangeOps.sln` (0 errors, 0 warnings) and `dotnet test` and record the final passing count against the T001 baseline
- [X] T049 Walk through specs/004-model-draft-generator/quickstart.md steps 1 to 3 and 5 and confirm each expected outcome; step 4 (live check) is performed by the user by hand
- [X] T050 Constitution compliance review against v1.0.1 and record the result at the bottom of plan.md: Principle II (generator writes nothing, no generated content influences routing), III (non-consequential classification still holds), IV (no persisted record changed), VI (no production code calls the SDK except through IChatModelClient; no live call in the suite), VII (no logging package, no extra abstractions beyond the seam); confirm the Semantic Kernel note was resolved with the user — Review recorded in plan.md; Semantic Kernel choice confirmed by the user (Azure client library now, revisit at orchestration).

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2 → stories**: T001 to T003 first; Phase 2 blocks all stories.
- **US1 (P1)** is the MVP and must precede US2 (US2 extends the same generator and parser).
- **US3 (P3)** depends only on Phase 2 and can run in parallel with US2 after US1 if the
  generator constructor edits (T035, T029) are sequenced because they touch the same file.
- **US4 (P4)** depends on US3 (`FromEnvironment`) for `CreateFromEnvironment`.
- **US5 (P5)** depends on US4 (needs the real client) but its attribute (T043) can start earlier.
- **Polish** after all stories.

Within a story: tests → red run → CHECKPOINT approval → implementation → green run.

### Parallel opportunities

- Phase 2: T004 to T009, T011, T012 touch different files and can run together; T010 and T013
  follow.
- US1 tests: T014 and T015 together (T016 after, it shares the integration wiring pattern).
- US2 tests: T022, T023, T024 together; implementation T027 and T028 together.
- US4 tests: T037 and T038 together.

## Implementation Strategy

1. **MVP** = Phases 1 to 3 (US1): a well-formed model answer becomes a stored draft; proven
   with a fake client.
2. Add US2 for safety (all failures leave state untouched), then US3 (configuration), US4
   (real adapter and secret hygiene), US5 (live check).
3. Stop at each CHECKPOINT for test approval, as agreed for earlier slices; propose batched
   approval for US2 to US4 tests if the user agrees.
4. The real adapter (T040) is deliberately last: until then, no SDK type is exercised at all.

---

## Phase 9: Convergence

**Purpose**: Remaining gaps found by `/speckit-converge` on 2026-10-11. Both are test-only: the
behaviour already holds in code but is not yet proven for the model-backed generator. Show each
test passing on first run and confirm it fails if the guarded behaviour is broken, then mark off.

- [X] T051 Add to tests/AgenticChangeOps.Intake.Tests/Unit/GeneratorDraftingIntegrationTests.cs a test where the fake model answer's summary and requirements contain decision-like wording ("approve this request", "rejected", "defer"): after drafting, the change request status is unchanged, `IApprovalService.IsClearedToProceed` is still false, and the decision history is unchanged, per spec Edge Cases (answer addressed to the system) and FR-007 (partial)
- [X] T052 Add to tests/AgenticChangeOps.Intake.Tests/Unit/GeneratorDraftingIntegrationTests.cs a test that runs two `DraftAsync` calls concurrently with `Task.WhenAll` (a) for two different change requests and (b) for the same change request: each call makes its own model call (client CallCount equals the number of calls), versions are 1 and 2 for the same request with no gap or duplicate, and different requests each start at version 1, per spec Edge Cases (two draft requests at the same time) (partial)
