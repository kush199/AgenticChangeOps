# Data Model: HTTP Operation To Request A Review

No record, enum, audit event or storage layout changes. The response body is the existing `Review`
record (from slice 018), serialised with the API's existing JSON options (camel-case, string enums).

## New in the API layer only

| Type | Purpose |
|---|---|
| `ReviewAvailability` | Holds one flag, `IsConfigured`, set once from `ReviewSetup`. Holds no setting value. |
| `ErrorMapper.ForReviewing(ReviewResult)` | Maps a `ReviewResult` to status and `ErrorResponse`. |
| `ErrorMapper.ReviewNotConfigured()` | 503 `review-not-configured` with fixed text. |

## Existing shapes reused

- `ErrorResponse(code, message, errors?, category?)`: `errors` for conflicts, `category` for 502s.
- `ReviewResult`: `IsSuccess`, `Review`, `Errors`, `FailureCategory`, `Rejection`.

## State

No state transitions. A successful request adds one immutable `Review` version and one `ReviewGenerated`
audit entry (both by the service); the change request status is untouched.
