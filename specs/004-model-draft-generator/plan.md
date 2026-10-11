# Implementation Plan: Model-Backed Requirements Draft Generator

**Branch**: `004-model-draft-generator` (working branch: `feature/azure-openai-generator`) | **Date**: 2026-10-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-model-draft-generator/spec.md`

## Summary

Provide the production implementation of `IRequirementsDraftGenerator` (slice 003) backed by an
Azure-hosted language model. A new class library, `AgenticChangeOps.Intake.AzureOpenAI`, holds
the generator, its run-time configuration, the prompt builder, and the response parser. The
model call itself sits behind a small `IChatModelClient` seam with one SDK-backed adapter, so
prompt construction, parsing, timeouts, size limits and failure mapping are all testable with
fakes. Configuration (endpoint, deployment, limits) is read from environment variables and
validated once when the generator is created; authentication uses the operator's cloud identity,
so no key exists anywhere. Failures surface as a typed exception carrying only a category; the
existing drafting service turns that into its slice 003 generation-failure error, now with the
category added. The automated suite uses fakes only; a separate, opt-in live check runs by hand
against the real model.

## Technical Context

**Language/Version**: C# / .NET 8 (matches existing projects)

**Primary Dependencies**: New project only: `Azure.AI.OpenAI` 2.1.0 (stable) and
`Azure.Identity` (latest stable 1.x, pinned at implementation). `System.Text.Json` from the
framework. The core `AgenticChangeOps.Intake` library keeps zero production dependencies.

**Storage**: N/A (nothing is persisted by this slice)

**Testing**: xUnit in the existing test project, which gains a project reference to the new
library. New `FakeChatModelClient`; the live check is a skipped-by-default test (see research
decision 7).

**Target Platform**: .NET 8 class library

**Project Type**: In-process library (second production project in the solution)

**Performance Goals**: A draft request completes or fails within the configured time limit
(default 30 s) plus negligible overhead (SC-007).

**Constraints**: No live model call, network, credential, or configuration in the automated
suite (FR-017); no key, endpoint, deployment name, prompt, or model output in source, logs,
errors, or test output (FR-011/012/015); at most one model call per draft request, no retry
(FR-010); slices 001 to 003 behaviour and tests stay green.

**Scale/Scope**: One generator, one adapter, one options type, one exception type; all
acceptance scenarios in the spec.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — all pass.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Synthetic Data Only | ✅ Pass | Fixtures and the live-check input are fictional; no endpoint, key, or deployment name appears in the repo; documentation uses angle-bracket placeholders only |
| II. Deterministic Routing | ✅ Pass | The model returns text that is parsed into `DraftContent`; nothing from it feeds routing, status, eligibility, or clearance. The generator calls no service and writes nothing (FR-007) |
| III. Human Approval Gates | ✅ Pass (non-consequential) | Content only, as in slice 003; classification restated in spec Assumptions and flagged for the compliance check |
| IV. Typed Auditable Records | ✅ Pass | No persisted record changes. `DraftContent` is reused as the output payload. New types (`GenerationFailureCategory`, `DraftGenerationException`, options) are typed; no raw string blobs cross the generator boundary |
| V. Spec-Driven Slice by Slice | ✅ Pass | Spec clarified (4 answers); tasks to follow and require approval |
| VI. Test-First | ✅ Pass | The SDK is reachable only through `IChatModelClient`; tests use fakes only; tests are written and approved before implementation |
| VII. Simplicity | ⚠️ Pass with notes | No new agent. One new project (isolates cloud dependencies), one seam interface (mandated by Principle VI), no logging framework. See Complexity Tracking |

**Technology stack note (needs your confirmation)**: the constitution lists Microsoft Agent
Framework / Semantic Kernel as the agent framework. This slice makes a single chat call and has
no agents, tools, or orchestration, so it uses the Azure OpenAI client library directly behind
`IChatModelClient`. If you prefer Semantic Kernel's chat-completion connector here, only the
adapter changes. Recorded in Complexity Tracking.

**Gate result: PASS — checked against constitution v1.0.1. The stack note was confirmed by the user on 2026-10-11 (Azure client library now; revisit the agent framework when orchestration is added).**

## Project Structure

### Documentation (this feature)

```text
specs/004-model-draft-generator/
├── plan.md                                  # This file
├── research.md                              # Phase 0 output
├── data-model.md                            # Phase 1 output
├── quickstart.md                            # Phase 1 output
├── contracts/
│   └── AzureOpenAIDraftGenerator.md         # Phase 1 output (generator, seam, config, response format)
├── checklists/
│   └── requirements.md                      # Spec quality checklist
└── tasks.md                                 # Phase 2 output (/speckit-tasks — not yet created)
```

### Source Code

```text
src/AgenticChangeOps.Intake/                              (core, no new dependencies)
├── Models/
│   ├── Enums/
│   │   └── GenerationFailureCategory.cs      (NEW: Unreachable, TimedOut, ResponseTooLarge, MalformedOutput)
│   └── DraftGenerationException.cs           (NEW: carries Category only)
└── Services/
    └── RequirementsDraftingService.cs        (MODIFY: generator-failure error includes category)

