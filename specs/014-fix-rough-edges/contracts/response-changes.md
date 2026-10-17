# Contract: The Three Response Changes

Routes, methods, status codes, headers, error codes and the OpenAPI document do not change. Only the
bodies and outcomes below do. Identifiers are synthetic.

## Change 1 - validation messages (`400 validation-failed`)

Field entries in `errors[]` change; the envelope (`code`, `message`, `field`) does not.

| Field | Operation | Before | After |
|---|---|---|---|
| Priority | submit | `Priority '-1' is not valid. Accepted: Low, Medium, High, Critical.` | `Priority is not a recognised value. Accepted values: Low, Medium, High, Critical.` |
| ChangeType | submit | `ChangeType '-1' is not valid. Accepted: Feature, Bugfix, ConfigChange.` | `ChangeType is not a recognised value. Accepted values: Feature, Bugfix, ConfigChange.` |
| Decision | record decision | `Decision '-1' is not valid. Accepted values: Approve, Reject, Defer.` | `Decision is not a recognised value. Accepted values: Approve, Reject, Defer.` |

Missing values, case and whitespace handling, and every other field message are unchanged. No
decision is recorded for a rejected Decision.

## Change 2 - Approved request without a requirements draft

| Request | Before | After |
|---|---|---|
| `POST /change-requests/{id}/drafts`, Approved, no requirements draft | `409 conflict`, Status field, "...in status Approved and cannot be drafted; only Submitted or Deferred requests can be drafted." | `201 Created`, `Location` `.../drafts/latest`, body the new `RequirementsDraft` (version 1) |
| `GET /change-requests/{id}/workflow` (same request, before the draft) | `nextStep` none; reason `RequirementsDraftRequired` | `nextStep` `RequirementsDraft`; no reason; stage `AwaitingRequirementsDraft` |
| `POST /change-requests/{id}/workflow/advance` (same request) | `409 no-step-due`, reason `RequirementsDraftRequired` | `201 Created`, `Location` `./drafts/latest`, requirements draft produced; the status then shows the design as due |
| Automatic check, same request, drafting configured | Left alone | Draft produced like any due step (marked automatic) |

Unchanged: Approved **with** a requirements draft is refused with today's `409` body; Rejected,
UnderReview, Submitted and Deferred behave as before; approval accepts a request with no draft as
before.

## Change 3 - order of checks, `POST .../drafts` and `POST .../designs`

Order is now: malformed identifier -> unknown identifier -> drafting not configured -> existing
rules.

| Request (drafting or design not configured) | Before | After |
|---|---|---|
| Malformed identifier | `400 invalid-identifier` | same |
| Unknown identifier | `503 drafting-not-configured` / `503 design-not-configured` | `404 not-found` (the body already returned when configured) |
| Known identifier | `503 ...-not-configured` | same |

When configured, every response is unchanged.
