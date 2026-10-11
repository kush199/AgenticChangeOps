# Quickstart: Change Request Approval Gate

**Feature**: 002-change-request-approval-gate

Validation guide proving the gate works end to end. Contract details live in
[contracts/IApprovalService.md](contracts/IApprovalService.md); types in
[data-model.md](data-model.md).

## Prerequisites

- .NET 8 SDK
- Slice 001 (intake) present and its tests green

## Run the tests

```powershell
dotnet test AgenticChangeOps.sln
```

Expected: all slice 001 tests and all approval tests pass; no network or LLM access needed.

## Validation scenarios

Use synthetic data only (e.g., requester `Avery Quill`, system `Sample Billing Portal`,
approver `Jordan Reed`).

| # | Scenario | Expected |
|---|----------|----------|
| 1 | Submit a request, then `Approve` with approver, no reason | `IsSuccess`; status `Approved`; history = `Submitted`, `Approved` |
| 2 | Submit, `Reject` with reason | status `Rejected`; entry has approver, reason, timestamp |
| 3 | Submit, `Reject` with no reason | failure; error on `Reason`; status still `Submitted`; history length 1 |
| 4 | Submit, `Defer` twice, then `Approve` | history = `Submitted`, `Deferred`, `Deferred`, `Approved` in order |
| 5 | `Approve` an already `Approved` request | failure; `Status` error naming `Approved`; history unchanged |
| 6 | Decide with unknown ID | failure; `ChangeRequestId` error; nothing stored |
| 7 | Blank approver and missing reason on `Defer` | two errors (`ApproverName`, `Reason`) |
| 8 | `IsClearedToProceed` for Submitted / Deferred / Rejected | `false` each |
| 9 | `IsClearedToProceed` for Approved | `true` |
| 10 | `IsClearedToProceed` for unknown ID | `false`, no exception |

## Done when

Every scenario above is covered by an automated test and `dotnet test` is green.
