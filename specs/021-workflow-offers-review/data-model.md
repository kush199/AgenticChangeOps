# Data Model: Workflow Offers The Review

Nothing is stored. No persisted record changes.

## Changed type

- **WorkflowStep** (enum): `RequirementsDraft`, `DesignDraft`, `Proposal`, **`Review`** (appended). Serialised as a string. `Review` appears only in `offeredStep`; it is never a `nextStep` or a completed step.

## New reduced facts (Workflow, in memory only)

- **ProposalFact**: `Version`, `DesignVersion`.
- **ReviewFact**: `ProposalVersion`.

## New seams (Workflow)

- **IOfferFactSource**: `GetProposals(changeRequestId)`, `GetReviews(changeRequestId)`, each returning the reduced facts. Called under the store lock with the existing reads.
- **IReviewOffer**: `bool IsOffered`; true when reviewing is configured.

## Rule (OfferRules)

Inputs: stage, latest design version, proposal offer flag, review offer flag, proposal facts, review facts.

| Stage is `DesignComplete` | A proposal has `DesignVersion` = latest design | Review of its version exists | Reviewing available | Proposals available | Offer |
|---|---|---|---|---|---|
| no | – | – | – | – | none |
| yes | no | – | – | yes | `Proposal` |
| yes | no | – | – | no | none |
| yes | yes | no | yes | – | `Review` |
| yes | yes | no | no | – | none |
| yes | yes | yes | – | – | none |

`DesignComplete` already means Approved with the latest design based on the latest requirements. The offer
never alters `Stage`, `NextStep`, `Reason`, `CompletedSteps` or `Automatic`.
