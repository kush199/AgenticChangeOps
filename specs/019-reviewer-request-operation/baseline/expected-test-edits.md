# Expected edits to existing tests (T003)

Searched `tests/` (all `*.cs`) for tests that enumerate routes, enumerate the published description, or
assert the absence of a review route. Every other test, including every test that reads
`/openapi/v1.json` for a single fact, is untouched. Edits are applied at T011 after approval.

## Edits proposed

| # | File | Test | Why it breaks | Edit |
|---|---|---|---|---|
| 1 | `tests/AgenticChangeOps.Api.Tests/DescriptionTests.cs` | `Description_IsServedAsJson_AndListsExactlyTheSeventeenRoutes` | It lists every operation exactly. | Add `"POST /change-requests/{id}/reviews"` to `expected`, and rename the test to `...ListsExactlyTheEighteenRoutes` (the name says seventeen). Nothing else in the file changes. |
| 2 | `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json` | recorded document, verified by `ApiBaselineTests` and `ReviewSettingsTests.TheApiDescription_IsTheRecordedOne_EvenWithGarbageReviewSettings` | The generated document gains the operation. | Regenerate only through the project's reviewed update variable (`ApiBaselineTests.UpdateVariable` = 1), then check the diff is only the `/change-requests/{id}/reviews` path (operation `RequestReview`, responses 201, 400, 404, 409, 500, 502, 503) and the `Review` schemas it needs. No assertion in either test class changes. |
| 3 | `tests/AgenticChangeOps.Api.Tests/ReviewOffMatchesBaselineTests.cs` | `NoRoute_MentionsReviews` | Asserts no route contains "review". | Replace with `OnlyOneRoute_MentionsReviews`: exactly one route contains `reviews`, `/change-requests/{id}/reviews`, and it accepts only POST. |
| 4 | same file | `TheApiDescription_MentionsNoReviewRouteTagOrSchema` | Asserts the description contains no "review" text. | Replace with `TheApiDescription_MentionsReviewsOnlyThroughTheOneOperation`: no path other than `/change-requests/{id}/reviews` contains "review", and no tag is added. |
| 5 | `tests/AgenticChangeOps.Api.Tests/Fakes/ReviewHost.cs` | class comment | Says "No route exists for reviews". | Reword that comment only (no code change except the additive helper from T004). |

## Checked and unchanged

- `ReviewOffMatchesBaselineTests` other tests (`ASubmittedRequest_AndTheExistingRoutes_NeverTouchTheReviewer`, `TheWorkflowStatus_HasNoReviewMember...`, `ARegisteredReviewer_AddsNothingToTheAuditListing...`): still true; the route is never called by them.
- `ReviewSettingsTests`: `AHostWithGarbageReviewSettings_StartsAndServesEveryExistingRouteIdentically` still holds because `ReviewAvailability` is built lazily on the first request to the review route, not at start-up. `TheNotConfiguredWarning_IsLoggedOnce_...` calls the service directly and is unaffected.
- `DescriptionTests.Description_DocumentsEveryErrorCode`: lists a subset of codes with `Assert.Contains`, so the new codes do not break it; the new codes are asserted in the new `ReviewDescriptionTests`.
- `ProposalCompositionTests`, `DesignEndpointTests`, `StorageErrorTests`, `NoLeakTests`: read the description for single facts or drive other routes only; unchanged.
- `ReviewCompositionTests`, `ReviewBasisAdapterTests`, `Baseline/DraftingSkeletonBaselineTests`: no route or description enumeration found.

No other existing test is edited. If any test outside this list fails, the new code is fixed, not the assertion.
