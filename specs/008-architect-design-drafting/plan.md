# Implementation Plan: Architect Design Drafting for Approved Change Requests

**Branch**: `feature/architect-agent` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/008-architect-design-drafting/spec.md`

## Summary

Add the second of the four V1 agents, the Architect, as its own project `AgenticChangeOps.Agents.Architect` beside the Requirement agent, following the layout slice 007 established. Given a `ChangeRequestId`, it produces a typed, versioned design draft (approach, affected components, impact and risks, dependencies, rollback, open questions) for a change request that is `Approved` and has a requirements draft. It reuses the agent-neutral `ModelAccess` seam, stores through the existing generic agent-output point with a new `DesignGenerated` audit event, and is exposed by three HTTP operations that follow the requirements drafting conventions. The Requirement agent, intake and approval services, and `ModelAccess` are not edited. The only edits outside the new projects are one appended enum value in intake (additive, see Complexity Tracking) and registration in the API composition root.

The Architect must not reference the Requirement agent (rule D6 of slice 007). It therefore declares its own small read port, `IRequirementsBasisSource`, and the API composition root supplies the adapter that reads the latest requirements draft through the Requirement agent's existing service.

The design registration is additive: a new private method `CreateDesignSetup` in `ApiServiceRegistration.cs` reads the same `AzureOpenAIGeneratorOptions.FromEnvironment` keys independently, builds an `AzureOpenAIDesignGenerator`, and records configured or not-configured in a new `DesignAvailability` singleton (sibling of `DraftingAvailability`). Existing registration lines and the private `DraftingSetup` are not touched (research R10).

## Technical Context

**Language/Version**: C# on .NET 8 (net8.0), as today

**Primary Dependencies**: None added. Reuses `AgenticChangeOps.Intake` (store, models, clock, ids) and `AgenticChangeOps.ModelAccess` (chat seam, options, failure categories). xUnit 2.5.3 for tests; Swashbuckle.AspNetCore 6.6.2 already in the API.

**Storage**: In-memory store only. Design drafts are saved with their audit entry through `IChangeRequestStore.SaveAgentOutput<T>` and read with `GetAgentOutputs<T>`.

**Testing**: xUnit with fakes (`IChatModelClient`, `IDesignDraftGenerator`, `IRequirementsBasisSource`). No live model in the suite; one opt-in live check, skipped unless the operator enables it, mirroring the Requirement agent's.

**Target Platform**: CI on ubuntu runner; local Windows. Unchanged.

**Project Type**: Multi-project .NET solution (library projects, minimal API, test projects).

**Performance Goals**: None beyond requirements drafting. Drafting is on demand, one model call, no retry.

**Constraints**: No edit to `Agents.Requirement`, intake and approval services, or `ModelAccess` beyond the single appended enum value; no new package; no model text, endpoint, deployment name or credential in any error, log or response; every existing test passes with no assertion changed (the API golden files gain additions only, including one appended `AuditEventType` value; see research R7).

**Scale/Scope**: About 14 source files in one new project, 1 new test project, 1 new API endpoint file plus additive edits to 3 API files, 1 appended enum value, 2 new solution projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

Reviewed against constitution v1.0.2.

| Principle | Assessment |
|---|---|
| I. Synthetic data only | Pass. All fixtures are synthetic; the existing secret and endpoint scans cover `src`, `tests` and `specs`. Test model text includes fake endpoints and keys, clearly synthetic. |
| II. Deterministic routing | Pass. Eligibility (Approved, requirements draft present) and storage are plain C# predicates in the service; the generator only returns content and cannot influence status, approval or any next step. |
| III. Human approval gates | Pass. Design drafting is non-consequential content generation (same classification as requirements drafting). It never approves, rejects, defers or clears; the Approved status it requires comes from the existing human approval gate. A human reviews the draft. |
| IV. Typed auditable records | Pass. `DesignDraft` is a typed record implementing `IAgentOutput`, linked to `ChangeRequestId` with `AgentName`, timestamp and the basis requirements version. Every stored draft has a `DesignGenerated` audit entry saved atomically with it. Appending one enum value is the additive change Principle IV allows (recorded below). Input and output are carried by the linked requirements version and the stored sections, as for requirements drafts; see Complexity Tracking. |
| V. Spec-driven | Pass. Spec exists and was checked for ambiguity; this plan precedes tasks. No code until tasks are approved. |
| VI. Test-first | Pass. Tests are written and shown red for approval before implementation. All model access is behind interfaces with fakes. |
| VII. Simplicity | Pass. The Architect is one of the four V1 agents; no fifth agent. No shared helper is introduced: the parser, validator and prompt builder are agent-specific and deliberately not extracted from the Requirement agent (extraction would edit it). The one new abstraction, `IRequirementsBasisSource`, is a single-method port needed to honour D6; it is recorded below. |

Technology stack: unchanged (C# .NET 8, Azure OpenAI through `ModelAccess`, xUnit).

**Gate status: PASS before research. PASS after design** (re-checked against the data model and contracts: no new violation; the three items under Complexity Tracking are the only deviations and each is justified).

## Project Structure

### Documentation (this feature)

```text
specs/008-architect-design-drafting/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── http-api.md
│   └── dependency-rules.md
├── checklists/requirements.md
└── tasks.md            # produced by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.Intake/                       # one appended enum value only
│   └── Models/Enums/AuditEventType.cs             # + DesignGenerated (appended last)
├── AgenticChangeOps.ModelAccess/                  # unchanged
├── AgenticChangeOps.Agents.Requirement/           # unchanged
├── AgenticChangeOps.Agents.Architect/             # NEW: everything specific to the Architect
│   ├── AgenticChangeOps.Agents.Architect.csproj   # references Intake and ModelAccess only
│   ├── DesignDraft.cs                             # typed record, IAgentOutput
│   ├── DesignDraftStatus.cs                       # { Draft }
│   ├── DesignContent.cs                           # raw generator output before validation
│   ├── DesignContentValidator.cs                  # completeness and size rules
│   ├── DesignDraftingResult.cs                    # success, errors, failure category
│   ├── RequirementsBasis.cs                       # read model + IRequirementsBasisSource port
│   ├── IDesignDraftGenerator.cs                   # content-only seam
│   ├── IDesignDraftingService.cs
│   ├── DesignDraftingService.cs                   # eligibility, validation, version, save
│   ├── DesignPromptBuilder.cs
│   ├── DesignResponseParser.cs
│   ├── AzureOpenAIDesignGenerator.cs              # uses IChatModelClient from ModelAccess
│   └── DesignDraftStoreExtensions.cs              # SaveDesign / GetDesigns over the generic point
└── AgenticChangeOps.Api/                          # registration plus HTTP surface
    ├── Endpoints/DesignEndpoints.cs               # NEW: POST /designs, GET /designs/latest, GET /designs
    ├── Composition/RequirementsBasisAdapter.cs    # NEW: IRequirementsBasisSource over the requirements service
    ├── Composition/DesignAvailability.cs          # NEW: records whether design drafting is configured
    ├── Composition/ApiServiceRegistration.cs      # + design registrations (additive)
    ├── Errors/ErrorMapper.cs                      # + ForDesigning, NoDesignDraft, DesignNotConfigured (additive)
    └── Program.cs                                 # + DesignEndpoints.Map, description text
