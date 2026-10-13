# Quickstart: Verify the Shared Plumbing Slice

Run from the repository root.

## 1. Before any change (baseline)

```text
dotnet test AgenticChangeOps.sln                                       # record pass counts in baseline-tests.txt
pwsh specs/009-shared-agent-plumbing/measure-duplication.ps1           # record the "before" figure (production code)
pwsh specs/009-shared-agent-plumbing/measure-duplication.ps1 -Scope Tests   # and the test-code figure, reported separately
dotnet test tests/AgenticChangeOps.Api.Tests --filter "SharedPlumbingBaseline|DesignOperationsBaseline|CallerCancellationBaseline"
                                                                       # writes goldens (update variable), then compares
```

Expected: all green; goldens captured before any extraction. The last two tests were added after the extraction, so their goldens are captured from a temporary `git worktree` of commit `a49108f` (see `contracts/http-api-unchanged.md`); the measure script's `-Root` option points it at that worktree for the "before" figures.

## 2. After the change

```text
dotnet test AgenticChangeOps.sln
pwsh specs/009-shared-agent-plumbing/measure-duplication.ps1
pwsh specs/009-shared-agent-plumbing/measure-duplication.ps1 -Scope Tests
```

Expected:

- Same pass counts as baseline plus the new shared tests, 0 failed, 0 newly skipped (the live checks stay skipped).
- `SharedPlumbingBaselineTests`, `DesignOperationsBaselineTests`, `CallerCancellationBaselineTests` and `ApiBaselineTests` pass against unchanged goldens.
- The "after" shared-line figure for production code is lower than "before"; the test-code figure is not higher; all are in `duplication.md`.
- `git diff --stat HEAD -- tests '*.csproj' '*.sln'` is empty for tracked files: only new test files and goldens were added, and no assertion line in an existing test changed.

## 3. Add-a-third-agent walk-through (rule N7)

A new agent project references Intake and ModelAccess, then:

1. Builds its prompt, builds a `ModelCaller` with the options and client, and calls `GetAnswerTextAsync`.
2. Calls `ModelAnswerReader.Read(text, a => new MyContent(a.ReadString("..."), a.ReadList("...")))`.
3. Uses `SectionNormaliser.Text` and `SectionNormaliser.List` for its own sections and turns any error string into its own validation result.

Expected: no file in `ModelAccess`, `Agents.Requirement` or `Agents.Architect` is edited. `StandInAgentTests` is the executable form of this.

## 4. Hygiene

- Secret and endpoint scans (`NoSecretsTests`) pass for `src`, `tests`, `specs`.
- No new setting appears in `AzureOpenAIGeneratorOptions` and no package reference changes in any `.csproj`.
