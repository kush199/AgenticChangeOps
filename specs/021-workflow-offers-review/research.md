# Research: Workflow Offers The Review

No NEEDS CLARIFICATION remained. Decisions:

## 1. Where the rule lives

- **Decision**: a pure static `OfferRules` in `AgenticChangeOps.Workflow`, called from `WorkflowService.Derive` after the status is built.
- **Rationale**: same shape as `WorkflowRules`: reduced facts in, value out, trivially table-testable and provably free of stores and models (Principle II).
- **Alternatives**: keep the inline `if` in the service (grows past one condition and is harder to test); put it in the Api (the workflow status is built in Workflow).

## 2. How Workflow learns about proposals and reviews

- **Decision**: Workflow defines `ProposalFact(Version, DesignVersion)` and `ReviewFact(ProposalVersion)` and an `IOfferFactSource`; the Api composition root implements it over `IChangeRequestStore.GetAgentOutputs<ChangeProposal>` and `<Review>`.
- **Rationale**: Workflow references only Requirement and Architect today. Following `IProposalOffer`, the Api adapter is the one place that sees all agents.
- **Alternatives**: add project references from Workflow to Developer and Reviewer (more coupling; breaks the one-way pattern).

## 3. Reviewing availability

- **Decision**: `IReviewOffer.IsOffered`, implemented over `ReviewAvailability.IsConfigured`, evaluated only after every other condition holds.
- **Rationale**: `ReviewSetup` is built lazily so a server that never uses the Reviewer is unaffected by review settings; evaluating last keeps most status reads from touching it. A missing seam (null) means no review offer, exactly as a missing `IProposalOffer` means no proposal offer.
- **Alternatives**: evaluate on every read (needless setup for unrelated statuses).

## 4. What "current proposal" means with several proposals

- **Decision**: take the highest-version proposal based on the latest design; the review is offered only if no review has that proposal's version as `ProposalVersion`. Proposals on older designs are ignored; reviews of older proposal versions do not count.
- **Rationale**: matches spec FR-002/FR-010; proposal versions are unique per change request, so version equality identifies "a review of this proposal".
- **Alternatives**: match on design version too (redundant, since a proposal version already fixes its design).

## 5. Interplay with the existing proposal offer

- **Decision**: once a current proposal exists the proposal is never offered; with reviewing unavailable or already done, nothing is offered.
- **Rationale**: the spec says the review *replaces* the proposal where a current proposal exists and that nothing further is offered after a review. Offering a proposal that is already done is the defect this slice removes.
- **Alternatives**: fall back to offering the proposal when reviewing is unavailable (contradicts "replaces").

## 6. Published description

- **Decision**: the golden `openapi.json` is regenerated for the new enum value only.
- **Rationale**: the spec forbids any other interface change.
