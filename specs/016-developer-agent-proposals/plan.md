# Implementation Plan: Developer Agent Proposes A Change Set

**Branch**: `feature/developer-agent-proposals` | **Date**: 2026-10-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/016-developer-agent-proposals/spec.md`

## Summary

Add the Developer agent as its own project. For an Approved change request that has a requirements
draft and a design draft based on the latest requirements draft, it asks the model (with the
Architect's read-only sample tools, required) for a text-only change proposal, then the system
verifies every edit against the real sample files before storing anything: each quote must appear
exactly once in its named file, each "add" must name a file that does not exist, every path must stay
inside the sample, and count and size ceilings must hold. One bad edit rejects the whole proposal.
Stored proposals are typed, versioned, linked to the requirements and design versions, and audited with
a new `ProposalGenerated` entry plus one `ToolCalled` entry per tool call. Three HTTP operations mirror
the design operations. The workflow status gains one additive, opt-in field that offers the proposal
as a person-requested step; the automatic path never reads it. With the Developer unused, nothing
existing changes.

## Technical Context

**Language/Version**: C# on .NET 8 (unchanged)

**Primary Dependencies**: none new. Reuses `ModelCaller`, `IToolChatModelClient`, `IToolSessionFactory`,
`SampleToolSessionFactory`, `AgentFrameworkToolChatModelClient` and `SamplePathResolver` as they are.

**Storage**: Existing `agent_outputs` table and in-memory store via `SaveAgentOutput<T>` /
`GetAgentOutputs<T>`, keyed by the record type. No layout change (layout version stays 2).

**Testing**: xUnit with fakes (scripted model client, fake tool session, in-memory store); one shared
store behaviour suite already runs against both stores and gains proposal cases. No live model in the
suite. A live check is the maintainer's, run separately.

**Target Platform**: unchanged (ASP.NET minimal API host, Windows/Linux)

**Project Type**: multi-project .NET solution, one project per agent (slice 007 rule)

**Performance Goals**: none beyond the existing time limit of the tool session; verification reads at
most the files the proposal names, each capped by the existing per-file size limit.

**Constraints**: nothing is written, created, deleted, compiled or run; no network beyond the existing
model call; no file content, model text, endpoint, deployment name or credential in any error, log or
response; Developer must not reference another agent or the sample-code project.

**Scale/Scope**: defaults of at most 5 changes and 6000 bytes of edit text per proposal.

## Constitution Check

*GATE: passed before research; re-checked after design (v1.0.5).*

| Principle | Status |
|---|---|
| I. Synthetic data | Pass. Only the existing synthetic sample is read; test fixtures are invented. |
| II. Deterministic routing | Pass. Eligibility, re-check before store, quote verification, limits and the workflow offer are plain C#. The model only fills content fields; the "not compiled, run or tested" statement is a constant set by the system. |
| III. Human gates | Pass. Producing a proposal is classified non-consequential (nothing leaves the system, nothing is applied, no status moves). Applying is not built. |
| IV. Typed auditable records | Pass with additive changes: new record `ChangeProposal` (references the requirements and design versions it was based on, immutable once stored), `AuditEventType.ProposalGenerated` appended, `WorkflowStep.Proposal` appended, one optional `WorkflowStatus` member omitted when null. Listed in Complexity Tracking. |
| V. Spec-driven | Pass. Spec clarified; this plan precedes tasks; no code until tasks are approved. |
| VI. Test-first | Pass. Red tests are written and shown for approval before implementation. Model stays behind `IProposalGenerator` and the existing client seams. |
| VII. Simplicity | Pass with notes. The Developer is one of the four agents (no fifth). One new project, mirrors the Architect. A small copy of the Architect's tool-call audit-entry mapping is kept in the Developer project; see Complexity Tracking. |

Technology Stack: the Agent Framework / MCP notes say tools for another agent need their own slice and
spec, and the Architect is named as the only user. This slice is that slice for the Developer, so a
PATCH amendment (1.0.5 to 1.0.6) is drafted in `constitution-amendment-draft.md` and opened as its own
PR before this slice's PR merges, following the 011/015 precedent. **Gate status: PASS before research
and after design, subject to that amendment.**

## Project Structure

### Documentation (this feature)

```text
specs/016-developer-agent-proposals/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── http-api.md
│   ├── verification-rules.md
│   ├── audit-and-record.md
│   ├── settings-and-failures.md
│   └── http-api-unchanged.md
├── constitution-amendment-draft.md   # written at the first task, not by /speckit-plan
└── tasks.md                          # /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.Agents.Developer/            # NEW: everything specific to the Developer
│   ├── ChangeProposal.cs  ProposedChange.cs  ProposalStatus.cs  ProposalChangeKind.cs
│   ├── ProposalContent.cs  ProposalLimits.cs
│   ├── IProposalGenerator.cs  AzureOpenAIProposalGenerator.cs
│   ├── ProposalPromptBuilder.cs  ProposalResponseParser.cs  ProposalContentValidator.cs
│   ├── ProposalVerifier.cs  ISampleFiles.cs  ProposalRejection.cs
│   ├── ProposalBasis.cs  (ProposalRequirementsBasis, ProposalDesignBasis, IProposalBasisSource)
│   ├── IProposalService.cs  ProposalService.cs  ProposalResult.cs
│   └── ProposalStoreExtensions.cs
├── AgenticChangeOps.Intake/Models/Enums/AuditEventType.cs     # + ProposalGenerated (appended)
├── AgenticChangeOps.Workflow/                    # additive offer only
│   ├── WorkflowStep.cs  (+ Proposal, appended)   IProposalOffer.cs  (new)
│   ├── WorkflowStatus.cs (+ OfferedStep, omitted when null)   WorkflowService.cs (optional ctor arg)
└── AgenticChangeOps.Api/
    ├── Endpoints/ProposalEndpoints.cs            # three routes
    ├── Composition/ProposalAvailability.cs  ProposalBasisAdapter.cs  SampleFilesAdapter.cs
    │               ProposalOffer.cs  (+ registration in ApiServiceRegistration.cs)
    └── Errors/ErrorMapper.cs                     # + ForProposing, proposal not-found/none/off

