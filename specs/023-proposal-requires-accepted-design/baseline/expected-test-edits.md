# Expected edits to existing tests (setup only, no assertion changes)

Method: stubs from T004 in place, then a throwaway copy of the implementation was run against the
unchanged suite (the copy was reverted; the real implementation waits for T019). Every failure was a test
that requests a proposal without first accepting the design. The edits below made the whole suite green
(2,477 tests passed, 5 opt-in live checks skipped as at baseline). No assertion is changed anywhere.

| File | Edit |
|---|---|
| `tests/AgenticChangeOps.Agents.Developer.Tests/Fakes/FakeProposalBasisSource.cs` | `Design(...)` and `Current(...)` gain an optional `decision` parameter; `Current` defaults to `Accepted` (the "eligible basis" helper), `Design` defaults to `None`. Adds `SetDecision(id, verdict)`. Fixes about 39 Developer tests (ProposalRejection, ProposalServiceStore, ProposalRetrieval, ProposalHostileContent, ProposalEligibility, CancellationCharacterization) with no change to those files. |
| `tests/AgenticChangeOps.Api.Tests/Fakes/ProposalHost.cs` | `FakeProposalBasis.Current(...)` gains `decision`, default `Accepted`, passed to the design basis. Fixes the fake-basis proposal tests (ProposalEndpoint, ProposalFailureEndpoint, ProposalLogging, ProposalRefusal, DraftingSkeletonBaseline, Review* that build a proposal through it). |
| `tests/AgenticChangeOps.Api.Tests/Fakes/ReviewHost.cs` | `ReadyForProposalAsync` adds one step after drafting the design: `POST /change-requests/{id}/designs/1/decisions` with Accept. Fixes the Review* tests that make a proposal over HTTP. |
| `tests/AgenticChangeOps.Api.Tests/ProposalCompositionTests.cs` | `ReadyAsync` adds the same Accept step after the design is drafted (real chain, `useFakeBasis: false`). |
| `tests/AgenticChangeOps.Api.Tests/ReviewOfferCompositionTests.cs` | `ANewerDesignAndProposal_...`: before requesting the proposal on the newer design, add an Accept step for design version 2. |

Not edited and still passing: every Workflow, Persistence, Intake, Drafting, Requirement, Architect,
ModelAccess, SampleCode and Reviewer test; all structure tests (`DesignDecisionStructureTests` in Api and
Architect pass because the Developer uses the names `ProposalDesignVerdict` / `DesignAcceptance` and the
decision lookup sits in `ApiServiceRegistration.cs`); the golden `openapi.json` (unchanged).

Note for T019: tests the new work adds must construct the undecided or rejected cases explicitly
(`Design(...)` default, or `decision:` argument), since `Current` now means "eligible".
