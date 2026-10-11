# Quickstart Validation Guide: Change Request Intake

**Feature**: 001-change-request-intake | **Date**: 2026-10-11

This guide describes how to validate that the Change Request Intake slice is working
correctly end-to-end. Run these scenarios after implementation to confirm all acceptance
criteria pass.

---

## Prerequisites

- .NET 8 SDK installed
- Repository cloned; working directory: repository root
- No external services, databases, or LLM credentials required

---

## Build

```bash
dotnet build AgenticChangeOps.sln
```

Expected: 0 errors, 0 warnings.

---

## Run All Tests

```bash
dotnet test AgenticChangeOps.sln --logger "console;verbosity=normal"
```

Expected: All tests pass. No tests skipped. No live service calls made.

---

## Scenario Walkthroughs

The following scenarios map directly to the spec's acceptance criteria and can be
exercised via the test suite or a short driver program.

---

### Scenario 1 — Submit a Valid Change Request (Story 1, P1)

**Setup**: Construct a `SubmissionRequest` with all fields populated using synthetic data:

```
Title          = "Implement export to CSV for audit logs"
Description    = "Users need to export audit logs as CSV for offline review."
Requester      = "Alex Taylor"
AffectedSystem = "AuditReportingService"
Priority       = High
ChangeType     = Feature
```

**Action**: Call `IChangeRequestService.Submit(request)`.

**Expected outcome**:
- `result.IsSuccess` is `true`
- `result.ChangeRequest` is not null
- `result.ChangeRequest.ChangeRequestId` is a non-empty GUID
- `result.ChangeRequest.Status` is `Submitted`
- `result.ChangeRequest.SubmittedAt` is a UTC timestamp close to the call time
- `result.Errors` is empty

**Verify persistence**: Call `GetById(result.ChangeRequest.ChangeRequestId)`.
- Returns the same record with all original fields intact (SC-004).

**Verify audit**: Call `GetAuditHistory(result.ChangeRequest.ChangeRequestId)`.
- Returns a list with exactly 1 entry (SC-005).
- Entry has `EventType = Submitted` and `OccurredAt = result.ChangeRequest.SubmittedAt`.

---

### Scenario 2 — Reject Invalid Submission: Missing Field (Story 2, P2)

**Setup**: Construct a `SubmissionRequest` with `Title` set to empty string or whitespace only.
All other fields valid.

**Action**: Call `IChangeRequestService.Submit(request)`.

**Expected outcome**:
- `result.IsSuccess` is `false`
- `result.Errors` contains exactly 1 `ValidationError`
- `error.FieldName` is `"Title"`
- `error.Message` contains the word "required" (case-insensitive)
- `result.ChangeRequest` is null

**Verify nothing persisted**: `GetById` with any GUID returns null (SC-003).

---

### Scenario 3 — Reject Invalid Submission: Bad Enum Value (Story 2, P2)

**Setup**: Construct a `SubmissionRequest` with all string fields valid but `Priority` set to
a value not in the `Priority` enum (simulate by passing an out-of-range integer cast).

**Action**: Call `IChangeRequestService.Submit(request)`.

**Expected outcome**:
- `result.IsSuccess` is `false`
- `result.Errors` contains 1 error with `FieldName = "Priority"`
- Error message lists accepted values: Low, Medium, High, Critical

---

### Scenario 4 — Reject Multiple Invalid Fields (Story 2, P2)

**Setup**: Construct a `SubmissionRequest` with both `Title` blank and `Description` blank.

**Action**: Call `IChangeRequestService.Submit(request)`.

**Expected outcome**:
- `result.IsSuccess` is `false`
- `result.Errors` has exactly 2 entries: one for `Title`, one for `Description`
- Order of errors: validation collects all failures, not just the first

---

### Scenario 5 — Retrieve Unknown ID Returns Null (Story 3, P3)

**Action**: Call `GetById(Guid.NewGuid())` on a fresh store (no submissions made).

**Expected outcome**: Returns `null`.

---

### Scenario 6 — Audit History for Unknown ID Returns Null (Story 4, P4)

**Action**: Call `GetAuditHistory(Guid.NewGuid())` on a fresh store.

**Expected outcome**: Returns `null`.

---

### Scenario 7 — Field Trimming (Edge Case)

**Setup**: Construct a `SubmissionRequest` with `Title = "  Export Feature  "` (leading/trailing
spaces). All other fields valid.

**Action**: Call `Submit(request)`.

**Expected outcome**:
- `result.IsSuccess` is `true`
- `result.ChangeRequest.Title` is `"Export Feature"` (trimmed)

---

### Scenario 8 — Boundary Value: Title Exactly 200 Characters (Edge Case)

**Setup**: `Title` = a string of exactly 200 characters (e.g., `new string('A', 200)`).

**Expected outcome**: `result.IsSuccess` is `true`.

---

### Scenario 9 — Boundary Value: Title 201 Characters Rejected (Edge Case)

**Setup**: `Title` = a string of 201 characters.

**Expected outcome**: `result.IsSuccess` is `false`; error on `Title` mentioning the 200-character limit.

---

## Test Coverage Expectation

Per SC-006: 100% of deterministic logic paths must be covered by the automated test suite.
Run coverage report to verify:

```bash
dotnet test AgenticChangeOps.sln --collect:"XPlat Code Coverage"
```

Review the coverage report and confirm:
- All validation rules (FR-001 through FR-008) have at least one positive and one negative test.
- All service methods (`Submit`, `GetById`, `GetAuditHistory`) have both found and not-found paths covered.
- `FakeSystemClock` and `FakeIdGenerator` are used for all deterministic assertions.
- No test makes a real LLM call or hits any external service.

---

## References

- [Service Contract](contracts/IChangeRequestService.md)
- [Data Model](data-model.md)
- [Feature Spec](spec.md)
- [Research Decisions](research.md)
