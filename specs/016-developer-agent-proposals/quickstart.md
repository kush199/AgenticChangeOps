# Quickstart: Validating The Developer Agent

Prerequisites: .NET 8 SDK; repository root as working directory. Automated checks need no model or
network.

## 1. Whole suite, tools off (nothing existing changed)

```text
dotnet test
```

Expect: every pre-existing test passes with assertions unchanged, plus the new Developer, API,
workflow and store cases. Compare against `baseline/baseline-tests.txt`.

## 2. Automated scenarios (scripted model, fake or temp-folder sample)

Run `dotnet test --filter "FullyQualifiedName~Developer"` and check:

| Scenario | Expect |
|---|---|
| Approved request, requirements and design drafts, valid scripted proposal | 201; version 1; links to both versions; status `Draft`; verification note present; one `ProposalGenerated` and one `ToolCalled` per call; request status unchanged; sample folder byte-identical |
| Two requests | latest is version 2; history is 1 then 2; version 1 unchanged |
| Not Approved / no requirements / no design / design on older requirements | 409 naming the rule; no model call; nothing stored |
| Unknown or malformed id | 404 / 400 on all three routes |
| Tools off, or limits invalid | 503, no model call |
| One wrong quote among valid edits; ambiguous quote; escaping path; add of existing file; over count; over size; zero edits | 502 `proposal-rejected` with the reason; nothing stored |
| Model fails, times out, malformed, tools unopenable, limits exhausted | 502 `generation-failed` with category; nothing stored |
| Newer requirements or design draft saved mid-request | 409 outdated; nothing stored |
| Hostile text in a sample file | tool set, status and verification unaffected |
| Workflow at `DesignComplete` | `offeredStep` present only when available; `nextStep` null; automatic advancement makes no proposal |
| Leak scan over errors, logs, audit | no file content, model text, endpoint, deployment name, credential |

## 3. Manual run (maintainer, optional, not part of CI)

1. Set the model settings as for designs, plus `AGENTICCHANGEOPS_SAMPLE_TOOLS_ENABLED=true`.
2. Start the API (`dotnet run --project src/AgenticChangeOps.Api`).
3. Submit a synthetic orders-service change request, draft requirements, approve it, request a design,
   then `POST /change-requests/{id}/proposals`.
4. Read it back with `GET .../proposals/latest`; confirm each quote exists once in the named sample
   file and that `git status` shows the sample folder untouched.

Live results are recorded by the maintainer separately; the suite never calls a live model.
