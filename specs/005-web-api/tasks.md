# Tasks: HTTP API for Change Requests

**Input**: Design documents from `specs/005-web-api/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/ ✅

**Tests**: Included — Constitution Principle VI (Test-First) mandates tests written and
approved before implementation for all deterministic logic. Within each story, test tasks come
first, MUST be red (failing for the right reason, not a compile error), and end with a
human-approval CHECKPOINT before any implementation task of that story starts. One batched
approval across several stories is acceptable if the user gives it. No test may call a real
model, open a network socket, or read real credentials; drafting always uses a fake generator.

**Organization**: Tasks are grouped by user story. This slice adds one production project
(`src/AgenticChangeOps.Api`), one test project (`tests/AgenticChangeOps.Api.Tests`), and one
small additive change to the core library (`DraftingResult.FailureCategory` and the lines that
set it).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks). Tasks
  that edit the same file are never marked `[P]`.
- **[Story]**: Which user story this task belongs to (US1–US4)

---

## Phase 1: Setup

**Purpose**: Confirm the baseline and create the two new projects.

- [X] T001 Run `dotnet test AgenticChangeOps.sln` and confirm all slice 001 to 004 tests pass; record the passing count as the regression baseline
- [X] T002 Create web project src/AgenticChangeOps.Api/AgenticChangeOps.Api.csproj with `Microsoft.NET.Sdk.Web`, target net8.0, ImplicitUsings and Nullable enabled, project references to src/AgenticChangeOps.Intake/AgenticChangeOps.Intake.csproj and src/AgenticChangeOps.Intake.AzureOpenAI/AgenticChangeOps.Intake.AzureOpenAI.csproj, and package reference `Swashbuckle.AspNetCore` version 6.6.2; add the project to AgenticChangeOps.sln under the `src` folder
- [X] T003 Create test project tests/AgenticChangeOps.Api.Tests/AgenticChangeOps.Api.Tests.csproj targeting net8.0 (same xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, coverlet.collector versions as tests/AgenticChangeOps.Intake.Tests, `<Using Include="Xunit" />`), package reference `Microsoft.AspNetCore.Mvc.Testing` version 8.0.0, and project references to the Api project and the Intake project; add it to AgenticChangeOps.sln under the `tests` folder
- [X] T004 Run `dotnet build AgenticChangeOps.sln` and confirm 0 errors, 0 warnings

**Checkpoint**: Solution builds with both new projects empty.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared types, compile-only skeletons and fakes so story tests can be written and
fail for the right reason. MUST be complete before any user story work begins.

- [X] T005 Add the additive member to src/AgenticChangeOps.Intake/Models/DraftingResult.cs: `public GenerationFailureCategory? FailureCategory { get; }` defaulting to null, plus an overload `static DraftingResult Failure(IReadOnlyList<ValidationError> errors, GenerationFailureCategory category)`; the existing `Failure(errors)` and `Success` behave exactly as before; do not change RequirementsDraftingService yet
- [X] T006 [P] Create ErrorResponse in src/AgenticChangeOps.Api/Contracts/ErrorResponse.cs: sealed record `(string Code, string Message, IReadOnlyList<ErrorField>? Errors = null, string? Category = null)` and sealed record `ErrorField(string Field, string Message)`; `Category` is "only for `generation-failed`; one of `Unreachable`, `TimedOut`, `ResponseTooLarge`, `MalformedOutput`"; `Errors` is only for `validation-failed` and `conflict`
- [X] T007 [P] Create request/response DTOs in src/AgenticChangeOps.Api/Contracts/: SubmitChangeRequestBody.cs with string? `Title`, `Description`, `Requester`, `AffectedSystem`, `Priority`, `ChangeType`; DecisionBody.cs with string? `ApproverName`, `Decision`, `Reason`; ClearanceResponse.cs `(Guid ChangeRequestId, bool IsCleared)`. Property names camelCase on the wire, matched case-insensitively; "Unknown extra properties are ignored"
- [X] T008 [P] Create ErrorMapper skeleton in src/AgenticChangeOps.Api/Errors/ErrorMapper.cs: static class with methods for submission, decision, draft and query results that each return `(int StatusCode, ErrorResponse Body)` and throw NotImplementedException
- [X] T009 [P] Create DraftingAvailability in src/AgenticChangeOps.Api/Composition/DraftingAvailability.cs: sealed class with `bool IsConfigured` set only through its constructor; it holds no setting value
- [X] T010 Create skeleton host files that compile and do nothing yet: src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs (extension method `AddChangeOpsApi(IServiceCollection)`; model settings are read lazily from `IConfiguration`), src/AgenticChangeOps.Api/Errors/JsonErrorMiddleware.cs, endpoint mapping classes ChangeRequestEndpoints.cs, DecisionEndpoints.cs and DraftEndpoints.cs in src/AgenticChangeOps.Api/Endpoints/ each with `static void Map(IEndpointRouteBuilder)` that maps no routes, and src/AgenticChangeOps.Api/Program.cs that builds and runs a host and ends with `public partial class Program { }`
- [X] T011 [P] Create test fakes in tests/AgenticChangeOps.Api.Tests/Fakes/: FakeDraftGenerator.cs implementing `IRequirementsDraftGenerator` with `ReturnsSyntheticContent()` (fictional "Sample Catalogue" content), `ThrowsCategory(GenerationFailureCategory)` and `ThrowsArbitrary(Exception)` plus `CallCount`; ApiFactory.cs deriving from `WebApplicationFactory<Program>` that replaces the generator and `DraftingAvailability` and captures log output into a list; SyntheticData.cs with a valid SubmitChangeRequestBody and a valid DecisionBody (all fictional)
- [X] T012 Verify `dotnet build AgenticChangeOps.sln` has 0 errors and 0 warnings, and `dotnet test` still shows the T001 baseline passing

**Checkpoint**: Everything compiles; new behaviour is unimplemented; earlier slices green.

---

## Phase 3: User Story 1 - Submit and Retrieve a Change Request (Priority: P1) 🎯 MVP

**Goal**: Submit, retrieve by identifier, and list audit history over HTTP with the intake
capability's exact behaviour and the specified error shapes.

**Independent Test**: With no model configuration, submit a synthetic change request, retrieve
it and list its audit history; confirm content matches the intake service and that each error
case returns its specified response.

### Tests for User Story 1 (write first; must fail for the right reason)

- [X] T013 [P] [US1] Write tests/AgenticChangeOps.Api.Tests/IntakeEndpointTests.cs covering: POST /change-requests with a valid body returns 201, JSON, a `Location` of `/change-requests/{id}`, and a body equal in content to `IChangeRequestService.GetById`; GET /change-requests/{id} returns 200 with the same request; GET /change-requests/{id}/audit returns 200 with entries in recorded order (Submitted first); an empty title plus `priority` "Urgent" returns 400 `validation-failed` whose `errors` names both `Title` and `Priority` and stores nothing; body `{` returns 400 `invalid-body`; an empty body returns 400 `invalid-body`; GET with `not-a-guid` returns 400 `invalid-identifier` on both routes; GET with an unknown well-formed GUID returns 404 `not-found` on both routes
- [X] T014 [P] [US1] Write tests/AgenticChangeOps.Api.Tests/ErrorMapperTests.cs for the submission and query rules: validation errors map to 400 `validation-failed` listing every error in order; a `ChangeRequestId` error maps to 404 `not-found`; each ErrorResponse serialises without null members (`errors` and `category` omitted when null)
- [X] T015 [P] [US1] Write tests/AgenticChangeOps.Api.Tests/EnumParsingTests.cs: priority and changeType strings parse case-insensitively ("high" is accepted); an unparseable or numeric-out-of-range value is reported by the existing slice 001 validation message for that field and never throws; a missing property is reported as "required"

**CHECKPOINT (human approval)**: Show the red US1 tests and obtain explicit approval before T016.

### Implementation for User Story 1

- [X] T016 [US1] Implement the submission and query rules of src/AgenticChangeOps.Api/Errors/ErrorMapper.cs per research decision 4, rules 1 and 4, returning fixed message text per code
- [X] T017 [US1] Implement src/AgenticChangeOps.Api/Errors/JsonErrorMiddleware.cs for malformed or empty bodies (400 `invalid-body`) and unhandled exceptions (500 `unexpected-error`, fixed message, logs the exception type name only)
- [X] T018 [US1] Implement src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs: singleton `InMemoryChangeRequestStore`, `SystemClock`, `GuidIdGenerator`, `ChangeRequestValidator`, `IChangeRequestService`; JSON options with camelCase names, string enums and case-insensitive property matching
- [X] T019 [US1] Implement src/AgenticChangeOps.Api/Endpoints/ChangeRequestEndpoints.cs: POST /change-requests, GET /change-requests/{id}, GET /change-requests/{id}/audit; parse `{id}` as a GUID before any service call; convert enum strings so undefined values reach the existing validator; call only `IChangeRequestService`
- [X] T020 [US1] Wire Program.cs to call `AddChangeOpsApi`, use the middleware, and map ChangeRequestEndpoints; run the US1 tests and confirm they pass

**Checkpoint**: Intake works over HTTP with no model configuration.

---

## Phase 4: User Story 2 - Record a Decision and Check Clearance (Priority: P2)

**Goal**: Record Approve, Reject or Defer decisions and check clearance with the approval
gate's exact behaviour.

**Independent Test**: Submit, check clearance (not cleared), approve, check clearance
(cleared), then send a second decision and confirm a 409 with the specific reason.

### Tests for User Story 2

- [X] T021 [P] [US2] Write tests/AgenticChangeOps.Api.Tests/DecisionEndpointTests.cs covering: a valid Approve returns 200 with status `Approved` and one new `Approved` audit entry; Defer without a reason returns 400 `validation-failed` naming `Reason` and leaves the request unchanged; a decision value of "Maybe" returns 400 naming `Decision`; Reject on an already Approved request without a reason returns 400 listing both `Reason` and `Status`; Approve on an already Approved request returns 409 `conflict` whose `errors` carries the service's final-state message; unknown id returns 404 `not-found`; `not-a-guid` returns 400 `invalid-identifier`; an identifier in the body is ignored in favour of the route
- [X] T022 [P] [US2] Write tests/AgenticChangeOps.Api.Tests/ClearanceEndpointTests.cs: a Submitted request returns `isCleared` false; after Approve returns true; after Reject returns false; the value equals `IApprovalService.IsClearedToProceed` for each; unknown id returns 404; invalid id returns 400 `invalid-identifier`
- [X] T023 [P] [US2] Extend tests/AgenticChangeOps.Api.Tests/ErrorMapperTests.cs with the decision rules: only `Status` errors map to 409 `conflict` carrying the message; any other field error alongside `Status` maps to 400 listing all; `ChangeRequestId` error maps to 404 and takes precedence

**CHECKPOINT (human approval)**: Show the red US2 tests and obtain explicit approval before T024.

### Implementation for User Story 2

- [X] T024 [US2] Implement the decision rules in src/AgenticChangeOps.Api/Errors/ErrorMapper.cs per research decision 4, rules 1, 4 and 5
- [X] T025 [US2] Register `ApprovalDecisionValidator` and `IApprovalService` in src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs, reusing the same store singleton
- [X] T026 [US2] Implement src/AgenticChangeOps.Api/Endpoints/DecisionEndpoints.cs: POST /change-requests/{id}/decisions (200 with the updated change request) and GET /change-requests/{id}/clearance (200 `ClearanceResponse`); identifier from the route only; call only `IApprovalService`
- [X] T027 [US2] Map DecisionEndpoints in Program.cs; run the US1 and US2 tests and confirm they pass

**Checkpoint**: The approval gate works over HTTP.

---

## Phase 5: User Story 3 - Request and Retrieve Requirements Drafts (Priority: P3)

**Goal**: Request drafts and retrieve latest and history with the drafting capability's exact
behaviour, using the real generator when configured and a clear 503 when not.

**Independent Test**: With a fake generator, submit, draft twice, retrieve latest and history,
then confirm every refusal and failure case.

### Tests for User Story 3

- [X] T028 [P] [US3] Write tests/AgenticChangeOps.Intake.Tests/Unit/DraftingFailureCategoryTests.cs for the additive member: a generator throwing `DraftGenerationException(TimedOut)` yields a failed result with `FailureCategory` `TimedOut` and nothing stored; invalid generated content (blank summary) yields `FailureCategory` `MalformedOutput` and nothing stored; a generator throwing an arbitrary exception yields `FailureCategory` null with the existing `Generator` error; an eligibility failure and a success both have null `FailureCategory`; the existing error text is unchanged
- [X] T029 [P] [US3] Write tests/AgenticChangeOps.Api.Tests/DraftEndpointTests.cs covering: POST /change-requests/{id}/drafts returns 201, a `Location` of `/change-requests/{id}/drafts/latest`, version 1, and one new `DraftGenerated` audit entry; a second POST returns version 2; GET .../drafts/latest returns the highest version; GET .../drafts returns all versions ascending; a request with no drafts returns 404 `no-draft` on latest and 200 `[]` on history; an unknown id returns 404 `not-found` on all three routes; `not-a-guid` returns 400 `invalid-identifier`; drafting an Approved request returns 409 `conflict` with the service's specific reason and the generator was not called
- [X] T030 [P] [US3] Write tests/AgenticChangeOps.Api.Tests/DraftFailureTests.cs covering: for each of `Unreachable`, `TimedOut`, `ResponseTooLarge`, `MalformedOutput` the POST returns 502 `generation-failed` with `category` equal to that name, no `errors`, and the response body contains no other text; no draft, version or audit entry exists afterwards; a generator throwing an arbitrary exception whose message holds a sentinel string returns 500 `unexpected-error` and the sentinel appears in neither the response nor captured logs; blank generated content returns 502 with category `MalformedOutput`
- [X] T031 [P] [US3] Write tests/AgenticChangeOps.Api.Tests/NotConfiguredTests.cs covering: with `DraftingAvailability.IsConfigured` false, POST drafts returns 503 `drafting-not-configured` with a fixed message and the generator is never called; submit, get, audit, decision, clearance, latest and history all still work; an invalid id on the draft route still returns 400 before the 503; startup with the model variables missing, blank and placeholder each leaves drafting not configured and does not throw; startup with valid synthetic variables marks drafting configured without making any network call
- [X] T032 [P] [US3] Extend tests/AgenticChangeOps.Api.Tests/ErrorMapperTests.cs with the draft rules: a result with `FailureCategory` maps to 502 with that category; section-field errors without a category map to 502 `MalformedOutput`; `Status` errors map to 409; `Generator` errors without a category map to 500

**CHECKPOINT (human approval)**: Show the red US3 tests and obtain explicit approval before T033.

### Implementation for User Story 3

- [X] T033 [US3] Update src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs to set `FailureCategory`: pass `ex.Category` in the `DraftGenerationException` catch and `MalformedOutput` when content validation fails; leave the arbitrary-exception path and eligibility path with null; keep every existing message and behaviour; run slices 001 to 004 tests and confirm still green
- [X] T034 [US3] Implement the draft rules in src/AgenticChangeOps.Api/Errors/ErrorMapper.cs per research decision 4, rules 2, 3 and 5 plus the `no-draft` case
- [X] T035 [US3] Implement src/AgenticChangeOps.Api/Composition/ApiServiceRegistration.cs drafting registration per research decision 6: build `AzureOpenAIGeneratorOptions.FromEnvironment` from `IConfiguration` and construct the generator, on `GeneratorConfigurationException` record `DraftingAvailability(false)` and register an inert generator that throws `DraftGenerationException(Unreachable)`; register `DraftContentValidator` and `IRequirementsDraftingService`; log only the fact "drafting not configured", never a setting value
- [X] T036 [US3] Implement src/AgenticChangeOps.Api/Endpoints/DraftEndpoints.cs: POST /change-requests/{id}/drafts (identifier check → availability check → `DraftAsync` with the request's cancellation token), GET .../drafts/latest, GET .../drafts; call only `IRequirementsDraftingService`
- [X] T037 [US3] Map DraftEndpoints in Program.cs; run all tests and confirm they pass

**Checkpoint**: Drafting works over HTTP with or without model configuration.

---

## Phase 6: User Story 4 - Safe, Self-Describing, Local-Only Service (Priority: P4)

**Goal**: JSON everywhere, no leaks, a published description and browsable page, and
local-only listening by default.

**Independent Test**: Fetch the description and confirm seven operations and no configuration
value; drive every failure class and scan all output for sentinels.

### Tests for User Story 4

- [X] T038 [P] [US4] Write tests/AgenticChangeOps.Api.Tests/ErrorShapeTests.cs covering: an unknown route returns 404 `route-not-found` as JSON; PUT on /change-requests returns 405 `method-not-allowed` as JSON; a POST body over 64 KB returns 413 `body-too-large` as JSON; a POST with `Content-Type: text/plain` returns a 4xx JSON error; every response in the whole suite has a JSON content type (assert in the factory's response helper); no response body contains "Exception", "StackTrace", " at ", or a file path
- [X] T039 [P] [US4] Write tests/AgenticChangeOps.Api.Tests/NoLeakTests.cs: configure the factory's environment lookup with sentinel endpoint `https://sentinel-endpoint.invalid/` and deployment `sentinel-deployment`; drive all eight routes including every failure class; assert the sentinels, any key-shaped string, and any generator-supplied sentinel text appear in no response body, no response header, the published description, or captured logs
- [X] T040 [P] [US4] Write tests/AgenticChangeOps.Api.Tests/DescriptionTests.cs: the machine-readable description is served, parses as JSON, lists exactly the eight routes and their methods (seven operations), documents the error codes from contracts/http-api.md, uses only synthetic example values, and contains neither sentinel; GET /swagger returns the browsable page
- [X] T041 [P] [US4] Write tests/AgenticChangeOps.Api.Tests/ListenAddressTests.cs: `ListenAddress.Resolve(getVariable)` returns only `http://127.0.0.1:5080` when `ASPNETCORE_URLS` is unset or blank, and returns the operator's value unchanged when set; no response carries an `Access-Control-Allow-Origin` header and an `OPTIONS` preflight is not granted; `AllowedHosts` rejects a foreign `Host` header with a JSON 400

**CHECKPOINT (human approval)**: Show the red US4 tests and obtain explicit approval before T042.

### Implementation for User Story 4

- [X] T042 [US4] Extend src/AgenticChangeOps.Api/Errors/JsonErrorMiddleware.cs to write JSON for routing 404, 405, body-too-large (413) and unsupported media type; never enable the developer exception page; log only route template, status and outcome code
- [X] T043 [US4] Create src/AgenticChangeOps.Api/Composition/ListenAddress.cs with `Resolve(Func<string,string?>)` per research decision 11, and use it in Program.cs; set `ChangeOps:AllowedHosts` to `localhost;127.0.0.1;[::1]` in src/AgenticChangeOps.Api/appsettings.json (framework `AllowedHosts` is `*`); register no CORS services; set the 64 KB request body limit
- [X] T044 [US4] Add Swashbuckle to Program.cs: description at `/openapi/v1.json`, browsable page at `/swagger`; add route metadata (names, summaries, produced status codes and `ErrorResponse` types) to the three endpoint files; examples use only the synthetic values in contracts/http-api.md; the description generator reads no configuration
- [X] T045 [US4] Run all tests and confirm they pass

**Checkpoint**: All four stories complete.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T046 [P] Extend tests/AgenticChangeOps.Intake.Tests/Unit/NoSecretsTests.cs (or add tests/AgenticChangeOps.Api.Tests/NoSecretsTests.cs) so the repository scan also covers src/AgenticChangeOps.Api, tests/AgenticChangeOps.Api.Tests and specs/005-web-api for keys, tokens, real endpoints and deployment names
- [X] T047 [P] Add src/AgenticChangeOps.Api/Properties/launchSettings.json (or equivalent) that sets only the loopback address and no secret; confirm `.gitignore` already excludes bin/obj
- [X] T048 Run the full suite with no environment variables set and no network: `dotnet test AgenticChangeOps.sln`; confirm baseline plus new tests pass, zero real model calls, live check still skipped
- [X] T049 Manual smoke per specs/005-web-api/quickstart.md steps 2 and 3 with no model configuration: start with `dotnet run --project src/AgenticChangeOps.Api`, confirm loopback only, `/swagger` opens, each operation works, and the draft route returns 503 `drafting-not-configured`
- [X] T050 Constitution compliance check against v1.0.1 for spec, plan, tasks and the code diff; confirm the additive `FailureCategory` is recorded in plan.md Complexity Tracking and that Semantic Kernel is explicitly still deferred

---

## Dependencies & Execution Order

- Phase 1 → Phase 2 → stories. Phase 2 blocks everything.
- US1 is the MVP. US2 and US3 depend only on Phase 2 and US1's registration (T018) and middleware (T017); they can follow US1 in either order. US4 depends on all route files existing (T019, T026, T036) because it documents and guards them.
- Within a story: tests → approval checkpoint → ErrorMapper rules → registration → endpoints → wiring.
- T033 touches the core library and MUST keep the baseline green before any API work builds on it.

## Parallel Opportunities

- Phase 2: T006, T007, T008, T009 and T011 touch different files.
- Each story's test tasks (marked [P]) are different files and can be written together.
- Polish T046 and T047.

## Implementation Strategy

1. **MVP**: Phases 1, 2 and 3 deliver intake and retrieval over HTTP.
2. Add US2 for the approval gate, then US3 for drafting, then US4 for the safety and
   self-description guarantees.
3. After each story, run the full suite; stop at any red baseline test.
4. The live model path is never exercised by automated tests; check it by hand using
   quickstart.md step 5.

---

## Phase 8: Convergence

**Purpose**: Remaining work found by `/speckit-converge` on 2026-10-12. Test tasks come first
and need human approval (red) before the fix tasks that follow them.

- [X] T051 HIGH: Write failing tests in tests/AgenticChangeOps.Api.Tests/ConcurrentDecisionTests.cs: send 20 simultaneous Approve decisions over HTTP for one Submitted change request (release them together with a shared gate) and repeat for 10 fresh requests; assert exactly one 200, all others 409 `conflict`, and exactly one `Approved` audit entry per request, per Edge Cases "Two requests arrive at once" and FR-006 (missing)
- [X] T052 HIGH: Make decision recording atomic in src/AgenticChangeOps.Intake/Services/ApprovalService.cs so the validate, status update and audit entry save form one uninterrupted step (a `lock` shared with the store's guard, mirroring the existing `_gate` in src/AgenticChangeOps.Intake/Services/RequirementsDraftingService.cs); no change to messages or results, so slice 002 tests stay green, per FR-002 and Spec Assumptions "Concurrent requests are supported" (partial)
- [X] T053 MEDIUM: Write failing tests in tests/AgenticChangeOps.Intake.Tests/Unit/StoreConcurrencyTests.cs that run parallel Submit, GetById, GetAuditHistory and DraftAsync calls against one shared store and assert no exception is thrown and every submitted request is retrievable with a consistent audit history, then make src/AgenticChangeOps.Intake/Stores/InMemoryChangeRequestStore.cs safe for concurrent use (one private lock around every dictionary access), per Spec Assumptions "Concurrent requests are supported" and plan: Constraints (partial)
- [X] T054 LOW: Log the route template, response status and outcome `code` for every error response in src/AgenticChangeOps.Api/Errors/JsonErrorMiddleware.cs (never bodies, headers, exception messages or configuration values), with a test in tests/AgenticChangeOps.Api.Tests/NoLeakTests.cs that these lines appear for a 404 and contain none of the sentinels, per plan: research decision 10 (partial)
- [X] T055 LOW: Strengthen tests/AgenticChangeOps.Api.Tests/ErrorShapeTests.cs `NonJsonBody_ReturnsAClientErrorAsJson` to assert status 415 and code `unsupported-media-type`, so every error class in contracts/http-api.md is asserted by name, per FR-017 (partial)

---

## Phase 9: Convergence

**Purpose**: Remaining work found by the second `/speckit-converge` pass on 2026-10-12.

- [X] T056 LOW: Remove the `ASPNETCORE_ENVIRONMENT` entry (the Development environment) from src/AgenticChangeOps.Api/Properties/launchSettings.json so the launch profile cannot enable the framework developer exception page, and add a test in tests/AgenticChangeOps.Api.Tests/NoSecretsTests.cs that no `launchSettings.json` under src/AgenticChangeOps.Api sets the environment to `Development`, per plan: research decision 9 "the developer exception page is never enabled" (partial)
- [X] T057 LOW: Update the "Exposing beyond the local machine" section of specs/005-web-api/quickstart.md to say that besides `ASPNETCORE_URLS` the operator must also set `ChangeOps__AllowedHosts` (or `*`) because the default host list refuses non-local `Host` headers with `invalid-host`, per FR-012 and contracts/http-api.md error mapping (partial)
