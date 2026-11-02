# Quickstart: Validate The Review Offer

## Prerequisites

.NET 8 SDK. No model, endpoint or credential is needed; tests use fakes.

## Automated checks

```text
dotnet test AgenticChangeOps.sln
```

Expected: all pre-existing tests pass with assertions unchanged, except those that enumerate step names
or assert `offeredStep` for a request with a current proposal. New tests cover [the offer table](data-model.md),
stale proposals and reviews, no stored offer, advance reporting nothing due, reviewing configured and
unconfigured, and the golden description.

## Scenarios (see [contract](contracts/workflow-status.md))

1. Approve a synthetic change request; draft requirements; design; request a proposal (tools on, fake model). Read the workflow status: `offeredStep` is `Review`; stage, `nextStep`, `reason`, `completedSteps` unchanged.
2. Request the review. Read the status again: no `offeredStep`.
3. Regenerate the design, then request a new proposal: `Review` is offered again; the old review does not suppress it.
4. Call advance while the only offer is the review: nothing due, no new record.
5. Run with reviewing unconfigured: the status still works and shows no review offer.

A live check against a real model is not part of this slice.
