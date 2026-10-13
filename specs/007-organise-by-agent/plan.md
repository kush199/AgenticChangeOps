# Implementation Plan: Organise Agent Code by Agent

**Branch**: `feature/organise-by-agent` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/007-organise-by-agent/spec.md`

## Summary

Behaviour-preserving restructure. The Requirement agent's code moves out of `AgenticChangeOps.Intake` and `AgenticChangeOps.Intake.AzureOpenAI` into one agent project, `AgenticChangeOps.Agents.Requirement`. The model-access seam, its Azure implementation, run-time settings and failure mapping move into an agent-neutral project, `AgenticChangeOps.ModelAccess`. Intake keeps one generic, agent-neutral "agent output" extension point on its store (clarification Q1, option B) and no longer names the draft type. Existing store call sites in tests and services stay literally unchanged through extension methods that live in the agent project. A characterization test with goldens captured **before** any move proves HTTP responses and the API description are unchanged.

## Technical Context

**Language/Version**: C# on .NET 8 (net8.0), unchanged

**Primary Dependencies**: Azure.AI.OpenAI 2.1.0 and Azure.Identity 1.21.0 (move with the Azure implementation, same versions); Swashbuckle.AspNetCore 6.6.2; xUnit 2.5.3. No dependency added.

**Storage**: In-memory store only, unchanged

**Testing**: xUnit; fakes only, no live model in the suite. Live model check stays opt-in and skipped as today.

**Target Platform**: Same as today (CI on ubuntu runner, local Windows)

**Project Type**: Multi-project .NET solution (library projects, minimal API, test projects)

**Performance Goals**: N/A, no behaviour change

**Constraints**: Zero assertion changes in existing tests; zero change to HTTP status codes, bodies, headers, error codes; zero new settings, dependencies or capabilities; no type renamed or removed

**Scale/Scope**: About 25 source files relocated, 4 new projects (1 agent, 1 neutral, and a test project for each), the old `Intake.AzureOpenAI` project retired, 1 generic interface and 2 generic store methods added in intake

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

Reviewed against constitution v1.0.1 at planning; re-checked against v1.0.2 on 2026-10-13 (see gate status below).

| Principle | Assessment |
|---|---|
| I. Synthetic data only | Pass. Goldens use the same synthetic fixtures as existing tests; the existing secret and endpoint scans keep covering `src`, `tests`, `specs` and are re-pointed at the moved folder (see research R6). |
| II. Deterministic routing | Pass. No routing, transition or orchestration change; the generator stays content-only. |
| III. Human approval gates | Pass. Approval code and its audit records are untouched. Drafting remains non-consequential and unchanged. |
| IV. Typed auditable records | Pass. The generic extension point is typed (`IAgentOutput`, generic over the concrete record); no `object` crosses a boundary. Persisted shape of every record and enum value is unchanged; additive interface implementation only. Recorded in Complexity Tracking. |
| V. Spec-driven | Pass. Spec exists and is clarified; plan precedes tasks. |
| VI. Test-first | Pass. Goldens and structure tests are written and approved red or green-on-baseline before any move; LLM interactions stay behind `IChatModelClient` and `IRequirementsDraftGenerator`. |
| VII. Simplicity | Pass under v1.0.2 (relocated code and a one-for-one generic replacement are not "introduced"; both recorded in Complexity Tracking). The neutral project and the generic store point are used today by one agent only. They are required by the explicit slice rules and by clarification Q1, and no new behaviour or speculative helper is added. Only four agents in V1 is unaffected (no agent added). |

Technology stack: unchanged (C# .NET 8, Azure OpenAI, xUnit). Spec FR-011 forbids stack changes.

**Gate status: PASS (re-checked 2026-10-13 against constitution v1.0.2, merged in PR #12).** The v1.0.2 PATCH amendment to Principle VII states that relocated or slice-separated code, and a one-for-one generic replacement that removes an agent dependency, are not "introduced" abstractions, provided no behaviour is added and the slice records them in Complexity Tracking. Both rows are recorded below and the slice adds no behaviour (baseline goldens unchanged).

## Project Structure

### Documentation (this feature)

```text
specs/007-organise-by-agent/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── dependency-rules.md
│   ├── type-move-map.md
│   └── http-api-unchanged.md
├── checklists/requirements.md
└── tasks.md            # produced by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.Intake/                  # no project references; no agent knowledge
│   ├── Models/ ... (ChangeRequest, audit, approval, enums; draft types removed)
│   ├── Models/IAgentOutput.cs                # NEW generic extension point (ChangeRequestId, Version)
│   └── Stores/ (IChangeRequestStore: SaveAgentOutput<T>, GetAgentOutputs<T>; in-memory impl)
├── AgenticChangeOps.ModelAccess/             # NEW, agent-neutral; no project references
│   ├── IChatModelClient.cs                   # seam + ChatModelResponse
│   ├── AzureOpenAIChatModelClient.cs         # Azure implementation
│   ├── AzureOpenAIGeneratorOptions.cs        # run-time settings
│   ├── GeneratorConfigurationException.cs
│   ├── ModelFailureMapper.cs                 # failure mapping
│   ├── DraftGenerationException.cs           # failure carrier (moved, name kept)
│   └── GenerationFailureCategory.cs          # failure categories (moved, name kept)
├── AgenticChangeOps.Agents.Requirement/      # NEW, everything specific to the Requirement agent
│   ├── RequirementsDraft.cs, DraftContent.cs, DraftingResult.cs, DraftStatus.cs
│   ├── DraftContentValidator.cs
│   ├── IRequirementsDraftGenerator.cs, IRequirementsDraftingService.cs
│   ├── RequirementsDraftingService.cs
│   ├── AzureOpenAIDraftGenerator.cs          # the generator
│   ├── DraftPromptBuilder.cs, DraftResponseParser.cs
│   └── RequirementDraftStoreExtensions.cs    # SaveDraft / GetDrafts over the generic point
└── AgenticChangeOps.Api/                     # references Intake, ModelAccess, Agents.Requirement
tests/
├── AgenticChangeOps.Intake.Tests/            # intake and approval only; no agent or model references
├── AgenticChangeOps.ModelAccess.Tests/       # NEW: failure mapper, settings, hygiene scans, neutrality checks
├── AgenticChangeOps.Agents.Requirement.Tests/# NEW: drafting, validation, prompt, parser, generator, live check
└── AgenticChangeOps.Api.Tests/               # existing tests + baseline characterization test and goldens
```

**Structure Decision**: One project per concern, named for what it owns: `Agents.<Name>` for an agent, `ModelAccess` for shared model access, `Intake` for the intake and approval capabilities. A second agent is added as `AgenticChangeOps.Agents.<Other>` (plus its test project) beside the first, referencing Intake and ModelAccess, and is registered in the API composition root only.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Principle VII: shared neutral project `ModelAccess` with one agent consumer | The slice's explicit rule requires agent-neutral code in its own location and the repeatable structure for a second agent. The code is existing, relocated unchanged. | Leaving it inside the agent project breaks the stated rule and forces the second agent to reference the first. |
| Principle VII: generic `IAgentOutput` + `SaveAgentOutput<T>`/`GetAgentOutputs<T>` in intake with one concrete use | Clarification Q1 option B: intake must stop naming the draft type and still store drafts together with their audit entry atomically. | Option A (agent-owned store) would split the atomic draft-plus-audit save across two stores and change concurrency behaviour. Option C leaves intake depending on the agent. |
| Principle IV (schema): `RequirementsDraft` gains an interface implementation | Needed for the generic point. Purely additive: no member, serialized shape or value changes. | N/A; recorded as the non-breaking additive change the principle allows. |
