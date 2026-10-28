# Data Model: HTTP Operations To Retrieve The Latest Review And The Review History

No record, enum, audit event or storage layout changes. Bodies are the existing `Review` record (slice 018),
serialised with the API's existing JSON options (camel-case, string enums), exactly as stored.

## New in the API layer only

| Type | Purpose |
|---|---|
| `ErrorMapper.NoReview()` | 404 `no-review` with fixed text. |

## Existing shapes reused

- `IReviewService.GetLatestReview` -> `QueryResult<Review?>`; `GetReviewHistory` -> `QueryResult<IReadOnlyList<Review>>`.
- `ErrorMapper.ForQuery(errors)`: maps unknown change request and storage failures.
- `ErrorResponse(code, message, errors?, category?)`.

## State

None. Both operations are read-only: no review, audit entry, status or workflow state is written.
