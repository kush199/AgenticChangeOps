# Contract: Record A Decision On A Design Draft

Same conventions as the other operations: camel-case JSON, string enums, shared error body
(`code`, `message`, optional `errors`, optional `category`), id parsed as a GUID.

## New operation

`POST /change-requests/{id}/designs/{version}/decisions` (`RecordDesignDecision`)

Request body (`application/json`): `{ "approverName": string, "decision": "Accept" | "Reject",
"reason": string? }`. The id and version come from the route only; any in the body are ignored. Matching of
`decision` is case-insensitive, as for the approval decision.

Success: `201 Created`, body the stored decision (`decisionId`, `changeRequestId`, `version`, `designId`,
`outcome`, `approverName`, `reason`, `decidedAt`), header `Location: /change-requests/{id}/designs/latest`.

## Errors

| Status | Code | When |
|---|---|---|
| 400 | `invalid-identifier` | id is not a valid GUID |
| 400 | `validation-failed` | version is not a whole number >= 1; approver missing or too long; reason missing for Reject or too long; decision not recognised (message lists `Accept, Reject`, never the sent value). Every failing field is listed |
| 404 | `not-found` | no such change request |
| 404 | `design-version-not-found` | the change request has no design with that version |
| 409 | `conflict` | the change request is not Approved |
| 409 | `design-outdated` | version is not the latest, or the latest is based on older requirements |
| 409 | `design-already-decided` | that version already has a decision |
| 503 | `storage-unavailable` | as for the other operations |
| 500 | `unexpected-error` | fixed text |

Order of checks: id and version token (400), body parse (400), then change request (404), status (409),
version exists (404), outdated (409), already decided (409), fields (400). Nothing is stored on any error.
Never present in any response, log or error: model text, sample content or path, endpoint, deployment
name, credential, or the caller's decision value.

## Changed responses (additive only)

`GET /change-requests/{id}/designs/latest` and `GET /change-requests/{id}/designs`: each design gains an
optional `decision` object (`outcome`, `approverName`, `reason`, `decidedAt`), present only once a decision
exists. Status codes, errors, headers and all other members are unchanged.

## Published description

Adds `RecordDesignDecision` (summary "Record an Accept or Reject decision on a design draft") and the
`DesignView` and `DesignDecision` schemas; the two design GETs reference `DesignView`. No other operation
changes.
