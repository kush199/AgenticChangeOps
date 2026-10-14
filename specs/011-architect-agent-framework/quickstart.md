# Quickstart: validating the slice

## Prerequisites

- .NET 8 SDK or later
- For the optional live check only: your own Azure OpenAI deployment and a signed-in cloud identity

## 1. Baseline (before any change)

```text
dotnet test AgenticChangeOps.sln
```

Record pass counts in `baseline-tests.txt`. Expected: all green, live checks skipped.

## 2. After the change

```text
dotnet test AgenticChangeOps.sln
```

Expected: same counts plus the new ModelAccess tests; zero assertion edits to existing tests (`git diff --stat tests` shows additions only, plus the live check file untouched).

## 3. Containment

```text
git diff --stat main -- src/AgenticChangeOps.Agents.Requirement src/AgenticChangeOps.Intake src/AgenticChangeOps.Workflow
```

Expected: no output.

## 4. Live check (opt-in)

Set the existing model variables (endpoint, deployment) and the existing live-check opt-in switch, then run the Architect live test. Expected: a valid design draft; nothing printed that contains model text, endpoint or deployment name. Record "run" or "not run" in the slice notes.

## 5. Comparison

Read `comparison.md`: replaced / added / kept / differed, with the parity-test output for every listed difference.
