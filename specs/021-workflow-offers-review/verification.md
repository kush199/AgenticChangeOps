# Verification: slice 021, the workflow status offers the review

`dotnet test AgenticChangeOps.sln` after implementation, all projects green, 0 failed.

| Project | Baseline (T002) | Now | New tests |
|---|---|---|---|
| Intake | 118 | 118 | 0 |
| Persistence | 81 | 81 | 0 |
| Drafting | 45 | 45 | 0 |
| ModelAccess | 271 | 271 | 0 |
| SampleCode | 123 | 123 | 0 |
| Workflow | 194 | 233 | 39 (OfferRules, ReviewOffer, ReviewOfferAdvance, OfferStructure) |
| Requirement | 158 (1 skipped) | 158 (1 skipped) | 0 |
| Architect | 158 (2 skipped) | 158 (2 skipped) | 0 |
| Developer | 172 | 172 | 0 |
| Reviewer | 403 | 403 | 0 |
| Api | 513 (2 skipped) | 525 (2 skipped) | 12 (ReviewOfferCompositionTests, ReviewOfferSettingsTests) |

Skipped are the opt-in live model checks. The live check was not run.

## Edits to existing tests (approved)

1. `ReviewOffMatchesBaselineTests.TheWorkflowStatus_HasNoReviewMember_AtAnyStageOfTheFullChain`: now also asserts the one `offeredStep: Review` member is present and that nothing else mentions a review.
2. `ReviewCompositionTests.TheAdvisoryReview_ChangesNoStatus`: the workflow body before and after the review is equal except for the `offeredStep: Review` member.

This differs from `expected-test-edits.md` as first approved (none expected); the correction is recorded there.

## Golden diff

`tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json`: the `WorkflowStep` enum gains `3`. Nothing else.

## Notes

- `ReviewOffer` checks the model and review settings itself and logs nothing, so a status read neither builds the review setup nor writes the "not configured" line (`ReviewSettingsTests.AHostWithGarbageReviewSettings...` passes unedited).
- `SampleCode.Tests.McpToolListTests.OpeningASession_StartsNoProcess_AndUsesAnInMemoryTransport` failed once in one full run and passed alone and in two later full runs; it does not touch this slice's code (flaky, not investigated).
