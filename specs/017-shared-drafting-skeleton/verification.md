# Verification: Shared Drafting Skeleton

Recorded 2026-10-22 on branch `feature/shared-drafting-skeleton`, base commit `7bf6f44`.

| Check | Result |
|---|---|
| Existing tracked test files changed (`git diff HEAD -- tests`) | 3 files, 1 line each: the expected project-reference list in `StructureTests.cs` (Requirement, Architect) and `DeveloperStructureTests.cs`, now `AgenticChangeOps.Drafting`, `AgenticChangeOps.Intake`, `AgenticChangeOps.ModelAccess`. This is the single exception approved at planning (FR-011). The tests' method names are unchanged. |
| New test files | Characterization test in each of the three agent test projects; `DraftingSkeletonBaselineTests` and its golden in `Api.Tests`; the whole `Drafting.Tests` project. All written before the code they guard. |
| Existing goldens changed | None. `transcript.txt`, `openapi.json`, `design-operations-transcript.txt`, `shared-plumbing-transcript.txt` and `caller-cancellation-transcript.txt` are untouched; `drafting-skeleton-transcript.txt` was captured on the old code and not rewritten. |
| HTTP replay (T027) | `DraftingSkeletonBaselineTests` passes on the final code against the golden captured on commit `7bf6f44` (three identical runs when captured). |
| Project files (`git diff HEAD -- '*.csproj' '*.sln'`) | Only the new `Drafting` and `Drafting.Tests` projects and the three `ProjectReference` lines to `Drafting`. No package added. |
| Full suite (`dotnet test AgenticChangeOps.sln`) | 0 failed. Skipped are the same opt-in live checks as before (Requirement 1, Architect 2, Api 1). Totals: Intake 118, Persistence 81, ModelAccess 249, SampleCode 123, Workflow 194, Requirement 157 (+1), Architect 156 (+1), Developer 172 (+1), Api 345 (+1), Drafting 45 (new). |
| Release build | Succeeds; no warnings from the new projects. |
| Secret, endpoint and leak scans | Green. The repository-wide scans cover `src`, `tests` and `specs`, which include the new project. |
| Existing overlap and atomicity tests | Pass unchanged: `StoreConcurrencyTests`, `DesignConcurrencyTests`, `OverlappingRequests_GetDistinctConsecutiveVersions_AndOneAuditEntryEach`, `AReader_NeverSeesAProposalWithoutItsAuditEntry`, `DraftStoreTests`, `DesignVersioningTests`. |
| Sequence exists once | `StructureTests.NoAgentSource_HoldsACopyOfTheSequence` passes; a deliberate `Count + 1` added to the Requirement service made it fail, and was removed. |
| Intentional differences still true in code | Cancellation (N1), tool-call loop (Architect null-tolerant, Developer not), automatic marker (Requirement, Architect), eligibility order, basis checks, audit event types (`DraftGenerated`, `DesignGenerated`, `ProposalGenerated`), Developer quote check. |

## Not run

No live model check was run: no model code, prompt or generator changed, and the live checks stay opt-in.

## Deviations from the plan, recorded

- `IDraftingAgent.CheckStillCurrent` takes the change request id as its first argument. The Architect and Developer messages name the change request; the first draft of the interface left the id out. Found while converting the Architect; the interface, the engine, the stand-in agent and `data-model.md` were updated before any test depended on it.
- The HTTP baseline golden lives in `tests/AgenticChangeOps.Api.Tests/Baseline/golden` (the earlier slices' convention) rather than under `specs/017.../baseline/`. This also settles analysis finding C8.
- Word `Change` was not added to the measuring script's word list (see `duplication.md`).

## Convergence follow-up (T041 to T043)

- T041: kept `ToolCallAuditEntries` shared. FR-005, rule D2 and register entry N2 now state the reading: the change request and the audit entry are Intake-level shapes every agent stores.
- T042: the three structure-test methods were renamed to `AgentProject_ReferencesOnlyIntakeModelAccessAndDrafting`; no assertion changed.
- T043: `plan.md` and `data-model.md` reconciled with the built code.
