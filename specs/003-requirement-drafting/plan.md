# Implementation Plan: Requirement Drafting for Change Requests

**Branch**: `003-requirement-drafting` (working branch: `feature/requirement-agent`) | **Date**: 2026-10-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-requirement-drafting/spec.md`

## Summary

Add on-demand requirement drafting to the existing in-process .NET 8 library
(`AgenticChangeOps.Intake`). A new `IRequirementsDraftingService` exposes `DraftAsync`
(eligibility check, call the generator, validate, store a versioned `RequirementsDraft` plus
one audit entry as a single unit) and two retrieval methods (`GetLatestDraft`, `GetDraftHistory`).
Content generation sits behind `IRequirementsDraftGenerator`; this slice ships only the
interface and test fakes, so every scenario runs with no live model, network, or credentials.
Slice 001/002 types change only additively: one appended `AuditEventType` value, two nullable
trailing fields on `ChangeRequestAuditEntry`, and two new store methods. Drafting never writes
to the change request itself.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Dependencies**: xUnit 2.x (test project only); zero production NuGet dependencies

**Storage**: Existing `InMemoryChangeRequestStore`, extended with draft storage

**Testing**: xUnit with `FakeSystemClock`, `CountingIdGenerator` (existing) and a new
`FakeDraftGenerator` (returns configured content, throws, or returns null; counts calls). The
real in-memory store is used, no mocking.

**Target Platform**: .NET 8 class library

**Project Type**: In-process library (same project as slices 001 and 002)

**Performance Goals**: None specified. Latency is dominated by the generator, which is out of
this slice's control; the deterministic work around it is trivial.

**Constraints**: No live model calls anywhere in the repository (FR-020); no credentials or
endpoints in source (FR-021); slice 001 and 002 behaviour and tests stay green; drafting must
not modify `ChangeRequest` (FR-014)

**Scale/Scope**: Single process; all acceptance scenarios in the spec

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — all pass.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Synthetic Data Only | ✅ Pass | Fixtures and fake generator output are fictional; no endpoint, key, or deployment name appears in the repo (FR-021) |
| II. Deterministic Routing | ✅ Pass | The model only produces `DraftContent`. Eligibility, validation, versioning, and storage are deterministic C#; nothing generated feeds routing, status, or clearance (FR-014/015) |
| III. Human Approval Gates | ✅ Pass (non-consequential) | Drafting changes no status, commits no artefact downstream, and triggers nothing external. It is classified non-consequential in the spec (Assumptions), as the constitution requires, and the classification is for review at the compliance check. A draft cannot advance a request; only the existing approval gate can |
| IV. Typed Auditable Records | ✅ Pass (constitution v1.0.1) | `RequirementsDraft`, `DraftContent`, `DraftingResult` are typed; every draft is linked to `ChangeRequestId` and carries `AgentName` and `GeneratedAt`; input payload is the linked change request (immutable after intake except `Status`, which does not feed the content, so no snapshot is stored; see spec Assumptions) and output payload is the content sections. Audit record gains two nullable fields and one appended enum value: additive and non-breaking; recorded in Complexity Tracking |
| V. Spec-Driven Slice by Slice | ✅ Pass | Spec and one clarification complete; tasks to follow |
| VI. Test-First | ✅ Pass | Generator hidden behind an interface; tests use fakes only; tests to be written and approved before implementation |
| VII. Simplicity | ✅ Pass | No new agent (this is the Requirement/Product Agent's first capability); one service, one validator; reuses `ValidationError`, store, clock, id generator. `QueryResult<T>` has two callers (latest, history) |

**Gate result: PASS — checked against constitution v1.0.1.**

**Post-implementation compliance review (T042, 2026-10-11)**: PASS. Principle III: drafting is
classified non-consequential in spec Assumptions; the service never calls `Update`,
`IApprovalService` or `IChangeRequestService`. Principle IV: audit-record extension is
additive and recorded in Complexity Tracking; `AgentName` is on both the draft and the audit
entry. Principle VI: all drafting tests use `FakeDraftGenerator`; no production
`PackageReference`, model SDK, or HTTP client exists; the secret scan over `src` and `tests`
found nothing. Final suite: 163 passing (baseline 100 before this slice).

## Project Structure

### Documentation (this feature)

```text
specs/003-requirement-drafting/
├── plan.md                                  # This file
├── research.md                              # Phase 0 output
├── data-model.md                            # Phase 1 output
├── quickstart.md                            # Phase 1 output
├── contracts/
│   └── IRequirementsDraftingService.md      # Phase 1 output (service + generator contracts)
├── checklists/
│   └── requirements.md                      # Spec quality checklist
└── tasks.md                                 # Phase 2 output (/speckit-tasks — not yet created)
```

### Source Code

```text
src/AgenticChangeOps.Intake/
├── Models/
│   ├── Enums/
│   │   ├── AuditEventType.cs                (MODIFY: + DraftGenerated, appended last)
│   │   └── DraftStatus.cs                   (NEW: Draft)
│   ├── ChangeRequestAuditEntry.cs           (MODIFY: + AgentName, DraftVersion; nullable, trailing)
│   ├── DraftContent.cs                      (NEW: raw generator output, nullable members)
│   ├── RequirementsDraft.cs                 (NEW)
│   ├── DraftingResult.cs                    (NEW)
│   └── QueryResult.cs                       (NEW)
├── Services/
│   ├── IRequirementsDraftGenerator.cs       (NEW)
│   ├── IRequirementsDraftingService.cs      (NEW)
│   ├── RequirementsDraftingService.cs       (NEW)
│   └── DraftContentValidator.cs             (NEW)
└── Stores/
    ├── IChangeRequestStore.cs               (MODIFY: + SaveDraft, GetDrafts)
    └── InMemoryChangeRequestStore.cs        (MODIFY: implement both)

