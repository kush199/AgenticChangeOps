# Implementation Plan: The Workflow Status Offers The Review Once A Current Change Proposal Exists

**Branch**: `feature/workflow-offers-review` | **Date**: 2026-10-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/021-workflow-offers-review/spec.md`

## Summary

The workflow status already carries an optional, information-only `offeredStep` (slice 016: `Proposal`
at the design-complete stage). This slice adds one value, `Review`, and one rule: when the change request
is Approved, the latest design is based on the latest requirements (the existing `DesignComplete` stage),
a **current proposal** exists (a proposal based on that latest design), no review is based on that proposal,
and reviewing is configured, the status offers `Review`. Where a current proposal exists the proposal is
never offered any more (the review replaces it, or nothing is offered once reviewed or when reviewing is
unavailable). Where no current proposal exists the offer is exactly as today.

The rule is a pure function in the Workflow project over reduced facts (`ProposalFact`, `ReviewFact`),
like `WorkflowRules` over `RequirementsFact` and `DesignFact`. Two small seams, implemented in the Api
composition root like `IProposalOffer`, keep Workflow from referencing the Developer and Reviewer agents:
one supplies the facts, one says whether reviewing is configured. Stage, next step, reason, completed steps
and the automatic block are untouched, and neither advance path can run the offered step because it is not
a `NextStep`. The published description (golden `openapi.json`) gains the enum value `Review`.

## Technical Context

**Language/Version**: C# on .NET 8 (unchanged)

**Primary Dependencies**: none new. Workflow still references Intake, ModelAccess, Requirement and
Architect only.

**Storage**: unchanged; read-only. Facts are read under the store lock in the same step as the existing
drafts and designs, through the existing `IChangeRequestStore.GetAgentOutputs<T>`.

**Testing**: xUnit. Pure rule tests (table of fact combinations) in `Workflow.Tests`; service-level tests
with fake seams; Api tests with real composition for the end-to-end offer, with reviewing configured and
unconfigured; the golden `openapi.json` check. No live model.

**Target Platform / Project Type**: unchanged (ASP.NET minimal API plus libraries).

**Performance Goals**: none; two extra list reads per status read.

**Constraints**: derived on every read, never stored; no model call; no write; nothing from the model,
sample, endpoint, deployment name or credential in any response, log or error.

**Scale/Scope**: one enum value, one pure rule, two seams plus two Api adapters, one service edit, golden
file edit, tests.

## Constitution Check (v1.0.6)

| Principle | Result |
|---|---|
| I Synthetic data | Pass. Test data is synthetic. |
| II Deterministic routing | Pass. The offer is a deterministic predicate over recorded facts; no model decides it. |
| III Human approval gates | Pass. The offer is information only, implies no decision, and requesting a review stays a person's action; advancement never runs it. |
| IV Typed auditable records | Pass. No record is added or changed; the offer is never stored. `WorkflowStatus` gains no member; only the existing `OfferedStep` takes a new enum value, an appended value (non-breaking). Recorded under Complexity Tracking. |
| V Spec-driven | Pass. Spec exists; plan and tasks await approval before any code. |
| VI Test-first | Pass. Rule and service tests written and approved before implementation; fakes only. |
| VII Simplicity | Pass with note. No fifth agent. The two seams each have two callers (production adapter and test fake) and mirror `IProposalOffer`; see Complexity Tracking. |

Re-checked after design: no change to the result.

## Project Structure

### Documentation (this feature)

```text
specs/021-workflow-offers-review/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── workflow-status.md
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks, not created here
```

### Source Code

```text
src/AgenticChangeOps.Workflow/
├── WorkflowStep.cs        # append Review
├── WorkflowFacts.cs       # add ProposalFact, ReviewFact
├── IReviewOffer.cs        # NEW seam: is reviewing available (IsOffered)
├── IOfferFactSource.cs    # NEW seam: proposals and reviews as reduced facts
├── OfferRules.cs          # NEW pure rule: facts -> WorkflowStep? (null, Proposal or Review)
└── WorkflowService.cs     # Derive: replace the inline proposal offer with OfferRules

src/AgenticChangeOps.Api/Composition/
├── ProposalAvailability.cs / ReviewAvailability.cs   # adapters ReviewOffer, OfferFactSource added
└── ApiServiceRegistration.cs                        # register the two seams

src/AgenticChangeOps.Api/ (published description)  # golden openapi.json: enum gains "Review"

tests/AgenticChangeOps.Workflow.Tests/   # OfferRulesTests, service offer tests, advance-never-runs-it
tests/AgenticChangeOps.Api.Tests/        # composition offer test, unconfigured test, openapi golden
```

**Structure Decision**: The rule lives with the other workflow rules; adapters live in the Api composition
root, the one place that may see several agents, so no agent project references another and Workflow gains
no project reference.

## Decisions Resolving Spec Ambiguity

- Where a current proposal exists the proposal is never offered, even when reviewing is unavailable (the
  spec's "the review replaces the proposal" and "when a review exists nothing further is offered" both
  imply it; FR-009 says no review offer when unconfigured). Result: current proposal + reviewing
  unavailable => no offer.
- "Current proposal" = the highest-version proposal whose `DesignVersion` equals the latest design's version
  while the stage is `DesignComplete`. A review is "of it" when its `ProposalVersion` equals that proposal's
  version.

## Complexity Tracking

| Item | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| `IReviewOffer`, `IOfferFactSource` seams | Workflow must learn about proposals and reviews without referencing the Developer and Reviewer agents (D4/D5 rules) | Referencing both agents from Workflow couples the orchestrator to two more agents and breaks the pattern set by `IProposalOffer` |
| Enum value `Review` appended to `WorkflowStep` | The offer needs a name in the published description | Reusing `Proposal` would be wrong; a string would be untyped (Principle IV). Additive and appended, so non-breaking |
