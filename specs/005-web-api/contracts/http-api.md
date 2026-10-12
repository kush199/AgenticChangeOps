# Contract: HTTP API

Base address by default: `http://127.0.0.1:5080`. All requests with bodies use
`Content-Type: application/json`. All responses are `application/json`. No authentication.
`{id}` is a change request identifier (GUID).

## Routes

| # | Operation | Method and route | Success |
|---|-----------|------------------|---------|
| 1 | Submit a change request | `POST /change-requests` | 201, change request, `Location: /change-requests/{id}` |
| 2 | Retrieve a change request | `GET /change-requests/{id}` | 200, change request |
| 3 | List audit history in order | `GET /change-requests/{id}/audit` | 200, array of audit entries in recorded order |
| 4 | Record an approval decision | `POST /change-requests/{id}/decisions` | 200, updated change request |
| 5 | Check clearance | `GET /change-requests/{id}/clearance` | 200, `{ changeRequestId, isCleared }` |
| 6 | Request a requirements draft | `POST /change-requests/{id}/drafts` | 201, draft, `Location: /change-requests/{id}/drafts/latest` |
| 7a | Latest requirements draft | `GET /change-requests/{id}/drafts/latest` | 200, draft |
| 7b | Full draft history | `GET /change-requests/{id}/drafts` | 200, array of drafts in ascending version order (empty when none) |

The published description is at `GET /openapi/v1.json` and the browsable page at
`GET /swagger`; they are not counted as operations.

## Example bodies (synthetic)

Submit:

```json
{
  "title": "Rotate demo cache settings",
  "description": "Adjust the demo cache time-to-live for the sample catalogue.",
  "requester": "Alex Example",
  "affectedSystem": "Sample Catalogue",
  "priority": "Medium",
  "changeType": "ConfigChange"
}
```

Decision:

```json
{ "approverName": "Sam Sample", "decision": "Defer", "reason": "Needs a clearer rollback plan." }
```

## Error mapping

Each error is `{ code, message, errors?, category? }` per [data-model.md](../data-model.md).

| Situation | Status | `code` | Notes |
|-----------|--------|--------|-------|
| Body not valid JSON, empty, or wrong shape | 400 | `invalid-body` | No field detail beyond a fixed message |
| Any field fails existing validation | 400 | `validation-failed` | `errors` lists every failing field and the existing message |
| `{id}` is not a valid identifier | 400 | `invalid-identifier` | Checked before any service call |
| `{id}` valid but no such change request | 404 | `not-found` | Routes 2 to 7b |
| No draft exists (route 7a only) | 404 | `no-draft` | Distinct from `not-found` |
| Unknown route | 404 | `route-not-found` | JSON, not framework default |
| Method not supported for the route | 405 | `method-not-allowed` | JSON |
| Request refused by business rules (final state, not draftable) | 409 | `conflict` | `errors` carries the service's specific reason |
| Request body larger than 64 KB | 413 | `body-too-large` | |
| Body is not `application/json` | 415 | `unsupported-media-type` | JSON error body |
| `Host` header not in the allowed list (default: `localhost`, `127.0.0.1`, `[::1]`) | 400 | `invalid-host` | Setting `ChangeOps:AllowedHosts` (`*` allows any) |
| Generation failed (draft route only) | 502 | `generation-failed` | `category` is one of `Unreachable`, `TimedOut`, `ResponseTooLarge`, `MalformedOutput`; nothing else |
| Drafting not configured (route 6 only) | 503 | `drafting-not-configured` | Names no setting value |
| Anything else | 500 | `unexpected-error` | Fixed generic message |

Order of checks for route 6: identifier validity (400) → drafting configured (503) → service
call (404, 409, 502, or success). Routes 7a and 7b do not depend on drafting configuration.

## Behavioural guarantees

- Each route calls exactly one existing service operation and returns its result unchanged in
  content (FR-002); the API validates nothing itself except identifier and body syntax.
- A refused or failed request changes nothing (the services already guarantee this).
- No response, log line or the published description contains an endpoint, deployment name,
  credential, prompt, model output, stack trace or file path.
- Drafting through route 6 may take up to the slice 004 time limit; every other route is
  immediate.
- No CORS headers are sent; browsers from another origin cannot call the API.
