# Quickstart: validating workflow orchestration

All data is synthetic and no live model is used. Detail of shapes is in
[contracts/http-api.md](contracts/http-api.md) and [data-model.md](data-model.md).

## Prerequisites

- .NET 8 SDK
- Repository restored: `dotnet restore`

## Automated validation

```text
dotnet build AgenticChangeOps.sln
dotnet test AgenticChangeOps.sln
```

Expected: all tests pass offline; every pre-existing test passes with only the two reviewed API
description updates (route list, recorded description).

## Scenario walk-through (in-process, fake generators)

Run through `WorkflowEndpointTests`, or by hand against a host built with the fake generators:

1. Submit a synthetic change request. `GET .../workflow` shows stage `AwaitingRequirementsDraft`,
   `nextStep: RequirementsDraft`.
2. `POST .../workflow/advance`. Expect 201, `step: RequirementsDraft`, a draft identical in form to
   `POST .../drafts`, and `status` now showing `AwaitingDecision` with reason `AwaitingDecision`.
   The audit history shows exactly one `DraftGenerated` entry.
3. `POST .../workflow/advance` again. Expect 409 `no-step-due`; no new draft; no audit entry.
4. Approve the request with the existing approval operation (a human step, not part of advance).
   `GET .../workflow` shows `ReadyForDesign`, `nextStep: DesignDraft`.
5. `POST .../workflow/advance`. Expect 201, `step: DesignDraft`, then `status` stage `DesignComplete`
   with reason `NothingDue`. The audit history shows one `DesignGenerated` entry.
6. Design currency: the Requirement agent only drafts for Submitted or Deferred requests, so a newer
   requirements draft after a design is not reachable through the HTTP surface today. Verify the
   rule with a service-level fixture whose design was based on an older requirements version:
   status shows `ReadyForDesign` again and advance stores design version 2.
7. Reject or defer a second request; confirm status names the reason and advance is refused.

## Failure walk-through

- Malformed id: `GET` and `POST` both 400 `invalid-identifier`. Unknown id: both 404 `not-found`.
- Fake generator that fails or returns an incomplete answer: advance returns 502 `generation-failed`
  with a category only; `GET .../workflow` is unchanged; no draft, no audit entry.
- Model settings absent: advance on a due step returns 503 with the agent's own code; advance when
  nothing is due still returns 409 `no-step-due`.
- Fake generator output containing a fake endpoint and credential: neither appears in any response,
  log or error.

## Optional live check

Not part of the suite. With real model settings in the environment, advancing a synthetic request
through both steps exercises the same generators as `/drafts` and `/designs`. Never run by default.
