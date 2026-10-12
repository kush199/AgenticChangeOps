# Research: HTTP API for Change Requests

Each decision resolves a planning item deferred by the spec.

## 1. Web framework and hosting style

- **Decision**: ASP.NET Core minimal API on .NET 8, in a new project `AgenticChangeOps.Api`.
- **Rationale**: Matches the C#/.NET 8 stack; eight small routes do not need controllers; the
  in-process test host makes the whole suite network-free (FR-017).
- **Alternatives**: MVC controllers (more ceremony for no gain); a library-only "handler" layer
  with no host (cannot prove framework-generated responses are JSON, FR-003).

## 2. Machine-readable description and browsable page

- **Decision**: `Swashbuckle.AspNetCore` 6.6.2: description at `/openapi/v1.json`-style path
  chosen at implementation, browsable page at `/swagger`, both enabled in all environments
  because the service is local-only. Examples use synthetic values only.
- **Rationale**: Satisfies FR-011 with the least code; both are generated from the routes so
  they cannot drift. The description is generated from route metadata and DTOs and never
  reads configuration, so it cannot contain an endpoint or deployment name (FR-010).
- **Alternatives**: Hand-written description (drifts); no browsable page (fails US4).

## 3. Status codes (planning decision deferred by the spec)

| Outcome | Status | `code` |
|---------|--------|--------|
| Created change request | 201 + `Location` | n/a |
| Successful read / decision / clearance | 200 | n/a |
| Draft stored | 201 + `Location` (latest draft route) | n/a |
| Malformed JSON, wrong shape, oversized-field problems | 400 | `invalid-body` |
| Field validation failures | 400 | `validation-failed` |
| Identifier is not a valid identifier | 400 | `invalid-identifier` |
| Unknown change request | 404 | `not-found` |
| No draft exists yet (latest draft only) | 404 | `no-draft` |
| Unknown route | 404 | `route-not-found` |
| Unsupported method | 405 | `method-not-allowed` |
| Business rule refusal (final state, not draftable) | 409 | `conflict` |
| Body too large | 413 | `body-too-large` |
| Body is not JSON media type | 415 | `unsupported-media-type` |
| Host header not allowed | 400 | `invalid-host` |
| Generation failure (unreachable, timed out, too large, malformed) | 502 | `generation-failed` + `category` |
| Drafting not configured | 503 | `drafting-not-configured` |
| Unexpected failure | 500 | `unexpected-error` |

- **Rationale**: 400 for client input, 404 vs 400 keeps invalid-identifier distinct from
  not-found (FR-005), 409 for state refusals (FR-006), 502 for an upstream model failure so it
  is distinct from every client and conflict code (FR-007), 503 for an environment that cannot
  draft (FR-009). Every error also carries a stable machine `code`, so clients do not depend on
  status alone.
- **Alternatives**: 422 for validation (equally valid; 400 chosen to keep one client-error
  family for input problems); 424 for model failure (less widely understood than 502).

## 4. Classifying existing results (no business rules added)

The services return typed results whose errors carry a `FieldName` and message. The mapping is
one table in `ErrorMapper`, applied in this order:

1. `ChangeRequestId` error from a decision or draft call, or a null from get/history → 404
   `not-found`.
2. Generation failure category present (see decision 5) → 502 `generation-failed`.
3. Any error whose field is one of the five draft sections or `Generator` with no category
   (content invalid or incomplete) → treated as malformed output, 502 with category
   `MalformedOutput`; message text is never forwarded.
4. Any field error other than `Status` → 400 `validation-failed`, listing **all** errors
   (including a `Status` error if one was also reported), so every failing field is named.
5. Only `Status` errors → 409 `conflict`, carrying the service's specific message.

Messages from the existing validators and eligibility checks are static strings that include
only the change request's own id and status, so forwarding them is safe. Messages from the
generator path are never forwarded.

- **Rationale**: Deterministic and testable (Principle II); uses field names the services
  already return, so the API needs no knowledge of rules.
- **Alternatives**: Matching message text (brittle); changing the services to return error
  kinds (a larger change to three tested slices than this slice warrants).

## 5. Carrying the generation failure category

- **Decision**: Add a nullable `GenerationFailureCategory? FailureCategory` to `DraftingResult`
  (additive, non-breaking under constitution v1.0.1). `RequirementsDraftingService` sets it in
  the `DraftGenerationException` path and sets `MalformedOutput` when content validation of
  generated content fails. Unexpected generator exceptions (non-category) leave it null and
  surface as the generic 500, so internals never leak.
