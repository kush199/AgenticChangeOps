# Implementation Plan: Architect Agent on the Agent Framework

**Branch**: `feature/agent-framework-architect` | **Date**: 2026-10-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/011-architect-agent-framework/spec.md`

## Summary

Behaviour-preserving swap of one adapter. The hand-written model call lives in `AzureOpenAIChatModelClient` (ModelAccess), behind the `IChatModelClient` seam that `ModelCaller` already uses. This slice adds a second implementation of that same seam, `AgentFrameworkChatModelClient`, built on the Microsoft Agent Framework's agent abstraction (a single-shot `ChatClientAgent` over the Azure OpenAI chat client), and wires **only the Architect path** to it:

- `AzureOpenAIDesignGenerator.FromProcessEnvironment()` and `ApiServiceRegistration.CreateDesignSetup` construct the framework client instead of `AzureOpenAIChatModelClient`.
- `AzureOpenAIChatModelClient` stays untouched and keeps serving the Requirement agent (clarification 2026-10-14).
- Everything above the seam is unchanged: `IDesignDraftGenerator`, `AzureOpenAIDesignGenerator.GenerateAsync`, `DesignPromptBuilder`, `ModelCaller` (time limit, size limit, failure categories), `DesignResponseParser`, services, API.

Because the framework sits below `ModelCaller`, time limits, size checks, cancellation vs time-out handling and category mapping are kept by construction rather than re-implemented. The framework client itself only has to (a) make exactly one request with no retry, no tools, no session, (b) send the same system and user text, JSON-object response format and output-token limit, and (c) translate every failure to a `DraftGenerationException` with a category and nothing else.

Proof is by an offline **wire-parity test**: both clients are run against the same fake HTTP transport and the request bodies are compared, so "same instructions, same structure" is shown rather than asserted. The same transport counts calls to prove one call, zero retries and no state between requests. Any difference found is listed in `comparison.md`.

## Technical Context

**Language/Version**: C# on .NET 8 (net8.0), unchanged

**Primary Dependencies**: adds `Microsoft.Agents.AI` and `Microsoft.Agents.AI.OpenAI` (1.24.0, both ship a net8.0 target) to `AgenticChangeOps.ModelAccess`. This lifts the transitive `OpenAI` package from 2.1.0 to 2.14.0 (required by Agents.AI.OpenAI); `Azure.AI.OpenAI` 2.1.0 and `Azure.Identity` stay as they are. See research R-002 and R-003.

**Storage**: In-memory store only, untouched

**Testing**: xUnit; fakes and a fake HTTP transport only, no live model in the suite. Live check stays opt-in and skipped when unconfigured.

**Target Platform**: Unchanged (CI on ubuntu runner, local Windows)

**Project Type**: Multi-project .NET solution (unchanged)

**Performance Goals**: N/A, no behaviour change

**Constraints**: Zero assertion changes in existing tests; zero change to HTTP status codes, bodies, headers, error codes; no new setting, endpoint, project, or record shape; framework types only in `AgenticChangeOps.ModelAccess` (and nowhere in the Architect project, Api, Requirement, Intake, Workflow); Requirement agent's call path unchanged

**Scale/Scope**: 1 new class + 1 small shared helper extraction in ModelAccess; 2 call-site edits (Architect `FromProcessEnvironment`, Api `CreateDesignSetup`); 1 package change in one csproj; 9 new test classes (hand-written wire baseline, wire parity and keyless sign-in, result, failure mapping, one call, statelessness, no leak, through-caller limits, containment) and 2 fakes; 1 live-check retarget; constitution stack note amendment (separate PR); `comparison.md`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

Reviewed against constitution v1.0.3.

| Principle | Assessment |
|---|---|
| I. Synthetic data only | Pass. Fixtures use fictional content and the existing sentinel endpoint and deployment strings; fake transport returns canned synthetic JSON. No credential anywhere (keyless sign-in retained). |
| II. Deterministic routing | Pass. The agent has no tools and no handoffs; framework only generates content. No routing, transition or orchestration code changes. |
| III. Human approval gates | Pass. Approval and audit code untouched; design drafting stays non-consequential. |
| IV. Typed auditable records | Pass. No record, enum or serialized shape changes. `ChatModelResponse` and `DesignContent` unchanged. |
| V. Spec-driven | Pass. Spec exists and is clarified; plan precedes tasks; no code until tasks are approved. |
| VI. Test-first | Pass. Parity and one-call tests are written and approved red before the client exists; LLM access stays behind `IChatModelClient`; no live call in the suite. |
| VII. Simplicity | **Flagged, justified.** The new client has one production caller (Architect) where the rule asks for two. It implements the existing two-caller seam one-for-one rather than adding an abstraction, and the one-agent scope is the point of the slice (user-requested study of the framework on a single agent). Recorded in Complexity Tracking. No agent is added. |

Technology stack: the stack note already names the framework but describes it as "Semantic Kernel Agent Framework". This slice amends the note (PATCH, 1.0.3 → 1.0.4) to say how and why the framework is used, as its own constitution PR, following the 007 and 008 precedent. **Gate status: PASS (with the Principle VII note) before research and after design.**

## Project Structure

### Documentation (this feature)

```text
specs/011-architect-agent-framework/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── framework-client.md       # what the new client must do, input to output and failure map
│   ├── wire-parity.md            # request-body fields compared and the allowed differences
│   ├── dependency-rules.md       # where framework types may appear, with the check for each
│   └── http-api-unchanged.md     # what is recorded before and compared after
├── checklists/requirements.md
├── baseline-tests.txt            # created in T001 (pass counts before the change)
├── comparison.md                 # created at the end: replaced / added / kept / differed
└── tasks.md                      # produced by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.ModelAccess/
│   ├── AgenticChangeOps.ModelAccess.csproj      # + Microsoft.Agents.AI, Microsoft.Agents.AI.OpenAI
│   ├── AgentFrameworkChatModelClient.cs         # NEW: IChatModelClient over a single-shot framework agent
│   ├── AzureOpenAIChatModelClient.cs            # unchanged behaviour; content-filter probe moved to shared helper
│   ├── ContentFilterProbe.cs                    # NEW: the one shared "response mentions content_filter" check (two callers)
│   └── (ModelCaller, ModelFailureMapper, ... unchanged)
├── AgenticChangeOps.Agents.Architect/
│   └── AzureOpenAIDesignGenerator.cs            # FromProcessEnvironment builds the framework client
└── AgenticChangeOps.Api/
    └── Composition/ApiServiceRegistration.cs    # CreateDesignSetup builds the framework client

