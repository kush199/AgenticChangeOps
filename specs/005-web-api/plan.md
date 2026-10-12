# Implementation Plan: HTTP API for Change Requests

**Branch**: `005-web-api` (working branch: `feature/web-api`) | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-web-api/spec.md`

## Summary

Add a thin HTTP delivery layer over the existing intake, approval and drafting services. A new
project, `AgenticChangeOps.Api`, hosts an ASP.NET Core minimal API with eight routes (seven
operations; operation 7 is two retrievals). Handlers do only three things: parse the identifier
and body, call the existing service, and translate the service's typed result into a JSON
response using one deterministic mapping table (not-found, invalid identifier, validation,
conflict, generation failure, not configured, unexpected). No business rule lives in the API.

The core library gains one **additive** member: `DraftingResult` carries the generation failure
category as a typed value, so the API never parses message text. Drafting uses the slice 004
generator when its environment configuration is valid; otherwise a "not configured" state is
recorded at startup, drafting returns 503, and every other route works. A machine-readable
description and a browsable page are published by the API itself. It listens on loopback only
by default and keeps all data in the existing in-memory store.

## Technical Context

**Language/Version**: C# / .NET 8 (matches existing projects)

**Primary Dependencies**: `Microsoft.NET.Sdk.Web` (framework reference, no package) and
`Swashbuckle.AspNetCore` 6.6.2 for the description and browsable page. Existing projects are
referenced: `AgenticChangeOps.Intake`, `AgenticChangeOps.Intake.AzureOpenAI`.

**Storage**: Existing `InMemoryChangeRequestStore` as a process-lifetime singleton (FR-013)

**Testing**: xUnit in a new project `AgenticChangeOps.Api.Tests` using
`Microsoft.AspNetCore.Mvc.Testing` 8.0.0 (in-process host, no sockets, no network). Fake
generators only; no real model call.

**Target Platform**: Local .NET 8 process (Windows/Linux/macOS)

**Project Type**: Web service (single API project, no front end)

**Performance Goals**: Each non-drafting operation under 1 s locally (SC-008); drafting bounded
by the slice 004 time limit.

**Constraints**: JSON for every response including framework-generated ones; no configuration
value, prompt, model text, stack trace or path in any response, log line or the published
description; loopback-only by default; no authentication, CORS or persistence; slices 001 to
004 tests stay green.

**Scale/Scope**: Eight routes, one error-mapping component, one DI composition root, one
additive change to `DraftingResult`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — all pass.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Synthetic Data Only | ✅ Pass | Examples in the description and tests are fictional; no endpoint, deployment name or key is stored; documentation uses angle-bracket placeholders |
| II. Deterministic Routing | ✅ Pass | The API maps result types to responses with a fixed table; it makes no routing, state or eligibility decision and never reads model output |
| III. Human Approval Gates | ✅ Pass | Decisions are still recorded only by the approval service; the API adds no auto-approval and no shortcut. Drafting stays non-consequential as classified in slice 003 |
| IV. Typed Auditable Records | ✅ Pass | No persisted record changes. `DraftingResult` gains an additive nullable member (non-breaking per v1.0.1); recorded in Complexity Tracking. Transport DTOs are typed |
| V. Spec-Driven Slice by Slice | ✅ Pass | Spec clarified; tasks follow and need approval before code |
| VI. Test-First | ✅ Pass | Tests use `WebApplicationFactory` with fake generators; the SDK stays behind `IChatModelClient`; tests are written and approved before implementation |
| VII. Simplicity | ⚠️ Pass with notes | No new agent. One new web project and one new test project (the web SDK cannot share the existing class-library test project). No framework abstractions beyond one error mapper. See Complexity Tracking |

**Technology stack note**: the constitution lists the Microsoft Agent Framework / Semantic
Kernel as the agent framework. That choice was deferred in slice 004 to this web API slice.
This slice still has no orchestration of multiple agents and makes
no direct model call itself, so Semantic Kernel is **not** introduced here; the existing
`IChatModelClient` seam remains the only place a model is reached. Revisit when a second agent
or tool use is added. Needs your confirmation at the compliance check.

**Gate result: PASS — checked against constitution v1.0.1.**

## Project Structure

### Documentation (this feature)

```text
specs/005-web-api/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── http-api.md      # Phase 1 output (routes, bodies, error mapping)
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.Intake/                 # existing; one additive change
│   └── Models/DraftingResult.cs             #   + nullable FailureCategory
│   └── Services/RequirementsDraftingService.cs  # sets the category on failure paths
├── AgenticChangeOps.Intake.AzureOpenAI/     # existing; unchanged
└── AgenticChangeOps.Api/                    # NEW
    ├── AgenticChangeOps.Api.csproj
    ├── Program.cs                           # host setup, loopback default, JSON, description
    ├── Composition/
    │   ├── ApiServiceRegistration.cs        # singletons; generator-or-not-configured
    │   └── DraftingAvailability.cs          # records configured / not configured
    ├── Endpoints/
    │   ├── ChangeRequestEndpoints.cs        # submit, get, audit history
    │   ├── DecisionEndpoints.cs             # decision, clearance
    │   └── DraftEndpoints.cs                # draft, latest, history
    ├── Contracts/                           # request/response DTOs
    │   ├── SubmitChangeRequestBody.cs
    │   ├── DecisionBody.cs
    │   ├── ClearanceResponse.cs
    │   └── ErrorResponse.cs
    └── Errors/
        ├── ErrorMapper.cs                   # result -> ErrorResponse + status (one table)
        └── JsonErrorMiddleware.cs           # unexpected, malformed JSON, size, 404/405

