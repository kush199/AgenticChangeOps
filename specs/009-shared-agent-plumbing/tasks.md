# Tasks: Shared Agent Plumbing

**Input**: Design documents from `specs/009-shared-agent-plumbing/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included and written first (Constitution Principle VI and the project's test-approval checkpoint). The model-backed HTTP baseline is captured green on the **unchanged** code (Phase 2). The shared tests are shown red against signature-only stubs that throw `NotImplementedException` and approved (T014) before any stub is filled in. Red means failing for the right reason: behaviour, not missing types.

**Constitution**: [x] Reviewed against constitution v1.0.3. See plan.md Constitution Check; no Complexity Tracking row is needed.

**Organization**: Grouped by user story (US1 to US5 from spec.md). US1 carries the extraction; US2 is the proof that nothing observable changed; US3 to US5 are the neutrality, visibility and measurement stories.

**Rules that hold for every task**:
- Permitted production edits: new files in `src/AgenticChangeOps.ModelAccess`, and the six agent files named in T018 to T023. Nothing else in `src` changes. In particular no edit to `src/AgenticChangeOps.Intake`, `src/AgenticChangeOps.Api`, either agent's services, prompt builders, content or record types, or any existing ModelAccess file.
- No assertion in an existing test is edited and no existing test file is edited. New tests go in new files. If an existing test fails, fix the new code, never the assertion. Existing goldens are never rewritten.
- Behaviour must be identical, including error wording and evaluation order. When in doubt, copy the existing statement, do not improve it.
- Synthetic data only; no endpoint, deployment name, key or real name in any file (Principle I). The sentinel strings already used by the existing tests are the only endpoint-like values allowed.
- No new package reference, project, setting or endpoint.
- Shared source must not contain the text `Requirement`, `Architect`, `Design`, `AgenticChangeOps.Intake`, `AgenticChangeOps.Agents`, `ChangeRequest` or `ValidationError` (the existing neutrality scan already forbids some of these).
- After every task that touches code, `dotnet build AgenticChangeOps.sln` must succeed; after every phase, `dotnet test AgenticChangeOps.sln` must be green (except the deliberately red state at T014).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task). Tasks editing the same file are never `[P]`.
- **[Story]**: US1 to US5, from spec.md.
- **[USER]**: outward-facing step (push, PR). Claude drafts, the user confirms.

---

## Phase 0: Gate

- [X] T000 **GATE (human approval)**: the user confirms spec.md, plan.md and tasks.md are approved. No test or source file is written before this (Principle V).

## Phase 1: Setup (before any code moves)

- [X] T001 Confirm branch `feature/shared-agent-plumbing` and a clean tree. Run `dotnet test AgenticChangeOps.sln` and save the per-project totals (and the skipped live check) to `specs/009-shared-agent-plumbing/baseline-tests.txt`, in the same layout as `specs/008-architect-design-drafting/baseline-tests.txt`. This is the SC-001 reference: every earlier test must still pass afterwards.
- [X] T002 Create `specs/009-shared-agent-plumbing/measure-duplication.ps1` implementing exactly the method in research.md R6: for each of `src/AgenticChangeOps.Agents.Requirement` and `src/AgenticChangeOps.Agents.Architect` read every `*.cs` file outside `bin` and `obj`; trim lines; drop blank lines, lines starting `//` or `///`, lines starting `using ` or `namespace `, and lines made only of `{ } [ ] ( ) , ;`; remove the words `Requirements`, `Requirement`, `Architect`, `Design`, `Draft`, `Basis` (case-sensitive, in that order); then print lines per agent, the multiset-intersection count of shared lines, and shared lines as a percentage of the smaller agent. The script takes no parameters and changes no file.
- [X] T003 Run the script from T002 on the unchanged code and write the figures, the date and the commit hash to a new `specs/009-shared-agent-plumbing/duplication.md` under a "Before" heading. Leave an "After" heading empty for T034.

## Phase 2: Foundational (blocks every story): capture the model-backed baseline

