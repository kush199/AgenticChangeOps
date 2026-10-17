# Data Model: Fix Three Rough Edges

**No data-model change.** No record, field, enum value, table or layout version is added or altered.

## Rules whose outcome changes

| Entity | Rule | Before | After |
|---|---|---|---|
| Change request (Approved) | May receive a requirements draft | Never | Only when it has no requirements draft |
| Workflow status (Approved, no requirements draft) | `nextStep` / `reason` | `nextStep` none, reason `RequirementsDraftRequired` | `nextStep` `RequirementsDraft`, no reason; stage stays `AwaitingRequirementsDraft` |

All other statuses keep their rows in the existing rule table:

| Status | Has requirements draft | May receive one | Workflow outcome |
|---|---|---|---|
| Submitted | no | yes | next step requirements draft |
| Submitted | yes | yes (new version) | awaiting decision |
| Deferred | no / yes | yes | next step / stop (unchanged) |
| UnderReview | any | no | awaiting decision |
| Approved | **no** | **yes (changed)** | **next step requirements draft (changed)** |
| Approved | yes | no | design due / design complete (unchanged) |
| Rejected | any | no | stop |

## Records produced

Unchanged shape: a `RequirementsDraft` (version 1) and a `DraftGenerated` audit entry with
`StatusBefore = StatusAfter = Approved`, `Automatic = true` only when produced by an automatic
check. `WorkflowReasonCode.RequirementsDraftRequired` stays in the enum but the rules no longer
return it.
