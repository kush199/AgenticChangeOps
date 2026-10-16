# Data Model: Durable Storage

No record shape changes. The existing records are stored whole as JSON; tables only add keys.

## Layout version 1 (`PRAGMA user_version = 1`)

| Table | Columns | Keys / constraints |
|---|---|---|
| `change_requests` | `id TEXT`, `payload TEXT` | `PRIMARY KEY (id)`; insert of an existing id fails (mirrors in-memory `Save`) |
| `audit_entries` | `seq INTEGER`, `change_request_id TEXT`, `occurred_at_ticks INTEGER`, `payload TEXT` | `PRIMARY KEY (seq AUTOINCREMENT)`; index on `(change_request_id, occurred_at_ticks, seq)` |
| `agent_outputs` | `change_request_id TEXT`, `output_type TEXT`, `version INTEGER`, `payload TEXT` | `PRIMARY KEY (change_request_id, output_type, version)` |

- `id` / `change_request_id`: lower-case `D` format GUID text.
- `payload`: the record serialized by `System.Text.Json` with string enums and default property names; read back into the same record type.
- `output_type`: `typeof(T).FullName`. The store never inspects it or the payload beyond `ChangeRequestId` and `Version` from `IAgentOutput`.

## Operations and guarantees

| Store operation | Behaviour |
|---|---|
| `Save` | Insert; existing id → `InvalidOperationException` (same as in-memory) |
| `Update` | Replace payload; missing id → `InvalidOperationException` (same as in-memory) |
| `FindById` | Read and deserialize, `null` if absent |
| `SaveAuditEntry` | Insert one row |
| `SaveWithAudit` | One transaction inserts the change request and its audit entry; existing id → `InvalidOperationException`; mismatched change request ids → `ArgumentException`; nothing stored on any failure |
| `UpdateWithAudit` | One transaction replaces the change request and inserts its audit entry; unknown id → `InvalidOperationException`; mismatched ids → `ArgumentException`; nothing stored on any failure |
| `SaveAgentOutput<T>` | Argument checks first (same exceptions as in-memory); one transaction inserts the output and the audit entry; duplicate version → `StorageVersionConflictException`, nothing stored |
| `GetAuditEntries` | Ordered by `(occurred_at_ticks, seq)` |
| `GetAgentOutputs<T>` | Ordered by `version` ascending |

## Failure types (in Intake)

| Type | Meaning | Carries |
|---|---|---|
| `StorageUnavailableException` | Storage cannot be opened or written, or its layout is unsupported | `Category`: `Unavailable` or `UnsupportedLayout`; fixed message; no inner exception |
| `StorageVersionConflictException` | The version is already taken | fixed message |

## State and lifecycle

- First use of a missing file: create it, create tables, set `user_version = 1`, in one transaction.
- Existing file with `user_version = 1`: use as is.
- Anything else, including a non-empty file that is not SQLite, or a SQLite file with other tables and `user_version = 0`: refuse, change nothing. Tables are created only in a file that has none.
- Records are never deleted or updated except `Update` on a change request (status changes), as today.