**Purpose**: no HTTP test today runs a real generator and parser end to end. Record that behaviour on the unchanged code so US2 can compare against it.

- [X] T004 [P] Create `tests/AgenticChangeOps.Api.Tests/Fakes/ScriptedChatModelClient.cs`: an `IChatModelClient` whose behaviour is a `Func<CancellationToken, Task<ChatModelResponse>>` set by the test, with helpers `ReturnsText(text, wasCutOff = false)`, `Throws(exception)` and `DelaysUntilCancelled()`, mirroring the helper shapes in `tests/AgenticChangeOps.Agents.Architect.Tests/Fakes/FakeChatModelClient.cs` but containing no agent sample text. Count calls with `Interlocked`.
- [X] T005 Create `tests/AgenticChangeOps.Api.Tests/Baseline/SharedPlumbingBaselineTests.cs`. For **each** agent, register the real generator over the scripted client: Requirement through `new ApiFactory(new AzureOpenAIDraftGenerator(options, client), draftingConfigured: true)`; Architect through `DesignHost` or an `ApiFactory` `configureServices` override that registers `new AzureOpenAIDesignGenerator(options, client)` as `IDesignDraftGenerator` plus `DesignAvailability(true)` and the fake basis source. Use `AzureOpenAIGeneratorOptions(new Uri("https://sentinel-endpoint.invalid/"), "sentinel-deployment", TimeSpan.FromSeconds(30), 32000)` except where a case needs a tiny time limit or a small size limit. Drive the POST (draft or design) and the follow-up GET latest and audit through every behaviour in `contracts/http-api-unchanged.md` rows 1 to 12, using valid answers shaped for each agent's section names. Record each response with the existing `TranscriptBuilder` and compare with `GoldenFile.Verify("shared-plumbing-transcript.txt", ...)`. Cancellation by the caller (row not in the table) is recorded by cancelling the request token and noting that the server returns whatever it returns today.
- [X] T006 In the same test class add assertions, evaluated for every recorded run: the sentinel endpoint string, the sentinel deployment string, and the scripted answer text appear in no response body, header or captured log line (use `CapturingLoggerProvider`, as `NoLeakTests` does).
- [X] T007 On the **unchanged** code run `AGENTICCHANGEOPS_UPDATE_BASELINE=1 dotnet test tests/AgenticChangeOps.Api.Tests --filter SharedPlumbingBaseline` to write `tests/AgenticChangeOps.Api.Tests/Baseline/golden/shared-plumbing-transcript.txt`, then run it again **without** the variable and confirm it is green and byte-stable. Record in `baseline-tests.txt` the commit hash it ran against.
- [X] T008 Run `dotnet test AgenticChangeOps.sln`. Everything is green; the totals are the new baseline plus the new test cases.

**Checkpoint**: baseline captured on unchanged code. No `src` file has been touched.

---

## Phase 3: User Story 1 + 3 tests and stubs (Priority: P1)

**Goal**: each shared behaviour exists once, in ModelAccess, proven once; a stand-in third agent proves reuse.

**Independent Test**: the new `ModelAccess.Tests` classes pass, and a stand-in agent with its own sections uses all three pieces.

- [X] T009 [US1] Create four signature-only stubs in `src/AgenticChangeOps.ModelAccess/`, namespace `AgenticChangeOps.ModelAccess`, every body `throw new NotImplementedException();`, per data-model.md:
  - `ModelCaller.cs`: `public sealed class ModelCaller { public ModelCaller(AzureOpenAIGeneratorOptions options, IChatModelClient client); public Task<string> GetAnswerTextAsync(string systemInstruction, string userContent, CancellationToken cancellationToken); }`
  - `ModelAnswerReader.cs`: `public static class ModelAnswerReader { public static T Read<T>(string text, Func<ModelAnswer, T> map); }`
  - `ModelAnswer.cs`: `public sealed class ModelAnswer` with `public string ReadString(string name)` and `public IReadOnlyList<string> ReadList(string name)`; it wraps a `System.Text.Json.JsonElement` object and is created only by the reader.
  - `SectionNormaliser.cs`: `public static class SectionNormaliser` with `public const int MaxTextLength = 2000;` and `public const int MaxListItems = 50;`, `public static SectionOutcome<string> Text(string? value, string section)`, `public static SectionOutcome<IReadOnlyList<string>> List(IReadOnlyList<string>? items, string section, bool requireItem)`, and `public sealed record SectionOutcome<T>(T? Value, string? Error)`.
