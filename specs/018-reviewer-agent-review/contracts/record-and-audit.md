# Contract: Stored Review And Audit Entry

## Stored record

`Review` as in `data-model.md`, stored through the agent-neutral output point
(`SaveAgentOutput<Review>`), keyed by the record type name, serialised with enum names as strings.
Layout version is unchanged (2).

| Rule | Detail |
|---|---|
| Atomic | The review and its `ReviewGenerated` entry are stored together or not at all |
| Version | Existing review count for the change request plus one, assigned in the engine's locked step |
| Links | `ChangeRequestId`, `RequirementsVersion`, `DesignVersion`, `ProposalVersion` are the versions the model was given and that were still the latest at store time |
| Immutable | Never updated or deleted; a new request is a new version |
| Status | Always `Draft` |
| Agent | `ReviewerAgent` |
| System check | Always the system's own result (see `system-check-rules.md`) |
| Note | `AdvisoryNote` is a constant set by the system |

## Retrieval (internal service only)

| Method | Result |
|---|---|
| `ReviewAsync(changeRequestId, ct)` | `ReviewResult`: success with the stored review, or errors (with a failure category when generation or content failed), or a typed rejection |
| `GetLatestReview(changeRequestId)` | `QueryResult<Review?>`: the last version, or null when there is none ("none yet"); an unknown id is an error |
| `GetReviewHistory(changeRequestId)` | `QueryResult<IReadOnlyList<Review>>`: ascending version order; an unknown id is an error |

Reads take the store lock so a reader never sees a review mid-store.

## Audit entry

| Field | Value |
|---|---|
| EventType | `ReviewGenerated` (appended to `AuditEventType`) |
| AgentName | `ReviewerAgent` |
| DraftVersion | review version |
| StatusBefore / StatusAfter | equal (status never moves) |
| Details | null |
| Count | exactly one per stored review; none for any failure, refusal or rejection |

No audit entry, log line or error contains model text, a finding statement, a path, a quote, file
text, an endpoint, a deployment name or a credential.
