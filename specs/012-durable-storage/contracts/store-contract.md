# Contract: Store behaviour (both implementations)

The abstract suite `ChangeRequestStoreContract` runs unchanged against `InMemoryChangeRequestStore`
and `SqliteChangeRequestStore`. Each case uses synthetic records.

| # | Behaviour | Spec |
|---|---|---|
| C1 | `Save` then `FindById` returns an equal record; unknown id returns `null` | FR-002, FR-012 |
| C2 | `Save` of an existing id is refused and the stored record is unchanged | FR-004 |
| C3 | `Update` replaces the record; update of an unknown id is refused | FR-002 |
| C4 | Audit entries come back equal and in recording order, equal timestamps keep insertion order | FR-007 |
| C5 | `SaveAgentOutput` stores the output and its audit entry together | FR-003 |
| C6 | Mismatched change request ids, or a null argument, are refused and nothing is stored | FR-003, FR-004 |
| C7 | A duplicate version for the same change request and output type is refused and nothing (output or audit entry) is stored | FR-005, FR-006 |
| C8 | The same version number for a different change request, or a different output type, is accepted | FR-005 |
| C9 | `GetAgentOutputs<T>` returns ascending versions; none → empty | FR-005 |
| C10 | A record type the store has never seen (test-only `FakeAgentOutput`) is stored and returned with no store change | FR-016 |
| C11 | Every field, enum, `DateTime` (including `Kind`) and list value is returned exactly as stored. List members are compared element by element (records compare lists by reference) | FR-002, FR-012 |
| C12 | Overlapping callers racing for one version: exactly one succeeds | FR-006 |
| C13 | `SaveWithAudit` and `UpdateWithAudit` store the change request and its audit entry together; a refused call (existing id, unknown id, mismatched ids) stores neither | FR-003, FR-004 |
| C14 | An entry recorded later with an earlier timestamp comes back in timestamp order; entries with equal timestamps keep insertion order. Identical for both stores | FR-007 |

Durable-only cases (not in the shared suite):

| # | Behaviour | Spec |
|---|---|---|
| D1 | Close and reopen the file: all of C1–C11 hold for data written before | FR-001, FR-002 |
| D2 | A failure injected between the first and second insert of `SaveAgentOutput`, `SaveWithAudit` and `UpdateWithAudit` (a trigger on the audit table, created by the test on a separate connection, that aborts the insert) leaves no partial row | FR-003, FR-004 |
| D3 | New file is stamped layout version 1; a file stamped with another version, a foreign SQLite file with other tables, and a non-SQLite file are refused and their bytes are unchanged | FR-009, FR-010 |
| D4 | Unwritable or missing-directory location is refused with `StorageUnavailableException`; message contains no path or driver text | FR-011 |
| D5 | Persistence source and project file name no agent and no agent project | FR-016 |
| D6 | 50 overlapping saves for one version across threads: one success, 49 `StorageVersionConflictException`; versions 1..N afterwards | SC-004 |
