# Contract: Operator Summary Endpoint

## `GET /operator/automatic-advancement`

Read-only. No authentication (out of scope, as for every existing endpoint). No body, no parameters.

### 200 OK

Before any check has run since the system started (including when the feature is off):

```json
{ "enabled": false, "lastCheck": null }
```

After a check:

```json
{
  "enabled": true,
  "lastCheck": {
    "checkedAt": "2026-10-16T09:30:00Z",
    "examined": 12,
    "ran": 2,
    "failed": 1
  }
}
```

| Member | Type | Meaning |
|---|---|---|
| `enabled` | boolean | The effective setting after validation. |
| `lastCheck` | object or null | The latest check only; not stored durably. |
| `checkedAt` | UTC timestamp | When the check finished. |
| `examined` | integer | Requests looked at in that check. |
| `ran` | integer | Steps that stored a draft. |
| `failed` | integer | Steps that failed with a category, plus steps that raised an unexpected error. |

The response has no string member other than the fixed member names: no model text, endpoint,
deployment name, credential, path, request id or failure detail. It cannot carry them by construction.

### Errors

None specific. Any unexpected error uses the existing error body shape and carries no internal detail.

### Log line (one per check)

`Automatic advancement check: examined=12 ran=2 failed=1` — counts only. When any step failed with a
category, the line also carries the per-category counts, using the existing category names:
`Automatic advancement check: examined=12 ran=2 failed=2 categories=Timeout:1,MalformedOutput:1`.
(An unexpected error is in `failed` but in no category.) The endpoint response does not include the
categories.

## Settings (documented in README)

| Key | Env var | Default |
|---|---|---|
| `ChangeOps:AutoAdvance:Enabled` | `ChangeOps__AutoAdvance__Enabled` | `false` |
| `ChangeOps:AutoAdvance:IntervalSeconds` | `ChangeOps__AutoAdvance__IntervalSeconds` | `300` |
| `ChangeOps:AutoAdvance:MaxStepsPerCheck` | `ChangeOps__AutoAdvance__MaxStepsPerCheck` | `5` |
| `ChangeOps:AutoAdvance:FailureWaitSeconds` | `ChangeOps__AutoAdvance__FailureWaitSeconds` | `900` |
| `ChangeOps:AutoAdvance:MaxConsecutiveFailures` | `ChangeOps__AutoAdvance__MaxConsecutiveFailures` | `3` |

An invalid value uses that setting's default; one warning names the key (never the value).
