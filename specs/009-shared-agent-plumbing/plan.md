# Implementation Plan: Shared Agent Plumbing

**Branch**: `feature/shared-agent-plumbing` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/009-shared-agent-plumbing/spec.md`

## Summary

Behaviour-preserving extraction. Four pieces that are line-for-line identical in the Requirement and Architect agents move into the existing agent-neutral `AgenticChangeOps.ModelAccess` project, which both agents already reference:

1. **`ModelCaller`**: the single time-limited model call, the output-size bound, the cut-off/oversize/blank checks and the mapping of every failure to a category with no detail attached.
2. **`ModelAnswerReader` + `ModelAnswer`**: reading a bare JSON object or one fenced block, then strict-typed text and list reads by section name.
3. **`SectionNormaliser`**: trimming, blank-item removal, count and length limits for text and list sections, with the required/optional choice passed in by the agent. It returns an error string, not an intake validation type, so the shared project stays free of intake.

Each agent keeps its existing entry points (`DraftResponseParser.Parse`, `DesignResponseParser.Parse`, `DraftContentValidator.Validate`, `DesignContentValidator.Validate`, `AzureOpenAIDraftGenerator`, `AzureOpenAIDesignGenerator`) as thin adapters containing only its section names, content type and required-section choices. Existing tests, structure tests and the API composition therefore need **no edits**. The two drafting services, prompts, eligibility, storage and audit are untouched.

Because no HTTP test today runs a real generator and parser end to end (API tests use fake generators), a model-backed characterization test with goldens is captured **before** any code moves, for both agents.

## Technical Context

**Language/Version**: C# on .NET 8 (net8.0), unchanged

**Primary Dependencies**: none added. `System.Text.Json` is part of the framework already used by both agents.

**Storage**: In-memory store only, untouched

**Testing**: xUnit; fakes only, no live model in the suite. Live model check stays opt-in and skipped as today.

**Target Platform**: Unchanged (CI on ubuntu runner, local Windows)

**Project Type**: Multi-project .NET solution (library projects, minimal API, one test project per library)

**Performance Goals**: N/A, no behaviour change

**Constraints**: Zero assertion changes; zero reference changes expected; zero change to HTTP status codes, bodies, headers, error codes; no new setting, dependency, endpoint, capability, project, or record shape; shared source must name no agent, intake or stored record

**Scale/Scope**: 3 new shared types plus 2 small result types in ModelAccess; 6 agent files shrink to adapters; 1 new test class group in ModelAccess.Tests; 1 new baseline test in Api.Tests with goldens; 1 measurement script in the slice folder. Pre-slice duplication prototype: about 240 of 293/340 normalised lines shared between the agents (crude, whole-project, includes the two services); the real figures come from the script in task T001.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

Reviewed against constitution v1.0.3.

| Principle | Assessment |
|---|---|
| I. Synthetic data only | Pass. New fixtures and goldens use fictional content and the existing sentinel endpoint and deployment strings; existing secret scans keep covering `src`, `tests`, `specs`. |
| II. Deterministic routing | Pass. No routing, transition or orchestration change; generators stay content-only. |
| III. Human approval gates | Pass. Approval code and audit records untouched; drafting stays non-consequential. |
| IV. Typed auditable records | Pass. No record, enum value or serialized shape changes (FR-014). Shared results are typed (`SectionOutcome<T>`), no `object` crosses a boundary. |
| V. Spec-driven | Pass. Spec exists and is clarified; plan precedes tasks; no code before tasks are approved. |
| VI. Test-first | Pass. Shared tests and the model-backed baseline are written and approved before code moves; LLM access stays behind `IChatModelClient`. |
| VII. Simplicity | Pass. Every shared piece has two existing callers; the extraction adds no behaviour; no generic framework, base class or plug-in registry is introduced. No agent is added. |

Technology stack: unchanged. **Gate status: PASS before research and after design.** No Complexity Tracking row is needed.

## Project Structure

### Documentation (this feature)

```text
specs/009-shared-agent-plumbing/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── shared-api.md            # the three shared pieces, their inputs, outputs and guarantees
│   ├── dependency-rules.md      # references and neutrality rules, with the check for each
│   ├── http-api-unchanged.md    # what is recorded before and compared after
│   └── kept-separate-register.md# similar code left separate, with reasons
├── checklists/requirements.md
├── baseline-tests.txt           # created in T001 (pass counts before the change)
├── duplication.md               # created in T001/final task (before and after)
└── tasks.md                     # produced by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.ModelAccess/               # existing, agent-neutral, no project references
│   ├── ModelCaller.cs                          # NEW: time-limited call, size/blank checks, failure mapping
│   ├── ModelAnswerReader.cs                    # NEW: unwrap + parse + map, malformed on any failure
│   ├── ModelAnswer.cs                          # NEW: ReadString / ReadList by section name
│   ├── SectionNormaliser.cs                    # NEW: text and list normalisation + SectionOutcome<T>
│   └── (existing files unchanged)
├── AgenticChangeOps.Agents.Requirement/
│   ├── AzureOpenAIDraftGenerator.cs            # shrinks: prompt + ModelCaller + parser
│   ├── DraftResponseParser.cs                  # shrinks: section names + DraftContent only
│   └── DraftContentValidator.cs                # shrinks: required-section choices + DraftContent only
├── AgenticChangeOps.Agents.Architect/
│   ├── AzureOpenAIDesignGenerator.cs           # shrinks the same way
│   ├── DesignResponseParser.cs
│   └── DesignContentValidator.cs
└── (Intake, Api unchanged)
tests/
├── AgenticChangeOps.ModelAccess.Tests/         # NEW tests: ModelCaller, ModelAnswerReader, SectionNormaliser,
│   │                                           #   stand-in third agent, neutrality scan additions
│   └── Fakes/ScriptedChatModelClient.cs        # NEW: tiny neutral fake (no agent sample text)
├── AgenticChangeOps.Api.Tests/Baseline/        # NEW SharedPlumbingBaselineTests + goldens (both agents, real generators)
└── (agent test projects and existing tests unchanged)
```

**Structure Decision**: No new project. The shared place is `ModelAccess` because both agents already reference it, it already holds the model seam, the failure carrier and the failure categories the shared pieces use, and its existing neutrality tests already forbid agent and intake names. Agents keep adapter files under their existing names, so the type-location structure tests and every test reference stay as they are.

## Complexity Tracking

No constitution violations to justify. Recorded for the review:

| Item | Note |
|---|---|
| Shared helpers in ModelAccess | Principle VII permits shared helpers with at least two existing callers; each has exactly two (one per agent). |
| Adapters kept in each agent | Not a new abstraction: they are the existing files, reduced. They keep `StructureTests` and all test references valid without edits. |
