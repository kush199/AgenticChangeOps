# Contract: Workflow Status Offered Step

No new route. `GET /change-requests/{id}/workflow` (and the `status` inside the advance responses) keeps
every existing field, status code, header and error code.

## Changed

`offeredStep` (optional, omitted when null) may now also be `"Review"`.

| Situation | `offeredStep` |
|---|---|
| `DesignComplete`, no proposal on the latest design, proposals available | `"Proposal"` (as today) |
| `DesignComplete`, no proposal on the latest design, proposals not available | omitted (as today) |
| `DesignComplete`, current proposal, no review of it, reviewing configured | `"Review"` |
| `DesignComplete`, current proposal, no review of it, reviewing not configured | omitted |
| `DesignComplete`, current proposal with a review of it | omitted |
| any other stage | omitted (as today) |

The replaced case: a `DesignComplete` request with a current proposal previously showed `"Proposal"`.

## Advance

`POST /change-requests/{id}/workflow/advance` on a request whose only offer is the review returns the
existing "nothing due" outcome and changes nothing. Automatic advancement is unchanged.

## Published description

The golden `openapi.json` lists `Review` among the enum values of the step schema. Nothing else changes.

## Never present

Model text, sample content, endpoint, deployment name, credential, in any response, log or error.
