# Contract: Workflow HTTP operations

Conventions are those of the existing operations. Bodies are camelCase JSON; enums are strings;
error bodies are `{ code, message, errors?, category? }` and never carry model text, endpoint,
deployment name, credential, exception text or paths. Neither operation takes a request body.

## GET /change-requests/{id}/workflow

Read-only. Writes nothing, writes no audit entry, never calls a generator.

| Situation | Status | Body |
|---|---|---|
| Existing change request | 200 | `WorkflowStatus` |
| `{id}` is not a valid identifier | 400 | `invalid-identifier` |
| Unknown change request | 404 | `not-found` |

## POST /change-requests/{id}/workflow/advance

Runs at most one agent step.

| Situation | Status | Body |
|---|---|---|
| Step ran and stored a draft | 201, `Location` = that draft's own `.../drafts/latest` or `.../designs/latest` | `AdvanceResponse` |
| `{id}` is not a valid identifier | 400 | `invalid-identifier` |
| Unknown change request | 404 | `not-found` |
| No step is due | 409 | `no-step-due`, `message` = the specific reason sentence, `errors: [{ field: "Reason", message: "<WorkflowReasonCode>" }]` |
| Due step's model settings not configured | 503 | `drafting-not-configured` or `design-not-configured` (as on the agent's own route) |
| Step failed: model failure, timeout, answer too large, incomplete or malformed | 502 | `generation-failed`, `category` only (as on the agent's own route) |
| Requirements draft changed while the design was generated | 409 | `conflict` with field `RequirementsDraft` (as on the agent's own route) |
| Anything unexpected | 500 | `unexpected-error` |

Every non-201 outcome stores nothing, writes no audit entry, and for refusals never calls the
generator. A refusal or failure leaves `GET .../workflow` identical to before.

## WorkflowStatus JSON

```json
{
  "changeRequestId": "00000000-0000-0000-0000-000000000000",
  "changeRequestStatus": "Approved",
  "stage": "ReadyForDesign",
  "completedSteps": [
    { "step": "RequirementsDraft", "version": 1, "generatedAt": "2026-10-14T09:00:00Z" }
  ],
  "nextStep": "DesignDraft",
  "reason": null
}
```

When no step can run, `nextStep` is `null` and `reason` is `{ "code": "AwaitingDecision", "message": "…" }`.
A completed design step carries `requirementsVersion`.

## AdvanceResponse JSON

```json
{
  "step": "DesignDraft",
  "draft": { "…": "exactly the body POST /designs returns for the same draft" },
  "status": { "…": "WorkflowStatus after the step" }
}
```

`draft` is the same serialised record the agent's own route returns (`RequirementsDraft` or
`DesignDraft`), so a caller can use one parser for both.

## Compatibility

- No existing route, body, status code or error code changes.
- One error code is added: `no-step-due` (409). The API description lists it.
- The recorded API description gains the two operations and the `WorkflowStatus`, `CompletedStep`,
  `AdvanceResponse` schemas.
