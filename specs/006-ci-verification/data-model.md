# Data Model: Automated Change Verification

This slice persists no application data and adds no typed record. The entities below describe what
the platform holds for a run, so the spec's terms map to something concrete.

## Verification Run

One execution of the `verify` job for one commit.

| Field | Meaning | Source |
|-------|---------|--------|
| Trigger | `pull_request` update or `push` to `main` | Platform event |
| Commit | The commit that was verified (the merge result for a pull request) | Platform |
| Started / Ended | Timestamps; the difference is bounded by 15 minutes | Platform |
| Outcome | `success`, `failure`, `cancelled` | Platform |
| Output | Full console log of every step | Platform run log |
| Evidence | Test result files (`.trx`) as one artifact | Upload step |

### Outcome rules

| Situation | Outcome | Counts as pass? |
|-----------|---------|-----------------|
| Build and all tests succeed, at least one test executed | `success` | Yes |
| Restore or build fails | `failure` | No |
| Any test fails | `failure` | No |
| No test executed, or no result file produced | `failure` | No |
| Model setting or live-check variable present | `failure` | No |
| Job exceeds 15 minutes | `failure` or `cancelled` (to confirm) | No |
| Superseded by a newer run of the same pull request | `cancelled` | No |

A skipped test (the live model check) is not a failure and is not counted as executed.

## Verification Result

The check named `verify` shown on a pull request or on a commit on `main`. It is the Outcome of
the latest Run for that commit. A pull request with no Run yet has no result and is not mergeable
once the check is required.

## Proposed Change

A pull request. It may receive many pushes, and each push creates a Run. Only the newest Run of a
pull request is allowed to finish; older in-progress ones are cancelled.

## State transitions

```text
queued → in progress → success
                     → failure
                     → cancelled   (superseded)
```

No transition is ever initiated by model output; every transition is decided by exit codes of
deterministic steps (constitution principle II).
