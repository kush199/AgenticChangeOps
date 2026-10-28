# Implementation Plan: HTTP Operations To Retrieve The Latest Review And The Review History

**Branch**: `feature/reviewer-retrieval-operations` | **Date**: 2026-10-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/020-reviewer-retrieval-operations/spec.md`

## Summary

Expose the existing `IReviewService.GetLatestReview` and `GetReviewHistory` as two read-only operations in
the same shape as the proposal retrieval operations: `GET /change-requests/{id}/reviews/latest` (200 with
the highest-version `Review`; 404 `no-review` when none) and `GET /change-requests/{id}/reviews` (200 with
every `Review`, oldest first, empty list when none). Both parse the id (400 `invalid-identifier`), map an
unknown change request to 404 `not-found` through the existing `ErrorMapper.ForQuery`, and never touch
`ReviewAvailability`, so they work whether or not reviewing is configured. The successful review request
changes from a bare 201 to a 201 with `Location: /change-requests/{id}/reviews/latest`, exactly as proposals
do. The published description gains two operations. No service, record, store, workflow or other-agent
change; one new `ErrorMapper.NoReview()`.

## Technical Context

**Language/Version**: C# on .NET 8 (unchanged)

**Primary Dependencies**: none new; uses `IReviewService` (retrieval members already exist),
`RequestParsing`, `ErrorMapper`.

**Storage**: unchanged; read-only. Retrieval goes through the service's existing query methods over the
shared store, which return whole stored records.

**Testing**: xUnit; extend the existing `ReviewHost` fake (real composition, scripted chat client, in-memory
store) to call the new routes; also run against a host with reviewing unconfigured. The golden `openapi.json`
is updated for the two operations only. No live model.

**Target Platform / Project Type**: unchanged (ASP.NET minimal API in `AgenticChangeOps.Api`).

**Performance Goals**: none; reads only.

**Constraints**: no model call, no write, no log line needed on success; nothing from the model, sample,
endpoint, deployment name or credential in any response, log or error; reviews returned exactly as stored.

**Scale/Scope**: two routes in the existing `ReviewEndpoints` class, one error-mapper method, one `Location`
header on the existing route.

## Constitution Check

*GATE: passed before research; re-checked after design (constitution v1.0.6).*

| Principle | Status |
|---|---|
| I. Synthetic data | Pass. Fixtures stay synthetic; no new data. |
| II. Deterministic routing | Pass. Plain C# lookups; no model involved. |
| III. Human gates | Pass. Read-only; changes no status and decides nothing. The existing structure test keeps Workflow from referencing review endpoints. |
| IV. Typed auditable records | Pass. No record, enum or audit change; reviews are returned as the stored immutable records. |
| V. Spec-driven | Pass. Spec clarified, this plan precedes tasks; no code until tasks are approved. |
| VI. Test-first | Pass. Red tests shown for approval before implementation. No LLM involved. |
| VII. Simplicity | Pass. No new agent, project, class or abstraction beyond one error-mapper method. |

Technology Stack: no new technology, so **no amendment is needed. Gate: PASS before research and after design.**

## Project Structure

### Documentation (this feature)

```text
specs/020-reviewer-retrieval-operations/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── http-api.md
└── tasks.md              # /speckit-tasks
```

### Source Code (repository root)

```text
src/AgenticChangeOps.Api/
├── Endpoints/ReviewEndpoints.cs   # + GET .../reviews/latest, GET .../reviews; POST returns Created with Location
└── Errors/ErrorMapper.cs          # + NoReview()

tests/AgenticChangeOps.Api.Tests/
├── ReviewRetrievalEndpointTests.cs, ReviewRetrievalRefusalTests.cs      # NEW
├── ReviewRetrievalUnconfiguredTests.cs, ReviewRetrievalReadOnlyTests.cs # NEW
├── ReviewEndpointTests.cs         # the no-Location assertions change to the latest link (permitted)
├── Fakes/ReviewHost.cs            # + retrieval helpers
└── Baseline/golden/openapi.json   # + the two operations (reviewed update)
```

**Structure Decision**: follow `ProposalEndpoints` exactly; add the two routes to the existing
`ReviewEndpoints` class. The Reviewer project is untouched.

## Complexity Tracking

| Item | Why needed |
|---|---|
| `ErrorMapper.NoReview()` (404 `no-review`) | The spec requires a distinct "no review" outcome; mirrors `no-proposal`. Additive new code. |

None of the other items add structure.

## Constitution compliance check (pre-code)

Development Workflow step 4: spec.md, plan.md and tasks.md checked against constitution v1.0.6.

| Principle | spec.md | plan.md | tasks.md |
|---|---|---|---|
| I. Synthetic data | Pass | Pass | To confirm at /speckit-tasks |
| II. Deterministic routing | Pass | Pass | To confirm |
| III. Human gates | Pass (read-only) | Pass | To confirm |
| IV. Typed auditable records | Pass (returns stored records) | Pass | To confirm |
| V. Spec-driven | Pass | Pass | To confirm |
| VI. Test-first | Pass | Pass | To confirm |
| VII. Simplicity | Pass | Pass | To confirm |

Technology Stack: no new technology, no amendment. Commit messages reference the slice name
"020 reviewer-retrieval-operations".
