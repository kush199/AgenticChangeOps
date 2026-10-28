# Quickstart: Validate The Review Retrieval Operations

Prerequisites: .NET 8 SDK, repository restored. No live model is needed for the automated checks.

## Automated

```powershell
dotnet test tests/AgenticChangeOps.Api.Tests --filter "FullyQualifiedName~Review"
dotnet test
```

Expected: the new retrieval tests pass; the full suite passes with only these assertions changed: the
golden `openapi.json` (two new operations) and the review request test that now expects the latest link.

## What the tests prove (see [contracts/http-api.md](contracts/http-api.md))

1. Latest returns the highest version; history returns all versions oldest first, version 1 unchanged.
2. Malformed id -> 400, unknown id -> 404 `not-found`, no review -> 404 `no-review` (latest) or `[]` (history).
3. On a host with reviewing unconfigured, both operations still return stored reviews.
4. Retrieval writes nothing: reviews, audit entries and statuses are identical before and after.
5. No sentinel endpoint, deployment, model text or file content appears in any response or log line.
6. `POST .../reviews` success carries `Location: /change-requests/{id}/reviews/latest`; body and status as before.

## Manual (optional, maintainer's live check, not run in this slice)

Request a review as in slice 019, then `GET /change-requests/{id}/reviews/latest` and
`GET /change-requests/{id}/reviews`.
