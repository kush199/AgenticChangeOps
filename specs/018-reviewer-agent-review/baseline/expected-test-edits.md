# Expected edits to existing test data (T004, awaiting approval)

Found by temporarily appending `ReviewGenerated` to `AuditEventType` and running the whole suite
(the edit was reverted straight afterwards; `git status` shows only `specs/018-reviewer-agent-review/`).

| Result | Count |
|---|---|
| Existing tests that failed | 1 of 1,644 |
| Tests that list every `AuditEventType` value | 0 |

The one failure: `AgenticChangeOps.Api.Tests.Baseline.ApiBaselineTests.ApiDescription_IsTheRecordedOne`
(compares the served API description with `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json`).
The `AuditEventType` schema there lists the numeric values 0 to 7.

## Proposed additive edit

1. **At T005:** `golden/openapi.json` gains the value `8` in the `AuditEventType` enum list.
   Regenerated with `AGENTICCHANGEOPS_UPDATE_BASELINE=1`, diff reviewed to be that one added line and
   nothing else; any line-ending-only change to other goldens from the regeneration is restored with
   `git checkout`.

No assertion in any test file is edited. No other existing test needs to change. No route is added, so
the OpenAPI document gains nothing else.

## Applied (after approval, T005)

- `golden/openapi.json` regenerated with `AGENTICCHANGEOPS_UPDATE_BASELINE=1`; the diff is exactly one
  added value (`8`) in the `AuditEventType` list (the previous last value gains a comma).
- The regeneration also rewrote the five transcript goldens (line endings only); restored with
  `git checkout`, so they are unchanged.
- No assertion in any test file was edited.
