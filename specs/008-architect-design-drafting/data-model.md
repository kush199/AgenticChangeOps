# Data Model: Architect Design Drafting

## DesignDraft (new typed record, implements `IAgentOutput`)

| Field | Type | Rule |
|---|---|---|
| `DesignId` | Guid | Unique, from the id generator |
| `ChangeRequestId` | Guid | Existing change request; links the draft |
| `Version` | int | 1-based, consecutive per change request and per output type (independent of requirements versions) |
| `RequirementsVersion` | int | Version of the requirements draft the model was given |
| `GeneratedAt` | DateTime (UTC) | From the clock at store time |
| `Status` | `DesignDraftStatus` | Always `Draft` |
| `AgentName` | string | Constant `ArchitectAgent` |
| `ApproachSummary` | string | Required, non-blank, at most 2000 characters |
| `AffectedComponents` | list of string | Required, at least one non-blank item |
| `ImpactAndRisks` | list of string | Required, at least one non-blank item |
| `Dependencies` | list of string | Required key; may be empty |
| `RollbackConsiderations` | list of string | Required, at least one non-blank item |
| `OpenQuestions` | list of string | Required key; may be empty |

Validation (shared rule set with requirements drafting): trim text, drop blank list items, each item at most 2000 characters, each list at most 50 items.
Immutable once stored; earlier versions are never altered.

## DesignDraftStatus

`Draft` (single value, as for requirements drafts). No transitions in this slice.

## DesignContent (raw generator output, not stored)

Same members as the stored sections, each nullable: a null member means the section was absent; an empty list means present but empty. Validation turns it into the stored sections or into errors naming the failing section.

## RequirementsBasis (read model owned by the Architect)

| Field | Type |
|---|---|
| `Version` | int |
| `Summary` | string |
| `FunctionalRequirements` | list of string |
| `AcceptanceCriteria` | list of string |
| `Assumptions` | list of string |
| `OpenQuestions` | list of string |

Obtained through `IRequirementsBasisSource.GetLatest(changeRequestId)`; null when the change request has no requirements draft.

## Audit entry (existing record, new event value)

`AuditEventType.DesignGenerated` is appended after `DraftGenerated`. The entry sets `AgentName = ArchitectAgent`, `DraftVersion = DesignDraft.Version`, `StatusBefore = StatusAfter = Approved`, `OccurredAt = GeneratedAt`, `Details = null`. No member of `ChangeRequestAuditEntry` is added.

## Relationships and state

- A change request has zero or more design drafts; each links to exactly one requirements version.
- Eligibility is a pure predicate over the change request status (`Approved`) and the presence of a requirements basis; it never writes to the change request.
- Save is atomic: design draft and audit entry are stored together or not at all (existing store behaviour).
