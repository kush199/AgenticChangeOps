# Expected Edits To Existing Tests (slice 021)

Search of `tests/` for `WorkflowStep`, `OfferedStep` and `offeredStep` (excluding build output).

## Correction after implementation: two edits are needed

T003 said no test needed an edit. That was wrong: the full run after implementation showed two existing tests
in `AgenticChangeOps.Api.Tests` that read the workflow status of a request that already has a current proposal
(the full chain), which now correctly shows `offeredStep: Review`. Both are in the spec's allowed exception
("assert the offered step for a request that has a current proposal"). Proposed edits, awaiting approval:

1. `ReviewOffMatchesBaselineTests.TheWorkflowStatus_HasNoReviewMember_AtAnyStageOfTheFullChain`
   (line 99): the body now holds the offered step. Edit: remove the one member `"offeredStep":"Review"`
   from the body before `Assert.DoesNotContain("review", ...)`, so the test still proves no review data is in
   the status.
2. `ReviewCompositionTests.TheAdvisoryReview_ChangesNoStatus` (line 92): the workflow body before the review
   carries `offeredStep: Review` and, correctly, loses it once the review exists. Edit: compare the body
   after the review with the body before it **with only that member removed**; every other character equal.

The other failures seen during implementation (three description tests) were fixed in code or by the golden
update: the golden `openapi.json` gained `3`, and `ReviewOffer` was changed to check the settings without
building the review setup, so a status read writes no "not configured" log line
(`ReviewSettingsTests.AHostWithGarbageReviewSettings...` is unchanged and passes).

## Original list (superseded for the two tests above)

## Tests edited: none expected

| Test | Why it is unaffected |
|---|---|
| `ProposalCompositionTests.TheWorkflowOffersTheProposal_AtDesignComplete_WithoutMakingItTheNextStep` (asserts `"Proposal"`) | The request has a design but **no proposal**, so no current proposal exists and the proposal is still offered. |
| `ProposalOfferTests` (all, including the line asserting `Proposal == 2` and `DesignDraft == 1`) | No proposal fact source is given, so the offer is as today; `Review` is appended as 3, so existing numbers are unchanged. |
| `ProposalOffMatchesBaselineTests` | Asserts no `offeredStep` when proposals are off; unchanged. |
| `WorkflowRulesTests`, `AdvanceTests`, `AdvanceRefusalTests`, `WorkflowStatusTests`, `DueSelectionTests` | Assert `NextStep`/`Step` of `DesignDraft`/`RequirementsDraft` only. |

No existing test enumerates all step names, and none asserts the offered step for a request that has a
current proposal.

## Golden file edited (not a test assertion)

`tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json`: the `WorkflowStep` enum gains the value
`3` (Review). Regenerated at T015 through the reviewed update variable; the diff must be that one value.

If any other existing test fails after implementation, the new code is fixed, never the assertion.
