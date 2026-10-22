# Implementation Plan: Shared Drafting Skeleton

**Branch**: `feature/shared-drafting-skeleton` | **Date**: 2026-10-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/017-shared-drafting-skeleton/spec.md`

## Summary

Behaviour-preserving extraction. The Requirement, Architect and Developer drafting services repeat one skeleton: look up, eligibility, generate, failure translation, validate, then one locked step (re-check eligibility, currency check, next version, save) plus latest and history reads. That skeleton moves into one new agent-neutral project, `AgenticChangeOps.Drafting`, which references Intake and ModelAccess and no agent. Each agent's service stays as the public type callers and tests use today and becomes a thin class that supplies its own parts to a shared `DraftingEngine`:

- eligibility and its inputs/basis (its own order of checks and messages),
- the currency check of the basis, run inside the locked step,
- content review (its own validator, plus the Developer's quote verification),
- the save step (its own record, audit event type, tool-call entries, store call),
- reading its own stored collection,
- one visible flag for cancellation handling (Developer rethrows; the other two report a failure).

Result types (`DraftingResult`, `DesignDraftingResult`, `ProposalResult`) and all HTTP code are untouched; each service maps the engine's neutral outcome to its own result type. Because the engine's observable behaviour must equal the old services exactly, a characterization baseline of each service (all failure branches, overlapping calls) is written and run green **before** code moves, and must stay green after, unchanged.

## Technical Context

**Language/Version**: C# on .NET 8 (net8.0), unchanged

**Primary Dependencies**: none added. New project references Intake and ModelAccess only.

**Storage**: Untouched. The engine uses `IChangeRequestStore.FindById` and `SyncRoot` already used by all three services; each agent still calls its own `Get*`/`Save*` store methods.

**Testing**: xUnit, fakes only. New `AgenticChangeOps.Drafting.Tests` project (test code, mirrors every other library).

**Target Platform**: Unchanged

**Project Type**: Multi-project .NET solution

**Constraints**: zero assertion changes except the three structure-test expectations (FR-011); same HTTP status codes, bodies, headers, error codes; no new setting, package, endpoint, capability or record shape; shared source names no agent, no stored record, no approval capability.

**Scale/Scope**: 1 new source project (about 6 small files), 1 new test project, 3 services shrink, 3 structure tests edited by one line, 1 solution edit, 1 measurement script extended, 1 register file.

## Constitution Check

Reviewed against constitution v1.0.6.

| Principle | Assessment |
|---|---|
| I. Synthetic data only | Pass. Fixtures fictional; existing secret scans keep covering `src`, `tests`, `specs`. |
| II. Deterministic routing | Pass. No routing or transition change; the engine is deterministic C#; generators stay content-only. |
| III. Human approval gates | Pass. Approval code untouched; drafting stays non-consequential; no approval type is referenced by shared code. |
| IV. Typed auditable records | Pass. No record or serialized shape changes (FR-017). Engine is generic over typed records, no `object` crosses a boundary. |
| V. Spec-driven | Pass. Spec clarified; plan precedes tasks; no code before tasks are approved. |
| VI. Test-first | Pass. Baseline and skeleton tests are written and approved before code moves; LLM access stays behind existing interfaces. |
| VII. Simplicity | Pass with record. The engine has three existing callers (at least two required). It relocates existing behaviour and adds none; no plug-in registry or base-class hierarchy. The generic parameters are a readability cost, recorded below. |

Technology stack: unchanged (no package, protocol or framework change). **Gate: PASS before research and after design.**

## Project Structure

### Documentation (this feature)

```text
specs/017-shared-drafting-skeleton/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── engine-contract.md           # what the engine does, in order, and what each agent supplies
│   ├── dependency-rules.md          # references and neutrality rules with the check for each
│   ├── http-api-unchanged.md        # what is recorded before and compared after
│   └── kept-separate-register.md    # K1..K15 re-examined, plus new entries
├── checklists/requirements.md
├── baseline-tests.txt               # created first task: pass counts before the change
├── measure-duplication.ps1          # earlier script extended to three agents
├── duplication.md                   # before and after
└── tasks.md                         # produced by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.Drafting/                  # NEW; references Intake, ModelAccess; no agent
│   ├── DraftingEngine.cs                       # run sequence + locked store step + reads
│   ├── IDraftingAgent.cs                       # the parts an agent supplies
│   ├── DraftingOutcome.cs                      # neutral outcome the engine returns
│   ├── ReviewOutcome.cs                        # valid | invalid | rejected
│   ├── SaveStep.cs                             # the agent's per-call save delegate
│   ├── NoBasis.cs                              # basis of an agent generated from the request alone
│   ├── NoRejection.cs                          # rejection type of an agent that never rejects
│   ├── DraftingErrors.cs                       # the not-found error every agent shares
│   └── ToolCallAuditEntries.cs                 # the audit entry for one tool call (agent name passed in)
#   (the category-only failure translation is a private part of DraftingEngine.cs)
├── AgenticChangeOps.Agents.Requirement/RequirementsDraftingService.cs   # shrinks
├── AgenticChangeOps.Agents.Architect/DesignDraftingService.cs           # shrinks
├── AgenticChangeOps.Agents.Developer/ProposalService.cs                 # shrinks
└── (each agent .csproj gains one ProjectReference to Drafting)
tests/
├── AgenticChangeOps.Drafting.Tests/            # NEW: engine tests, neutrality scan, stand-in fourth agent
├── AgenticChangeOps.Agents.*.Tests/*StructureTests.cs  # one expected-list line each (FR-011 exception)
├── AgenticChangeOps.Agents.*.Tests/Characterization/   # cancellation characterization, written on the old code
└── AgenticChangeOps.Api.Tests/Baseline/        # characterization of every drafting operation, golden files
AgenticChangeOps.sln                            # both new projects added
```

**Structure Decision**: A new project, as clarified, because the engine needs Intake types (change request, store, `ValidationError`, `QueryResult`) and rule N2 keeps ModelAccess free of Intake, while Intake should not hold agent plumbing. All three agents already reference Intake and ModelAccess, so the new project adds one reference per agent and no agent-to-agent reference. Public service types stay where they are, so Api, Workflow and every test keep their references.

## Complexity Tracking

| Item | Note |
|---|---|
| New project `Drafting` and 3 new project references | Explicit exception to "no new dependency" (FR-006, FR-017); the user chose it in clarification. |
| Three structure-test expectations edited | The one exception to FR-011, approved at planning; only the expected reference list changes. |
| Generic engine (record, basis, generated, content, rejection) | Replaces three copies one-for-one (Principle VII, relocation clause). **Readability cost** (FR-016): each service now names five type arguments and implements a small interface instead of reading top to bottom. Offset by services dropping from about 120-200 lines to their agent-specific methods. To be re-judged after implementation; the user decides if any service reads worse. |
| `PropagatesCancellation` flag | Keeps today's Developer difference visible beside its service rather than merged (FR-012). |
| New test project | Mirrors every other library; not production code. |

## Constitution compliance check (pre-code)

Reviewed 2026-10-22 against constitution v1.0.6 before any code (Development Workflow step 4).

- II. No routing, transition or orchestration change; the engine is deterministic C# and generators stay content-only.
- III. No approval code is touched or referenced by the shared project; drafting stays non-consequential.
- IV. No record, enum value or serialized shape changes.
- VII. The engine relocates behaviour that exists in three services and adds none; three existing callers.
- Technology stack unchanged: no package added; one new project and three project references are the recorded exception (Complexity Tracking).
