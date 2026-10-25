# Implementation Plan: Reviewer Agent Reviews A Change Proposal

**Branch**: `feature/reviewer-agent-review` | **Date**: 2026-10-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/018-reviewer-agent-review/spec.md`

## Summary

Add the Reviewer agent, the fourth and last of the four agents, as its own project. For an Approved
change request that has a requirements draft, a design draft based on the latest requirements draft
and a proposal based on the latest design draft, the system first runs its own re-check of every
proposed edit's quoted text against the current sample (without any model), then gives the result
and the three upstream records to the model, which returns advisory findings and open questions
only. The model uses no tools. Every finding must carry at least one reference of the form
document, list, position that resolves to a real entry in the records the review is based on; one
bad reference, an over-limit review or a malformed answer rejects the whole review and stores
nothing. The stored review is typed, versioned, Draft, linked to the three upstream versions, and
audited with a new `ReviewGenerated` entry. Latest and history reads are internal service methods
only. The shared drafting engine from slice 017 already provides the order of steps, the locked
store step and the reads, so no engine change is needed. No HTTP operation, workflow change or
change to another agent is made. With the Reviewer unused, nothing existing changes.

## Technical Context

**Language/Version**: C# on .NET 8 (unchanged)

**Primary Dependencies**: none new. Reuses `DraftingEngine`, `ModelCaller`, `IChatModelClient`
(the plain `AzureOpenAIChatModelClient`, as the Requirement agent does), `SectionNormaliser`,
`ModelAnswerReader` and `SamplePathResolver` / `SampleReader` as they are.

**Storage**: Existing `agent_outputs` table and in-memory store via `SaveAgentOutput<T>` /
`GetAgentOutputs<T>`, keyed by the record type. No layout change (layout version stays 2).

**Testing**: xUnit with fakes (scripted generator, fake sample files, in-memory store); the shared
store behaviour suite that runs against both stores gains review cases. No live model in the suite;
a live check is the maintainer's, run separately.

**Target Platform**: unchanged

**Project Type**: multi-project .NET solution, one project per agent (slice 007 rule)

**Performance Goals**: none beyond the existing model call time limit; the re-check reads at most
the files the proposal names, each capped by the existing per-file size limit.

**Constraints**: nothing is written, created, deleted, compiled or run; no network beyond the one
model call; no model text, file content, endpoint, deployment name or credential in any error, log,
audit entry or response; the Reviewer must not reference another agent or the sample-code project.

**Scale/Scope**: defaults of at most 8 findings and 8000 bytes of review text.

## Constitution Check

*GATE: passed before research; re-checked after design (v1.0.6).*

| Principle | Status |
|---|---|
| I. Synthetic data | Pass. The only file read is the existing synthetic sample, by the system's own re-check; fixtures are invented. |
| II. Deterministic routing | Pass. Eligibility, the sample re-check, reference resolution, limits, the store-time currency check and versioning are plain C#. The model only fills finding text; severity and category are labels nothing acts on. |
| III. Human gates | Pass. A review is classified non-consequential in the spec: it moves no status, approves, rejects or clears nothing, and nothing reads it to decide. A structure test keeps the Workflow project from referencing it. |
| IV. Typed auditable records | Pass with additive changes: new record `Review` (references the three upstream versions, immutable once stored) and `AuditEventType.ReviewGenerated` appended. Listed in Complexity Tracking. |
| V. Spec-driven | Pass. Spec clarified; this plan precedes tasks; no code until tasks are approved. |
| VI. Test-first | Pass. Red tests are shown for approval before implementation. The model stays behind `IReviewGenerator` and `IChatModelClient`; fakes only. |
| VII. Simplicity | Pass with notes. The Reviewer is one of the four agents (no fifth). One new project, no new framework abstraction: the slice 017 engine is used as it is. A small copy of the Developer's quote-matching rule is kept (Complexity Tracking). |

Technology Stack: the Reviewer uses the plain chat client and no tools, so it needs neither the
Agent Framework nor the sample tools. The sample is read only by the system's own re-check, which
is deterministic code. **No amendment is needed. Gate: PASS before research and after design.**
The Principle VII description of the Reviewer ("against requirements and principles") is wider than
this slice; using the principles as input is out of scope here (spec) and stays for a later slice.

## Project Structure

### Documentation (this feature)

```text
specs/018-reviewer-agent-review/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── record-and-audit.md
│   ├── system-check-rules.md
│   ├── reference-and-limit-rules.md
│   ├── settings-and-failures.md
│   └── http-api-unchanged.md
└── tasks.md              # /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.Agents.Reviewer/             # NEW: everything specific to the Reviewer
│   ├── Review.cs  (Review, ReviewFinding, ReviewReference, SystemCheck, EditCheck)  ReviewEnums.cs  ReviewLimits.cs
│   ├── ReviewContent.cs  (ReviewContent, ReviewFindingContent, ValidatedReview, ReviewValidation)
│   ├── ReviewBasis.cs  (ReviewRequirementsBasis, ReviewDesignBasis, ReviewProposalBasis, ReviewProposedChange, ReviewBases, IReviewBasisSource)
│   ├── IReviewSampleFiles.cs  SampleChecker.cs  ReferenceResolver.cs  ReviewContentValidator.cs
│   ├── IReviewGenerator.cs  AzureOpenAIReviewGenerator.cs  ReviewPromptBuilder.cs  ReviewResponseParser.cs
│   ├── IReviewService.cs  ReviewService.cs  ReviewResult.cs  ReviewStoreExtensions.cs
├── AgenticChangeOps.Intake/Models/Enums/AuditEventType.cs     # + ReviewGenerated (appended)
├── AgenticChangeOps.ModelAccess/ModelAnswer.cs, ObjectWithList.cs   # + one additive nested-list reader and its result record
└── AgenticChangeOps.Api/Composition/                          # registration only, no endpoint
    ├── ReviewBasisAdapter.cs  ReviewSampleFilesAdapter.cs  ReviewSettings.cs
    └── ApiServiceRegistration.cs  (+ ReviewSetup and IReviewService registration)