tests/
├── AgenticChangeOps.Agents.Architect.Tests/       # NEW
│   ├── Fakes/ (fixture, fake generator, fake chat client, fake requirements source)
│   ├── Unit/ (eligibility, drafting, versioning, validation, parser, prompt, failures,
│   │          retrieval, concurrency, isolation)
│   ├── Live/ (opt-in live check)
│   └── StructureTests.cs                          # D5 and D6 for the Architect
└── AgenticChangeOps.Api.Tests/                    # + DesignEndpointTests; goldens gain additive entries
```

**Structure Decision**: Follow slice 007: `Agents.Architect` references only `Intake` and `ModelAccess`; the API composition root alone knows both agents and wires them. The Architect names no other agent; the Requirement agent's data reaches it only through the Architect-owned port.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Principle IV (schema): `AuditEventType` gains `DesignGenerated`, appended last | FR-005 requires an audit entry of a new type. Purely additive: no existing value, entry or serialized form changes. This is the one edit to intake, and it is not a behaviour change there. | Reusing `DraftGenerated` would make design and requirements audit entries indistinguishable and contradict the spec. |
| Principle VII: `IRequirementsBasisSource` port in the Architect project with one production adapter in the API | Rule D6 forbids the Architect referencing the Requirement agent, yet it must read the latest requirements draft and its version. | Referencing `Agents.Requirement` breaks D6 and the structure tests. Putting a requirements read model in intake edits intake and makes it know agent output shapes. |
| Principle IV (minimum fields): `DesignDraft` has no separate `InputPayload`/`OutputPayload` members | Matches the accepted shape of `RequirementsDraft` (slices 003–005). Its input is reproducible from the linked `ChangeRequestId` and `RequirementsVersion`; its output is the six stored sections. Adding fields only here would diverge from the Requirement agent, and adding them there is forbidden by this slice. | Adding the fields to both agents belongs to a dedicated slice. Resolved by constitution v1.0.3 (PATCH clarification of Principle IV: input and output may be carried by reference to immutable upstream versions), drafted in this slice and merged by its own PR before this slice merges. |
| Duplicated shape: parser, validator and prompt builder resemble the Requirement agent's | Extracting a shared helper would edit the Requirement agent and `ModelAccess`, which the spec forbids; the sections differ. | A shared helper violates the "no edits" rule for this slice. A later slice may consolidate once both agents are stable. |
