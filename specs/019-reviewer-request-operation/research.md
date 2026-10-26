# Research: HTTP Operation To Request A Review

No NEEDS CLARIFICATION remained. Decisions:

## Route and verb
- **Decision**: `POST /change-requests/{id}/reviews`, no body, 201.
- **Rationale**: mirrors `POST .../proposals` and `.../designs`. The body is not read, so a body sent is ignored.
- **Alternatives**: `.../reviews/latest` style routes belong to the retrieval slice, which is out of scope.

## No Location header
- **Decision**: return a 201 JSON result with no `Location` header (`Results.Json(review, statusCode: 201)`).
- **Rationale**: spec forbids a retrieval link; the `latest` route does not exist yet.

## Distinct "not configured"
- **Decision**: `ReviewSetup` gets `IsConfigured` (true unless `GeneratorConfigurationException` was caught); a new `ReviewAvailability` exposes it; the endpoint checks it after the existence check and before the service call; code `review-not-configured`, 503.
- **Rationale**: the check must precede eligibility and must not call the model. Setup is built lazily on first use; resolving `ReviewAvailability` at request time builds it, with no start-up read, so servers that never use reviews are unaffected.
- **Alternatives**: detecting the failure after the fact from the generator's `Unreachable` category would be indistinguishable from a real outage and would run eligibility first. Rejected.

## Error mapping (`ForReviewing`)
- Not found: errors contain `ChangeRequestId` -> 404 `not-found`.
- Rejection set -> 502 `review-rejected`, `category` = `ReviewRejection` name, fixed message.
- Failure category set -> 502 `generation-failed`, category only, fixed message "The review could not be generated."
- Rule errors whose fields are all in {Status, RequirementsDraft, DesignDraft, DesignDraftVersion, Proposal, ProposalVersion} -> 409 `conflict` with those `errors` (fixed service messages, as for proposals).
- Anything else -> 500 `unexpected-error`.
- **Rationale**: identical shape to `ForProposing`; field names come from the service constants, so no rule is added.
- Storage unavailable and version conflict keep flowing through `JsonErrorMiddleware`, unchanged.

## Overlapping requests
- **Decision**: rely on the engine's locked store step (slice 017) and prove it with an HTTP-level concurrency test; add no lock in the endpoint.
- **Rationale**: the guarantee already exists for the service; the test shows it holds through the route.

## Published description
- **Decision**: add the operation with `Produces` for 201, 400, 404, 409, 500, 502, 503 and update the golden `openapi.json`. Operation id `RequestReview`.
- **Rationale**: the baseline test pins the document; only the added operation may differ.

## Logging
- **Decision**: one warning per request that stores nothing, `Review request stored nothing: {Outcome}.` with the rejection name, failure category or refused field names, on logger `AgenticChangeOps.Api.Reviewing`.