tests/
├── AgenticChangeOps.Intake.Tests/           # existing; small additions for DraftingResult
└── AgenticChangeOps.Api.Tests/              # NEW
    ├── AgenticChangeOps.Api.Tests.csproj
    ├── Fakes/ApiFactory.cs                  # WebApplicationFactory with fake generator
    ├── Fakes/ThrowingDraftGenerator.cs
    ├── IntakeEndpointTests.cs
    ├── DecisionEndpointTests.cs
    ├── DraftEndpointTests.cs
    ├── ErrorShapeTests.cs
    ├── NotConfiguredTests.cs
    ├── NoLeakTests.cs
    └── DescriptionTests.cs
```

**Structure Decision**: Keep the existing two class libraries untouched apart from the one
additive property. Add one web project and one test project beside them, mirroring the existing
`src/` and `tests/` layout. Endpoints are grouped by capability, one file each.

## Complexity Tracking

| Item | Why Needed | Simpler Alternative Rejected Because |
|------|-----------|--------------------------------------|
| Additive nullable `FailureCategory` on `DraftingResult` (and set in the drafting service) | The API must report the failure category (FR-007) without parsing message text, which would break silently if wording changed | Parsing "The content generator failed (TimedOut)." is brittle and couples the API to a message format; v1.0.1 allows additive nullable members, so no amendment is needed |
| Second test project | Web host testing needs the web SDK's framework reference, which the class-library test project does not have | Adding it to the existing project drags the web framework into tests of pure libraries |
| New web project | Keeps web dependencies out of the core library | Putting endpoints in the core library would break its zero-dependency property |
| Swashbuckle package | FR-011 requires a machine-readable description and a browsable page; .NET 8 has no built-in generator for the page | Hand-writing the description would drift from the routes |

## Post-Implementation Compliance Check (constitution v1.0.1, 2026-10-12)

| Principle | Result | Evidence |
|-----------|--------|----------|
| I. Synthetic Data Only | ✅ | Fixtures and descriptions are fictional; repository scans (intake `NoSecretsTests`, API `NoSecretsTests`) pass; no endpoint, deployment name or key in `src`, `tests` or `specs` |
| II. Deterministic Routing | ✅ | One fixed `ErrorMapper` table; no model output reaches routing, status or clearance |
| III. Human Approval Gates | ✅ | Decisions are recorded only by `IApprovalService`; the API has no auto-approve path; drafting remains non-consequential |
| IV. Typed Auditable Records | ✅ | Only additive change: nullable `DraftingResult.FailureCategory` (recorded in Complexity Tracking); no persisted record changed |
| V. Spec-Driven Slice | ✅ | Spec, plan and tasks existed before code; tests approved per story before implementation |
| VI. Test-First | ✅ | 93 API tests and 7 new intake tests written and approved red before implementation; no real model call, network or credential |
| VII. Simplicity | ✅ | No new agent; one error mapper; no extra abstractions |

Deviations from the plan, all minor: model settings are read from `IConfiguration` instead of a
lookup function; allowed hosts use `ChangeOps:AllowedHosts` (framework `AllowedHosts` is `*`) so
refusals are JSON; `415 unsupported-media-type` was added; `appsettings.json` is loaded from
beside the binaries. Semantic Kernel remains deferred (no multi-agent orchestration yet).

Known limitation, outside this slice: the in-memory store and the intake and approval services
take no locks, so heavily overlapping requests can race. Drafting is already serialised.
