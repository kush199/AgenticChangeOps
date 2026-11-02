# Data Model: Design Acceptance Decision

## DesignDecision (new, immutable, `IAgentOutput`)

| Member | Type | Notes |
|---|---|---|
| `DecisionId` | Guid | from `IIdGenerator` |
| `ChangeRequestId` | Guid | |
| `Version` | int | the design version decided; unique per change request and type |
| `DesignId` | Guid | the decided `DesignDraft.DesignId` |
| `Outcome` | `DesignDecisionOutcome` | `Accepted` or `Rejected` |
| `ApproverName` | string | trimmed, 1..100 |
| `Reason` | string? | trimmed, null when none; required for Rejected; <= 1000 |
| `DecidedAt` | DateTime | UTC, from `ISystemClock` |

Enums: `DesignVerdict { Accept, Reject }` (request input), `DesignDecisionOutcome { Accepted, Rejected }`
(stored). Stored once, never updated or deleted. A rejected decision does not block a later design version,
which starts with no decision.

## DesignView (Api transport, read-only)

Every `DesignDraft` member, unchanged, plus `decision` (a projection of `DesignDecision`: `outcome`,
`approverName`, `reason`, `decidedAt`) omitted when none exists.

## Audit

`AuditEventType.DesignDecided` (appended). Entry fields: `ChangeRequestId`, `OccurredAt` = `DecidedAt`,
`ApproverName`, `Reason`, `DraftVersion` = design version, `Details` = outcome name,
`StatusBefore` = `StatusAfter` = current status, `AgentName` null. Saved atomically with the decision.

## State

Per design version: `Undecided -> Accepted | Rejected`; no other transitions.
