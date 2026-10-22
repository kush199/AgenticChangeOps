# Duplication Report: Shared Drafting Skeleton

Method: `measure-duplication.ps1` in this folder, extending slice 009's script to three agents. Production code only; names, blank lines, comments, `using`, `namespace` and brace-only lines normalised away. Word list: slice 009's list plus `Developer`, `Proposal`, `Proposals` (the word `Change` listed in tasks T003 was not added: it would also rewrite `ChangeRequest` in every agent equally and add nothing). The headline is the sum of the three pairwise shared-line counts. The same script runs before and after.

Sanity check of the script: on slice 009's commit `633c359` with `-Agents Two` it gives 212, 259 and 155 shared lines, the same as slice 009's "after" figures. Today's commit gives 155 shared lines for the same two agents but 219 and 323 normalised lines (not the old 212 and 259), because slices 011 to 016 changed both agents; the old figure is therefore a method check, not a baseline for this slice.

## Before and after

| Figure | Before (commit `7bf6f44`) | After |
|---|---|---|
| Requirement agent normalised lines | 219 | 209 |
| Architect agent normalised lines | 323 | 305 |
| Developer agent normalised lines | 489 | 464 |
| Shared Requirement and Architect | 155 (70.8% of the smaller) | 145 (69.4%) |
| Shared Requirement and Developer | 98 (44.7%) | 86 (41.1%) |
| Shared Architect and Developer | 157 (48.6%) | 134 (43.9%) |
| **Pairwise total (headline)** | **410** | **365** |
| Lines present in all three | 94 | 83 |
| New agent-neutral lines in `Drafting` (6 files plus project) | n/a | 156 |
| Total production lines, three agents plus shared | 1031 | 1134 |

Shared lines between the agents fell by 45 (11.0%). **Total production code grew by 103 normalised lines**: the sequence now exists once (in 156 lines including the engine, the agent interface, the outcome types, the tool-call helper and the not-found error), but each agent also gained a small nested class with the interface members and a delegate-based save, and the three services still contain the same kind of record-building and result-mapping code. This is a cost, recorded for your decision (see `readability.md`).

## What the remaining shared lines are

The sequence, the failure translation, the reads and the tool-call entry are gone from the agents (rule D6 scans for them). What still looks alike across agents:

| Where | Register entry |
|---|---|
| Each service's `Save` method: building the record and the audit entry with the same member list | K3, K8 |
| Each nested `Agent` class: constructor assigning collaborators, the `PropagatesCancellation`, `CountExisting`, `ReadAll` members | K3, N1 (interface members the engine requires) |
| Result mapping from the engine outcome to each agent's own result type | K11 |
| The agents' result types, validators, generators, prompt builders and store extensions (not touched by this slice) | K5, K6, K7, K8, K11, K12, K13 |
| The Architect and Developer tool-call loop around the shared entry helper | N2 |

## Test code (reported separately)

Same normalisation, applied to the three agents' test projects with `-Scope Tests`. The headline figure above is production code only.

| Figure | Before | After |
|---|---|---|
| Pairwise total | 1292 | 1321 |
| Lines present in all three | 252 | 260 |

The test figure rose by 29 because the three cancellation characterization tests (one per agent project, written first on the old code) are near-copies of each other by design. The new skeleton tests live in `Drafting.Tests`.

## Fourth agent walk-through

A new agent project references Intake, ModelAccess and Drafting and edits nothing that exists:

1. Write its own record (an `IAgentOutput`), basis, content and rejection types.
2. Implement `IDraftingAgent<...>` with its own eligibility, generator call, review, currency check and store reads.
3. Write a service that builds a `DraftingEngine` and passes a save delegate that builds its record and audit entry and calls the store's atomic save.

No file in `Drafting` or in the three agents needs an edit. `StandInFourthAgentTests` is the executable proof, and `StructureTests` (rules D1 to D3, D6) keeps the shared code from naming any agent and finds new agent folders by pattern.

## Behaviour map: each shared behaviour proven once

| Behaviour | The one test |
|---|---|
| Step order | `SequenceTests.TheStepsRunInOrder_...` |
| Unknown id: the agent's error, generator not called | `SequenceTests.AnUnknownId_...` |
| Ineligible: error without category, generator not called | `SequenceTests.AnIneligibleRequest_...` |
| Category failure carries category only | `GeneratorFailureTests.ACategoryFailure_...` (all six categories) |
| Other exception: type name only, nothing else leaks | `GeneratorFailureTests.AnyOtherException_...` |
| Cancellation rethrown when the agent says so and the caller's token is cancelled | `GeneratorFailureTests.WhenTheAgentPropagatesCancellation_ACancelledCallersTokenRethrows_...` |
| Cancellation reported when the agent does not propagate | `GeneratorFailureTests.WhenTheAgentDoesNotPropagateCancellation_...` |
| Cancellation with a live token reported even when propagating | `GeneratorFailureTests.WhenTheAgentPropagatesCancellation_ACancellationWhileTheCallersTokenIsLive_...` |
| Invalid content gives `MalformedOutput` | `ReviewTests.UnusableContent_...` |
| Rejection passed through, nothing stored | `ReviewTests.ARejection_...` |
| Null generation reaches the agent's review | `ReviewTests.ANullGeneration_...` |
| Eligibility re-checked in the locked step | `LockedStepTests.ARequestThatBecameIneligibleAfterGeneration_...` |
| Currency check in the locked step | `LockedStepTests.ABasisThatChangedAfterGeneration_...` |
| Version is count plus one | `LockedStepTests.TheVersionIsTheCountOfStoredOutputsPlusOne_...` |
| Clock read once and passed to the save | `LockedStepTests.TheClockIsReadOnce_...` |
| Save runs under the store lock | `LockedStepTests.TheSaveStepRunsWhileTheStoreLockIsHeld_...` |
| Overlapping runs never share a version | `OverlapAndAtomicityTests.OverlappingRuns_...` |
| A failed save stores neither output nor audit entry | `OverlapAndAtomicityTests.ASaveThatThrows_...` |
| No version gap after a failed save | `OverlapAndAtomicityTests.ARunAfterAFailedSave_...` |
| Not found for both reads | `ReadTests.AnUnknownId_...` |
| Empty: latest null, history empty | `ReadTests.WithNothingStored_...` |
| Latest and history ordering | `ReadTests.Latest_IsTheLastStored_...` |
| Reads wait for the store lock | `ReadTests.ABlockedLock_...` |
| Tool-call entry content, target cleaning, timing, elapsed clamp, outcome | the five `ToolCallAuditTests` |
| Neutrality, no agent in the shared code, sequence exists once | `StructureTests` |
| A fourth agent adds only itself | `StandInFourthAgentTests` |

### Existing agent tests that now prove a behaviour a second time

Kept as they are (assertions unchanged); listed for a later slice:

| Agent tests | Also proven once in |
|---|---|
| Category and other-exception cases of `DraftGenerationFailureTests`, `DesignGenerationFailureTests`, `ProposalRejectionTests` | `GeneratorFailureTests` |
| Overlap cases in `StoreConcurrencyTests`, `DesignConcurrencyTests`, `ProposalServiceStoreTests` | `OverlapAndAtomicityTests` |
| `Retrieval` tests of the three agents (not found, null, ordering) | `ReadTests` |
| Tool-call target cleaning cases of `SampleToolsAuditTests` and `ProposalServiceStoreTests` | `ToolCallAuditTests` |

Each agent test still proves something the shared test cannot: that this agent's rules, messages and record feed the engine correctly.