tests/AgenticChangeOps.Intake.Tests/
├── Fakes/
│   ├── FakeDraftGenerator.cs                (NEW)
│   └── DraftingTestFixture.cs               (NEW: shared wiring + synthetic content helpers)
└── Unit/
    ├── DraftingTests.cs                     (NEW: US1, US2)
    ├── DraftRetrievalTests.cs               (NEW: US3)
    ├── DraftEligibilityTests.cs             (NEW: US4)
    ├── DraftContentValidationTests.cs       (NEW: US5 validation paths)
    ├── DraftGenerationFailureTests.cs       (NEW: US5 generator failure, no-state-change)
    ├── DraftIsolationTests.cs               (NEW: US6)
    └── StoreTests.cs                        (MODIFY: + draft storage cases)
```

**Structure Decision**: Extend the existing library and test project, as slice 002 did, rather
than add a project: drafting shares the store, clock, id generator, and records, and a second
assembly adds reference plumbing for no benefit (Principle VII). A separate service interface
keeps `IChangeRequestService` and `IApprovalService` unchanged.

**Scope decision**: This slice delivers the generator *abstraction* and fakes only. A production
adapter for Azure OpenAI (which needs a NuGet dependency and environment configuration) is a
separate later slice, so this slice stays fully verifiable offline and keeps zero production
dependencies. See research.md decision 1.

## Complexity Tracking

| Item | Why Needed | Simpler Alternative Rejected Because |
|------|------------|--------------------------------------|
| Additive nullable fields `AgentName`, `DraftVersion` on `ChangeRequestAuditEntry` and `DraftGenerated` appended to `AuditEventType` (non-breaking under constitution v1.0.1, Principle IV) | FR-011 requires the draft version on the audit entry; Principle IV requires `AgentName` on every record | Putting the version in `Details` would be an untyped string blob; a second audit record type would split history and break the single ordered audit list. Both fields are nullable, so every earlier construction and persisted entry stays valid |
| Two new methods on `IChangeRequestStore` (`SaveDraft`, `GetDrafts`) | Drafts must persist with the request chain and be stored atomically with their audit entry | A separate draft store would make "draft and audit entry together" (spec edge case) a cross-store concern with no atomicity |
| Async generator and service method (`DraftAsync`) while earlier services are synchronous | A real model call is I/O-bound and cancellable; making the abstraction synchronous now would force a breaking change when the adapter slice arrives | Sync-over-async in the adapter later is a known hazard; retrieval methods stay synchronous because they do no I/O |
