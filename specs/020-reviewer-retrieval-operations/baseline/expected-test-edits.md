# Expected edits to existing tests (T003)

Searched `tests/AgenticChangeOps.Api.Tests` for tests that enumerate routes, enumerate the published
description, assert the absence of a review retrieval route, or assert no `Location` on the review response.
Edits are applied at T011 after approval. Re-search `tests/` for other tests before approval (T003).

## Edits proposed

| # | File | Test | Why it breaks | Edit |
|---|---|---|---|---|
| 1 | `DescriptionTests.cs` | `Description_IsServedAsJson_AndListsExactlyTheEighteenRoutes` | Lists every operation exactly. | Add `"GET /change-requests/{id}/reviews"` and `"GET /change-requests/{id}/reviews/latest"` in the list's order, rename to `...ListsExactlyTheTwentyRoutes`. |
| 2 | `Baseline/golden/openapi.json` | recorded document (`ApiBaselineTests`, `ReviewSettingsTests.TheApiDescription_IsTheRecordedOne_EvenWithGarbageReviewSettings`) | Gains two operations. | Regenerate through the reviewed update variable; the diff must be only the two GET operations (`GetLatestReview`, `GetReviewHistory`) and any schema they need. No assertion changes. |
| 3 | `ReviewEndpointTests.cs` | `AnEligibleRequest_Is201_WithTheStoredReview_AndNoLocation` | Asserts no `Location` header. | Rename to `...AndTheLatestLink`; replace the two no-`Location` assertions with `Assert.Equal($"/change-requests/{id}/reviews/latest", response.Headers.Location!.OriginalString)`. |
| 4 | `ReviewEndpointTests.cs` | `TheBody_HasNoRetrievalLink` | Inspected: asserts the body text and `Link` header only; the link is in `Location`, so it stays true. | No edit. |
| 5 | `ReviewDescriptionTests.cs` | `TheReviewPath_HasNoOtherOperation_AndNoRetrievalPathExists` | Asserts the only review path is the POST one. | Replace with a test that the review paths are exactly `/change-requests/{id}/reviews` (`get`, `post`) and `/change-requests/{id}/reviews/latest` (`get`). |
| 6 | `ReviewDescriptionTests.cs` | `NoRetrievalRoute_Exists` (two cases) | Expects 404 `route-not-found` for `/reviews/latest`. | Remove the `/reviews/latest` case; keep the `/reviews` (no change-request prefix) case. |
| 7 | `ReviewDescriptionTests.cs` | `GetOnTheReviewRoute_IsMethodNotAllowed` | GET on `/reviews` is now valid. | Replace with `PutOnTheReviewRoute_IsMethodNotAllowed` (same expectation for PUT). |
| 8 | `ReviewEndpointStructureTests.cs` | `ExactlyOneRoute_MentionsReviews_AndItIsAPost` | Expects a single review route. | Replace with a test that exactly three routes mention reviews: POST `/change-requests/{id}/reviews`, GET `/change-requests/{id}/reviews`, GET `/change-requests/{id}/reviews/latest` (covered by T016). |
| 9 | `ReviewOffMatchesBaselineTests.cs` | `OnlyOneRoute_MentionsReviews` | Expects one review route. | Replace with the same three-route expectation. |
| 10 | `ReviewOffMatchesBaselineTests.cs` | `TheApiDescription_MentionsReviewsOnlyThroughTheOneOperation` | Expects one review path. | Expect exactly the two review paths above; the no-review-tag assertion stays. |
| 11 | `README.md` | prose | Says reading reviews back is internal only. | Documentation, done at T018. |

## Checked and unchanged

- `ReviewOffMatchesBaselineTests` tests about workflow status, audit listing and service resolution: still true.
- `ReviewSettingsTests`, `ReviewCompositionTests`, `ReviewCheckOrderTests`, `ReviewRefusalEndpointTests`, `ReviewFailureEndpointTests`, `ReviewLoggingTests`, `ReviewConcurrencyEndpointTests`: drive only the POST route.
- `ProposalEndpointTests`, `DesignEndpointTests`, `DescriptionTests.Description_DocumentsEveryErrorCode` (uses `Assert.Contains` on a subset), `NoLeakTests`, `StorageErrorTests`: unaffected.

No other existing test is edited. If any test outside this list fails, the new code is fixed, not the assertion.