- **Rationale**: FR-007 needs the category without parsing text; the type already exists.
- **Alternatives**: Parse the existing message (brittle); a new result type (breaks callers).

## 6. Optional drafting configuration

- **Decision**: At startup the composition root calls the slice 004 environment factory. On
  success it registers that generator. On `GeneratorConfigurationException` it records
  "not configured" in a `DraftingAvailability` singleton and registers an inert generator.
  The draft route checks availability after the identifier is parsed and returns 503 when not
  configured. Retrieval routes do not depend on it. Only the *setting name* may be logged,
  never a value, and only the fact "drafting not configured" appears in responses.
- **Rationale**: Preserves slice 004's fail-fast rule for the generator itself while meeting
  FR-009 for the API. The 503 check sits before the service so the inert generator is never
  reached.
- **Alternatives**: Fail startup (violates FR-009); report the setting name to the caller
  (spec requires a response naming no value; a name is not a value but adds no value to a
  caller and reveals deployment shape).

## 7. Request body parsing and enum handling

- **Decision**: Transport DTOs use strings for enum fields. The API parses them
  case-insensitively with defined-value checks; an unparseable value becomes an out-of-range
  enum value, which the existing validators already report by field name. Missing strings pass
  as null/empty and are reported by the existing "required" rules. Malformed JSON or a wrong
  top-level shape yields 400 `invalid-body`.
- **Rationale**: All validation messages come from slices 001 and 002, so no rule is
  duplicated, and every failing field is named in a single response (FR-004).
- **Alternatives**: Let the framework fail enum binding (names no field, short-circuits other
  errors); duplicate validators in the API (adds rules, violates FR-002).

## 8. Identifier parsing

- **Decision**: Route values are parsed as GUIDs by the handler. A value that does not parse is
  400 `invalid-identifier`; a parsed but unknown id is 404. For the decision route, the
  identifier comes from the path and any identifier in the body is ignored.
- **Rationale**: Distinguishes the two cases required by FR-005 before any service call.
- **Alternatives**: Route constraints (framework returns an empty 404, which is not
  distinguishable and not JSON).

## 9. Every response is JSON

- **Decision**: A small middleware catches unhandled exceptions, malformed bodies, 404 and 405
  from routing, and body-size rejection, and writes the standard error body. The host's
  developer exception page is never enabled. Enums serialise as strings.
- **Rationale**: FR-003 and FR-008; one place to guarantee no stack trace or message leaks.
- **Alternatives**: Per-handler try/catch (inconsistent).

## 10. Logging

- **Decision**: Default framework request logging at Warning and above; the middleware logs
  only the route template, the status, an outcome code, and for unexpected failures the
  exception *type name*. Request and response bodies, exception messages, prompts, and
  configuration values are never logged.
- **Rationale**: FR-010; exception messages from real clients can embed endpoints.
- **Alternatives**: Full request logging (leaks change request text and, via errors, config).

## 11. Local-only listening

- **Decision**: Unless the operator supplies `ASPNETCORE_URLS` (or `--urls`), the host binds
  `http://127.0.0.1:5080` only. Exposing it more widely therefore needs an explicit setting.
  `ChangeOps:AllowedHosts` (default `localhost;127.0.0.1;[::1]`; the framework `AllowedHosts` is `*`
  so the API can answer refusals in JSON). No CORS services are registered.
- **Rationale**: FR-012 and FR-014.
- **Alternatives**: Wildcard bind (rejected); HTTPS (no secret or certificate to manage in a
  local-only tool; revisit when authentication is added).

## 12. Body size limit

- **Decision**: 64 KB maximum request body, enforced by the host; larger returns 413
  `body-too-large`. Field-length limits stay with the existing validators.
- **Rationale**: FR-016; the largest valid body is a few kilobytes.
- **Alternatives**: No limit (violates FR-016).

## 13. Test strategy

- **Decision**: `WebApplicationFactory<Program>` with a replaceable generator registration and
  a replaceable drafting-availability. Fakes: a generator returning fixed synthetic content,
  one throwing each failure category, one throwing an arbitrary exception. A "no leak" test
  seeds a sentinel string as endpoint/deployment value in the environment lookup and scans
  every response body, the published description, and captured log output for it.
- **Rationale**: Covers FR-017, SC-004, SC-005 with no network.
- **Alternatives**: Real sockets (slower, flaky, needs ports).