tests/
├── AgenticChangeOps.ModelAccess.Tests/
│   ├── Fakes/RecordingTransport.cs              # NEW: fake HTTP transport that records and scripts responses
│   ├── Fakes/FakeTokenCredential.cs             # NEW
│   ├── HandWrittenClientWireBaselineTests.cs    # NEW: wire baseline of the hand-written client, captured before the package change
│   ├── FrameworkClientWireParityTests.cs        # NEW: parity and keyless sign-in
│   ├── FrameworkClientResultTests.cs            # NEW
│   ├── FrameworkClientFailureTests.cs           # NEW: every failure to category
│   ├── FrameworkClientOneCallTests.cs           # NEW: one call, no retry, no tools
│   ├── FrameworkClientStatelessTests.cs         # NEW
│   ├── FrameworkClientNoLeakTests.cs            # NEW
│   ├── FrameworkClientThroughCallerTests.cs     # NEW: ModelCaller over the framework client (limits, categories)
│   └── FrameworkContainmentTests.cs             # NEW
├── AgenticChangeOps.Agents.Architect.Tests/Live/LiveDesignCheckTests.cs   # unchanged file; exercises FromProcessEnvironment, now the framework path
└── AgenticChangeOps.Workflow.Tests/Unit/StructureTests.cs                 # unchanged
```

**Structure Decision**: No new project. The framework lives in the existing agent-neutral `ModelAccess` project, the "shared model-access area" the spec permits, so the Architect project's references stay exactly `Intake` + `ModelAccess` and its existing structure tests still hold. A framework-types-only-in-ModelAccess test is added to ModelAccess.Tests.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `AgentFrameworkChatModelClient` has one production caller (Principle VII asks for two) | The slice's purpose is to study the framework on exactly one agent; it implements the existing `IChatModelClient` seam one-for-one, so it replaces rather than adds an abstraction | Switching both agents would change the Requirement agent (forbidden by spec FR-011) and defeat the single-agent comparison |
| `OpenAI` transitive package lifted 2.1.0 → 2.14.0 for the whole ModelAccess project | `Microsoft.Agents.AI.OpenAI` requires `OpenAI` ≥ 2.14.0 and ModelAccess is one project | A separate framework project would put framework types outside the area the spec allows; risk is covered by running the full suite and the hand-written client's wire test (research R-003) |
