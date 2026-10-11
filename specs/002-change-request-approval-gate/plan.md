# Implementation Plan: Change Request Approval Gate

**Branch**: `002-change-request-approval-gate` | **Date**: 2026-10-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-change-request-approval-gate/spec.md`

## Summary

Add the approval gate to the existing in-process .NET 8 library (`AgenticChangeOps.Intake`).
A new `IApprovalService` exposes two methods: `RecordDecision` (Approve / Reject / Defer with
collect-all validation, status transition, and one audit entry per decision) and
`IsClearedToProceed` (true only for `Approved`). It reuses the slice 001 store, clock, ID
generator, `ChangeRequest`, and `ValidationError`. The only changes to slice 001 types are
additive: three new `AuditEventType` values, four nullable trailing fields on
`ChangeRequestAuditEntry`, and one new store method (`Update`). No AI agents, no LLM calls,
no new production dependencies.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Dependencies**: xUnit 2.x (test project only); zero production NuGet dependencies

**Storage**: Existing `InMemoryChangeRequestStore`; single-threaded sequential access assumed

**Testing**: xUnit with `FakeSystemClock` and `FakeIdGenerator` (already in the test project);
the real in-memory store is used, no mocking

**Target Platform**: .NET 8 class library

**Project Type**: In-process library (same project as slice 001)

**Performance Goals**: Decision recorded in < 500 ms (SC-001; trivially met in memory)

**Constraints**: No concurrency; no external calls; no LLM calls (FR-014); slice 001 behavior
and tests must remain green

**Scale/Scope**: Single process; covers all acceptance scenarios in the spec

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — all pass.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Synthetic Data Only | ✅ Pass | Fixtures use fictional approvers, systems, reasons |
| II. Deterministic Routing | ✅ Pass | Eligibility and transitions are a pure predicate table in C#; no LLM |
| III. Human Approval Gates | ✅ Pass | This slice *implements* the gate; every decision persists approver, action, timestamp, status before/after. Artefact version = `ChangeRequestId` + `SubmittedAt` + status before/after (the record has no version counter and is immutable except `Status`; see spec Assumptions) |
| IV. Typed Auditable Records | ✅ Pass (constitution v1.0.1) | `ApprovalDecisionRequest`, `DecisionResult`, extended `ChangeRequestAuditEntry` are typed. The audit record gains nullable fields and three enum values appended at the end — additive and non-breaking under v1.0.1; recorded in Complexity Tracking |
| V. Spec-Driven Slice by Slice | ✅ Pass | Spec and clarification complete; this plan follows it; tasks to follow |
| VI. Test-First | ✅ Pass | All logic deterministic; tests to be written and approved before implementation |
| VII. Simplicity | ✅ Pass | No agents; one new service, one validator; reuses existing types and `ValidationError`; no speculative abstractions |

**Gate result: PASS — checked against constitution v1.0.1; the additive record extension is
recorded below.**

## Project Structure

### Documentation (this feature)

```text
specs/002-change-request-approval-gate/
├── plan.md                          # This file
├── research.md                      # Phase 0 output
├── data-model.md                    # Phase 1 output
├── quickstart.md                    # Phase 1 output
├── contracts/
│   └── IApprovalService.md          # Phase 1 output
├── checklists/
│   └── requirements.md              # Spec quality checklist
└── tasks.md                         # Phase 2 output (/speckit-tasks — not yet created)
```

### Source Code

```text
src/AgenticChangeOps.Intake/
├── Models/
│   ├── Enums/
│   │   ├── AuditEventType.cs            (MODIFY: + Approved, Rejected, Deferred)
│   │   └── ApprovalDecision.cs          (NEW: Approve, Reject, Defer)
│   ├── ChangeRequestAuditEntry.cs       (MODIFY: + ApproverName, Reason, StatusBefore, StatusAfter)
│   ├── ApprovalDecisionRequest.cs       (NEW)
│   └── DecisionResult.cs                (NEW)
├── Services/
│   ├── IApprovalService.cs              (NEW)
│   ├── ApprovalService.cs               (NEW)
│   └── ApprovalDecisionValidator.cs     (NEW)
└── Stores/
    ├── IChangeRequestStore.cs           (MODIFY: + Update)
    └── InMemoryChangeRequestStore.cs    (MODIFY: implement Update)

tests/AgenticChangeOps.Intake.Tests/
├── Fakes/
│   └── ApprovalTestFixture.cs           (NEW: shared wiring + synthetic seed helpers)
└── Unit/
    ├── ApprovalDecisionTests.cs         (NEW: US1, US2)
    ├── ApprovalValidationTests.cs       (NEW: US3)
    ├── ClearanceTests.cs                (NEW: US4)
    ├── ApprovalAuditTests.cs            (NEW: US5)
    └── StoreTests.cs                    (MODIFY: + Update cases)
```

**Structure Decision**: Extend the existing library and test project rather than adding a
project: the gate shares the store, clock, ID generator and records with intake, and a second
assembly would add reference plumbing with no benefit (Principle VII). A separate
`IApprovalService` keeps `IChangeRequestService` unchanged so slice 001's contract is stable.

## Complexity Tracking

| Item | Why Needed | Simpler Alternative Rejected Because |
|------|------------|--------------------------------------|
| Additive nullable fields on `ChangeRequestAuditEntry` and three appended `AuditEventType` values (non-breaking under constitution v1.0.1, Principle IV) | FR-008 requires approver, reason, and status before/after on each decision entry, in one ordered history with the submission entry | A second audit record type would force merging two lists to honor FR-009 ordering; stuffing values into `Details` would be an untyped string blob, which Principle IV forbids. Fields are nullable, so every slice 001 construction and persisted entry stays valid |
