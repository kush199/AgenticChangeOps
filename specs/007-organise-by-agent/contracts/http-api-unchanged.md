# Contract: HTTP API Unchanged

The HTTP contract of slice 005 (`specs/005-web-api/contracts/http-api.md`) is the contract of this slice. Nothing in it changes.

## Operations (8)

| Operation | Route |
|---|---|
| Submit change request | `POST /change-requests` |
| Get change request | `GET /change-requests/{id}` |
| Get audit trail | `GET /change-requests/{id}/audit` |
| Get clearance | `GET /change-requests/{id}/clearance` |
| Record decision | `POST /change-requests/{id}/decisions` |
| Draft requirements | `POST /change-requests/{id}/drafts` |
| Get draft history | `GET /change-requests/{id}/drafts` |
| Get latest draft | `GET /change-requests/{id}/drafts/latest` |

## What is compared (before vs after)

For each operation, over success and every error path covered by existing tests, including drafting not configured, generator failure categories, validation failures, unknown id, wrong status:

- status code
- response body (normalised for generated ids and timestamps only)
- response headers of interest (`Content-Type`, plus any the API sets deliberately)
- error code in the error body
- the served API description (`/openapi/v1.json`)

Goldens are captured on pre-move code and committed under `tests/AgenticChangeOps.Api.Tests/Baseline/` before any code moves.

## Expected differences

None. Schema names are short type names, so namespace and project moves do not appear. If the comparison finds any difference after the move, it is listed here with its cause and the slice is not complete until it is explained or removed.

| Difference | Cause | Status |
|---|---|---|
| (none) | | |
