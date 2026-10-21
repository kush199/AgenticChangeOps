# Comparison: Developer Unused Versus The Baseline

Baseline: commit 29a733e, recorded in `baseline-tests.txt` and the existing golden files.

## Existing behaviour with the Developer unused (tools off, the default)

| Check | Result |
|---|---|
| Existing tests (1280 passed, 3 skipped before) | Same counts and results after, in all eight existing projects; no existing assertion edited |
| Golden transcripts (`transcript.txt`, `design-operations`, `shared-plumbing`, `caller-cancellation`) | Byte-identical (their tests pass unchanged) |
| Design and audit responses | Identical; `ProposalOffMatchesBaselineTests` also checks the design response has no proposal members and the audit has no proposal entries |
| Workflow status at `DesignComplete` | Same members as before (`automatic`, `changeRequestId`, `changeRequestStatus`, `completedSteps`, `nextStep`, `reason`, `stage`); no `offeredStep` |
| Advance at `DesignComplete` | Still `409 no-step-due` |
| Automatic advancement | Makes no proposal and adds no audit entry, offered or not (`ProposalNotAutomaticTests`) |

## Classification (FR-008)

Producing a proposal is **non-consequential**: nothing leaves the system, nothing is created,
changed, deleted, compiled or run, nothing is applied, and no status moves. Applying a proposal would
be consequential and is not built.

## Additive members actually added (Principle IV)

| Member | Where |
|---|---|
| `AuditEventType.ProposalGenerated` (appended) | Intake |
| `WorkflowStep.Proposal` (appended) | Workflow |
| `WorkflowStatus.OfferedStep` (omitted when null) | Workflow |
| `IProposalOffer` and the optional `proposalOffer` argument of `WorkflowService` | Workflow |
| `ModelAnswer.ReadObjectList` | ModelAccess (shared) |
| New record `ChangeProposal` and the Developer project | Agents.Developer |
| New routes, `ProposalSettings`, `SampleFilesAdapter`, `ProposalBasisAdapter`, `ProposalAvailability`/`ProposalOffer` | Api |

## Edits to existing test data

| File | Edit | Status |
|---|---|---|
| `tests/AgenticChangeOps.Api.Tests/Baseline/golden/openapi.json` | Additions only (new enum values, `offeredStep`, three operations, proposal schemas); two comma-only line changes | Applied, approved |
| `tests/AgenticChangeOps.Api.Tests/DescriptionTests.cs` | The expected list of operations gains the three new routes | Applied, approved by the user (renamed to `...ListsExactlyTheSeventeenRoutes`; three lines added) |
