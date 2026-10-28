# Contract: HTTP Operations To Retrieve The Latest Review And The Review History

Same conventions as the proposal retrieval operations: camel-case JSON, string enums, the shared error body
(`code`, `message`, optional `errors`, optional `category`), id parsed as a GUID. No request body; any body
is ignored. Both operations are read-only and do not depend on reviewing being configured.

| Operation | Route | Success |
|---|---|---|
| Get latest review | `GET /change-requests/{id}/reviews/latest` | 200, body `Review` with the highest `version` |
| Get review history | `GET /change-requests/{id}/reviews` | 200, body array of `Review`, oldest first; `[]` when none |

## Errors

| Status | Code | When | Operation |
|---|---|---|---|
| 400 | `invalid-identifier` | id is not a valid GUID | both |
| 404 | `not-found` | no such change request | both |
| 404 | `no-review` | change request exists, no review yet | latest only |
| 503 | `storage-unavailable` | as for the other retrieval operations | both |
| 500 | `unexpected-error` | anything unforeseen; fixed text | both |

No error carries a stack trace or internal detail. Never present in any error, log or response: model text,
sample file content or path, endpoint, deployment name, credential.

## Change to an existing response

`POST /change-requests/{id}/reviews` success stays 201 with the same body, and gains the header
`Location: /change-requests/{id}/reviews/latest` (as `POST .../proposals` does). Nothing else changes.

## Published description

- `GetLatestReview`, summary "Retrieve the latest review": 200 `Review`, 400, 404.
- `GetReviewHistory`, summary "List every review version in ascending order": 200 `Review[]`, 400, 404.
- `RequestReview` unchanged.

## Unchanged

No other route, status code, header, body or error code changes. Only assertions that enumerate operations,
the published description, or the successful review response may change in existing tests.
