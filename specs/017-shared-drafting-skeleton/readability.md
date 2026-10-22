# Readability Cost (for your decision)

Each service now has two parts: the public service (constructor, the operation, the two reads, a `Save` method) and a private nested `Agent` class holding that agent's rules. The shared sequence is in `Drafting/DraftingEngine.cs`.

| Service | Lines before | Lines after | Reads better, same or worse? |
|---|---|---|---|
| `RequirementsDraftingService` | 160 | 148 | **Same.** The eligibility rules, the draftable-status rule and the record are easier to find than before because nothing else is mixed in. The five type arguments on the engine field are the main cost. |
| `DesignDraftingService` | 210 | 184 | **Same.** The currency message now sits in `CheckStillCurrent`, away from the save it protects; a reader must know the engine calls it inside the lock. |
| `ProposalService` | 242 | 208 | **Slightly worse for the flow, better for the rules.** The quote check, the two-version currency rule and the cancellation flag are visible and commented, but the order "validate, then verify, then lock, then re-check" is no longer visible in one method; it is in the engine contract. |

## Costs

- A reader of any service now needs the engine's step order (`contracts/engine-contract.md`) to see when each of its methods runs. Each nested `Agent` class carries a summary comment naming its intentional differences.
- The engine has five type arguments (`TRecord, TBasis, TGenerated, TContent, TRejection`). Two of them are placeholders for most agents (`NoBasis` for the Requirement agent, `NoRejection` for the Requirement and Architect agents).
- Total production code grew by 103 normalised lines (see `duplication.md`), although shared lines between agents fell by 45.

## Decision for you

Accept as is, or choose a remedy. Options I can describe but have not done:

1. Accept: the sequence is in one place, which was the goal; the cost is a layer of indirection.
2. Reduce type arguments by merging `TGenerated` and `TContent` where an agent has one content type (Requirement, Architect). This removes one argument for two agents and needs a `Review` that returns the same type; it touches the shared interface, so it is a new slice.
3. Revert this slice if the indirection is judged not worth 45 fewer shared lines.

## Decision recorded

Accepted as is (option 1), 2026-10-22. The sequence stays in the shared engine; the indirection is the agreed cost.