- [X] T010 [P] [US1] Create `tests/AgenticChangeOps.ModelAccess.Tests/Fakes/ScriptedChatModelClient.cs` (same shape as T004, namespace `AgenticChangeOps.ModelAccess.Tests.Fakes`) and `tests/AgenticChangeOps.ModelAccess.Tests/ModelCallerTests.cs` covering every row of `contracts/shared-api.md` under ModelCaller: one call and no retry; output bound `(MaxResponseCharacters + 2) / 3` for several limits; categorised failure passes through for each of the four categories; caller cancellation propagates as `OperationCanceledException` and not as a category; time-limit expiry (use `TimeSpan.FromMilliseconds(50)` and `DelaysUntilCancelled`) gives `TimedOut`; `TimeoutException` gives `TimedOut`; any other exception gives `Unreachable`, with the thrown carried exception's message and type name free of the original message (use a sentinel text), and `InnerException` null; `WasCutOff` true gives `ResponseTooLarge`; text length `MaxResponseCharacters + 1` gives `ResponseTooLarge` and exactly `MaxResponseCharacters` is accepted; empty and whitespace-only text gives `MalformedOutput`; null options or client throws `ArgumentNullException` naming `options` or `client`.
- [X] T011 [P] [US1] Create `tests/AgenticChangeOps.ModelAccess.Tests/ModelAnswerReaderTests.cs` and `SectionNormaliserTests.cs` covering every row of `contracts/shared-api.md` under their headings. Reader: bare object; unlabelled fence; ` ```json `, ` ```JSON `, ` ```Json `; a fence labelled `yaml`; no newline after the opening fence; missing closing fence; an inner fence; non-object root (array, string, number); invalid JSON; empty text; missing key; text value that is a number, null, array; list that is a string, an object; list item that is a number or null; empty string and empty list returned unchanged; an exception thrown by the map becomes `MalformedOutput`; the thrown exception's message never contains the answer text (use a sentinel inside the answer). Normaliser (verbatim wording from data-model.md): text trimmed; null, empty, whitespace give `"{section} is required and must not be blank."`; 2000 characters accepted, 2001 gives `"{section} must not exceed 2000 characters."`; list null gives `"{section} section is missing."`; blanks dropped, items trimmed, order kept; none left with `requireItem: true` gives `"{section} must contain at least one non-blank item."` and with `false` returns an empty list; 50 items accepted, 51 gives `"{section} must not exceed 50 items."`; item of 2000 accepted, 2001 gives `"Each {section} item must not exceed 2000 characters."`; blank items are not counted toward the 50.
- [X] T012 [US3] Create `tests/AgenticChangeOps.ModelAccess.Tests/StandInAgentTests.cs`: a private `StandInContent(string Headline, IReadOnlyList<string> Steps, IReadOnlyList<string> Notes)` and a private `StandInAgent` class defined in the test file that (a) builds a `ModelCaller` from options and a scripted client and gets answer text for its own prompt, (b) reads it with `ModelAnswerReader.Read` using its own section names `headline`, `steps`, `notes`, (c) normalises with `SectionNormaliser` requiring `Steps` and not `Notes`, returning its own result record with errors as strings. Cover: valid answer, fenced answer, malformed answer, blank required section, optional section empty, model failure. The file must not reference any project except ModelAccess.
- [X] T013 [P] [US1] Create `tests/AgenticChangeOps.Agents.Requirement.Tests/SharedBehaviourStructureTests.cs` and `tests/AgenticChangeOps.Agents.Architect.Tests/SharedBehaviourStructureTests.cs` (new files; same repository-root helper style as the existing `StructureTests`): rule N4. Every `*.cs` file under the agent's `src` folder (excluding `bin`, `obj`) contains none of: `CancellationTokenSource`, `JsonDocument`, the three-backtick fence literal, `GetProperty(`, `IsNullOrWhiteSpace(i)`. These fail red until the adapters are written.
- [X] T014 **CHECKPOINT (human approval)**: run `dotnet test AgenticChangeOps.sln`. Show the user the red list. Expected red: every case in `ModelCallerTests`, `ModelAnswerReaderTests`, `SectionNormaliserTests`, `StandInAgentTests` (all `NotImplementedException`) and the two `SharedBehaviourStructureTests` classes (agent source still contains the copies). Expected green: every earlier test and both baselines. **Do not implement until the user types "approved"** (see the project's test-approval feedback).

---

## Phase 4: User Story 1 (Priority: P1): implement and slim the agents

**Goal**: one implementation of each behaviour; each agent keeps only what is specific to it.

**Independent Test**: after this phase the shared tests and the structure tests are green and neither agent holds a second copy.

### Fill the stubs (ModelAccess)

- [X] T015 [P] [US1] Implement `src/AgenticChangeOps.ModelAccess/SectionNormaliser.cs`. `Text`: `var trimmed = value?.Trim();` empty gives error `$"{section} is required and must not be blank."`; length over `MaxTextLength` gives `$"{section} must not exceed {MaxTextLength} characters."`; otherwise value. `List`: null gives `$"{section} section is missing."`; `kept = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList()`; then in this order: `requireItem && kept.Count == 0` gives `$"{section} must contain at least one non-blank item."`; `kept.Count > MaxListItems` gives `$"{section} must not exceed {MaxListItems} items."`; any item over `MaxTextLength` gives `$"Each {section} item must not exceed {MaxTextLength} characters."`; otherwise `kept.AsReadOnly()`. Copy the statement order from `DraftContentValidator.NormaliseList`.
- [X] T016 [P] [US1] Implement `src/AgenticChangeOps.ModelAccess/ModelAnswer.cs` and `ModelAnswerReader.cs` by moving the logic of `DraftResponseParser` (`Unwrap`, `ReadString`, `ReadList`, `Malformed`, the outer `try/catch (DraftGenerationException) throw; catch (Exception) throw Malformed();`) unchanged: `Read<T>` unwraps, `JsonDocument.Parse`, requires `JsonValueKind.Object`, calls `map(new ModelAnswer(root))`, all inside the same try. `ReadString` and `ReadList` use `root.GetProperty(name)` and the same kind checks and `Malformed()`.
- [X] T017 [US1] Implement `src/AgenticChangeOps.ModelAccess/ModelCaller.cs` by moving `CharactersPerToken`, the null checks, the `maxOutputTokens` formula, the post-call `WasCutOff`, length and blank checks, and `CallModelAsync` (timeout source, linked source, the four catch clauses in the same order, `ConfigureAwait(false)`) unchanged from `AzureOpenAIDraftGenerator`. `GetAnswerTextAsync` returns `response.Text`. Keep the explanatory comments that say why the original exception is not attached.

### Slim the agents (adapters keep today's public names and signatures)

- [X] T018 [P] [US1] Edit `src/AgenticChangeOps.Agents.Requirement/DraftResponseParser.cs`: keep `public static class DraftResponseParser` and `public static DraftContent Parse(string text)`; body is `ModelAnswerReader.Read(text, a => new DraftContent(Summary: a.ReadString("summary"), FunctionalRequirements: a.ReadList("functionalRequirements"), AcceptanceCriteria: a.ReadList("acceptanceCriteria"), Assumptions: a.ReadList("assumptions"), OpenQuestions: a.ReadList("openQuestions")))`. Keep the existing summary comment. Remove `Unwrap`, `ReadString`, `ReadList`, `Malformed`, `Fence` and the unused `using`s.
- [X] T019 [P] [US1] Edit `src/AgenticChangeOps.Agents.Architect/DesignResponseParser.cs` the same way, with `approachSummary`, `affectedComponents`, `impactAndRisks`, `dependencies`, `rollbackConsiderations`, `openQuestions` in that order, building `DesignContent`.
- [X] T020 [P] [US1] Edit `src/AgenticChangeOps.Agents.Requirement/DraftContentValidator.cs`: keep the class, the `null` content check returning the `Generator` validation error (register K5), and `Validate` with its signature. Replace the summary block and `NormaliseList` with `SectionNormaliser.Text(content.Summary, "Summary")` and `SectionNormaliser.List(..., requireItem: ...)` calls in the same section order and with the same required choices: `FunctionalRequirements` true, `AcceptanceCriteria` true, `Assumptions` false, `OpenQuestions` false. A non-null `Error` becomes `errors.Add(new ValidationError(section, error))`. Return the normalised content only when `errors.Count == 0`. Remove the local constants and `NormaliseList`.
- [X] T021 [P] [US1] Edit `src/AgenticChangeOps.Agents.Architect/DesignContentValidator.cs` the same way: `Text(content.ApproachSummary, "ApproachSummary")`; lists in order `AffectedComponents` true, `ImpactAndRisks` true, `Dependencies` false, `RollbackConsiderations` true, `OpenQuestions` false.
- [X] T022 [P] [US1] Edit `src/AgenticChangeOps.Agents.Requirement/AzureOpenAIDraftGenerator.cs`: keep the class, constructor `(AzureOpenAIGeneratorOptions, IChatModelClient)`, `CreateFromEnvironment()`, `GenerateAsync(ChangeRequest, CancellationToken)`. Replace the two fields with `private readonly ModelCaller _caller;` created in the constructor (`new ModelCaller(options, client)`); `GenerateAsync` becomes: build the prompt with `DraftPromptBuilder.Build`, `var text = await _caller.GetAnswerTextAsync(system, user, cancellationToken).ConfigureAwait(false);`, `return DraftResponseParser.Parse(text);`. Remove `CharactersPerToken` and `CallModelAsync`. Keep the class summary comment.
- [X] T023 [P] [US1] Edit `src/AgenticChangeOps.Agents.Architect/AzureOpenAIDesignGenerator.cs` the same way, keeping `FromProcessEnvironment()` and `GenerateAsync(ChangeRequest, RequirementsBasis, CancellationToken)`.
- [X] T024 [US1] Run `dotnet build AgenticChangeOps.sln` (zero warnings that were not there before) and `dotnet test AgenticChangeOps.sln`. Expected: everything green, including the T010 to T013 tests, both baselines unchanged, and the live check still skipped. Fix code, never tests.

**Checkpoint**: US1 delivered. Shared tests prove each behaviour once; agents hold adapters only.

---

## Phase 5: User Story 2 (Priority: P1): callers and users see no difference

**Goal**: prove nothing observable changed.

**Independent Test**: the full existing suite and both baselines pass with no edited assertion.

- [X] T025 [US2] Confirm no existing test changed: run `git diff --stat main -- tests` and `git diff main -- tests | grep '^-' | grep -v '^---'`. The second command must print nothing (additions only). Record the output in `specs/009-shared-agent-plumbing/duplication.md` under "Verification".
- [X] T026 [US2] Confirm no golden changed: `git diff main -- tests/AgenticChangeOps.Api.Tests/Baseline/golden` shows only the new `shared-plumbing-transcript.txt` file added; `transcript.txt` and `openapi.json` are untouched.
- [X] T027 [US2] Run `dotnet test AgenticChangeOps.sln` and compare per-project totals with `baseline-tests.txt`: same passed counts plus only the new cases, zero failed, skipped still 1 (live check). Append the after totals to `baseline-tests.txt`.
- [X] T028 [US2] Run the existing secret and endpoint scans (`NoSecretsTests` in the Api, ModelAccess and Architect test projects) and `NoLeakTests`; all green, covering the new test files and `specs/009-shared-agent-plumbing`.

---

## Phase 6: User Story 3 (Priority: P2): a third agent adds only itself

**Goal**: the shared code is agent-neutral and enforceably so.

**Independent Test**: neutrality scans and the stand-in test pass; walk-through shows no edit to existing agents.

- [X] T029 [P] [US3] Create `tests/AgenticChangeOps.ModelAccess.Tests/SharedNeutralityTests.cs` (new file, same helpers as `StructureTests`): rules N1 to N3. Every ModelAccess source file contains none of `Architect`, `DesignDraft`, `DesignContent`, `DesignPromptBuilder`, `ChangeRequest`, `ValidationError`, `ApprovalDecision`, `AuditEvent`; and the project file still has no `ProjectReference`. This test is green on arrival (the rule is a guard for the future) and must be seen failing once by temporarily adding a forbidden word to a shared file, then reverting.
- [X] T030 [US3] Walk the quickstart section 3 on paper against the code: write a short note in `specs/009-shared-agent-plumbing/duplication.md` under "Third agent walk-through" naming the three calls a new agent makes and confirming no ModelAccess, Requirement or Architect file needs an edit; cite `StandInAgentTests` as the executable proof (rule N7).
- [X] T031 [US3] Confirm no dependency between agents: the existing agent `StructureTests` reference checks pass and `git diff main -- '*.csproj'` shows no change.

---

## Phase 7: User Story 4 (Priority: P2): intentional differences stay visible

**Goal**: nothing that differs on purpose was merged, and every look-alike left separate is explained.

**Independent Test**: the register is complete and the seven differences in `contracts/dependency-rules.md` are still true in code.

- [X] T032 [US4] Complete `specs/009-shared-agent-plumbing/contracts/kept-separate-register.md`: for each of K1 to K10 compare the two files line by line and confirm the reason; add any further similar pair found during implementation (for example the null-content check, the factory shapes, `NotFound` helpers). Each entry names both files.
- [X] T033 [US4] Verify by reading code that each row of the "Intentional differences" table in `contracts/dependency-rules.md` still holds (required choices in T020 and T021, eligible status and basis check in the two services, audit event names, generator entry points, factory names). Note the result as a short list under "Verification" in `duplication.md`; any mismatch is a defect to fix before completion.

---

## Phase 8: User Story 5 (Priority: P3): duplication is measurably lower

- [X] T034 [US5] Run `measure-duplication.ps1` unchanged on the final code and write the figures under "After" in `duplication.md`, with before, after, the difference and the percentage change. The after figure must be lower than before (SC-004). Explain the remainder by pointing to register entries K1 to K4 and K7.
- [X] T035 [US5] Add a "Redundant agent tests" list to `duplication.md`: the agent tests that now prove a behaviour a second time (research.md R8), for example `DraftResponseParserTests`, `DesignResponseParserTests`, the model-call tests in `DraftGeneratorFailureTests`, `DesignGenerationFailureTests`, and the list-limit tests in the two content validation tests. They are left in place and flagged for a later slice (FR-016).

---

## Phase 9: Polish and hand-off

- [X] T036 Update `README.md` only if it describes the model call, parser or validator as agent-owned (check with a search for `ResponseParser` and `ModelAccess`); keep any change to a sentence or two.
- [X] T037 Update the slice status in `spec.md` from Draft to Implemented, tick the compliance box in the PR description draft, and re-run `dotnet build AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln` one last time with the totals in `baseline-tests.txt`.
- [X] T038 [USER] Push `feature/shared-agent-plumbing` and open a PR with the standard PR attribution line. Claude drafts the description (summary, before and after figures, the verification list, a note that the live check was not run); the user confirms before anything is pushed.

---

## Dependencies and order

```text
T000 gate
  -> Phase 1 (T001..T003)
  -> Phase 2 (T004..T008)            baseline captured on unchanged code
  -> Phase 3 (T009..T014)            stubs, tests, red, approval
  -> Phase 4 (T015..T024)            implement, slim, green
  -> Phase 5 US2 (T025..T028)        no-difference proof
  -> Phase 6 US3 (T029..T031), Phase 7 US4 (T032..T033), Phase 8 US5 (T034..T035)   independent of each other
  -> Phase 9 (T036..T038)
```

Parallel groups: T004 with T002/T003 (different files); T010 with T011 and T013; T015 with T016; T018 to T023 are six different files and can run together once T015 to T017 are done.

## Implementation strategy

- **MVP**: Phases 0 to 5 deliver the whole behavioural change and its proof (US1 and US2). US3 to US5 are guards, records and measurement and add no production code.
- **Order is the safety net**: the baseline (Phase 2) is captured before a single `src` line moves, and is never rewritten.
- **If a baseline differs after Phase 4**: the extraction changed behaviour. Fix the shared or adapter code until the golden matches; do not touch the golden.

---

## Phase 10: Convergence

Appended by `/speckit-converge` on 2026-10-13. Nothing above was changed. Findings are ordered by severity; each task names its source and gap type. Tasks T039 and T040 need a "before" capture from commit `a49108f`; take it from a temporary `git worktree` outside the repository (for example under the session scratchpad), copy only the new test files into it, write the golden there with `AGENTICCHANGEOPS_UPDATE_BASELINE=1`, copy only the golden back, then remove the worktree. Never write a golden from the current code.

- [X] T039 MEDIUM Record the non-model design operations in a golden captured before the extraction, per FR-017 and US2 acceptance scenario 2 (partial). Today `SharedPlumbingBaselineTests` records only model behaviours; the design cases for unknown id, invalid id, request not Approved (Submitted, Deferred, Rejected), Approved with no requirements draft, not configured, latest and history before any design, and history after two designs are asserted by `DesignEndpointTests` and `DesignFailureTests` but not recorded as responses. Add `tests/AgenticChangeOps.Api.Tests/Baseline/DesignOperationsBaselineTests.cs` (new file) using `DesignHost`, `FakeDesignGenerator` and `FakeBasisSource`, recording each with `TranscriptBuilder` and `GoldenFile.Verify("design-operations-transcript.txt", ...)`. Capture the golden in the `a49108f` worktree, add it under `tests/AgenticChangeOps.Api.Tests/Baseline/golden/`, and confirm the test passes on the current code without the update variable.
- [X] T040 LOW Record caller cancellation over HTTP, per T005 as written, which asked for it, and the spec edge case on cancellation (partial). Add one case per agent to the same new test file (or to `SharedPlumbingBaselineTests` if the capture is redone in the worktree): the real generator with a `ScriptedChatModelClient` whose behaviour is `DelaysUntilCancelled`, a request token cancelled after about 100 ms, and a recording of whatever response, or client-side cancellation, the server produces today; assert that no sentinel appears in any response or log. Capture the before state in the `a49108f` worktree (copy `ScriptedChatModelClient.cs` and the test file in) and verify against current code. If the client observes only a cancelled request with no response, record that outcome in the golden as the text `client cancelled, no response`.
- [X] T041 LOW Complete the kept-separate register per FR-013, FR-019 and US4 acceptance scenario 1 (partial). `contracts/kept-separate-register.md` has no entry for the two agents' sets of required sections, which FR-013 names explicitly, and none for the two new `ScriptedChatModelClient` copies. Add K14 (required and optional sections: each agent states its own choice in its validator; identical shared rules, different intentional choices; link to the table in `contracts/dependency-rules.md`) and K15 (`Api.Tests/Fakes/ScriptedChatModelClient.cs` and `ModelAccess.Tests/Fakes/ScriptedChatModelClient.cs`: near copies; the ModelAccess one also records the instruction, user content and output bound that its output-bound test needs; with the two `FakeChatModelClient` copies from K9 this is four fakes of one seam, left separate to avoid a test-support project). Do not change any code.
- [X] T042 LOW Report test-code duplication separately, per the spec Assumptions ("Test code is reported separately and is not part of the headline figure") (partial). `duplication.md` reports production code only. Add an optional `-Scope Tests` switch to `measure-duplication.ps1` that applies the identical normalisation to `tests/AgenticChangeOps.Agents.Requirement.Tests` and `tests/AgenticChangeOps.Agents.Architect.Tests`, leaving the default (`src`) behaviour and output byte-for-byte unchanged (re-run it and confirm it still prints 212, 259, 155, 73.1). Record the tests figure after, and the before figure from the `a49108f` worktree, in a new "Test code (reported separately)" section of `duplication.md`.
- [X] T043 Re-run `dotnet build AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln`; confirm 0 failed, existing goldens untouched (`git diff HEAD -- tests/AgenticChangeOps.Api.Tests/Baseline/golden` shows only added files), and append the new totals to `baseline-tests.txt`. Then run `/speckit-converge` again; the expected result is converged.

---

## Phase 11: Convergence

Appended by `/speckit-converge` on 2026-10-13, after Phase 10 was completed. Nothing above was changed.

- [X] T044 LOW Replace the two per-agent copies of the shared-behaviour scan with one scan, per FR-016 ("each behaviour proven once rather than once per agent"), SC-004 and FR-011 (partial). `tests/AgenticChangeOps.Agents.Requirement.Tests/SharedBehaviourStructureTests.cs` and `tests/AgenticChangeOps.Agents.Architect.Tests/SharedBehaviourStructureTests.cs` are identical apart from the agent name and added 31 shared lines to test code (545 to 576, see `duplication.md`). Delete both files (both were added by this slice; no existing test file is touched) and add `tests/AgenticChangeOps.ModelAccess.Tests/AgentsHoldNoSharedBehaviourCopyTests.cs`: one theory over the same five tokens (`CancellationTokenSource`, `JsonDocument`, `GetProperty(`, `IsNullOrWhiteSpace(`, and the three-backtick fence literal) that scans every `*.cs` file, outside `bin` and `obj`, under every folder matching `src/AgenticChangeOps.Agents.*`, asserts at least two such folders exist so the scan cannot pass vacuously, and reports offending file names. A future agent folder is then covered with no edit. Show the new test red once by temporarily adding one token to an agent source file, then revert it.
- [X] T045 Re-run `dotnet build AgenticChangeOps.sln` and `dotnet test AgenticChangeOps.sln` (expected: 0 failed; Requirement and Architect test projects 5 fewer cases each, ModelAccess 5 more); run `measure-duplication.ps1 -Scope Tests` and confirm shared lines are back at or below the before figure of 545; update the "Test code (reported separately)" section of `duplication.md` and append the totals to `baseline-tests.txt`. Existing tests and goldens must again show no tracked change (`git diff HEAD -- tests` empty).

---

## Phase 12: Convergence

Appended by `/speckit-converge` on 2026-10-13, after Phase 11 was completed. Nothing above was changed.

- [X] T046 LOW Bring the slice's verification documents in line with the recordings that now exist, per plan: `contracts/http-api-unchanged.md` and `quickstart.md` (partial). `contracts/http-api-unchanged.md` still says "Two recordings prove it" and lists the design operations only as assertion tests; it does not describe `DesignOperationsBaselineTests` (golden `design-operations-transcript.txt`, 23 responses) or `CallerCancellationBaselineTests` (golden `caller-cancellation-transcript.txt`), both added by T039 and T040. `quickstart.md` step 1 filters only `SharedPlumbingBaseline`, and its step 2 "Expected" list names only `SharedPlumbingBaselineTests` and `ApiBaselineTests`. Update both documents (documentation only, no code): in the contract, change the opening count, move the design operations out of the assertion-only row into a "Recorded before the extraction" table naming all four goldens, and record that `design-operations-transcript.txt` and `caller-cancellation-transcript.txt` were captured from commit `a49108f` through a temporary worktree and that no golden is ever rewritten from later code; in the quickstart, extend the step 1 filter to `SharedPlumbingBaseline|DesignOperationsBaseline|CallerCancellationBaseline` (as the "before" capture, noting the latter two need the `a49108f` worktree) and the step 2 Expected list to all four recordings, and add `measure-duplication.ps1 -Scope Tests` to step 2. Then confirm `git diff HEAD -- tests '*.csproj' '*.sln'` is still empty.
