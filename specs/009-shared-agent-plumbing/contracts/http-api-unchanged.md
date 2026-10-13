# Contract: HTTP API Unchanged

No endpoint, status code, header, body shape or error code changes. Four recordings, all taken from code as it stood before the extraction, prove it. Goldens live in `tests/AgenticChangeOps.Api.Tests/Baseline/golden`.

## Recorded before the extraction

| Recording (golden) | Test | Covers | Captured from |
|---|---|---|---|
| `transcript.txt`, `openapi.json` | `ApiBaselineTests` | Intake, approval and requirement-draft operations with fake generators; the served API description | slice 007, unchanged |
| `shared-plumbing-transcript.txt` | `SharedPlumbingBaselineTests` | Both real generators, parsers and normalisers over HTTP with a scripted model client (table below): 258 responses | this repository at `a49108f` plus the new test files, before any `src` change |
| `design-operations-transcript.txt` | `DesignOperationsBaselineTests` | The design operations that do not depend on the model's answer: unknown and invalid id, Submitted, Deferred and Rejected requests, Approved with no requirements draft, not configured, latest and history before and after designs: 23 responses | a temporary worktree of commit `a49108f` (convergence T039) |
| `caller-cancellation-transcript.txt` | `CallerCancellationBaselineTests` | A caller that abandons a request while the model is still answering, for both agents: the client sees a cancelled request with no response, nothing is stored, the audit shows no entry | a temporary worktree of commit `a49108f` (convergence T040) |

No golden is ever rewritten from code written after the extraction. Design endpoint assertions in `DesignEndpointTests` and `DesignFailureTests` remain as additional, unchanged tests.

## Model-backed recording (`SharedPlumbingBaselineTests`)

`SharedPlumbingBaselineTests` registers the real `AzureOpenAIDraftGenerator` and `AzureOpenAIDesignGenerator` with a scripted model client. For each agent it drives the draft or design POST and the follow-up reads through these model behaviours and records status code, headers, normalised body, error code and audit entries (captured log lines are scanned for the sentinels, not recorded):

| # | Model behaviour | Expected category or result (recorded, not asserted by hand) |
|---|---|---|
| 1 | Valid bare object | success |
| 2 | Valid fenced block, unlabelled | success |
| 3 | Valid fenced block labelled `json` | success |
| 4 | Fence labelled other, unterminated, or with an inner fence | malformed output |
| 5 | Root not an object; invalid JSON | malformed output |
| 6 | Missing key; wrong value kind; non-string list item | malformed output |
| 7 | Required section blank or empty; list over the limit | validation failure reported as malformed output |
| 8 | Answer cut off; answer over the size limit | response too large |
| 9 | Empty answer | malformed output |
| 10 | Client throws a categorised failure for each category | that category |
| 11 | Client throws an arbitrary exception whose message holds the sentinel endpoint and deployment | unreachable, no sentinel anywhere |
| 12 | Model does not answer within a very small time limit | timed out |

Each run also asserts that the sentinel endpoint, deployment name and the model's answer text do not appear in any response body, header or captured log line.

## Comparison rule

After the extraction the same tests run unchanged against the new code. Any difference fails the slice; goldens are rewritten only through the existing update variable and only on a reviewed, intended change, which this slice never makes. A golden taken in a later convergence pass is captured from a worktree of `a49108f` (copy only the new test files in, write the golden there with the update variable, copy only the golden back, remove the worktree), never from the current code.
