# HTTP Contract Change: POST /change-requests/{id}/proposals

Route, request, 201 body, headers and all other status codes are unchanged.

New 409 conflict cases (existing `ErrorResponse` shape and conflict code, with the existing validation
`errors` entry naming the field):

| Condition | Field | Meaning |
|---|---|---|
| Latest design has no decision | `DesignAcceptance` | Not decided yet |
| Latest design decision is Rejected | `DesignAcceptance` | Rejected |
| Latest design stops being the accepted one while preparing | `DesignDraftVersion` | Existing outdated-design refusal |

Order: 400 invalid id, 404 unknown, availability errors, then the service checks in the order given in
data-model.md. A refusal makes no model call and stores and audits nothing. Messages never echo input.
