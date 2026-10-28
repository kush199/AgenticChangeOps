# Research: HTTP Operations To Retrieve The Latest Review And The Review History

No open unknowns. Decisions, each mirroring the proposal retrieval operations:

| Topic | Decision | Rationale | Alternative rejected |
|---|---|---|---|
| Routes | `GET /change-requests/{id}/reviews/latest` and `GET /change-requests/{id}/reviews` | Same shape as `/proposals/latest` and `/proposals`; the POST already owns `/reviews`. | `/reviews/{version}`: spec asks for latest and history only. |
| Unknown change request | `ErrorMapper.ForQuery(result.Errors)` on the service result | Already maps the service's not-found and storage errors for proposals. | A separate existence check: duplicates the service. |
| No review (latest) | New `ErrorMapper.NoReview()`: 404 `no-review`, fixed text | Distinct from `not-found`, as `no-proposal` is. | Reusing `not-found`: callers could not tell the cases apart. |
| Empty history | 200 with `[]` | Spec requirement; mirrors proposals. | 404: contradicts the spec. |
| Configuration | Retrieval routes do not take `ReviewAvailability` | Spec: works whether or not reviewing is configured. | Checking it: would break that rule. |
| Link on POST | Replace `Results.Json(..., 201)` with `Results.Created("/change-requests/{id}/reviews/latest", review)` | Exactly what `ProposeAsync` does; body and status unchanged, only `Location` is added. | A `Link` header: differs from every other agent. |
| Logging | None on success or on these refusals | Proposal retrieval logs nothing; nothing sensitive reaches a log. | Per-request logs: no value, more leak surface. |
| Ordering and exactness | Rely on the service, which returns stored records in version order | The service and its tests (slice 018) already guarantee it; the HTTP tests assert it end to end. | Re-sorting in the endpoint: adds a rule. |
| Existing tests | `ReviewEndpointTests` no-`Location` assertions become a latest-link assertion; golden `openapi.json` gains the two operations | These are the permitted exceptions in the spec (successful review response, published description). | Anything else: forbidden by FR-015. |
