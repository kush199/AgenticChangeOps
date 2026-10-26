# Quickstart: Validate The Review Request Operation

Prerequisites: .NET 8 SDK, repository restored. No live model is needed for the automated checks.

## Automated

```powershell
dotnet test tests/AgenticChangeOps.Api.Tests --filter "FullyQualifiedName~Review"
dotnet test
```

Expected: the new review endpoint tests pass; the full suite passes with only operation- or
description-enumerating assertions changed (the golden `openapi.json` gains `RequestReview`).

## What the tests prove (see [contracts/http-api.md](contracts/http-api.md))

1. Eligible request -> 201, stored `Review` body, no `Location`, change request status unchanged.
2. Order of checks: malformed id, unknown id and unconfigured server each win over later conditions.
3. Each conflict, `generation-failed` and `review-rejected` case returns its code and stores nothing.
4. Sentinel endpoint, deployment, model text and file content never appear in a response or log line.
5. Overlapping requests give versions exactly 1..N.

## Manual (optional, maintainer's live check, not run in this slice)

Start the API with valid model settings, approve a change request, create a requirements draft, design and
proposal, then `POST /change-requests/{id}/reviews` and check for 201.
