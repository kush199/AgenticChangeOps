# Data Model: HTTP API for Change Requests

The API owns no stored data. All persisted records are the existing in-memory types from slices
001 to 004. This document defines the transport shapes and the one additive change.

## Existing records (unchanged)

`ChangeRequest`, `ChangeRequestAuditEntry`, `RequirementsDraft`, `ValidationError`, and the
enums `Priority`, `ChangeType`, `ChangeRequestStatus`, `ApprovalDecision`, `AuditEventType`,
`DraftStatus`, `GenerationFailureCategory`. Enums are written and read as their names
(strings). Timestamps are ISO 8601 UTC, as already stored.

## Additive change to an existing type

| Type | Member | Type | Default | Notes |
|------|--------|------|---------|-------|
| `DraftingResult` | `FailureCategory` | `GenerationFailureCategory?` | `null` | Set only when drafting fails because generation failed or its content was unusable; null for eligibility failures and success. Non-breaking under constitution v1.0.1 |

## Request bodies

**SubmitChangeRequestBody** (submit)

| Field | Type | Notes |
|-------|------|-------|
| `title` | string | validated by slice 001 |
| `description` | string | validated by slice 001 |
| `requester` | string | validated by slice 001 |
| `affectedSystem` | string | validated by slice 001 |
| `priority` | string | `Low`, `Medium`, `High`, `Critical`; invalid reported by slice 001 |
| `changeType` | string | `Feature`, `Bugfix`, `ConfigChange`; invalid reported by slice 001 |

**DecisionBody** (record decision; the identifier comes from the route)

| Field | Type | Notes |
|-------|------|-------|
| `approverName` | string | validated by slice 002 |
| `decision` | string | `Approve`, `Reject`, `Defer`; invalid reported by slice 002 |
| `reason` | string or null | required for Reject and Defer by slice 002 |

Property names are camelCase on the wire and matched case-insensitively. Unknown extra
properties are ignored. The draft request has no body.

## Response bodies

**Change request**, **audit entry**, **draft**: the existing records, serialised as-is.

**ClearanceResponse**

| Field | Type | Notes |
|-------|------|-------|
| `changeRequestId` | guid | echoes the route |
| `isCleared` | boolean | exactly `IApprovalService.IsClearedToProceed` |

**Draft history**: a JSON array of drafts in ascending version order; empty when none.

**ErrorResponse** (every non-success outcome)

| Field | Type | Present when |
|-------|------|--------------|
| `code` | string | always; one of the codes in [contracts/http-api.md](contracts/http-api.md) |
| `message` | string | always; fixed text per code, or the service's specific static reason for `conflict` |
| `errors` | array of `{ field, message }` | `validation-failed` and `conflict` |
| `category` | string | `generation-failed` only; one of `Unreachable`, `TimedOut`, `ResponseTooLarge`, `MalformedOutput` |

`ErrorResponse` never contains prompt text, model output, endpoint, deployment name, identity
detail, exception text, stack traces, or paths.

## Process state (not persisted)

**DraftingAvailability**: a single boolean, `IsConfigured`, set once at startup. It holds no
setting value.

## State transitions

None added. All transitions remain in the approval and drafting services.
