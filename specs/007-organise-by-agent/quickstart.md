# Quickstart: Validate the Reorganisation

Prerequisites: .NET 8 SDK, repository checked out on `feature/organise-by-agent`. No model credentials are needed; live checks stay skipped.

## 1. Baseline before any move

```powershell
dotnet test AgenticChangeOps.sln
```

Expected: all existing tests pass. Add `ApiBaselineTests` and its goldens (see [research R8](research.md)); the test passes against pre-move code. Commit goldens before moving code.

## 2. After the move

```powershell
dotnet build AgenticChangeOps.sln -c Release
dotnet test AgenticChangeOps.sln -c Release
```

Expected:
- build succeeds with no new warnings attributable to the move;
- every previously existing test passes, same assertions;
- `ApiBaselineTests` passes against the unchanged goldens.

## 3. Agent location holds everything agent-specific (US1, FR-001)

Check that only `src/AgenticChangeOps.Agents.Requirement` declares: `RequirementsDraft`, `DraftContent`, `DraftingResult`, `DraftStatus`, `DraftContentValidator`, `IRequirementsDraftGenerator`, `IRequirementsDraftingService`, `RequirementsDraftingService`, `AzureOpenAIDraftGenerator`, `DraftPromptBuilder`, `DraftResponseParser`. Its tests live in `tests/AgenticChangeOps.Agents.Requirement.Tests`. Automated by rule D5.

## 4. Neutral code names no agent (US2, FR-003)

```powershell
dotnet test tests/AgenticChangeOps.ModelAccess.Tests
```

Expected: rules D3 and D4 pass.

## 5. Intake builds and tests without agents (US3, FR-004)

```powershell
dotnet test tests/AgenticChangeOps.Intake.Tests
```

Expected: pass; the project has no reference to ModelAccess or an agent (rules D1, D2).

## 6. Adding a second agent (US4, FR-006) - walk-through, nothing built

A developer adding, for example, `Agents.<Other>`:

1. Create `src/AgenticChangeOps.Agents.<Other>` referencing Intake and ModelAccess.
2. Create `tests/AgenticChangeOps.Agents.<Other>.Tests`.
3. Add both to the solution.
4. Register the agent's services in the API composition root and reference the new project from `Api`.

Confirm that none of the steps edits `Agents.Requirement` or `ModelAccess`. Step 4 touches only the composition root.

## 7. Documentation (FR-013, SC-008)

Open `README.md`. Confirm the layout section and the flow diagram show the three-way split, and a short "adding an agent" section lists the steps above.

## Walk-through result (T031, 2026-10-13)

Dry run in a scratch copy, discarded afterwards. Added `src/AgenticChangeOps.Agents.Probe`
(references Intake and ModelAccess; one output record implementing `IAgentOutput`, one call to
`store.SaveAgentOutput`, one use of `GenerationFailureCategory`) and an empty
`tests/AgenticChangeOps.Agents.Probe.Tests`, added both to the solution.

- Solution built and every existing test passed (112 + 154 + 61 + 99, one live check skipped).
- File hashes under `src/AgenticChangeOps.Agents.Requirement`, `src/AgenticChangeOps.ModelAccess`
  and `src/AgenticChangeOps.Intake` were identical before and after: zero edits.
- Files added: 2 project files, 1 source file. The API composition root was not touched in the
  dry run (registration is a documented step, not needed to compile).

## Verification record (2026-10-13)

| Check | Result |
|---|---|
| Existing tests, assertions unchanged | All pass. Only references, namespaces, splits and two qualified names changed (see tasks T034) |
| `ApiBaselineTests` against goldens captured pre-move | Pass; goldens never regenerated after capture |
| Test names vs `baseline-tests.txt` | None missing, except six `StoreTests.*Draft*` methods that now live, same names, in `DraftStoreTests` |
| Dependency rules D1 to D6 | Pass |
| New packages or settings | None (only the four existing `AGENTICCHANGEOPS_MODEL_*` variables) |
