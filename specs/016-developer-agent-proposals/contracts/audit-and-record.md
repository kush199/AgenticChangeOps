# Contract: Stored Record And Audit Trail

## Stored record

`ChangeProposal` is saved with `SaveAgentOutput<ChangeProposal>` together with its `ProposalGenerated`
audit entry, or neither. Reads use `GetAgentOutputs<ChangeProposal>` and return ascending version order.
Layout version stays 2; older databases open unchanged and read as having no proposals.

## Audit entries for one stored proposal, in order

1. `ToolCalled` once per tool call, in call order: `AgentName = DeveloperAgent`, tool name, target
   (control characters removed, truncated to 120 characters), outcome name, result bytes, elapsed
   milliseconds. `OccurredAt` is the call start, capped at the proposal's time. No file content.
2. `ProposalGenerated`: `AgentName = DeveloperAgent`, `DraftVersion = proposal version`,
   `StatusBefore = StatusAfter = the change request's status`, `Details` absent, `Automatic` absent.

A failed request stores neither; the log carries counts only (calls made, refusals by kind, time taken).

## Additive changes (Principle IV)

| Change | Why it is non-breaking |
|---|---|
| `AuditEventType.ProposalGenerated` appended | enum value appended at the end; existing values and serialised entries unchanged |
| `WorkflowStep.Proposal` appended | same |
| `WorkflowStatus.OfferedStep`, omitted when null | existing bodies serialise identically |
| `WorkflowService` optional constructor argument | existing construction still compiles and behaves the same |
| New record type `ChangeProposal` | a new type in the generic output table; no existing record touched |

The existing test that lists every `AuditEventType` value, if any, gains the new value as an addition
only; no existing assertion is changed. This is checked at the first task and reported.
