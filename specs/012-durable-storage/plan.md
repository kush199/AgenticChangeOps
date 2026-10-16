# Implementation Plan: Durable Storage for Change Requests

**Branch**: `feature/persistence` | **Date**: 2026-10-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/012-durable-storage/spec.md`

## Summary

Add a second implementation of the existing `IChangeRequestStore` that keeps change requests, audit
entries and agent outputs in a single local SQLite file, and make it the store the API uses by
default. The in-memory store stays, unchanged, for tests. Services, agents, endpoints and record
shapes do not change. The store is agent-neutral: agent outputs are stored as JSON keyed by the
output's CLR type name, exactly the way the in-memory store keys them by `typeof(T)`.

Two new failure outcomes reach callers (clarified in the spec): storage unavailable (`503
storage-unavailable`) and lost version race (`409 version-conflict`), both in the existing error-body
shape with no internal detail.

Spec FR-003 also covers a change request stored or updated together with its audit entry. Intake and
approval today make two separate store calls for that (`Save` + `SaveAuditEntry`, `Update` +
`SaveAuditEntry`), which would be two transactions. This slice therefore adds two members to
`IChangeRequestStore`, `SaveWithAudit` and `UpdateWithAudit`, implements them in both stores, and
switches those two services to them. No other service, endpoint or record changes.

## Technical Context

**Language/Version**: C# on .NET 8 (`net8.0`, as every existing project)

**Primary Dependencies**: `Microsoft.Data.Sqlite` 8.0.x (new, only in the new project); `System.Text.Json` (in the framework)

**Storage**: One SQLite database file; location from setting `ChangeOps:Storage:Path` (env `ChangeOps__Storage__Path`), default `<user local app data>/AgenticChangeOps/changeops.db`; `ChangeOps:Storage:Provider` = `Sqlite` (default) or `InMemory`

**Testing**: xUnit; one abstract store contract suite run against both stores; durable-only tests use real temp files (no database mocking, per the constitution)

**Target Platform**: Local Windows/Linux process, single running copy

**Project Type**: Existing multi-project web-service solution (adds one library + one test project)

**Performance Goals**: None beyond "not noticeably slower than before" for a local single-user API; one transaction per write

**Constraints**: All-or-nothing writes; durable on commit (full synchronous); no path, connection detail or driver message in any error, log or response; no change to any record shape or existing response

**Scale/Scope**: Local portfolio scale: thousands of records, one process

## Constitution Check

*Constitution v1.0.3.*

| Principle | Assessment |
|---|---|
| I. Synthetic data only | Pass. Tests use synthetic records; the database file is git-ignored (`*.db` and the default path is outside the repo); no connection strings or secrets. |
| II. Deterministic routing | Pass. No routing or model involvement. |
| III. Human approval gates | Pass. Approval audit entries persist unchanged; nothing new auto-proceeds. |
| IV. Typed auditable records | Pass. Records are stored whole as typed JSON and returned as the same typed records; output records stay immutable (never updated or deleted, versions unique by constraint). No schema change to any record. |
| V. Spec-driven | Pass. Spec and clarifications done; plan, then tasks, must be approved before code. |
| VI. Test-first | Pass. Contract tests and durable tests are written, shown red and approved before implementation. No LLM involved. |
| VII. Simplicity | Pass, with one tracked item below: a new project is the second implementation of an existing interface, not a speculative abstraction. No generic repository or ORM. |
| Tech stack: "Local or Azure-hosted storage; schema defined per typed record type" | Pass. Local file storage. |

No violations. **Post-design re-check**: unchanged; the design adds no interface, no agent knowledge and no record change.

## Project Structure

### Documentation (this feature)

```text
specs/012-durable-storage/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── store-contract.md
│   └── error-responses.md
└── tasks.md             # /speckit-tasks, not created here
```

### Source Code (repository root)

```text
src/
├── AgenticChangeOps.Intake/
│   ├── Services/ChangeRequestService.cs  # Save+SaveAuditEntry → SaveWithAudit
│   ├── Services/ApprovalService.cs       # Update+SaveAuditEntry → UpdateWithAudit
│   └── Stores/
│       ├── IChangeRequestStore.cs        # + SaveWithAudit, UpdateWithAudit
│       ├── InMemoryChangeRequestStore.cs # + the same two members (check first, then mutate)
│       └── StorageException.cs           # NEW: StorageUnavailableException, StorageVersionConflictException (+ category enum)
├── AgenticChangeOps.Persistence/         # NEW project (refs Intake only; no agent, no ModelAccess)
│   ├── SqliteChangeRequestStore.cs      # default path falls back to the user profile, never the working directory
│   ├── SqliteStorageOptions.cs
│   ├── StorageSchema.cs                  # layout version + DDL
│   └── RecordJson.cs                     # JSON options shared by write and read
└── AgenticChangeOps.Api/
    ├── Composition/ApiServiceRegistration.cs  # choose store from settings
    ├── Errors/ErrorMapper.cs                  # + StorageUnavailable(), VersionConflict()
    └── Errors/JsonErrorMiddleware.cs          # map the two exceptions