tests/
└── AgenticChangeOps.Agents.Developer.Tests/      # NEW: Unit, Fakes, StructureTests, NoSecretsTests, Live
    (+ added cases in Api.Tests, Persistence.Tests shared store suite, Workflow.Tests, ModelAccess.Tests)
```

**Structure Decision**: mirror the Architect: own project referencing only Intake and ModelAccess,
never another agent and never `AgenticChangeOps.SampleCode`. Anything that needs the sample folder on
disk (the file lookup used by verification) is an interface owned by the Developer and implemented by
an adapter in the Api composition root over `SamplePathResolver`, the same way
`RequirementsBasisAdapter` feeds the Architect. The `AgentsHoldNoSharedBehaviourCopyTests` guard scans
every `Agents.*` folder, so the Developer must not contain `CancellationTokenSource`, `JsonDocument`,
`GetProperty(`, `IsNullOrWhiteSpace(` or code fences; it uses `ModelCaller`, `ModelAnswerReader` and
`SectionNormaliser`, adding shared pieces to ModelAccess only if a second caller needs them.

## Key design decisions (detail in research.md)

1. **Verification is the system's job, after validation, before storing.** Order: shape and
   normalisation, count and size ceilings, then per-edit path, existence and quote checks against the
   real files, then the store step under the store lock.
2. **Re-check at store time.** Inside the store lock, eligibility is re-run and both the requirements
   version and design version must equal those the model was given; otherwise refuse with the outdated
   error and store nothing (clarification Q1).
3. **Tools are mandatory.** No proposal without a tool session. Switched off or invalid settings means
   the route refuses before any model call; a session that cannot be opened reuses the existing
   `ToolsUnavailable` category.
4. **Workflow offer is opt-in and additive.** `WorkflowStatus.OfferedStep` is set to `Proposal` only
   when the stage is `DesignComplete` and the Developer is available. `NextStep`, `Stage`, `Reason` and
   the automatic path are untouched, so automatic advancement cannot run it and, with tools off, every
   status response is byte-identical. This resolves the tension between FR-020 and FR-021; the exact
   wording needs your approval (see research.md R7).
5. **Rejections are categories, never content.** A rejected proposal returns a fixed reason code and
   message with no path, quote or model text.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| New project `AgenticChangeOps.Agents.Developer` (Principle VII) | The Developer is one of the four named agents; slice 007 requires one project per agent with a structure test | Putting it in the Architect project would break the one-agent-per-project rule and the structure tests |
| Small copy of the tool-call audit mapping (about 20 lines) in the Developer service | Two callers now exist, but a shared home needs a new dependency edge (Intake would have to see `ToolCallRecord`) or a new project, and the spec forbids changing the Architect's behaviour | Extracting now would edit the Architect service for no behavioural gain; extraction is noted as a follow-up once a third caller or a shared home exists |
| Additive members (Principle IV): `AuditEventType.ProposalGenerated`, `WorkflowStep.Proposal` (appended), `WorkflowStatus.OfferedStep` (omitted when null), optional `proposalOffer` constructor argument on `WorkflowService` | Spec requires a new audit type and a workflow mention | A separate status route would add a second place to read workflow state; all additions leave every existing value valid and serialised output unchanged |
| New persisted record type `ChangeProposal` | Spec requires a stored typed record | Reusing the design record would mix agents; the generic agent-output table needs no layout change |
| Separate tools switch is not added; Developer reuses `AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED` | Spec: "the sample tools keep their ... off-by-default switch" | A second switch would let one agent's tools be on while the other's are off, which the spec does not ask for |

## Constitution compliance check (pre-code)

Reviewed against constitution v1.0.5 (Development Workflow step 4) before any code or test file.

| Check | Result |
|---|---|
| Principle I, synthetic data | Pass. Only the existing invented `sample-codebase/` is read; fixtures are invented; hostile text uses a made-up marker. |
| Principle II, deterministic routing | Pass. Eligibility, the store-time re-check, quote verification, ceilings and the workflow offer are plain C#. The model fills content fields only; the "not compiled, run or tested" note is a constant. |
| Principle III, human gates | Pass. Producing a proposal is classified non-consequential: nothing leaves the system, nothing is applied, no status moves. Applying is not built. |
| Principle IV, typed auditable records | Pass with additive changes only (`ChangeProposal`, `ProposalGenerated`, `WorkflowStep.Proposal`, optional `OfferedStep`), listed in Complexity Tracking. |
| Principle V, spec-driven | Pass. Spec, plan and tasks approved by the maintainer before code. |
| Principle VI, test-first | Pass. Tests are written first and shown red for approval (T028). Model behind interfaces; fakes only. |
| Principle VII, simplicity | Pass with note. The Developer is one of the four agents; the audit-mapping copy is recorded in Complexity Tracking. |
| Technology Stack | Amendment 1.0.5 to 1.0.6 drafted at T002, opened as its own PR (T049) before the slice PR merges. |
