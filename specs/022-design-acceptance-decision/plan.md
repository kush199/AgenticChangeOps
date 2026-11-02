# Implementation Plan: A Person Accepts Or Rejects A Design Draft

**Branch**: `feature/design-acceptance-decision` | **Date**: 2026-11-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/022-design-acceptance-decision/spec.md`

## Summary

Add one write operation, `POST /change-requests/{id}/designs/{version}/decisions` with body
`{approverName, decision, reason}`, that stores an immutable `DesignDecision` record against one design
version. The record goes through the store's existing agent-neutral output point
(`SaveAgentOutput<DesignDecision>`), keyed by the design version, so the store's "one output per change
request, type and version" rule is the exactly-one-decision guarantee; the check-then-write runs under the
store's `SyncRoot`, so the loser of an overlap sees a clean "already decided" refusal. A new
`AuditEventType.DesignDecided` entry is written in the same atomic call. A new `IDesignDecisionService` in
the Architect project does the checks in a fixed order (change request, Approved, version exists, latest
and based on the latest requirements, not yet decided, then field validation reusing the approval
decision's limits). The two design retrieval routes keep their routes and status codes and return a
`DesignView`: the stored `DesignDraft` plus an optional `decision` member that is omitted while no decision
exists. No workflow, proposal, review or automatic-advancement code changes.

## Technical Context

**Language/Version**: C# on .NET 8 (unchanged)

**Primary Dependencies**: none new. Uses `IChangeRequestStore`, `IRequirementsBasisSource`,
`ISystemClock`, `IIdGenerator`, `IDesignDraftingService` (read), `RequestParsing`, `ErrorMapper`.

**Storage**: `DesignDecision` is stored as an agent output (JSON payload row keyed by change request, type
and version) in both the in-memory and SQLite stores. No schema change, no new table, no layout version
bump. `AuditEventType` gains `DesignDecided` appended at the end (additive, non-breaking under
Principle IV).

**Testing**: xUnit. New tests in `AgenticChangeOps.Agents.Architect.Tests` (service rules over the
in-memory store; the shared store suite covers SQLite) and `AgenticChangeOps.Api.Tests` (endpoint,
retrieval shape, description, no-leak, concurrency, structure). Golden `openapi.json` updated for the new
operation and the design response schema. No model is involved.

**Target Platform / Project Type**: unchanged (ASP.NET minimal API in `AgenticChangeOps.Api`).

**Performance Goals**: none.

**Constraints**: a decision is a human action only: nothing in Workflow, the hosted advancement service or
any agent may call the new service (structure test, as for reviews). A decision never calls
`IChangeRequestStore.Update*`, so status cannot change. Nothing from the model, sample, endpoint,
deployment name or credential in any response, log or error; refusals never echo the supplied decision
value, name or reason.

**Scale/Scope**: one record, two small enums, one service, one validator, one route, one view type, one
audit event value, one error-mapper method.

## Constitution Check

*GATE: passed before research; re-checked after design (constitution v1.0.6).*

| Principle | Status |
|---|---|
| I. Synthetic data | Pass. Fixtures use synthetic names and text. |
| II. Deterministic routing | Pass. All checks are plain C# predicates; no model call. |
| III. Human gates | Pass. This slice *is* a recorded human decision: approver, time, action and artefact version are persisted. It is never automated and approves nothing else (no status change, no step triggered). |
| IV. Typed auditable records | Pass. `DesignDecision` is a typed record carrying `ChangeRequestId`, the design version it refers to (the typed reference that reconstructs its input), approver, reason, time and outcome; immutable, never deleted. Additive enum value only. |
| V. Spec-driven | Pass. Spec clarified; this plan precedes tasks; no code until tasks are approved. |
| VI. Test-first | Pass. Red tests shown for approval before implementation. No LLM involved. |
| VII. Simplicity | Pass. No new agent or project. The service and `DesignView` each have two callers (the decision route and the two retrieval routes). |

Technology Stack: no new technology, so **no amendment is needed. Gate: PASS before research and after
design.**

## Project Structure

### Documentation (this feature)

```text
specs/022-design-acceptance-decision/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── http-api.md
└── tasks.md              # /speckit-tasks
```

### Source Code (repository root)

```text
src/AgenticChangeOps.Intake/Models/Enums/AuditEventType.cs     # + DesignDecided (appended)
src/AgenticChangeOps.Agents.Architect/
├── DesignDecision.cs               # NEW record : IAgentOutput; DesignVerdict + DesignDecisionOutcome enums
├── IDesignDecisionService.cs       # NEW: Decide, GetDecisions
├── DesignDecisionService.cs        # NEW: ordered checks, atomic save under SyncRoot
├── DesignDecisionResult.cs         # NEW: success record or one refusal kind + field errors
└── DesignDecisionValidator.cs      # NEW: name/reason/verdict rules, same limits as approval decision
src/AgenticChangeOps.Api/
├── Contracts/DesignDecisionBody.cs # NEW transport body
├── Contracts/DesignView.cs         # NEW: DesignDraft members + optional decision
├── Endpoints/DesignEndpoints.cs    # + POST .../designs/{version}/decisions; GETs return DesignView
├── Errors/ErrorMapper.cs           # + ForDesignDecision(result)
└── Composition/ApiServiceRegistration.cs  # register IDesignDecisionService

tests/AgenticChangeOps.Agents.Architect.Tests/Unit/DesignDecision*Tests.cs      # NEW
tests/AgenticChangeOps.Api.Tests/DesignDecision*Tests.cs                         # NEW
tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json                    # reviewed update
```

**Structure Decision**: the decision belongs to the design, so it lives with the Architect's design types
(no new agent, no new project). The view type lives in the Api project because it is a transport shape and
the Architect must not learn about it. The Workflow project references neither the new service nor the new
route (structure test).

## Complexity Tracking

| Item | Why needed |
|---|---|
| `AuditEventType.DesignDecided` | The spec requires an audit entry of a new type. Appended enum value; existing serialized values unchanged. |
| `DesignView` wrapper | A decision is recorded after a design is stored and the stored `DesignDraft` is immutable, so the optional member is added at read time. Omitted when null, so responses without a decision are identical to today. |
| `DesignDecision : IAgentOutput` with `Version` = design version | Reuses the store's existing uniqueness rule for exactly-one-decision; avoids a new store method. `Version` already means "unique per change request and output type". |

## Constitution compliance check (pre-code)

Development Workflow step 4: spec.md, plan.md and tasks.md checked against constitution v1.0.6.

| Principle | spec.md | plan.md | tasks.md |
|---|---|---|---|
| I. Synthetic data | Pass | Pass | To confirm at /speckit-tasks |
| II. Deterministic routing | Pass | Pass | To confirm |
| III. Human gates | Pass (is the human decision, never automated) | Pass | To confirm |
| IV. Typed auditable records | Pass | Pass | To confirm |
| V. Spec-driven | Pass | Pass | To confirm |
| VI. Test-first | Pass | Pass | To confirm |
| VII. Simplicity | Pass | Pass | To confirm |

Technology Stack: no new technology, no amendment. Commit messages reference the slice name
"022 design-acceptance-decision".
