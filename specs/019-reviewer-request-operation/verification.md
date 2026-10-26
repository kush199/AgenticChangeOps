# Verification: HTTP Operation To Request A Review

Run on branch `feature/reviewer-request-operation`, 2026-10-26, `dotnet test AgenticChangeOps.sln`: 0 failed.

| Project | Baseline (T002) | Now |
|---|---|---|
| Intake | 118 | 118 |
| Persistence | 81 | 81 |
| Drafting | 45 | 45 |
| ModelAccess | 271 | 271 |
| SampleCode | 123 | 123 |
| Workflow | 194 | 194 |
| Requirement | 157 (1 skipped) | 157 (1 skipped) |
| Architect | 156 (2 skipped) | 156 (2 skipped) |
| Developer | 172 | 172 |
| Reviewer | 403 | 403 |
| Api | 423 (2 skipped) | 483 (2 skipped): +60 new review route tests |

Skipped are the opt-in live model checks. The live check of the new route was not run.

## Edits to existing tests (equal to `baseline/expected-test-edits.md`)

1. `DescriptionTests.cs`: route list gains `POST /change-requests/{id}/reviews`; test renamed to "...Eighteen...".
2. `Baseline/golden/openapi.json`: regenerated through the update variable. Diff is 294 insertions, 0 deletions: the new path (`RequestReview`, responses 201, 400, 404, 409, 500, 502, 503) and the schemas it needs.
3. `ReviewOffMatchesBaselineTests.cs`: the two "no review route/description" tests replaced by "only one route / only one operation mentions reviews".
4. `Fakes/ReviewHost.cs`: class comment reworded; additive `RequestReviewAsync` helpers and a `BasisShaper` hook (default pass-through).

No other existing assertion changed.

## Notes

- The error body member is `errors` (the existing `ErrorResponse`), not `fields`.
- A model client exception becomes 502 `generation-failed`, so the 500 `unexpected-error` code is not reachable from a model call and is not tested.
- `ReviewRejection.MalformedFinding` is defined but never produced today, so it is not tested.
- `ReviewAvailability` is built lazily on the first request to the route; a host that never uses reviews reads no review setting.
