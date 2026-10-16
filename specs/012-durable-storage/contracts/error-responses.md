# Contract: New HTTP error responses

All existing statuses, bodies, headers and codes are unchanged. Two outcomes are added, in the
existing `ErrorResponse` shape (`code`, `message`, optional `category`). Neither carries a path,
connection detail, driver text or record content.

| Situation | Status | `code` | `message` | `category` |
|---|---|---|---|---|
| Storage cannot be opened or written | 503 | `storage-unavailable` | `The change request storage is unavailable.` | `Unavailable` |
| Storage has an unsupported layout version | 503 | `storage-unavailable` | `The change request storage is unavailable.` | `UnsupportedLayout` |
| Version already taken (lost race) | 409 | `version-conflict` | `The request conflicts with a concurrent change; try again.` | — |

- Any operation that reaches the store while it is unavailable gets the 503; the API itself still starts and operations that never touch the store (route 404, Swagger) are unaffected.
- The refusal log line is the existing one: method, route template, status, code.
- The existing API description text is pinned by a recorded baseline test and is unchanged; the two codes are documented here and in the README.