tests/
└── AgenticChangeOps.Agents.Reviewer.Tests/       # NEW: Unit, Fakes, StructureTests, NoSecretsTests, Live
    (+ added cases in Persistence.Tests shared store suite, Api.Tests registration, Workflow.Tests structure)
```

**Structure Decision**: mirror the Developer: own project referencing only Drafting, Intake and
ModelAccess, never another agent and never `AgenticChangeOps.SampleCode`. Anything that touches
the sample folder is an interface owned by the Reviewer (`IReviewSampleFiles`) and implemented by an
adapter in the Api composition root over `SamplePathResolver` and `SampleReader`. The existing guards
(`AgentsHoldNoSharedBehaviourCopyTests`, the Drafting structure tests) scan every `Agents.*` folder,
so the Reviewer must not contain `CancellationTokenSource`, `JsonDocument`, `GetProperty(`,
`IsNullOrWhiteSpace(` or code fences, and must keep the shared skeleton words out of its own copy
of the order of steps; it uses `ModelCaller`, `ModelAnswerReader`, `SectionNormaliser` and
`DraftingEngine`.

## Key design decisions (detail in research.md)

1. **System check first, then the model.** The agent's generate step runs the re-check and passes the
   result to the model as read-only data (clarification Q2). The check result and the bases it used
   travel with the generated content to the review step, so the engine needs no change.
2. **The stored check is the system's.** The model's JSON has no field for it; the stored review takes
   the system's result, whatever the model wrote (extra keys are ignored).
3. **References are typed and resolved.** The model writes `document.list[position]` (for example
   `proposal.changes[2]`); the system parses it into document, list and 1-based position and checks
   that the entry exists in the exact upstream versions used. Anything else is rejected whole
   (clarification Q1).
4. **Eligibility is the Developer's chain plus the proposal.** Existence, Approved, requirements draft,
   design draft, design based on latest requirements, proposal, proposal based on latest design and
   latest requirements. Currency is re-checked in the engine's locked step against all three versions.
5. **Failures are categories, never content.** A rejected review returns a fixed reason code and message
   with no finding text, path or quote.
6. **Delivered in five small steps**, each green on its own: (1) pure logic nothing calls, (2) prompt,
   parser and generator, (3) service, store and audit, (4) composition registration, (5) verification
   and docs.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| New project `AgenticChangeOps.Agents.Reviewer` (Principle VII) | The Reviewer is one of the four named agents; slice 007 requires one project per agent with a structure test | Putting it in another agent's project breaks the one-agent-per-project rule and its structure tests |
| Small copy of the Developer's quote-matching rule (about 15 lines: line-ending normalisation, first and second occurrence) | Two callers now exist, but sharing it means editing the Developer or adding a dependency edge, and the spec forbids changing the other agents | Extracting now edits the Developer for no behavioural gain; extraction is noted as a follow-up for a later slice |
| Additive members (Principle IV): `AuditEventType.ReviewGenerated` (appended) | Spec requires a new audit type | A separate audit mechanism would split the trail; the addition leaves every existing value valid |
| New persisted record type `Review` | Spec requires a stored typed record | Reusing another record would mix agents; the generic agent-output table needs no layout change |
| One additive method on `ModelAnswer` in ModelAccess (reads a list of objects with string members and one nested list of strings) | A finding carries a list of references; the existing `ReadObjectList` reads string members only. ModelAccess is shared plumbing, not an agent | Encoding references as one delimited string would move parsing into agent code and weaken strictness; an edit to the existing reader would risk the other agents |
| Own copies of read-model shapes (`ReviewRequirementsBasis`, `ReviewDesignBasis`, `ReviewProposalBasis`) | The Reviewer may not reference the other agents; the same pattern the Developer uses | Referencing the other agents' records breaks the structure tests |

## Complexity Tracking: what was actually added (recorded at T064)

| Item | Where | Notes |
|---|---|---|
| New project and test project | `src/AgenticChangeOps.Agents.Reviewer`, `tests/AgenticChangeOps.Agents.Reviewer.Tests` | References Drafting, Intake and ModelAccess only; nothing else references it except the Api composition root |
| Quote-matching copy | `SampleChecker` | About 15 lines (line-ending normalisation, first and second occurrence), the Developer rule recorded as an outcome instead of a rejection; the Developer is untouched |
| Appended audit value | `AuditEventType.ReviewGenerated` | Inert until `ReviewService` stores a review; the openapi golden gained the value `8` |
| Persisted record | `Review` through `SaveAgentOutput` / `GetAgentOutputs`, layout version stays 2 | Immutable once stored; links to the three upstream versions |
| ModelAccess additive reader | `ModelAnswer.ReadObjectListWithStringList`, `ObjectWithList` | New method and new file; no existing reader or test edited |
| Read-model copies | `ReviewRequirementsBasis`, `ReviewDesignBasis`, `ReviewProposalBasis`, `ReviewBases` | The Reviewer may not reference the other agents; the Api adapter fills them |
| Composition root | `ReviewSettings`, `ReviewSampleFilesAdapter`, `ReviewBasisAdapter`, lazy `ReviewSetup`, three registrations | No route, no `Program.cs` change, no workflow change |
| Drafting engine | unchanged | The generated value carries the content, the system check and the bases, so the engine needed no new argument |

Non-consequential (FR-011): producing a review is classified non-consequential. Nothing leaves the system,
nothing is applied, no status moves, and nothing reads a severity. The Workflow, Intake, Persistence and
Drafting projects do not reference the Reviewer and never name a review type (structure tests).

## Constitution compliance check (pre-code)

Reviewed against constitution v1.0.6 (Development Workflow step 4) before any code or test file.

| Check | Result |
|---|---|
| Principle I, synthetic data | Pass. Only the invented `sample-codebase/` is read; fixtures are invented. |
| Principle II, deterministic routing | Pass. Every decision is plain C#; the model supplies finding text only. |
| Principle III, human gates | Pass. Non-consequential; no status moves; no consumer of a review exists. |
| Principle IV, typed auditable records | Pass with additive changes only, listed in Complexity Tracking. |
| Principle V, spec-driven | Pass once spec, plan and tasks are approved by the maintainer; no code before. |
| Principle VI, test-first | Pass. Tests written first and shown red for approval. Model behind interfaces; fakes only. |
| Principle VII, simplicity | Pass with note. Fourth agent only; the engine is reused; one recorded copy. |
| Technology Stack | No amendment needed (plain client, no tools, no framework use). |
