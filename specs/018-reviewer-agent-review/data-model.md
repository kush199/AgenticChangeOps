# Data Model: Reviewer Agent Reviews A Change Proposal

## Review (new stored record, implements `IAgentOutput`)

| Member | Type | Notes |
|---|---|---|
| ReviewId | Guid | from the existing id generator |
| ChangeRequestId | Guid | links to the change request |
| Version | int | 1-based, consecutive per change request, assigned under the store lock |
| RequirementsVersion | int | requirements draft version the review was based on |
| DesignVersion | int | design draft version the review was based on |
| ProposalVersion | int | proposal version the review was based on |
| GeneratedAt | DateTime | from the system clock |
| Status | ReviewStatus | `Draft` only |
| AgentName | string | `ReviewerAgent` |
| SystemCheck | SystemCheck | the system's own result; never from the model |
| Findings | list of ReviewFinding | zero to the maximum number of findings |
| OpenQuestions | list of string | may be empty |
| AdvisoryNote | string | constant: "Advisory only. Nothing acts on this review." Set by the system |

Immutable once stored, never deleted or overwritten, so the references it carries satisfy
Principle IV's by-reference rule: `InputPayload` is reconstructable from `ChangeRequestId` plus the
three version references; `OutputPayload` is its own content members. The classification
"non-consequential" is recorded in the spec and plan and enforced by structure tests (no consumer of
a review exists in Workflow or Intake); it is not a stored member.

## SystemCheck (value inside Review)

| Member | Type | Notes |
|---|---|---|
| Status | SystemCheckStatus | `Performed` or `NotPerformed` |
| Edits | list of EditCheck | one per proposal edit, in proposal order; empty when `NotPerformed` or the proposal has no edits |

`EditCheck(ChangeNumber: int, Outcome: EditCheckOutcome)`. `ChangeNumber` is the 1-based position in
the proposal's `Changes`. No path, quote or file text is stored.

## ReviewFinding (value inside Review)

| Member | Type | Notes |
|---|---|---|
| Category | FindingCategory | `AcceptanceCriteria`, `DesignConsistency`, `UnmentionedRisk` |
| Severity | FindingSeverity | `Low`, `Medium`, `High`; advisory, nothing reads it |
| Statement | string | required, non-blank, plain language |
| References | list of ReviewReference | at least one; each resolves |

## ReviewReference (value inside ReviewFinding)

`ReviewReference(Document: ReviewDocument, List: ReferenceList, Position: int)`; `Position` is 1-based.

## Enums

- `ReviewStatus`: `Draft`.
- `ReviewChangeKind`: `Change`, `Add` (the Reviewer own copy of the proposal edit kind, set by the Api adapter).
- `ReviewSamplePathState`: `OutsideSample`, `Missing`, `File`, `Folder` (what `IReviewSampleFiles.Probe` reports; never stored).
- `SystemCheckStatus`: `Performed`, `NotPerformed`.
- `EditCheckOutcome`: `Found`, `QuoteNotFound`, `QuoteAmbiguous`, `FileNotFound`, `PathOutsideSample`,
  `NewFileFree`, `NewFileExists`, `MalformedEdit`.
- `FindingCategory`: `AcceptanceCriteria`, `DesignConsistency`, `UnmentionedRisk`.
- `FindingSeverity`: `Low`, `Medium`, `High`.
- `ReviewDocument`: `Requirements`, `Design`, `Proposal`.
- `ReferenceList`: `FunctionalRequirements`, `AcceptanceCriteria`, `Assumptions`, `OpenQuestions`,
  `AffectedComponents`, `ImpactAndRisks`, `Dependencies`, `RollbackConsiderations`, `Changes`, `Risks`,
  `TestIdeas`. Which lists belong to which document is a fixed table in code
  (`contracts/reference-and-limit-rules.md`).
- `ReviewRejection` (service result only, never stored): `TooManyFindings`, `TooLarge`,
  `MissingReference`, `UnresolvedReference`, `MalformedFinding`.

## ReviewContent (generator output, before validation)

Findings as the model wrote them: category, severity and reference words as raw text, statement
nullable; open questions nullable (absent and empty stay distinguishable). Carries no check result.

## ReviewValidation (validator result, never stored)

`ReviewValidation(Value, Errors, Rejection)`: exactly one of a `ValidatedReview` (typed findings with resolved
references, and open questions), shape errors, or a typed `ReviewRejection`.

## GeneratedReview (engine's "generated" value, internal)

`ReviewContent`, the `SystemCheck` computed before the call, and the three bases the model was given.
Never stored; it exists so the review step can resolve references without another lookup.

## ReviewBasis (read model owned by the Reviewer)

`ReviewRequirementsBasis` (version, summary, functional requirements, acceptance criteria,
assumptions, open questions), `ReviewDesignBasis` (version, requirements version, approach summary,
affected components, impact and risks, dependencies, rollback considerations, open questions) and
`ReviewProposalBasis` (version, requirements version, design version, summary, changes with path,
kind, existing text and replacement text, risks, test ideas, open questions), supplied by
`IReviewBasisSource` and filled by an Api adapter from the existing services. The three parts travel
together as `ReviewBases(Requirements, Design, Proposal)`, the basis the engine is given; each part is
null when it does not exist. The Reviewer never references the other agents.

## ReviewLimits

`MaxFindings` (default 8), `MaxTotalBytes` (default 8000). Operator-set; see
`contracts/settings-and-failures.md`.

## Audit

- `AuditEventType.ReviewGenerated` appended after `ProposalGenerated`. One per stored review:
  `AgentName = ReviewerAgent`, `DraftVersion = review version`, status before and after equal,
  `Automatic` absent, `Details` null.
- No `ToolCalled` entries: the Reviewer uses no tools.
- Nothing is audited for a failed request; counts are logged (refusals by kind, rejections by
  reason, time), never content.

## State and lifecycle

A review has no transitions. Producing one never changes a change request's status or any draft or
proposal.

## Unchanged

Workflow types, `WorkflowStatus` and the automatic path are untouched; reviews are not workflow
steps and do not move the stage.
