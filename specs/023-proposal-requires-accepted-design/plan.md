# Implementation Plan: A Change Proposal Requires An Accepted Design

**Branch**: `feature/proposal-requires-accepted-design` | **Date**: 2026-11-04 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/023-proposal-requires-accepted-design/spec.md`

## Summary

Gate `POST /change-requests/{id}/proposals` on the latest design draft being Accepted. No new operation,
record, table or audit event. Three small changes:

1. `ProposalDesignBasis` (the Developer's own read model) gains an additive member `Decision`
   (`ProposalDesignVerdict { None, Accepted, Rejected }`, default `None`). `ProposalBasisAdapter` (Api
   composition, the only place that knows both agents) fills it from `IDesignDecisionService.GetDecisions`
   by matching the latest design's version, so a decision on an older version never counts and the Developer
   still references no other agent.
2. `ProposalService.Agent.CheckEligibility` refuses after its existing four checks: `None` -> "not
   decided", `Rejected` -> "was rejected", both as a `ValidationError` on a new field `DesignAcceptance`.
   `ErrorMapper.ForProposing` adds that field to the fields mapped to the existing 409 conflict, so the
   response has the same shape and code as the other proposal conflicts.
3. Storing time, with no change to the shared engine. `DraftingEngine` re-runs `CheckEligibility` inside the
   store lock, so on its own a design that changed after generation would be reported as "not decided"
   instead of "outdated" (spec FR-007). `ProposalService.ProposeAsync` therefore runs the agent's
   `CheckEligibility` once itself before calling the engine and returns its failure unchanged. If the engine
   later fails with a `DesignAcceptance` error, the first check had already passed, so the only possible cause
   is that the latest design changed while the proposal was being prepared; the service reports the existing
   outdated-design error instead. Decisions are immutable, so a decision error at storing time can only come
   from a newer design version. The cost is one extra read of the recorded facts per request. The shared
   `Drafting` project and the other three agents are untouched.

Everything is read-only over recorded facts: no model, no status change, no write, and no audit entry on
refusal.

## Technical Context

**Language/Version**: C# on .NET 8 (unchanged)

**Primary Dependencies**: none new. Uses `IDesignDecisionService` (read) in the Api adapter.

**Storage**: none. Reads existing `DesignDecision` outputs. No schema, layout or audit change.

**Testing**: xUnit. New and changed tests in `AgenticChangeOps.Agents.Developer.Tests` (service rules,
ordering, no model call, nothing stored, stale at storing), and
`AgenticChangeOps.Api.Tests` (409 for both reasons, accepted unchanged, older-version decision ignored,
overlap with a new design, existing refusals and order, no-leak, golden `openapi.json` unchanged). Existing
tests that request a proposal without accepting the design gain an acceptance step in setup only (FR-015).

**Target Platform / Project Type**: unchanged (ASP.NET minimal API).

**Performance Goals**: none.

**Constraints**: nothing from the model, sample, endpoint, deployment name or credential in any response,
log or error; refusal messages never echo caller input. Check order is fixed: exists and Approved,
requirements draft, design draft, design based on latest requirements, then decision.

**Scale/Scope**: one enum, one record member, two error messages, one pre-check and one error re-mapping in the service, one mapper line, one
adapter lookup.

## Constitution Check

*GATE: passed before research; re-checked after design (constitution v1.0.6).*

| Principle | Status |
|---|---|
| I. Synthetic data | Pass. Fixtures stay synthetic. |
| II. Deterministic routing | Pass. A plain predicate on recorded facts; no model involved in the check. |
| III. Human gates | Pass. Strengthens it: a proposal now needs a recorded human design decision. Nothing is automated, recorded or approved by this check. |
| IV. Typed auditable records | Pass. No new record. `ProposalDesignBasis.Decision` is an additive member with a default and is not persisted. A stored proposal already references its `DesignVersion`, whose decision is immutable. |
| V. Spec-driven | Pass. Spec, plan and tasks precede code; each needs explicit human approval. |
| VI. Test-first | Pass. Red tests are written and approved before implementation. |
| VII. Simplicity | Pass. No new agent and no new shared abstraction; the shared `Drafting` project is not changed. |

Post-design re-check: unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/023-proposal-requires-accepted-design/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/http-api.md
└── tasks.md             # /speckit-tasks
```

### Source Code (changes only)

```text
src/AgenticChangeOps.Agents.Developer/ ProposalBasis.cs (+ ProposalDesignVerdict, Decision member),
                                       ProposalService.cs (decision check in CheckEligibility, pre-check and outdated re-mapping in ProposeAsync)
src/AgenticChangeOps.Api/Composition/  ProposalAvailability.cs (ProposalBasisAdapter fills Decision)
src/AgenticChangeOps.Api/Errors/       ErrorMapper.cs (DesignAcceptance field -> conflict)
tests/                                 Developer.Tests, Api.Tests
```

**Structure Decision**: existing layout. The Developer stays independent of the Architect; the adapter
bridges them, as it already does for the drafts.

## Complexity Tracking

No violations. An earlier draft added a default `CheckReadiness` member to the shared `IDraftingAgent`; it was dropped because Principle VII allows a shared extension point only with two callers (or a one-for-one replacement), and this slice has one.
