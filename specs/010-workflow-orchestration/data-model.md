# Data Model: Workflow Orchestration

Nothing here is persisted. Every type is a derived, read-only view recomputed from recorded facts on
each call (FR-003). No change to any existing record, enum or audit event type.

## Inputs (existing, read only)

| Fact | Source | Used for |
|---|---|---|
| Change request `Status` | `IChangeRequestStore.FindById` | which rule row applies |
| Requirements drafts: `Version`, `GeneratedAt` | `GetDrafts` (Requirement agent store view) | completed step, latest version |
| Design drafts: `Version`, `GeneratedAt`, `RequirementsVersion` | `GetDesigns` (Architect store view) | completed step, currency |

All three are read under the store's `SyncRoot` in one step.

## WorkflowStep (enum)

`RequirementsDraft`, `DesignDraft`. Further values are appended when later agents are added.

## WorkflowStage (enum)

| Value | Meaning |
|---|---|
| `AwaitingRequirementsDraft` | No requirements draft yet (Submitted), or Approved without one |
| `AwaitingDecision` | A human decision is outstanding (Submitted with a draft, or UnderReview) |
| `ReadyForDesign` | Approved with a requirements draft; no design, or design older than latest requirements |
| `DesignComplete` | Approved, and the latest design is based on the latest requirements |
| `Deferred` | Deferred (a requirements draft may still be due; nothing beyond it) |
| `Rejected` | Rejected; nothing further |

## WorkflowReasonCode (enum)

`AwaitingDecision`, `Deferred`, `Rejected`, `RequirementsDraftRequired`, `NothingDue`.

## WorkflowStatus

| Field | Type | Rule |
|---|---|---|
| `ChangeRequestId` | Guid | The request this describes |
| `ChangeRequestStatus` | existing `ChangeRequestStatus` | Copied, never altered |
| `Stage` | `WorkflowStage` | By the rule table in [research.md](research.md) D3 |
| `CompletedSteps` | list of `CompletedStep` | Every stored draft, requirements first then designs, each ascending by version |
| `NextStep` | `WorkflowStep?` | Set iff a step can run now |
| `Reason` | `WorkflowReason?` | Set iff `NextStep` is null; never both set, never both null |

Invariant (SC-001): exactly one of `NextStep` and `Reason` is non-null.

## CompletedStep

| Field | Type | Rule |
|---|---|---|
| `Step` | `WorkflowStep` | Which agent step produced it |
| `Version` | int | The draft's own version |
| `GeneratedAt` | DateTime (UTC) | The draft's own timestamp |
| `RequirementsVersion` | int? | For a design step: the requirements version it was based on; null otherwise |

## WorkflowReason

| Field | Type | Rule |
|---|---|---|
| `Code` | `WorkflowReasonCode` | Machine-readable |
| `Message` | string | Fixed sentence per code, naming the change request status where relevant. Contains no model text. |

## AdvanceResult (service-level)

| Field | Type | Rule |
|---|---|---|
| `Outcome` | `AdvanceOutcome` | `Advanced`, `NotFound`, `NothingDue`, `NotConfigured`, `StepFailed` |
| `Step` | `WorkflowStep?` | The step that was (or would have been) run |
| `Requirements` | `DraftingResult?` | Set when the step was the requirements draft (success or failure) |
| `Design` | `DesignDraftingResult?` | Set when the step was the design draft (success or failure) |
| `Status` | `WorkflowStatus?` | State after the call: set for `Advanced`, `NothingDue` and `StepFailed`; null for `NotFound` |
| `Errors` | list of existing `ValidationError` | Only for `NotFound` |

On `Advanced` and `StepFailed`, the agent result is the same object the agent's own operation
returned, so its draft, errors and failure category are by construction in the agent's own form.

## State transitions

None stored. The only way the derived stage changes is that a recorded fact changes: an agent
operation stores a draft, or a human decision changes the status. Advance causes at most one of the
first and never the second.
