# Verification: Design Acceptance Decision (slice 022)

Run: `dotnet test AgenticChangeOps.sln` on branch `feature/design-acceptance-decision`, 2026-11-02.

## Totals

| Project | Baseline (T002) | Now | New |
|---|---|---|---|
| Intake.Tests | 118 | 118 | 0 |
| Persistence.Tests | 81 | 81 | 0 |
| Drafting.Tests | 45 | 45 | 0 |
| ModelAccess.Tests | 271 | 271 | 0 |
| SampleCode.Tests | 123 | 123 | 0 |
| Workflow.Tests | 233 | 233 | 0 |
| Agents.Requirement.Tests | 158 (1 skipped) | 158 (1 skipped) | 0 |
| Agents.Architect.Tests | 158 (2 skipped) | 230 (2 skipped) | 72 |
| Agents.Developer.Tests | 172 | 172 | 0 |
| Agents.Reviewer.Tests | 403 | 403 | 0 |
| Api.Tests | 525 (2 skipped) | 596 (2 skipped) | 71 |

0 failed anywhere. Skipped are the opt-in live model checks. 143 new tests (72 + 71), all named
`DesignDecision*`.

## Edits to existing tests (equal to `baseline/expected-test-edits.md`)

1. `tests/AgenticChangeOps.Api.Tests/DescriptionTests.cs`: the route list gains
   `POST /change-requests/{id}/designs/{version}/decisions`; the test name says twenty-one routes.
2. `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json`: regenerated through the reviewed update
   variable (see below). It also feeds `ApiBaselineTests.ApiDescription_IsTheRecordedOne` and
   `ReviewSettingsTests.TheApiDescription_IsTheRecordedOne_EvenWithGarbageReviewSettings`, whose assertions
   are unchanged.
3. Additive only: `tests/AgenticChangeOps.Agents.Architect.Tests/Fakes/DesigningTestFixture.cs` gained a
   `Deciding` service and `ApproveAndDesignAsync`; no existing member changed.

No other existing assertion changed. The design operations transcript and the other golden transcripts are
untouched (the update variable also rewrote `transcript.txt` with line endings only; that was reverted).

## Golden diff summary

- `AuditEventType` enum gains `9`.
- New path `/change-requests/{id}/designs/{version}/decisions` (`RecordDesignDecision`): 201, 400, 404, 409.
- New schemas `DesignDecision`, `DesignDecisionBody`, `DesignDecisionOutcome`, `DesignDecisionView`,
  `DesignView`.
- `GET .../designs` and `GET .../designs/latest` 200 responses reference `DesignView` instead of
  `DesignDraft`.
- Nothing else moves.

## Deviations from the plan

- Storage round-trip tests (T012) live in `Agents.Architect.Tests`, not the Persistence contract suite.
- The design read response type is `DesignView`; a design without a decision serializes byte for byte as the
  stored draft does (asserted in `DesignDecisionRetrievalTests`).
- `ErrorMapper` gained `InvalidDesignVersion()` for the route's version token, in addition to
  `ForDesignDecision`.

## Not run

- Live check against a real Azure OpenAI deployment: not run. It needs real model settings and a full chain
  to produce a design; the decision itself calls no model.

## Convergence pass (T031 to T035)

Added tests only; no source changed. Automatic advancement left decided designs alone (T031); a failed
decision save gives 503 `storage-unavailable` with nothing stored or leaked (T032); overlapping decisions on
the durable store store exactly one, and a store-reported version conflict is the same `AlreadyDecided`
(T033); a successful decision writes nothing at warning level or above, and a refused one writes only the
existing counts-only "Request refused" line (T034); history reads during the race never change a seen
decision (T035). Test support changed additively: `DesignHost` takes an optional store,
`CapturingLoggerProvider` also records levels (`Lines` unchanged), new `DesignDecisionSaveFailingStore`.
