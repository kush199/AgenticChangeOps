# Contract: Additions to Existing Responses

Every addition is a new member at the end of an existing object. No member is removed, renamed,
retyped or given a new meaning. Clients that ignore unknown members are unaffected.

## Workflow status (`GET /change-requests/{id}/workflow`, and the `status` / `workflowStatus` member of the advance response)

New member `automatic`:

```json
"automatic": { "mode": "Off" }
```

```json
"automatic": { "mode": "On" }
```

```json
"automatic": { "mode": "LeftForPerson", "failureCategory": "Timeout" }
```

| Member | Values | Notes |
|---|---|---|
| `mode` | `Off`, `On`, `LeftForPerson` | `Off` whenever the feature is off. `LeftForPerson` only when repeated automatic failures of the step now due reached the configured limit. |
| `failureCategory` | existing failure category names | Present only for `LeftForPerson`. No text, message or detail. |

`stage`, `nextStep` and `reason` keep exactly their current meaning. `LeftForPerson` does not change
`nextStep`: a person can still advance the request, and the manual advance behaves as it always has.

## Audit entry (`GET /change-requests/{id}/audit`)

New member `automatic`: `true` on the entry of a step run automatically, `null` on every other entry
(manual steps, submissions, decisions, and all entries recorded before this feature).

The entry is otherwise identical in content to a manual step's entry.

## Not changed

Every other response, status code and error body. A step failed automatically produces no response
anyone receives; it is visible only as the check summary counts and, after the limit, as `LeftForPerson`.
