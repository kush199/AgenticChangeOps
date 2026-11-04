# Research: A Change Proposal Requires An Accepted Design

**D1 Where the decision is read.** Decision: the Api's `ProposalBasisAdapter` reads
`IDesignDecisionService.GetDecisions` and sets `ProposalDesignBasis.Decision` for the latest design version
only. Rationale: the Developer references no other agent, and the adapter already bridges the drafts.
Alternatives: the Developer references the Architect (breaks independence); read the store directly
(duplicates the Architect's storage knowledge).

**D2 Default of the new member.** Decision: `Decision` defaults to `None`. Rationale: safe by default; a
fixture that forgets it is refused, never silently accepted. Existing fixtures that need an Accepted design
set it explicitly (FR-015 permits this). Alternative: default Accepted (unsafe).

**D3 Where the check runs.** Decision: the decision check lives in the Developer agent's `CheckEligibility`
(after the four existing checks). `ProposalService.ProposeAsync` runs that check once itself before calling
the shared engine and returns its failure unchanged; if the engine later fails with a `DesignAcceptance` error,
the service reports the existing outdated-design error, because the first check had passed and decisions are
immutable, so only a newer design version can explain it. Rationale: meets FR-007 and keeps the shared
`Drafting` project and the other agents unchanged (Principle VII). Alternatives: a new default
`CheckReadiness` member on `IDraftingAgent` (single caller, conflicts with Principle VII); a check inside
`GenerateAsync` (cannot return a conflict); relying on the engine alone (reports "not decided" instead of
"outdated" at storing time). Cost: one extra read of recorded facts per request.

**D4 Reasons.** Decision: new validation field `DesignAcceptance` with two distinct messages (not decided,
rejected), mapped by `ErrorMapper.ForProposing` to the existing 409 conflict body. No new status code or
error code. Stale at storing time reuses the existing outdated-design error (`DesignDraftVersion`).

**D5 Concurrency.** Decision: no new locking. Decisions are final and immutable, so the only way the latest
design stops being the accepted one is a newer design version; the engine's existing store-lock re-check
plus the version comparison already covers it.

**D6 Status offer.** Decision: out of scope per the spec. The workflow may still offer the proposal step
for an undecided design; recorded as a known follow-up.