src/AgenticChangeOps.Intake.AzureOpenAI/                  (NEW project)
├── AzureOpenAIDraftGenerator.cs              (NEW: IRequirementsDraftGenerator)
├── AzureOpenAIGeneratorOptions.cs            (NEW: endpoint, deployment, limits; FromEnvironment + validation)
├── GeneratorConfigurationException.cs        (NEW: names the setting, never the value)
├── DraftPromptBuilder.cs                     (NEW: instructions + delimited change request data)
├── DraftResponseParser.cs                    (NEW: single-fence tolerant, strict structure)
├── IChatModelClient.cs                       (NEW: seam; ChatModelResponse record)
├── AzureOpenAIChatModelClient.cs             (NEW: SDK adapter, identity auth, no retries)
└── ModelFailureMapper.cs                     (NEW: status/exception to category)

tests/AgenticChangeOps.Intake.Tests/
├── Fakes/
│   ├── FakeChatModelClient.cs                (NEW)
│   └── FakeEnvironment.cs                    (NEW: dictionary-backed variable lookup)
├── Unit/
│   ├── GeneratorConfigurationTests.cs        (NEW: US3, US4)
│   ├── DraftPromptTests.cs                   (NEW: US1 scenario 2, edge case on injected field text)
│   ├── DraftResponseParserTests.cs           (NEW: US1, US2 malformed paths, fence rule)
│   ├── DraftGeneratorFailureTests.cs         (NEW: US2 timeout, oversize, unreachable, no leakage)
│   ├── ModelFailureMapperTests.cs            (NEW: FR-008a mapping)
│   ├── GeneratorDraftingIntegrationTests.cs  (NEW: real service + store + new generator over fake client)
│   └── NoSecretsTests.cs                     (NEW: US4 scenario 2, repo scan for key/endpoint patterns)
└── Live/
    └── LiveModelCheckTests.cs                (NEW: US5, skipped unless explicitly enabled)

AgenticChangeOps.sln                                       (MODIFY: add the new project)
```

**Structure Decision**: Put the cloud dependencies in their own project so the core library
stays dependency-free and the slices 001 to 003 suite still builds and runs with nothing but the
.NET SDK. Tests stay in the existing test project (as slices 002 and 003 did) so one
`dotnet test` shows the whole picture, including the live check as an explicit skip.

## Complexity Tracking

| Item | Why Needed | Simpler Alternative Rejected Because |
|------|------------|--------------------------------------|
| New project `AgenticChangeOps.Intake.AzureOpenAI` | Keeps `Azure.*` packages out of the core library and its tests-only dependency story from slice 003 | Putting the SDK into the core project would make every consumer of intake and approval take cloud dependencies for a capability they may not use |
| `IChatModelClient` seam below `IRequirementsDraftGenerator` | Prompt building, parsing, timeout, size limit and failure mapping are real logic that must be tested without the SDK (Principle VI) | Testing only through `IRequirementsDraftGenerator` would leave all of that logic untestable offline or force it into the adapter |
| `DraftGenerationException` and `GenerationFailureCategory` added to the core library, and a small change to `RequirementsDraftingService`'s catch block | FR-009 requires the category to reach the caller in the slice 003 error; the service is the only place that builds that error | Returning the category in the message text from the generator would be an untyped string; a result type would change the slice 003 interface. The change is additive: other exceptions keep the existing message |
| Azure OpenAI client library used directly rather than Semantic Kernel (stack note above) | One chat call, no agents, tools, or orchestration in this slice | Adopting the framework now adds a large dependency surface for no behaviour; the seam lets it be swapped in when orchestration arrives |
| No logging implemented | Spec allows only outcome category and elapsed time; the category already travels in the error, so a logging dependency adds no information | A logger would add a package and a leakage surface to defend against in SC-004 for nothing |

## Post-Implementation Compliance Review (2026-10-11)

Checked against constitution v1.0.1: **PASS**, with one item still open.

- **I. Synthetic Data Only**: all fixtures and the live-check input are fictional; the secret
  guards in `NoSecretsTests` pass, and were verified to fail when a probe violation is added.
- **II. Deterministic Routing**: the generator writes nothing and calls no service; model text is
  only parsed into `DraftContent`. The one core change is a catch block that reports a category.
- **III. Human Approval Gates**: non-consequential classification unchanged (content only).
- **IV. Typed Auditable Records**: no persisted record changed.
- **VI. Test-First**: tests were written, shown red and approved before each story's
  implementation. The SDK is reached only through `IChatModelClient` in
  `AzureOpenAIChatModelClient`; no automated test constructs it (guard in
  `GeneratorConfigurationTests`); the live check is skipped unless explicitly enabled.
- **VII. Simplicity**: no new agent, no logging package, one seam interface.
- **Resolved (2026-10-11)**: the user confirmed using the Azure OpenAI client library directly.
  The agent framework is to be revisited when orchestration is added; no constitution amendment
  is needed for this slice.
- **Not verified here**: behaviour against the real service. That is the manual live check
  (quickstart step 4), which has not been run.
