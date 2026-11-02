# Expected edits to existing tests (slice 022)

Only assertions that enumerate operations, audit event types or the published description may change.
Found by searching `tests/` for `AuditEventType`, `operationId`, the route lists, `DesignGenerated` and the
golden files. Nothing else is edited; if any other existing test fails, the new code is wrong.

## 1. `tests/AgenticChangeOps.Api.Tests/DescriptionTests.cs`

`Description_IsServedAsJson_AndListsExactlyTheTwentyRoutes` enumerates every operation.
- Add `"POST /change-requests/{id}/designs/{version}/decisions"` to the `expected` array (in the sorted
  position, after `POST /change-requests/{id}/designs`).
- Rename the test to `...ListsExactlyTheTwentyOneRoutes` (the name states the count).
- Check whether the test asserts a count anywhere else in the file (for example in other methods); if so,
  change 20 to 21 there only.

## 2. `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json` (golden, reviewed update)

Regenerated through `AGENTICCHANGEOPS_UPDATE_BASELINE=1` at T027. The diff must be exactly:
- `AuditEventType` enum gains `9` (`DesignDecided`).
- New path `/change-requests/{id}/designs/{version}/decisions` (`RecordDesignDecision`) with the 201, 400,
  404 and 409 responses and a `DesignDecisionBody` request schema.
- New schemas `DesignDecisionBody` (its `decision` is a string, so no verdict enum appears), `DesignDecision`,
  `DesignView` and `DesignDecisionOutcome`.
- `GET .../designs/latest` and `GET .../designs` 200 responses reference `DesignView` instead of
  `DesignDraft`.
Nothing else moves.
Three existing checks compare against this golden and go red until it is regenerated, with no edit to their
assertions: `ApiBaselineTests.ApiDescription_IsTheRecordedOne`,
`ReviewSettingsTests.TheApiDescription_IsTheRecordedOne_EvenWithGarbageReviewSettings`, and (after the
DescriptionTests edit) the route list below.

## 3. Checked, no edit expected

- `Baseline/golden/design-operations-transcript.txt` and `DesignOperationsBaselineTests`: bodies of designs
  without a decision must stay byte-identical (the `decision` member is omitted when absent). If this
  transcript changes, the new code is wrong.
- `transcript.txt`, `shared-plumbing-transcript.txt`, `drafting-skeleton-transcript.txt`,
  `caller-cancellation-transcript.txt`: no design decisions are made in them; unchanged.
- Tests that use `AuditEventType.X` by name (Architect, Reviewer, Workflow, Api): unaffected by an appended
  value.
- `Architect.Tests/StructureTests.cs`: the new files live in the Architect project and add no project
  reference; unchanged unless it lists the project's public types (checked at T004).
- Persistence contract suite: only gains new cases (T012); no existing case changes.
