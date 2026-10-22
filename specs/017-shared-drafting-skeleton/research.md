# Research: Shared Drafting Skeleton

## R1. Which steps are identical in all three services?

Compared `RequirementsDraftingService`, `DesignDraftingService`, `ProposalService` line by line.

| Step | Identical? | Notes |
|---|---|---|
| Find request, compute eligibility, return failure | Same order; eligibility body differs | Engine owns the order, agent owns the check. |
| Generator failure: category exception becomes `Generator` error with category; any other exception becomes `Generator` error with type name only | Identical text and result shape | Developer additionally rethrows `OperationCanceledException` when the caller's token is cancelled. Kept as a visible flag. |
| Validate content; errors become failure with `MalformedOutput` | Requirement and Architect identical; Developer returns a third outcome (shape rejection) and then runs a verifier whose rejection is its own result | Engine's review step returns valid, invalid or rejected. |
| Locked step: find again, re-check eligibility, currency check, clock, next version = existing count + 1, build and save with audit entry | Same order and lock; building differs | Requirement has no currency check; Architect compares basis version; Developer compares two versions. Developer and Architect write tool-call entries before the main entry; Requirement none. |
| Latest and history reads | Identical shape: not-found error, read under `SyncRoot`, latest is last or null | Differ only in which store collection is read. |
| `NotFound` error text | Identical in all three | Moves with the engine (the error is part of lookup and reads). |

Decision: share the order and the guarantees; pass the differing parts in.

## R2. Where does the shared code live?

Decision: new project `AgenticChangeOps.Drafting` referencing Intake and ModelAccess (clarification, option A). Alternatives: Intake (no new reference but wrong home; user declined), ModelAccess (would need to reference Intake, reversing rule N2 and its structure test).

## R3. Composition or inheritance?

Decision: composition. The engine is a sealed generic class taking an `IDraftingAgent<...>` implemented by each service's private nested or companion type. Alternatives: abstract base class (hides the differences in overrides, merges the services, and makes a fourth agent inherit rather than add itself); delegates only (many constructor parameters, no named place for the intentional differences).

## R4. How does the automatic marker stay per agent without the engine knowing it?

The marker is a call argument of two services only. Decision: the service passes a per-call save delegate to the engine (`save(current, basis, content, version, generatedAt)`), closing over its own marker. The engine knows no marker. The Developer passes a delegate that ignores nothing, because it has none.

## R5. How are results kept unchanged?

Public result types and factories stay (callers, API mapping and tests use them: K11). The engine returns a neutral `DraftingOutcome<TRecord, TRejection>`; each service maps it in one line per case to its own result. Alternatives: a shared generic result replacing the three (changes the public API and tests; rejected).

## R6. How is "same behaviour" proven before code moves?

Existing tests already cover each service heavily (successes, every failure branch, concurrency, atomic save). The risk is gaps. Task 1 records the pass counts. A gap review lists any branch of the three services without an assertion (for example cancellation, outdated basis during save, tool-call entry order) and adds characterization tests first, run green on the old code, then kept unchanged. HTTP bodies are compared with golden files captured before any code moves.

## R7. Where are the skeleton's own tests?

New `AgenticChangeOps.Drafting.Tests`, with a throwaway stand-in agent. One test per engine behaviour (order of steps, failure translation, nothing but category in the failure, locked re-check, version assignment, overlapping calls never share a version, save failure leaves nothing stored, reads, cancellation flag both ways). Existing agent tests that now duplicate one of these stay as they are (assertions unchanged) and are listed for a later slice, as in slice 009.

## R8. How is duplication measured across three agents?

Extend the slice 009 script: same normalisation, same word list plus `Proposal`, `Proposals`, `Developer`; count lines shared by at least two agents (multiset intersection over every pair, and the total of lines present in all three). Run on the pre-slice commit via `-Root` on a worktree, and again after. Report both figures, headline as the sum of pairwise shared lines so it extends the earlier two-agent figure.

## R9. Which earlier register entries can be resolved?

K1 (failure translation), K2 (queries), K3 (save step overlap safety), K4 (eligibility order) are re-examined in the register: K1, K2 resolved by the engine; K3 resolved for lock, order and version, kept for building and saving; K4 kept (different rules, order, messages). K5, K12 kept (intake validation types are built per agent). K6, K7, K8, K9, K10, K13, K14, K15 kept with unchanged or updated reasons. K11 kept: result types stay per agent (R5). New entries cover the three services' eligibility, tool-call audit mapping (Architect and Developer copies are near identical: examine and decide), and the cancellation difference.

## R10. Tool-call audit mapping

`ToolCallEntry` and `MaxAuditTargetLength` are copied in the Architect and Developer services. They produce an intake audit entry with the agent name and a tool event type, so they are not agent-neutral in the sense of record shape. Decision to take in tasks after a line-by-line diff: if identical apart from the agent name, move once into the Drafting project as a function taking the agent name; if not identical, keep separate and register. The Requirement agent has no tools and is not affected.
