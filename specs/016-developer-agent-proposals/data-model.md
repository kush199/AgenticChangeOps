# Data Model: Developer Agent Proposes A Change Set

## ChangeProposal (new stored record, implements `IAgentOutput`)

| Member | Type | Notes |
|---|---|---|
| ProposalId | Guid | from the existing id generator |
| ChangeRequestId | Guid | links to the change request |
| Version | int | 1-based, consecutive per change request, assigned under the store lock |
| RequirementsVersion | int | version of the requirements draft the model was given |
| DesignVersion | int | version of the design draft the model was given |
| GeneratedAt | DateTime | from the system clock |
| Status | ProposalStatus | `Draft` only |
| AgentName | string | `DeveloperAgent` |
| Summary | string | required, non-blank |
| Changes | list of ProposedChange | 1 to the maximum number of changes |
| Risks | list of string | at least one item |
| TestIdeas | list of string | may be empty |
| OpenQuestions | list of string | may be empty |
| SampleFilesRead | list of string | from the tool session; relative, de-duplicated, may be empty |
| VerificationNote | string | constant: "Not compiled, run or tested." Set by the system |

Immutable once stored, never deleted or overwritten, so references from it (and to it) satisfy
Principle IV's by-reference rule. Satisfies `InputPayload` by `ChangeRequestId` plus the two version
references, `OutputPayload` by its own content members.

## ProposedChange (value inside ChangeProposal)

| Member | Type | Notes |
|---|---|---|
| Path | string | relative to the sample, forward slashes, no leading slash |
| Kind | ProposalChangeKind | `Change` or `Add` |
| ExistingText | string? | required for `Change` (exact quote, found exactly once), absent for `Add` |
| ReplacementText | string | required, may be multi-line; for `Add` it is the whole new file |

## Enums

- `ProposalStatus`: `Draft`.
- `ProposalChangeKind`: `Change`, `Add`.
- `ProposalRejection` (service result only, never stored): `NoChanges`, `TooManyChanges`, `TooLarge`,
  `PathOutsideSample`, `FileNotFound`, `QuoteNotFound`, `QuoteAmbiguous`, `NewFileExists`,
  `OverlappingEdits`, `MalformedEdit`.

## ProposalContent (generator output, before validation)

Same members as the stored record minus identity, links, time, status, agent and note; text members
nullable so "absent" and "empty" stay distinguishable. Carries `SampleFilesRead` and `ToolCalls`
(`ToolCallRecord`) from the tool session, passed through unchanged.

## ProposalBasis (read model owned by the Developer)

`ProposalRequirementsBasis` (version, summary, functional requirements, acceptance criteria, assumptions, open
questions) and `ProposalDesignBasis` (version, requirements version, approach summary, affected components,
impact and risks, dependencies, rollback considerations, open questions), supplied by
`IProposalBasisSource` and filled by an Api adapter from the existing services. The Developer never
references the other agents.

## ProposalLimits

`MaxChanges` (default 5), `MaxTotalBytes` (default 6000). Operator-set; see `settings-and-failures.md`.

## Audit

- `AuditEventType.ProposalGenerated` appended after `ToolCalled`. One per stored proposal:
  `AgentName = DeveloperAgent`, `DraftVersion = proposal version`, status before and after equal,
  `Automatic` absent.
- `ToolCalled` entries: one per tool call, exactly the Architect's fields (tool name, target, outcome,
  bytes, elapsed milliseconds), `AgentName = DeveloperAgent`, saved before the `ProposalGenerated` entry.
- Nothing is audited for a failed request; counts are logged (calls made, refusals by kind, time).

## State and lifecycle

A proposal has no transitions. Producing one never changes a change request's status, a requirements
draft, a design draft or any other record.

## Workflow (additive)

- `WorkflowStep` gains `Proposal` (appended). `WorkflowStatus` gains optional `OfferedStep`
  (`WorkflowStep?`, omitted from JSON when null). `CompletedSteps` is unchanged: proposals are not
  workflow steps and do not move the stage.
