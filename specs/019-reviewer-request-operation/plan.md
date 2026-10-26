# Implementation Plan: HTTP Operation To Request A Review

**Branch**: `feature/reviewer-request-operation` | **Date**: 2026-10-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/019-reviewer-request-operation/spec.md`

## Summary

Expose the existing `IReviewService.ReviewAsync` as one HTTP operation, `POST /change-requests/{id}/reviews`
with no body, in the same shape as the proposal operation. The endpoint checks, in order: identifier
(400 `invalid-identifier`), existence (404 `not-found`), review availability (503 `review-not-configured`),
then calls the service. A success is 201 with the stored `Review` and no `Location` header. Service refusals
map to the existing `conflict` code (409, `errors` name the rule), model failures to `generation-failed`
(502, category only), and rejected findings to a new `review-rejected` (502, category only). Storage and
version-conflict errors already flow through the shared middleware. The only composition change is that the
review setup records whether it is configured, so the endpoint can tell "not configured" before any review
decision (today an unconfigured server only fails inside the generator). One endpoint class, one error-mapper
method, one availability class, the published description gains one operation. No service, record, store,
workflow or other-agent change.

## Technical Context

**Language/Version**: C# on .NET 8 (unchanged)

**Primary Dependencies**: none new; uses `IReviewService`, `IChangeRequestService`, `RequestParsing`, `ErrorMapper`.

**Storage**: unchanged. Versioning and the locked store step already belong to the shared drafting engine
(slice 017), which gives gapless, duplicate-free versions under overlap; the HTTP layer adds no lock.

**Testing**: xUnit; extend the existing `ReviewHost` fake (real composition, scripted plain chat client) to
call the route; the golden `openapi.json` baseline is updated for the new operation only. No live model.

**Target Platform / Project Type**: unchanged (ASP.NET minimal API in `AgenticChangeOps.Api`).

**Performance Goals**: none beyond the existing model time limit.

**Constraints**: nothing from the model, sample, endpoint, deployment name or credential in any response, log
or error; one log line per request that stores nothing, naming only a rejection reason, failure category or
rule field names (as proposals do); no status change; no retrieval link.

**Scale/Scope**: one route, one availability class, one endpoint class, three error-mapper additions.

## Constitution Check

*GATE: passed before research; re-checked after design (constitution v1.0.6).*

| Principle | Status |
|---|---|
| I. Synthetic data | Pass. Fixtures stay synthetic; no new data. |
| II. Deterministic routing | Pass. The check order and the status mapping are plain C#; the model supplies finding text only. |
| III. Human gates | Pass. A review stays non-consequential content; the operation changes no status and nothing reads it. A structure test keeps Workflow from referencing the new endpoint or service. |
| IV. Typed auditable records | Pass. No record, enum or audit change; the existing `ReviewGenerated` entry is written by the service. |
| V. Spec-driven | Pass. Spec clarified, this plan precedes tasks; no code until tasks are approved. |
| VI. Test-first | Pass. Red tests are shown for approval before implementation; the model stays behind `IReviewGenerator`. |
| VII. Simplicity | Pass. No new agent, project or shared abstraction; `ReviewAvailability` mirrors the two existing availability classes (a third caller, not speculative). |

Technology Stack: no new technology, so **no amendment is needed. Gate: PASS before research and after design.**

## Project Structure

### Documentation (this feature)

```text
specs/019-reviewer-request-operation/
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
├── Endpoints/ReviewEndpoints.cs            # NEW: POST /change-requests/{id}/reviews (+ log line helper)
├── Composition/ReviewAvailability.cs       # NEW: records once whether reviews are configured
├── Composition/ApiServiceRegistration.cs   # ReviewSetup gains IsConfigured; register ReviewAvailability
├── Errors/ErrorMapper.cs                   # + ForReviewing, ReviewNotConfigured
└── Program.cs                              # + ReviewEndpoints.Map(app)

tests/AgenticChangeOps.Api.Tests/
├── ReviewEndpointTests.cs, ReviewRefusalEndpointTests.cs, ReviewFailureEndpointTests.cs   # NEW
├── ReviewCheckOrderTests.cs, ReviewDescriptionTests.cs, ReviewEndpointStructureTests.cs   # NEW
├── ReviewLoggingTests.cs, ReviewConcurrencyEndpointTests.cs                                # NEW
├── Fakes/ReviewHost.cs                     # + client/route helper
└── Baseline/golden/openapi.json            # + the one operation (reviewed update)
```

**Structure Decision**: follow `ProposalEndpoints` exactly. The Reviewer project is untouched and does not
learn about HTTP. The Api project already references it.

## Complexity Tracking

| Item | Why needed |
|---|---|
| `ReviewAvailability` (third availability class) | The spec requires a distinct "not configured" condition before any review decision; today an unconfigured Reviewer only throws inside the generator, which would surface as `generation-failed`. |
| New code `review-rejected` and `review-not-configured` | Same pattern as `proposal-rejected` / `proposal-not-configured`; additive. |

## Constitution compliance check (pre-code)

Development Workflow step 4: spec.md, plan.md and tasks.md checked against constitution v1.0.6.

| Principle | spec.md | plan.md | tasks.md |
|---|---|---|---|
| I. Synthetic data | Pass (all sample data synthetic) | Pass | Pass (T-rules: synthetic only, no credentials) |
| II. Deterministic routing | Pass (check order fixed in the spec) | Pass (mapping is plain C#) | Pass (T013, T014 hold all decisions; model supplies text only) |
| III. Human gates | Pass (never changes status, approves, rejects, defers or clears) | Pass (review stays non-consequential) | Pass (T016 keeps Workflow from referencing it) |
| IV. Typed auditable records | Pass (returns the existing `Review`) | Pass (no record change) | Pass (T005 checks the one audit entry) |
| V. Spec-driven | Pass | Pass | Pass (T001 gate before any code) |
| VI. Test-first | Pass | Pass | Pass (T011 gate, red run saved) |
| VII. Simplicity | Pass | Pass (third availability class, logged above) | Pass |

Technology Stack: no new technology, no amendment. Commit messages reference the slice name
"019 reviewer-request-operation". Tick at T001: [x] compliance check confirmed by the user.
