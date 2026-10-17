# Quickstart: Validating The Three Fixes

Prerequisites: .NET 8 SDK. All data is synthetic. No model, endpoint or credential is needed; the
automated suite uses fakes.

## Automated

```powershell
dotnet test AgenticChangeOps.sln
```

Expected: every test passes; the only changed assertions are the ones listed in
[plan.md](plan.md) under "Tests that assert a changed response". The golden diff for
`transcript.txt` touches only those lines; `openapi.json` and `design-operations-transcript.txt` do
not change.

## By hand (local run, drafting not configured is enough for steps 1 and 3)

Start the API with no model settings, then use any HTTP client.

1. **Messages.** Submit with `"priority": "urgent"` and `"changeType": "other"`; expect `400`, with
   one entry per field, each naming the field and listing the accepted values, with no `-1`, no
   `urgent`, no `other`. Post a decision `"maybe"`; expect the same for `Decision` and no audit
   entry.
2. **Order.** `POST /change-requests/not-a-guid/drafts` -> `400 invalid-identifier`;
   `POST /change-requests/<new random guid>/drafts` -> `404 not-found`; the same two on `/designs`;
   `POST` on a submitted request -> `503 ...-not-configured`.
3. **Approved without a draft** (needs drafting configured, or the test host): submit, approve,
   `GET .../workflow` -> next step `RequirementsDraft`; `POST .../workflow/advance` -> `201` with a draft;
   `GET .../workflow` -> design due; a second `POST .../drafts` -> `409` as before.

Pass criteria are SC-001 to SC-005 in [spec.md](spec.md); exact before/after bodies are in
[contracts/response-changes.md](contracts/response-changes.md).
