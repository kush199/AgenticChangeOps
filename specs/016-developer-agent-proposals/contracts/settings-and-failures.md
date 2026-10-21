# Contract: Settings And Failures

## Settings

| Setting | Default | Read when |
|---|---|---|
| `AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED` and the existing tool limits | off | as today; the Developer reuses them and adds no switch |
| `AGENTICCHANGEOPS_PROPOSAL_MAX_CHANGES` | 5 | only when the tools switch is on |
| `AGENTICCHANGEOPS_PROPOSAL_MAX_BYTES` | 6000 | only when the tools switch is on |

A present value that is not a positive whole number makes proposals "not configured" (503
`proposal-not-configured`); only the setting name is logged, never the value. It does not affect
design drafting. Model-call and tool-call limits are the existing per-request limits of the tool
session; they apply to each proposal request independently of designs.

## Availability

| Tools switch | Settings valid | Result of `POST /proposals` |
|---|---|---|
| off | n/a | 503 `proposal-tools-unavailable` |
| on | no | 503 `proposal-not-configured` |
| on | yes | proceeds to eligibility |

## Failure categories

Reuses the existing `GenerationFailureCategory` values; none is added. `ToolsUnavailable` (session
could not be opened, before any model call) and `LimitsExhausted` (no valid answer within limits) are
reported exactly as for designs. A valid, fully verified proposal produced after a limit was reached is
stored normally.

## Logging

Counts and categories only: tool calls made, refusals by kind, time taken, failure category,
rejection reason. Never content, paths, quotes, model text, endpoint, deployment name or credential.
