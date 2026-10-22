# Quickstart: Validating the Shared Drafting Skeleton

Prerequisites: .NET 8 SDK, clean working tree on `feature/shared-drafting-skeleton`.

1. **Before**: `dotnet test AgenticChangeOps.sln` and save the counts in `baseline-tests.txt`. Run the three-agent `measure-duplication.ps1` on the pre-slice commit (via `-Root` on a worktree) and record the figure in `duplication.md`. Capture the HTTP baseline goldens ([contract](contracts/http-api-unchanged.md)).
2. **After**: run the same full suite. Expect 0 failed and the same skipped (opt-in live checks). `git diff HEAD -- tests` shows only new files plus one expected-list line in each of the three agent structure tests.
3. Run `measure-duplication.ps1` again; both figures go in `duplication.md`.
4. The HTTP baseline comparison passes and no golden file differs.
5. `dotnet test tests/AgenticChangeOps.Drafting.Tests` passes the neutrality rules in [dependency-rules](contracts/dependency-rules.md).
6. Fourth-agent walk-through: the stand-in implements the agent interface, its service and a save delegate, referencing Drafting only; no shared or agent file is edited.
7. Every entry of the [register](contracts/kept-separate-register.md) has a final status and reason.
