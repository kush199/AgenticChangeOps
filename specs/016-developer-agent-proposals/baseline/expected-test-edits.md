# Expected edits to existing test data (T005, awaiting approval)

Found by appending the two enum values (T006 and T007 source edits) and running the whole suite:

| Result | Count |
|---|---|
| Existing tests that failed | 1 of 1280 |
| Tests that list every `AuditEventType` or `WorkflowStep` value | 0 |

The one failure: `AgenticChangeOps.Api.Tests.Baseline.ApiBaselineTests.ApiDescription_IsTheRecordedOne`
(compares the served API description with `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json`).

## Proposed additive edits

1. **Now (Phase 2, T006):** `golden/openapi.json` gains the `ProposalGenerated` value in the
   `AuditEventType` enum. Regenerated with `AGENTICCHANGEOPS_UPDATE_BASELINE=1`, diff reviewed to be
   that one added line (and nothing else).
2. **At T029, after the T028 gate:** the same golden gains the three new operations, their schemas
   (`ChangeProposal`, `ProposedChange`, the enums) and the optional `offeredStep` on the workflow
   status schema. Additions only, diff reviewed.

No assertion in any test file is edited. No other existing test needs to change.

## Applied so far (after approval)

- `golden/openapi.json` regenerated twice; the diff is additions only: `AuditEventType` gains `7`,
  `WorkflowStep` gains `2`, and the workflow status schema gains the optional `offeredStep`
  (applied at T007 because the schema member exists from then on).
- `golden/transcript.txt` was touched by the regeneration run (line endings only) and restored with
  `git checkout`; no content changed.
- Still to come at T029: the three new operations and the proposal schemas.
