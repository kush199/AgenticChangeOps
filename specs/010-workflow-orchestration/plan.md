# Implementation Plan: Workflow Orchestration for Change Requests

**Branch**: `feature/workflow-orchestration` | **Date**: 2026-10-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/010-workflow-orchestration/spec.md`

## Summary

Add a deterministic workflow layer over the two existing agents, exposing two operations by
`ChangeRequestId` alone: **status** (read-only) and **advance** (run the single next allowed step).

- A new library, `AgenticChangeOps.Workflow`, holds one pure rule function that turns the facts
  already recorded (change request status, requirements drafts, design drafts with the requirements
  version each was based on) into a `WorkflowStatus`. Nothing is stored; every call re-derives.
- `WorkflowService.AdvanceAsync` derives the status, and if a step is due calls the **existing**
  `IRequirementsDraftingService.DraftAsync` or `IDesignDraftingService.DesignAsync`. Those services
  already write the drafts and `DraftGenerated` / `DesignGenerated` audit entries, so audit is
  identical to calling the agent directly and orchestration writes nothing of its own.
- Advances for one change request are serialised, so a second concurrent advance re-derives after the
  first and reports nothing due rather than repeating the step.
- Two HTTP operations are added to the API: `GET /change-requests/{id}/workflow` and
  `POST /change-requests/{id}/workflow/advance`. Failures reuse the existing `ErrorMapper` tables, so
  agent failures look exactly as they do on the agents' own routes.

No agent, intake, approval or model-access code changes, other than registration in the API
composition.

## Technical Context

**Language/Version**: C# on .NET 8 (net8.0), unchanged

**Primary Dependencies**: none added

**Storage**: In-memory store only; no new record type, no new audit event type

**Testing**: xUnit; fake generators only (existing `FakeDraftGenerator` / `FakeDesignGenerator`
patterns); no live model in the suite; any live check stays opt-in and skipped

**Target Platform**: Unchanged (CI on ubuntu runner, local Windows)

**Project Type**: Multi-project .NET solution (library projects, minimal API, one test project per library)

**Performance Goals**: N/A. Status is a few in-memory reads.

**Constraints**: No change to agent internals, store, intake, approval or shared model code; no
generator call for any refused advance; no model text, endpoint, deployment name or credential in any
response, log or error; existing tests pass with only the two reviewed API-description assertions
changed (as in slice 008)

**Scale/Scope**: 1 new source project (about 6 files), 1 new test project, 2 new endpoints, 1 new
error code, 3 composition registrations; 2 reviewed test updates (route list and recorded API
description)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

Reviewed against constitution v1.0.3.

| Principle | Assessment |
|---|---|
| I. Synthetic data only | Pass. New fixtures are fictional; existing secret scans keep covering `src`, `tests`, `specs`. |
| II. Deterministic routing | Pass, and this slice is the realisation of it. Step order is a pure C# predicate over recorded facts; no model output reaches routing. The model only generates draft content inside the agents' own services. |
| III. Human approval gates | Pass. Advance never records, changes or implies a decision, never touches status, and cannot run a design step before a human has set `Approved`. Running a draft is non-consequential as in slices 003 and 008. |
| IV. Typed auditable records | Pass. `WorkflowStatus` is a typed, derived read model, not a persisted record; every persisted record is still written by the owning agent with its own audit entry. No `object`/`dynamic` crosses a boundary. No schema change to persisted types. |
| V. Spec-driven | Pass. Spec exists and is clarified; plan precedes tasks; no code before tasks are approved. |
| VI. Test-first | Pass. Rule table, service and HTTP tests are written and approved before implementation; models stay behind the existing generator interfaces and are faked. |
| VII. Simplicity | Pass. No fifth agent; orchestration is not an agent. No framework, state machine library, workflow engine or persisted workflow state. The one new project is justified in Complexity Tracking. Helpers have the callers that need them. |

Technology stack: unchanged. **Gate status: PASS before research and after design.**

## Project Structure

### Documentation (this feature)

```text
specs/010-workflow-orchestration/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── http-api.md             # the two operations, statuses, bodies
│   └── dependency-rules.md     # project references and neutrality rules, with checks
├── checklists/requirements.md
└── tasks.md                    # produced by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.Workflow/                  # NEW library: references Intake, Agents.Requirement,
│   │                                           #   Agents.Architect, ModelAccess
│   ├── WorkflowStep.cs                         # RequirementsDraft, DesignDraft
│   ├── WorkflowStage.cs                        # derived stage enum
│   ├── WorkflowReason.cs                       # reason code + message when no step can run
│   ├── WorkflowStatus.cs                       # status + CompletedStep records
│   ├── WorkflowRules.cs                        # pure derivation: facts in, WorkflowStatus out
│   ├── AdvanceResult.cs                        # outcome + underlying agent result + status after
│   ├── IWorkflowService.cs
│   ├── WorkflowService.cs                      # reads facts, serialises per request, calls agents
│   └── IStepAvailability.cs                    # "is this step's generator configured?"
├── AgenticChangeOps.Api/
│   ├── Endpoints/WorkflowEndpoints.cs          # NEW: status + advance
│   ├── Composition/StepAvailabilityAdapter.cs  # NEW: wraps DraftingAvailability / DesignAvailability
│   ├── Composition/ApiServiceRegistration.cs   # EDIT: register workflow service + adapter
│   ├── Errors/ErrorMapper.cs                   # EDIT: add NoStepDue()
│   ├── Program.cs                              # EDIT: map endpoints; description lists new code
│   └── AgenticChangeOps.Api.csproj             # EDIT: reference Workflow
└── (Intake, ModelAccess, Agents.Requirement, Agents.Architect unchanged)
tests/
├── AgenticChangeOps.Workflow.Tests/            # NEW: rule table, service, concurrency, no-leak, neutrality
└── AgenticChangeOps.Api.Tests/                 # NEW WorkflowEndpointTests, WorkflowNoLeakTests;
                                                #   2 reviewed updates (route list, openapi golden)
```

**Structure Decision**: A separate `Workflow` library rather than code in the API project, so the
rules and the serialisation are testable with no HTTP host, and so the API stays a thin adapter as it
is for the other capabilities. It is the only project that references both agents; the agents still
reference neither each other nor Workflow. Existing agent services are called through their
interfaces, so no agent file changes.

## Complexity Tracking

| Item | Why Needed | Simpler Alternative Rejected Because |
|------|------------|--------------------------------------|
| New project `AgenticChangeOps.Workflow` | The rules must know both agents' outputs; neither agent may reference the other (slice 008), and the shared model project must stay agent-neutral (slice 009). | Putting the rules in `Api` makes them testable only through HTTP and mixes routing rules into transport code. Putting them in either agent would invert the dependency rules. |
| `IStepAvailability` seam | "Not configured" is currently decided in the endpoint for each agent route; advance picks the step inside the service, so the check must be asked per step. | Checking both availabilities in the endpoint would refuse an advance whose due step is the configured one, or report "not configured" when nothing is due. |
| Per-request advance lock | Spec edge case: concurrent advances must not duplicate or skip a version. | Relying on the agents' own save lock does not help: both advances would already have decided the same step is due. |