README.md                                      # storage row, settings, dependency note
.gitignore                                     # database file patterns

tests/
├── AgenticChangeOps.Persistence.Tests/   # NEW
│   ├── Contract/ChangeRequestStoreContract.cs   # abstract; the shared behavioural tests
│   ├── Contract/InMemoryStoreContractTests.cs
│   ├── Contract/SqliteStoreContractTests.cs
│   ├── Durable/RestartTests.cs
│   ├── Durable/AtomicityTests.cs
│   ├── Durable/ConcurrencyTests.cs
│   ├── Durable/LayoutVersionTests.cs
│   ├── Durable/UnavailableStorageTests.cs
│   ├── Durable/NoLeakTests.cs
│   └── Durable/StructureTests.cs               # no agent names in Persistence; *.db ignored
└── AgenticChangeOps.Api.Tests/
    ├── Fakes/ApiFactory.cs              # sets Provider=InMemory by default (setup only, no assertion change)
    └── Storage/…                        # API-level restart and error-code tests (new files)
```

**Structure Decision**: One new library, `AgenticChangeOps.Persistence`, because Intake must keep a
zero-dependency project file (an existing structure test enforces it) and so cannot reference SQLite.
The exception types live in Intake beside the interface they belong to, so services and the API can
see them without referencing the new project. The API is the only project that references
Persistence. `.gitignore` gains `*.db`, `*.db-journal` and `*.sqlite*`.

## Complexity Tracking

| Item | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| New project `AgenticChangeOps.Persistence` | Second implementation of `IChangeRequestStore`; Intake's project file must stay dependency-free | Adding SQLite to Intake would break its dependency-free rule and its structure test |
| Two new store members (`SaveWithAudit`, `UpdateWithAudit`) and two service call-site edits | FR-003 needs a change request and its audit entry stored atomically; today's two calls would be two transactions. Two callers (intake, approval) use them, so Principle VII is met | Holding an ambient transaction across two calls is hidden coupling; leaving it would break FR-003 on a crash between the calls |
| Two exception types in Intake | The API must tell "storage unavailable" and "version conflict" apart without the driver's types | Letting driver exceptions escape would leak internals and map to a generic 500 |
| Test-factory setup edit (`ApiFactory` sets `Provider=InMemory`) | Default is now durable (spec Q2); the existing API tests must not write a real file | Per-test edits would touch many files; the factory change alters no assertion |

## Constitution compliance check (v1.0.3), after implementation, 2026-10-16

| Principle | Result |
|---|---|
| I. Synthetic data only | Pass. All fixtures synthetic; no connection string, endpoint or secret added; `*.db`, `*.db-journal`, `*.db-wal`, `*.db-shm`, `*.sqlite`, `*.sqlite3` ignored; the default file is outside the repository. |
| II. Deterministic routing | Pass. No routing or model involvement. |
| III. Human approval gates | Pass. Approval audit entries persist unchanged; `UpdateWithAudit` stores a decision and its audit entry together. |
| IV. Typed auditable records | Pass. Records are stored whole as typed JSON and returned as the same types; outputs are inserted only (never updated or deleted); versions are unique by key. No record shape changed. |
| V. Spec-driven | Pass. Spec, clarifications, plan, analysis edits and tasks approved before code; red tests approved at T025. |
| VI. Test-first | Pass. Tests written and shown red before implementation; no live model call anywhere; real files, no database mocking. |
| VII. Simplicity | Pass. New project and the two interface members are tracked in Complexity Tracking and match what was built. No ORM, no generic repository. |

Deviations from the plan, recorded: (1) the OpenAPI description string in `Program.cs` was not extended, because an existing test pins it exactly; the two new codes are documented in `contracts/error-responses.md` and the README; (2) `Microsoft.Data.Sqlite` is also referenced by `AgenticChangeOps.Api.Tests` so its error tests can create stamped or foreign database files.
