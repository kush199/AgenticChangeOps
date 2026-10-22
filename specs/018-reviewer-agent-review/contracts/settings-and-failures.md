# Contract: Settings And Failures

## Settings

| Setting | Default | Notes |
|---|---|---|
| `AGENTICCHANGEOPS_REVIEW_MAX_FINDINGS` | 8 | positive whole number |
| `AGENTICCHANGEOPS_REVIEW_MAX_BYTES` | 8000 | positive whole number |
| Existing model settings (endpoint, deployment, identity) | as today | the same ones the other agents use |
| `AGENTICCHANGEOPS_SAMPLE_CODEBASE_PATH` | `sample-codebase/` beside the host | only where the sample is looked for |

The tools switch is not read and not needed. A present value that is not a positive whole number, or
invalid model settings, makes the review "not configured": the review service fails with the
`NotConfigured` category before any model call. Only the setting name is ever logged, never a value.
The settings are read lazily, the first time the review service is used, so a server that never uses
the Reviewer is unaffected by them.

## Failure results

| Situation | Result |
|---|---|
| Unknown or malformed ChangeRequestId | errors (field `ChangeRequestId`), nothing stored, model not called |
| Not Approved | errors (field `Status`), nothing stored, model not called |
| No requirements draft / no design draft / no proposal | errors with a distinct field each (`RequirementsDraft`, `DesignDraft`, `Proposal`) |
| Design not based on latest requirements; proposal not based on latest design or requirements; any of the three changed while preparing | outdated error (field `ProposalVersion` or `DesignDraftVersion`), nothing stored |
| Model call fails, times out, or returns incomplete or malformed output | errors carrying only a `GenerationFailureCategory` |
| Reference, count or size rule broken | typed `ReviewRejection`, nothing stored |
| Sample unavailable | not a failure: stored with `SystemCheck.Status = NotPerformed` |

## Failure categories

Reuses the existing `GenerationFailureCategory` values; none is added.

## Logging

Counts and categories only: refusals by kind, rejections by reason, failure category, time taken.
Never content, paths, quotes, finding text, model text, endpoint, deployment name or credential.
