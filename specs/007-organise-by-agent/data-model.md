# Data Model: Organise Agent Code by Agent

No persisted or serialized record changes shape. This document lists ownership changes and the one additive contract.

## Ownership after the move

| Entity | Owner (project) | Change |
|---|---|---|
| `ChangeRequest`, `SubmissionRequest`, `SubmissionResult`, `ValidationError`, `QueryResult<T>` | Intake | none |
| `ApprovalDecisionRequest`, `DecisionResult`, `ChangeRequestAuditEntry` | Intake | none (`AgentName`, `DraftVersion` stay as nullable members) |
| `AuditEventType`, `ChangeRequestStatus`, `Priority`, `ChangeType`, `ApprovalDecision` | Intake | none (`DraftGenerated` value and order unchanged) |
| `IAgentOutput` | Intake | **new** generic extension point |
| `RequirementsDraft` | Agents.Requirement | moved; implements `IAgentOutput` |
| `DraftContent`, `DraftingResult`, `DraftStatus` | Agents.Requirement | moved, unchanged members and values |
| `GenerationFailureCategory`, `DraftGenerationException` | ModelAccess | moved, unchanged |
| `ChatModelResponse` | ModelAccess | moved, unchanged |
| `AzureOpenAIGeneratorOptions` (and its four setting names) | ModelAccess | moved, unchanged |

## IAgentOutput (additive)

| Member | Type | Meaning |
|---|---|---|
| `ChangeRequestId` | `Guid` | The change request the output belongs to |
| `Version` | `int` | 1-based, unique per change request and output type |

`RequirementsDraft` already exposes both members as positional record properties, so implementing the interface adds no member and changes no JSON.

## Store rules (unchanged, now generic)

- Save takes an output and its audit entry. They must share a change request id, otherwise the call is rejected and nothing is stored.
- A version already stored for the same change request and output type is rejected and nothing is stored.
- Output and audit entry are stored together or neither.
- Reads return outputs in ascending version order; an unknown change request returns an empty list.
- All operations take the store's existing lock; the lock is exposed as before.

## Audit linkage (unchanged)

A drafting save writes one audit entry with `EventType = DraftGenerated`, `AgentName = "RequirementAgent"`, `DraftVersion = <version>`, and `StatusBefore == StatusAfter`. The `"RequirementAgent"` constant now lives with the agent (`RequirementsDraft.RequirementAgentName`, same value, same public name).
