# Comparison: existing behaviour with the Reviewer unused

Recorded at T059 on `feature/reviewer-agent-review` after step 4.

## Result

With the Reviewer unused, every recorded response is byte-identical to before.

| Check | Result |
|---|---|
| Golden transcripts (intake, drafts, designs, proposals, shared plumbing, caller cancellation, drafting skeleton: status, headers, bodies, error codes, both stores) | Pass, unchanged. `Baseline` tests: 15 passed, 0 failed. |
| Generated API description (`golden/openapi.json`) | Identical except one added value (`8`) in the `AuditEventType` list. No route, tag or schema is added. |
| Route table | No route mentions reviews (`ReviewOffMatchesBaselineTests.NoRoute_MentionsReviews`). |
| Workflow status at the last stage of the full chain | No review member (`TheWorkflowStatus_HasNoReviewMember_AtAnyStageOfTheFullChain`); the shape asserted by the earlier slices is unchanged. |
| Automatic path and existing routes | Make no review: zero generator calls, zero model calls, no `ReviewGenerated` entry, no stored review. |
| Registering the service | Adds nothing to the audit listing; a host with garbage review settings starts and serves the recorded API description. |
| Existing test assertions | None edited. Against `main`, the only existing test file modified is `golden/openapi.json` (the one approved addition). |

## Additive members actually added in this slice

- `AuditEventType.ReviewGenerated` (appended after `ProposalGenerated`).
- `ModelAnswer.ReadObjectListWithStringList` and the `ObjectWithList` record in ModelAccess (new method and new file; no existing reader touched).
- New project `AgenticChangeOps.Agents.Reviewer` and its test project.
- In the Api composition root: `ReviewSettings`, `ReviewSampleFilesAdapter`, `ReviewBasisAdapter`, a lazily built review setup and the `IReviewService`, `IReviewGenerator` and `IReviewBasisSource` registrations. No endpoint, no `Program.cs` change, no workflow change.
- A project reference from the Api project to the Reviewer project.

## Classification

Producing a review is classified non-consequential (FR-011): nothing leaves the system, nothing is applied,
no status moves, and nothing reads a severity. The decision projects (Workflow, Intake, Persistence,
Drafting) do not reference the Reviewer and never name a review type.
