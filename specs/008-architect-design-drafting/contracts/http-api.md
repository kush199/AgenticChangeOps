# Contract: Design draft HTTP operations

Conventions are those of the requirements drafting operations (slice 005). Bodies are camelCase JSON; enums are strings; error bodies are `{ code, message, errors?, category? }` and never carry model text, endpoint, deployment name, credential, exception text or paths.

## POST /change-requests/{id}/designs

Requests a new design draft. No request body.

| Situation | Status | Body |
|---|---|---|
| Stored | 201, `Location: /change-requests/{id}/designs/latest` | `DesignDraft` |
| `{id}` is not a valid identifier | 400 | `invalid-identifier` |
| Unknown change request | 404 | `not-found` |
| Status is not `Approved` | 409 | `conflict`, `errors: [{ field: "Status", ... }]` |
| No requirements draft (or it changed during generation) | 409 | `conflict`, `errors: [{ field: "RequirementsDraft", ... }]` |
| Model failed, timed out, answer too large, incomplete or malformed | 502 | `generation-failed`, `category` only (`Unreachable`, `TimedOut`, `ResponseTooLarge`, `MalformedOutput`) |
| Model settings not configured | 503 | `design-not-configured` |
| Anything unexpected | 500 | `unexpected-error` |

Refusals and failures store nothing, write no audit entry, and (for refusals) never call the generator.

## GET /change-requests/{id}/designs/latest

| Situation | Status | Body |
|---|---|---|
| Has designs | 200 | highest-version `DesignDraft` |
| No designs yet | 404 | `no-design-draft` |
| Malformed id | 400 | `invalid-identifier` |
| Unknown change request | 404 | `not-found` |

## GET /change-requests/{id}/designs

| Situation | Status | Body |
|---|---|---|
| Existing change request | 200 | array of `DesignDraft` in ascending version order; `[]` when none |
| Malformed id | 400 | `invalid-identifier` |
| Unknown change request | 404 | `not-found` |

## DesignDraft JSON

```json
{
  "designId": "00000000-0000-0000-0000-000000000000",
  "changeRequestId": "00000000-0000-0000-0000-000000000000",
  "version": 1,
  "requirementsVersion": 1,
  "generatedAt": "2026-10-13T09:00:00Z",
  "status": "Draft",
  "agentName": "ArchitectAgent",
  "approachSummary": "…",
  "affectedComponents": ["…"],
  "impactAndRisks": ["…"],
  "dependencies": [],
  "rollbackConsiderations": ["…"],
  "openQuestions": []
}
```

## Compatibility

Existing routes, status codes, bodies, error codes and the `AuditEventType` values already in use are unchanged. The OpenAPI description gains the three paths and the audit enum gains `DesignGenerated` at the end.
