# Implementation Plan: Change Request Intake

**Branch**: `001-change-request-intake` | **Date**: 2026-10-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-change-request-intake/spec.md`

## Summary

Implement the Change Request Intake slice as an in-process .NET 8 class library
(`AgenticChangeOps.Intake`). The library exposes `IChangeRequestService` with three
methods: `Submit`, `GetById`, and `GetAuditHistory`. All logic is deterministic and
fully testable via xUnit with an in-memory store and thin infrastructure fakes
(`ISystemClock`, `IIdGenerator`). No AI agents, no LLM calls, no external dependencies
in production code.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Dependencies**: xUnit 2.x (test project only); zero production NuGet dependencies

**Storage**: In-memory (`Dictionary`-backed) scoped to the running process; single-threaded
sequential access assumed

**Testing**: xUnit with `FakeSystemClock` and `FakeIdGenerator` for deterministic assertions;
real `InMemoryChangeRequestStore` used in tests (no mocking of store needed)

**Target Platform**: .NET 8 class library

**Project Type**: In-process library

**Performance Goals**: Submission < 500ms (trivially met with in-memory store; SC-001)

**Constraints**: No concurrency; no external service calls; no LLM calls (FR-017)

**Scale/Scope**: Single process; covers all acceptance scenarios in spec

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — all pass.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Synthetic Data Only | ✅ Pass | All test fixtures must use synthetic names/systems; no real data |
| II. Deterministic Routing | ✅ Pass | No agents, no LLM; pure C# service logic |
| III. Human Approval Gates | ✅ Pass | Intake only; no consequential downstream action triggered in this slice |
| IV. Typed Auditable Records | ✅ Pass | `ChangeRequest`, `ChangeRequestAuditEntry`, `SubmissionResult` all typed; one audit entry per submission |
| V. Spec-Driven Slice by Slice | ✅ Pass | Approved spec exists; this plan is for that spec |
| VI. Test-First | ✅ Pass | All logic deterministic; tests written and approved before implementation begins |
| VII. Simplicity | ✅ Pass | No agents; zero production dependencies; no speculative abstractions |

**Gate result: PASS — no violations, no complexity justification required.**

## Project Structure

### Documentation (this feature)

```text
specs/001-change-request-intake/
├── plan.md                          # This file
├── research.md                      # Phase 0 output
├── data-model.md                    # Phase 1 output
├── quickstart.md                    # Phase 1 output
├── contracts/
│   └── IChangeRequestService.md    # Phase 1 output
├── checklists/
│   └── requirements.md             # Spec quality checklist
└── tasks.md                        # Phase 2 output (/speckit-tasks — not yet created)
```

### Source Code

```text
src/
└── AgenticChangeOps.Intake/
    ├── AgenticChangeOps.Intake.csproj
    ├── Models/
    │   ├── Enums/
    │   │   ├── ChangeRequestStatus.cs   (Submitted, UnderReview, Approved, Rejected, Deferred)
    │   │   ├── ChangeType.cs            (Feature, Bugfix, ConfigChange)
    │   │   ├── Priority.cs              (Low, Medium, High, Critical)
    │   │   └── AuditEventType.cs        (Submitted)
    │   ├── ChangeRequest.cs
    │   ├── ChangeRequestAuditEntry.cs
    │   ├── SubmissionRequest.cs
    │   ├── SubmissionResult.cs
    │   └── ValidationError.cs
    ├── Services/
    │   ├── IChangeRequestService.cs
    │   ├── ChangeRequestService.cs
    │   └── ChangeRequestValidator.cs
    ├── Stores/
    │   ├── IChangeRequestStore.cs
    │   └── InMemoryChangeRequestStore.cs
    └── Infrastructure/
        ├── ISystemClock.cs
        ├── SystemClock.cs
        ├── IIdGenerator.cs
        └── GuidIdGenerator.cs

tests/
└── AgenticChangeOps.Intake.Tests/
    ├── AgenticChangeOps.Intake.Tests.csproj
    ├── Fakes/
    │   ├── FakeSystemClock.cs
    │   └── FakeIdGenerator.cs
    └── Unit/
        ├── ValidationTests.cs
        ├── SubmissionTests.cs
        ├── RetrievalTests.cs
        └── AuditTests.cs
```

**Structure Decision**: Single library project + single test project under `src/` and
`tests/` respectively. The `Infrastructure/` folder holds the two thin seam interfaces
(`ISystemClock`, `IIdGenerator`) and their production implementations. The `Services/`
folder separates orchestration (`ChangeRequestService`) from validation rules
(`ChangeRequestValidator`).

## Complexity Tracking

No constitution violations. No complexity justification required.
