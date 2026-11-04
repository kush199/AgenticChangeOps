# Verification: A Change Proposal Requires An Accepted Design

Date: 2026-11-04. `dotnet build` clean, `dotnet test AgenticChangeOps.sln`: 0 failed.

## Totals against the baseline (baseline-tests.txt)

| Project | Baseline | Now |
|---|---|---|
| Intake / Persistence / Drafting / ModelAccess / SampleCode / Workflow / Requirement / Architect / Reviewer | unchanged | unchanged (all pass) |
| Agents.Developer.Tests | 172 | 199 (+27 new) |
| Api.Tests | 594 passed, 2 skipped | 614 passed, 2 skipped (+20 new) |

Skipped tests are the opt-in live model checks, as at baseline. The shared `Drafting` project is unchanged.

## Requirement to test

| Requirement | Tests |
|---|---|
| FR-001, FR-012 accepted design behaves as before | ProposalDecisionAcceptedTests; ProposalDecisionEndpointTests.AnAcceptedDesign_... |
| FR-002, FR-013 order and existing refusals | ProposalDecisionOrderTests; ProposalDecisionOrderEndpointTests |
| FR-003 undecided reason | ProposalDecisionUndecidedTests; ProposalDecisionEndpointTests |
| FR-004 rejected reason, distinct | ProposalDecisionRejectedTests; ProposalDecisionEndpointTests |
| FR-005 older decision ignored | ProposalDecisionLatestOnlyTests; ProposalBasisAdapterDecisionTests; ProposalDecisionOrderEndpointTests |
| FR-006 no model call, store or audit | Undecided/Rejected/PreCheck tests; ProposalDecisionEndpointTests (AssertNothingHappened) |
| FR-007 outdated at storing time | ProposalDecisionStaleAtStoreTests; ProposalDecisionPreCheckTests; ProposalDecisionRaceEndpointTests (smoke) |
| FR-008 existing proposals untouched | StaleAtStore (earlier proposals); OrderEndpoint (history 1,3) |
| FR-009, FR-010 reads only, changes nothing | ProposalBasisAdapterDecisionTests (writes nothing); ProposalDecisionIsolationTests; DeveloperStructureTests |
| FR-011 same conflict shape | ProposalDecisionEndpointTests.TheRefusal_HasTheSameShape... |
| FR-014 no leak | ProposalDecisionNoLeakTests |
| FR-015 existing tests | baseline/expected-test-edits.md (setup only); no assertion changed |

## Edited existing tests (setup only)

See `baseline/expected-test-edits.md`: two fake basis helpers, `ReviewHost.ReadyForProposalAsync`,
`ProposalCompositionTests.ReadyAsync` and one step in `ReviewOfferCompositionTests`. No assertion changed.
The golden `openapi.json` is unchanged.

## Deviations from the plan

- Field and enum names are `DesignAcceptance` / `ProposalDesignVerdict` (not `DesignDecision`) because the
  existing `DesignDecisionStructureTests` forbid that text in the Developer project.
- The decision lookup is a function supplied by `ApiServiceRegistration`, for the same reason.
- The Drafting engine was not changed (the pre-check approach replaced the `CheckReadiness` hook).

## Live check

None needed: the gate runs before the model. Not run.

## Known follow-up (out of scope)

The workflow status may still offer the proposal step while the latest design is undecided.

## Convergence (Phase 5)

T029 (automatic advancement leaves undecided and Rejected designs alone), T030 (one log line naming only the field) and T031 (rejection and storage failures unchanged after a passing check) added; the full suite is green and the Api suite was run three times with no flakiness.
