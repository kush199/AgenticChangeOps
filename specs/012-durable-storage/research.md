# Research: Durable Storage

## R1. Storage engine
- **Decision**: SQLite via `Microsoft.Data.Sqlite` 8.0.x, one file.
- **Rationale**: Local, single-file, transactional, durable on commit, enforces uniqueness with a constraint (the backstop for the version race), no server to run. Fits the constitution's "local storage".
- **Alternatives**: JSON files (no real atomic multi-record write, hard to make the version race safe); LiteDB (less standard, weaker tooling); EF Core (an ORM is speculative for three tables and adds a heavy dependency); a network DB (out of scope).
- **Open point**: the package is not in the local NuGet cache; restore needs network (CI has it).

## R2. How records are stored
- **Decision**: Each record is stored whole as JSON (`System.Text.Json`, enums as strings, camelCase not needed), beside the few columns needed for keys, uniqueness and ordering.
- **Rationale**: No record shape changes, so any record round-trips exactly; adding an agent needs no table or column. Records are positional C# records and deserialize through their constructors.
- **Fidelity**: `DateTime` values round-trip with their `Kind` and full tick precision in `System.Text.Json`; audit ordering uses a tick column, not text. Verified by contract tests comparing record equality after reload.
- **Alternatives**: One column per field (breaks "no agent knowledge" and needs a migration per record change).

## R3. Agent-neutral output key
- **Decision**: Output type key is `typeof(T).FullName`; table `agent_outputs(change_request_id, output_type, version, payload)` with primary key on the first three.
- **Rationale**: Mirrors the in-memory `(ChangeRequestId, typeof(T))` key; the store never names an agent. `GetAgentOutputs<T>` deserializes to the caller's `T`.
- **Risk**: renaming or moving a record type changes its key. Accepted: record shape and namespace changes are breaking changes under the constitution anyway; migration is out of scope.

## R4. Atomicity and the version race
- **Decision**: Each write is one SQLite transaction (output + audit entry together). A primary-key violation on `(change_request_id, output_type, version)` becomes `StorageVersionConflictException`. In-process, services still hold `SyncRoot` across check-then-write, so the constraint is a backstop plus the guarantee for any caller that does not hold the lock.
- **Rationale**: Gives "exactly one wins, other refused cleanly" even without the lock. The in-memory store throws `InvalidOperationException` for a duplicate version; the durable store throws the new conflict type, and the in-memory store keeps its existing exception so existing tests are unchanged. The shared contract test asserts "a duplicate version is refused and nothing is stored" by catching `Exception`-level refusal, not a specific type.
- **Alternatives**: Computing the next version inside the store (changes the interface and the services); rejected, out of scope.

## R4a. Change request plus audit entry atomically
- **Decision**: Add `SaveWithAudit` and `UpdateWithAudit` to `IChangeRequestStore`; implement in both stores; switch `ChangeRequestService` and `ApprovalService` to them (their only two paired-call sites).
- **Rationale**: FR-003 covers a change request and its audit entry. Two separate durable calls would be two transactions, so a crash between them could leave a change request or a status change with no audit entry.
- **Alternatives**: an ambient transaction held across two calls (hidden coupling, rejected); leaving the calls as they are (breaks FR-003).

## R5. Audit ordering
- **Decision**: Return audit entries ordered by `(occurred_at ticks, insertion sequence)`.
- **Rationale**: The in-memory store does a stable sort by `OccurredAt`, so this matches it exactly, including equal timestamps (insertion order). When clocks move forward this is also the recorded order, which is what the spec asks for.

## R6. Durability and concurrency settings
- **Decision**: One long-lived connection guarded by the store's own lock (`SyncRoot`); `PRAGMA synchronous=FULL`, default rollback journal; `PRAGMA foreign_keys` not needed (no foreign keys, mirroring in-memory, which does not require the change request to exist to store an audit entry).
- **Rationale**: Single process, single connection keeps ordering simple and removes cross-connection locking. FULL sync means a committed write survives a crash or power loss. Throughput is not a goal.

## R7. Layout version
- **Decision**: `PRAGMA user_version` holds the layout version (this slice: `1`). A new empty file is stamped `1` and the tables created in one transaction. An existing file with `user_version` ≠ 1 (including a non-empty file with `0` and no tables of ours) is refused with `StorageUnavailableException(UnsupportedLayout)`; the file is opened read-only for the check and never written.
- **Rationale**: Built-in, atomic with schema creation, no extra table.
- **Alternatives**: a `meta` table (equivalent, more code).

## R8. Failure behaviour and startup
- **Decision**: The store opens lazily on first use. Any open or write failure throws `StorageUnavailableException` (category `Unavailable` or `UnsupportedLayout`) with a fixed message; the driver exception is never wrapped as an inner exception that logging could print. A failed open is retried on the next operation. The API starts regardless (spec Q1).
- **Rationale**: Satisfies "start anyway, refuse each operation" and keeps paths and driver text out of every message.

## R9. HTTP mapping
- **Decision**: `JsonErrorMiddleware` catches the two exception types: `StorageUnavailableException` → `503 storage-unavailable` with `Category` = `Unavailable` | `UnsupportedLayout`; `StorageVersionConflictException` → `409 version-conflict`. Both use the existing `ErrorResponse` shape. The log line carries the route, status and code only, as today.
- **Rationale**: Spec Q3. No existing endpoint code changes; existing responses are untouched. The OpenAPI description string lists the two new codes.

## R10. Store selection and test isolation
- **Decision**: `ChangeOps:Storage:Provider` (`Sqlite` default, `InMemory`) and `ChangeOps:Storage:Path`. `ApiFactory` defaults `Provider=InMemory` unless a test sets it, so the existing API tests never touch a real file.
- **Rationale**: Spec Q2 (durable default). Only test setup changes.
- **Default path**: `Environment.SpecialFolder.LocalApplicationData/AgenticChangeOps/changeops.db`, created on first use; outside the repository, so it cannot be committed. `.gitignore` also gets `*.db*` as a guard for relative paths.

## R11. Verification of "same HTTP behaviour"
- **Decision**: Run the whole existing API suite unchanged (in-memory), and add API-level tests that run the key flows (submit, approve, draft, design, workflow status) against a temp SQLite file with a rebuilt host between steps, asserting the same status/body/headers as in the in-memory run.
