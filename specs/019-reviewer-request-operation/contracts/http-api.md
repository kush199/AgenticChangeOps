# Contract: HTTP Operation To Request A Review

Same conventions as the design and proposal operations: camel-case JSON, string enums, the shared error
body (`code`, `message`, optional `errors`, optional `category`), id parsed as a GUID.

| Operation | Route | Success |
|---|---|---|
| Request a review | `POST /change-requests/{id}/reviews` (no body; any body is ignored) | 201, body `Review`, **no `Location` header** |

Order of checks: identifier, existence, configuration, then the review capability.

## Errors

| Status | Code | When | Notes |
|---|---|---|---|
| 400 | `invalid-identifier` | id is not a valid GUID | first check |
| 404 | `not-found` | no such change request | before the configuration check |
| 503 | `review-not-configured` | model or review settings invalid | before any review decision; no model call |
| 409 | `conflict` | not Approved (`Status`), no requirements draft (`RequirementsDraft`), no design draft (`DesignDraft`), design not based on latest requirements (`DesignDraftVersion`), no proposal (`Proposal`), proposal not based on latest design (`ProposalVersion`), or a record changed while generating | `errors` name the rule; messages are fixed text |
| 502 | `generation-failed` | model unreachable, timed out, answer too large, answer malformed | `category` only |
| 502 | `review-rejected` | the system refused the findings (`TooManyFindings`, `TooLarge`, `MissingReference`, `UnresolvedReference`, `MalformedFinding`) | `category` is the rejection name; no finding text or reference |
| 500 | `unexpected-error` | anything unforeseen | fixed text |
| 503 | `storage-unavailable`, 409 `version-conflict` | as for designs and proposals | unchanged shapes |

Every refusal and failure stores nothing (no review, no audit entry). Never present in any error, log or
response: model text, sample file content or path, endpoint, deployment name, credential.

## Response body (`Review`)

The existing record from slice 018, unchanged (`reviewId`, `changeRequestId`, `version`,
`requirementsVersion`, `designVersion`, `proposalVersion`, `generatedAt`, `status` = `Draft`,
`agentName`, `systemCheck`, `findings`, `openQuestions`, `advisoryNote`).

## Published description

Operation id `RequestReview`, summary "Request an advisory review of the latest change proposal for an
approved change request"; responses 201, 400, 404, 409, 500, 502, 503.

## Unchanged

No existing route, status code, header, body or error code changes. Only operation- or
description-enumerating assertions in existing tests may change.
