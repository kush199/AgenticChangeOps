# Quickstart: Reviewer Agent Reviews A Change Proposal

Validation guide. No endpoint exists in this slice; everything runs through tests or the internal
service.

## Prerequisites

- .NET 8 SDK; repository at `feature/reviewer-agent-review`.
- No model settings are needed for the automated suite (scripted fakes only).
- For the optional live check: the existing model variables (endpoint, deployment) and a signed-in
  operator identity. Never commit them.

## 1. Baseline (before any code)

```text
dotnet test AgenticChangeOps.sln
```

Record the pass counts in `baseline/baseline-tests.txt`, and the recordings described in
`contracts/http-api-unchanged.md`.

## 2. Each step leaves everything green

```text
dotnet test AgenticChangeOps.sln
```

Expected after every step: the baseline count plus the new tests, zero failures, no existing
assertion edited.

## 3. Scenarios the new tests prove

| Scenario | Expected |
|---|---|
| Approved request with full chain, scripted answer with valid references | review stored: Draft, `ReviewerAgent`, three upstream versions, system check, findings, open questions; one `ReviewGenerated` entry; status unchanged |
| Dangling reference among valid findings | whole review rejected, nothing stored |
| Finding with no reference; too many findings; oversized review | rejected, nothing stored |
| Model failure, timeout, incomplete or malformed answer | nothing stored; error carries only a category |
| Not Approved; missing requirements, design or proposal; outdated design or proposal | specific distinct error; model not called; nothing stored |
| Newer proposal saved while the model call is running | outdated error; nothing stored |
| Sample folder missing | stored with check `NotPerformed` |
| Quote that no longer matches | stored; edit outcome `QuoteNotFound` |
| Model writes its own system check | ignored; stored check is the system's |
| Two reviews for one request | versions 1 and 2; latest is 2; history in order; version 1 unchanged |
| Overlapping requests | distinct consecutive versions |
| Reviewer unused | existing responses byte-identical; route table and OpenAPI identical |

## 4. Optional live check (maintainer only)

Set the model variables, remove the skip on the `Live` test, run it once against the synthetic
sample, and record the outcome honestly in `verification.md` (including "not run").
